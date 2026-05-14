using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 마우스 호버 툴팁 UI (싱글톤 MonoBehaviour)
    ///
    /// ── 동작 방식 ──────────────────────────────────────────────────
    /// [3D 오브젝트 호버]
    ///   매 프레임 Physics.Raycast로 마우스 아래 오브젝트를 감지합니다.
    ///   IHoverTooltipProvider를 구현한 오브젝트 위에 마우스가 오면 Show()를 호출합니다.
    ///   Provider가 바뀔 때만 Show/Hide를 호출해 불필요한 갱신을 방지합니다.
    ///
    /// [UI 요소 호버]
    ///   SkillPreviewHoverUI 등 UI 컴포넌트는 ShowPinned() / HidePinned()를 직접 호출합니다.
    ///   Pinned 상태에서는 Raycast 탐색을 건너뜁니다.
    ///
    /// ── 패널 구성 ──────────────────────────────────────────────────
    ///   [메인 패널]           기본 설명 텍스트 (panelRect)
    ///   [키워드 사전 패널]    본문에 등장한 키워드 목록 — 메인 패널 오른쪽에 표시
    ///   [상태이상 패널]       유닛의 현재 상태이상 목록 — 메인 패널 아래에 표시
    ///   [키워드 상세 팝업]    키워드 위에 마우스를 올리면 팝업 (좌클릭으로 고정)
    /// </summary>
    public class HoverTooltipUI : MonoBehaviour
    {
        public static HoverTooltipUI Instance { get; private set; }

        // ═══════════════════════════════════════════════════════
        // 인스펙터 참조
        // ═══════════════════════════════════════════════════════

        [Header("UI 참조")]
        [SerializeField] private Canvas canvas;                               // 툴팁을 감싸는 캔버스
        [SerializeField] private RectTransform panelRect;                     // 메인 툴팁 패널
        [SerializeField] private TextMeshProUGUI tooltipText;                 // 메인 설명 텍스트
        [SerializeField] private GlossaryContainerUI glossaryContainer;       // 키워드 + 상태이상 카드 컨테이너

        [Header("메인 패널 레이아웃")]
        [SerializeField] private Vector2 padding = new Vector2(14f, 10f);     // 텍스트와 패널 가장자리 사이 내부 여백
        [SerializeField] private Vector2 offset  = new Vector2(16f, -16f);   // 마우스 커서 기준 패널 오프셋
        [SerializeField] private float minMainPanelWidth = 180f;              // 메인 패널 최소 너비
        [SerializeField] private float maxMainPanelWidth = 520f;              // 메인 패널 최대 너비

        // ═══════════════════════════════════════════════════════
        // 런타임 상태 변수
        // ═══════════════════════════════════════════════════════

        // 현재 메인 패널이 화면에 보이는지 여부
        private bool _visible;

        // 현재 마우스 아래에 있는 IHoverTooltipProvider (3D 오브젝트)
        // Provider가 바뀔 때만 Show/Hide를 호출합니다
        private IHoverTooltipProvider _currentProvider;

        // true면 ShowPinned()로 UI가 툴팁을 고정한 상태 → Raycast 탐색을 건너뜁니다
        private bool _pinnedByUI;

        // 매 프레임 Camera.main 호출을 피하기 위한 카메라 캐시
        private Camera _cachedCamera;

        // ═══════════════════════════════════════════════════════
        // [1] 싱글톤 초기화
        // ═══════════════════════════════════════════════════════

        private void Awake()
        {
            // 싱글톤 중복 방지: 이미 인스턴스가 있으면 자신을 파괴합니다
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 전환 시에도 툴팁 UI를 유지합니다

            // 참조 자동 탐색 및 레이아웃 초기화
            ValidateReferences();
            SetupLayout();

            // 게임 시작 시 툴팁은 숨겨진 상태로 시작합니다
            Hide();
            Debug.Log("[HoverTooltipUI] 툴팁 시스템 초기화 완료");
        }

        /// <summary>
        /// 코드에서 Instance에 접근하기 전에 안전하게 호출하는 보조 메서드입니다.
        /// Instance가 없으면 씬에서 탐색 후 경고를 출력합니다.
        /// (코드로 생성하지 않고 씬/프리팹에 배치된 인스턴스를 찾습니다)
        /// </summary>
        public static void EnsureInstance()
        {
            if (Instance != null) return;

            Instance = Object.FindFirstObjectByType<HoverTooltipUI>();
            if (Instance == null)
                Debug.LogWarning("[HoverTooltipUI] 인스턴스를 찾을 수 없습니다. HoverTooltipUI를 씬에 배치해주세요.");
        }

        // ═══════════════════════════════════════════════════════
        // [2] Update 루프 — 매 프레임 처리
        // ═══════════════════════════════════════════════════════

        private void Update()
        {
            // 마우스 아래의 Provider를 Raycast로 감지해 툴팁을 갱신합니다
            UpdateHoveredTarget();

            // 툴팁이 숨겨진 상태면 아래 처리는 불필요합니다
            if (!_visible) return;

            // 메인 패널(및 보조 패널)을 현재 마우스 위치로 이동합니다
            FollowMouse();
        }

        /// <summary>
        /// 매 프레임 마우스 아래의 IHoverTooltipProvider를 Physics.Raycast로 탐색합니다.
        /// Provider가 이전 프레임과 달라진 경우에만 Show/Hide를 호출합니다.
        /// </summary>
        private void UpdateHoveredTarget()
        {
            // ShowPinned()로 UI가 고정한 경우 Raycast 탐색을 건너뜁니다
            if (_pinnedByUI) return;

            // 마우스 커서가 UI 레이어 위에 있으면 3D 오브젝트 Raycast를 실행하지 않습니다
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                ClearProvider();
                return;
            }

            var provider = GetProviderUnderMouse();

            // Provider가 바뀌지 않았으면 갱신 불필요
            if (ReferenceEquals(provider, _currentProvider)) return;

            _currentProvider = provider;

            if (_currentProvider != null)
            {
                Debug.Log($"[HoverTooltipUI] 오브젝트 감지: {((Component)_currentProvider).name}");
                Show(_currentProvider.GetHoverTooltipData()); // 새 Provider의 데이터로 툴팁 갱신
            }
            else
            {
                Debug.Log("[HoverTooltipUI] Provider 없음 → 툴팁 숨김");
                Hide();
            }
        }

        // ═══════════════════════════════════════════════════════
        // [3] 공개 API — Show / Hide / ShowPinned / HidePinned
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// HoverTooltipData를 받아 메인 패널과 보조 패널(키워드/상태이상)을 표시합니다.
        /// </summary>
        public void Show(HoverTooltipData data)
        {
            // 본문 텍스트가 비어있으면 툴팁을 표시하지 않습니다
            if (string.IsNullOrWhiteSpace(data.MainText))
            {
                Hide();
                return;
            }

            // panelRect, tooltipText가 없으면 표시 불가
            if (!IsReady()) return;

            // 본문 텍스트 설정 (끝 공백만 제거하고 나머지는 그대로 표시)
            tooltipText.text = TooltipKeywordFormatter.FormatMainTooltipText(data.MainText);

            // 텍스트 내용에 맞게 메인 패널 크기를 조정합니다
            UpdateSize();

            // 패널을 활성화하고 현재 마우스 위치로 이동합니다
            _visible = true;
            panelRect.gameObject.SetActive(true);
            FollowMouse();

            // 본문 텍스트에서 DB 키워드를 추출하고 상태이상과 함께 카드 컨테이너로 넘깁니다
            var matchedKeywords = TooltipKeywordFormatter.ExtractMatches(tooltipText.text);
            if (matchedKeywords.Count == 0 && !string.IsNullOrWhiteSpace(tooltipText.text))
                Debug.Log($"[HoverTooltipUI] 키워드 매칭 결과 없음: {tooltipText.text}");

            // 키워드 카드 + 패시브 카드 + 상태이상 카드를 한 번에 컨테이너로 전달합니다
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            glossaryContainer?.Show(matchedKeywords, data.Statuses, data.Passives, panelRect.position, panelRect.sizeDelta, scale);

            if (glossaryContainer == null)
                Debug.LogWarning("[HoverTooltipUI] glossaryContainer가 없습니다.");
        }

        /// <summary>
        /// 단순 문자열로 툴팁을 표시하는 편의 메서드입니다.
        /// </summary>
        public void Show(string message)
        {
            Show(new HoverTooltipData(message));
        }

        /// <summary>
        /// 툴팁과 모든 보조 패널을 숨기고 상태를 초기화합니다.
        /// </summary>
        public void Hide()
        {
            _visible = false;

            if (panelRect != null)
                panelRect.gameObject.SetActive(false);

            glossaryContainer?.Hide(); // 키워드 + 상태이상 카드를 함께 숨깁니다
        }

        /// <summary>
        /// UI 요소(예: 스킬 버튼)에서 호버 툴팁을 고정 표시합니다.
        /// 이 모드에서는 3D Raycast 탐색을 건너뜁니다.
        /// </summary>
        public void ShowPinned(string message)
        {
            _pinnedByUI      = true;
            _currentProvider = null; // 3D Provider 참조를 비워둡니다
            Show(message);
        }

        /// <summary>
        /// ShowPinned의 HoverTooltipData 오버로드입니다.
        /// </summary>
        public void ShowPinned(HoverTooltipData data)
        {
            _pinnedByUI      = true;
            _currentProvider = null;
            Show(data);
        }

        /// <summary>
        /// ShowPinned()로 고정된 툴팁을 해제하고 숨깁니다.
        /// </summary>
        public void HidePinned()
        {
            _pinnedByUI = false;
            Hide();
        }

        // ═══════════════════════════════════════════════════════
        // [4] 위치 & 크기 계산
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 메인 패널과 모든 보조 패널을 현재 마우스 위치로 이동합니다.
        /// 화면 경계를 벗어나지 않도록 위치를 클램프합니다.
        /// </summary>
        private void FollowMouse()
        {
            // 마우스 위치 + 오프셋으로 기본 위치를 계산합니다
            Vector2 pos  = GetMousePosition() + offset;

            // sizeDelta는 캔버스 로컬 단위 → 스크린 픽셀로 변환
            float scale      = canvas != null ? canvas.scaleFactor : 1f;
            Vector2 screenSize = panelRect.sizeDelta * scale;

            // 패널이 화면 오른쪽·하단 밖으로 나가지 않도록 클램프합니다
            pos.x = Mathf.Clamp(pos.x, 0f, Screen.width  - screenSize.x);
            pos.y = Mathf.Clamp(pos.y, screenSize.y, Screen.height);

            panelRect.position = pos;

            // 카드 컨테이너도 메인 패널 기준으로 위치를 재조정합니다
            glossaryContainer?.Reposition(panelRect.position, panelRect.sizeDelta, scale);
        }

        /// <summary>
        /// 현재 툴팁 텍스트에 맞게 메인 패널의 너비와 높이를 자동 계산합니다.
        /// </summary>
        private void UpdateSize()
        {
            float clampedMaxWidth = Mathf.Max(minMainPanelWidth, maxMainPanelWidth);

            // 텍스트가 최대 너비 안에서 필요한 크기를 계산합니다
            Vector2 textSize = tooltipText.GetPreferredValues(
                tooltipText.text,
                clampedMaxWidth - padding.x * 2f,
                0f);

            float width  = Mathf.Clamp(textSize.x + padding.x * 2f, minMainPanelWidth, clampedMaxWidth);
            float height = textSize.y + padding.y * 2f;

            panelRect.sizeDelta = new Vector2(width, height);
        }

        // ═══════════════════════════════════════════════════════
        // [5] Raycast — 3D Provider 탐색
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 현재 마우스 위치에서 Physics.Raycast를 수행해 IHoverTooltipProvider를 찾아 반환합니다.
        /// Collider에서 시작해 부모 오브젝트까지 올라가며 탐색합니다.
        /// </summary>
        private IHoverTooltipProvider GetProviderUnderMouse()
        {
            var cam = GetWorldCamera();
            if (cam == null) return null;

            Ray ray = cam.ScreenPointToRay(GetMousePosition());

            // Raycast가 아무것도 맞히지 못하면 null을 반환합니다
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f)) return null;

            // 충돌한 Collider 또는 그 부모 오브젝트에서 Provider를 탐색합니다
            return hit.collider.GetComponentInParent<IHoverTooltipProvider>();
        }

        /// <summary>
        /// 사용할 월드 카메라를 반환합니다. Camera.main이 없는 씬도 처리합니다.
        /// 불필요한 Camera.main 반복 호출을 피하기 위해 캐싱합니다.
        /// </summary>
        private Camera GetWorldCamera()
        {
            // 캐싱된 카메라가 여전히 유효하면 바로 반환합니다
            if (_cachedCamera != null && _cachedCamera.isActiveAndEnabled)
                return _cachedCamera;

            // Camera.main으로 시도합니다
            _cachedCamera = Camera.main;
            if (_cachedCamera != null && _cachedCamera.isActiveAndEnabled)
                return _cachedCamera;

            // 마지막 수단: MainCamera 태그 없이 씬에서 카메라를 직접 탐색합니다
            _cachedCamera = FindFirstObjectByType<Camera>();
            return _cachedCamera;
        }

        /// <summary>
        /// 현재 마우스의 스크린 좌표를 반환합니다.
        /// New Input System과 Legacy Input 모두 지원합니다.
        /// </summary>
        private Vector2 GetMousePosition()
        {
            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();

            // New Input System을 사용하지 않는 환경의 폴백
            return Input.mousePosition;
        }

        /// <summary>
        /// 현재 Provider를 초기화하고 툴팁을 숨깁니다.
        /// Provider가 이미 없는 경우 중복 Hide()를 호출하지 않습니다.
        /// </summary>
        private void ClearProvider()
        {
            if (_currentProvider == null) return;

            _currentProvider = null;
            Hide();
        }

        // ═══════════════════════════════════════════════════════
        // [6] 초기화 헬퍼 — 참조 검증 & 레이아웃 설정
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Inspector에서 할당되지 않은 참조를 씬에서 자동으로 찾아 채웁니다.
        /// 필수 참조가 끝내 없으면 경고를 출력합니다.
        /// </summary>
        private void ValidateReferences()
        {
            // Canvas: 자식 오브젝트에서 자동 탐색합니다
            if (canvas == null)
                canvas = GetComponentInChildren<Canvas>(true);

            // GlossaryContainer: 자식 → 씬 전체 순서로 탐색합니다
            if (glossaryContainer == null)
            {
                glossaryContainer = GetComponentInChildren<GlossaryContainerUI>(true);
                if (glossaryContainer == null)
                    glossaryContainer = Resources
                        .FindObjectsOfTypeAll<GlossaryContainerUI>()
                        .FirstOrDefault(p => p != null && p.gameObject.scene.IsValid());
            }

            // 필수 참조 누락 경고
            if (panelRect == null || tooltipText == null)
            if (glossaryContainer == null)
                Debug.LogWarning("[HoverTooltipUI] glossaryContainer가 없습니다.");
        }

        /// <summary>
        /// 패널 앵커/피벗, 텍스트 레이아웃, Raycast 차단 등을 초기 설정합니다.
        /// </summary>
        private void SetupLayout()
        {
            // 패널이 앵커 설정 때문에 화면 전체로 늘어나지 않도록 표준화합니다
            NormalizeFloatingRect(panelRect);

            // 텍스트 RectTransform을 패딩 기준으로 설정합니다
            // 이렇게 해야 텍스트와 배경 Image가 올바르게 정렬됩니다
            SyncTextLayout();

            // 툴팁 패널이 Raycast를 막으면 마우스가 패널에 가려져 Hover가 끊기고 깜빡입니다
            // 패널 내 모든 Graphic의 raycastTarget을 false로 설정합니다
            DisableRaycastForUI(panelRect);

            // TMP 텍스트 컴포넌트도 별도로 차단합니다
            if (tooltipText    != null) tooltipText.raycastTarget    = false;

            // 카드 컨테이너는 초기에 숨겨둡니다
            glossaryContainer?.Hide();
        }

        /// <summary>
        /// 떠다니는 패널의 앵커와 피벗을 표준화합니다.
        /// anchorMin = anchorMax = (0,0), pivot = (0,1) : 좌하단 앵커, 좌상단 피벗.
        /// 이 설정이 없으면 패널이 부모 크기에 맞춰 늘어나거나 위치 계산이 어긋납니다.
        /// </summary>
        private static void NormalizeFloatingRect(RectTransform rect)
        {
            if (rect == null) return;

            rect.anchorMin = Vector2.zero;           // 앵커를 좌하단으로 고정합니다
            rect.anchorMax = Vector2.zero;
            rect.pivot     = new Vector2(0f, 1f);   // 피벗을 좌상단으로 설정합니다 (위치 기준점)
        }

        /// <summary>
        /// 패널 내 모든 Graphic(Image, Text 등)의 raycastTarget을 false로 설정합니다.
        /// 툴팁이 Raycast를 소비하면 마우스가 패널 위에 있을 때 3D 오브젝트 감지가 끊겨 깜빡입니다.
        /// </summary>
        private static void DisableRaycastForUI(RectTransform root)
        {
            if (root == null) return;

            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic != null)
                    graphic.raycastTarget = false;
            }
        }

        /// <summary>
        /// 텍스트 컴포넌트의 RectTransform을 부모 패널 패딩에 맞게 설정합니다.
        /// 텍스트와 배경 Image가 따로 노는 현상을 방지합니다.
        /// </summary>
        private void SyncTextLayout()
        {
            // 메인 패널 텍스트: 패널 전체를 채우되 padding만큼 안쪽에 배치합니다
            if (tooltipText != null)
            {
                var r = tooltipText.rectTransform;
                r.anchorMin = Vector2.zero;
                r.anchorMax = Vector2.one;
                r.offsetMin = new Vector2( padding.x,  padding.y);
                r.offsetMax = new Vector2(-padding.x, -padding.y);
            }
        }

        /// <summary>
        /// 툴팁을 표시하기 위한 최소 요건(panelRect, tooltipText)이 갖춰져 있는지 확인합니다.
        /// </summary>
        private bool IsReady()
        {
            return panelRect != null && tooltipText != null;
        }
    }
}
