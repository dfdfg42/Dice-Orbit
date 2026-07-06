using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 경량 핀 툴팁 UI (싱글톤 MonoBehaviour).
    ///
    /// 역할: 커서 옆에 떠야 자연스러운 초경량 텍스트 전용 —
    ///   예상 피해(SkillTargetSelector), 선택 카운터, 스킬버튼 호버(SkillPreviewHoverUI).
    ///
    /// 유닛/타일 상세 정보는 BattleInfoPanelUI(오른쪽 상시 정보 패널)가 담당한다.
    /// 구 레이캐스트 호버 모드/글로서리 카드/키워드 팝업은 2026-07 정보 패널 도입과 함께 철거됨
    /// (스펙: Docs/superpowers/specs/2026-07-05-battle-info-panel-design.md §7).
    ///
    /// 공개 API: EnsureInstance() / ShowPinned(string) / HidePinned()
    /// </summary>
    public class HoverTooltipUI : MonoBehaviour
    {
        public static HoverTooltipUI Instance { get; private set; }

        // ═══════════════════════════════════════════════════════
        // 인스펙터 참조
        // ═══════════════════════════════════════════════════════

        [Header("UI 참조")]
        [SerializeField] private Canvas canvas;                       // 툴팁을 감싸는 캔버스
        [SerializeField] private RectTransform panelRect;             // 툴팁 패널
        [SerializeField] private TextMeshProUGUI tooltipText;         // 툴팁 본문 텍스트

        [Header("패널 레이아웃")]
        [SerializeField] private Vector2 padding = new Vector2(14f, 10f);   // 텍스트와 패널 가장자리 사이 내부 여백
        [SerializeField] private Vector2 offset  = new Vector2(16f, -16f);  // 마우스 커서 기준 패널 오프셋
        [SerializeField] private float minMainPanelWidth = 180f;            // 패널 최소 너비
        [SerializeField] private float maxMainPanelWidth = 520f;            // 패널 최대 너비

        [Header("렌더링 정렬")]
        [SerializeField] private bool forceTopMost = true;                  // true면 툴팁 캔버스를 항상 최상단 정렬로 강제
        [SerializeField] private int topMostSortingOrder = 30000;           // 다른 UI보다 크게 설정

        // ═══════════════════════════════════════════════════════
        // 런타임 상태
        // ═══════════════════════════════════════════════════════

        private bool _visible;      // 패널이 화면에 보이는지

        // ═══════════════════════════════════════════════════════
        // [1] 싱글톤 초기화
        // ═══════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            ValidateReferences();
            SetupLayout();
            EnsureTopMostOrder();

            Hide();
        }

        /// <summary>
        /// 코드에서 Instance에 접근하기 전에 안전하게 호출하는 보조 메서드입니다.
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
        // [2] Update — 표시 중이면 커서를 따라다님
        // ═══════════════════════════════════════════════════════

        private void Update()
        {
            if (!_visible) return;
            FollowMouse();
        }

        // ═══════════════════════════════════════════════════════
        // [3] 공개 API
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// UI 요소(스킬 버튼/타게팅 미리보기 등)에서 커서 옆 툴팁을 고정 표시합니다.
        /// </summary>
        public void ShowPinned(string message)
        {
            Show(message);
        }

        /// <summary>
        /// ShowPinned()로 고정된 툴팁을 해제하고 숨깁니다.
        /// </summary>
        public void HidePinned()
        {
            Hide();
        }

        // ═══════════════════════════════════════════════════════
        // [4] 내부 표시/숨김
        // ═══════════════════════════════════════════════════════

        private void Show(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                Hide();
                return;
            }

            if (!IsReady()) return;

            tooltipText.text = message.TrimEnd();
            UpdateSize();
            EnsureTopMostOrder();

            _visible = true;
            panelRect.gameObject.SetActive(true);
            FollowMouse();
        }

        private void Hide()
        {
            _visible = false;

            if (panelRect != null)
                panelRect.gameObject.SetActive(false);
        }

        // ═══════════════════════════════════════════════════════
        // [5] 위치 & 크기 계산
        // ═══════════════════════════════════════════════════════

        /// <summary>패널을 현재 마우스 위치로 이동합니다. 화면 경계를 벗어나지 않도록 클램프합니다.</summary>
        private void FollowMouse()
        {
            // sizeDelta는 캔버스 유닛이므로 scaleFactor를 곱해 스크린 픽셀로 변환합니다
            float   scale      = canvas != null ? canvas.scaleFactor : 1f;
            Vector2 pos        = GetMousePosition() + offset;
            Vector2 screenSize = panelRect.sizeDelta * scale;

            pos.x = Mathf.Clamp(pos.x, 0f, Screen.width  - screenSize.x);
            pos.y = Mathf.Clamp(pos.y, screenSize.y,      Screen.height);

            panelRect.position = pos;
        }

        /// <summary>현재 텍스트에 맞게 패널의 너비와 높이를 자동 계산합니다.</summary>
        private void UpdateSize()
        {
            float clampedMaxWidth = Mathf.Max(minMainPanelWidth, maxMainPanelWidth);

            Vector2 textSize = tooltipText.GetPreferredValues(
                tooltipText.text,
                clampedMaxWidth - padding.x * 2f,
                0f);

            float width  = Mathf.Clamp(textSize.x + padding.x * 2f, minMainPanelWidth, clampedMaxWidth);
            float height = textSize.y + padding.y * 2f;

            panelRect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>현재 마우스의 스크린 좌표를 반환합니다 (New Input System / Legacy 폴백).</summary>
        private Vector2 GetMousePosition()
        {
            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();

            return Input.mousePosition;
        }

        // ═══════════════════════════════════════════════════════
        // [6] 초기화 헬퍼
        // ═══════════════════════════════════════════════════════

        private void ValidateReferences()
        {
            if (canvas == null)
                canvas = GetComponentInChildren<Canvas>(true);

            if (panelRect == null || tooltipText == null)
                Debug.LogWarning("[HoverTooltipUI] panelRect 또는 tooltipText 참조가 없습니다. Inspector에서 설정해주세요.");
        }

        private void SetupLayout()
        {
            NormalizeFloatingRect(panelRect);
            SyncTextLayout();

            // 툴팁 패널이 Raycast를 막으면 마우스가 패널에 가려져 Hover가 끊기고 깜빡입니다
            DisableRaycastForUI(panelRect);
            if (tooltipText != null) tooltipText.raycastTarget = false;

            EnsureTopMostOrder();
        }

        /// <summary>툴팁 캔버스를 항상 최상단으로 렌더링되도록 정렬 우선순위를 강제합니다.</summary>
        private void EnsureTopMostOrder()
        {
            if (canvas == null) return;

            if (forceTopMost)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = topMostSortingOrder;
            }

            canvas.transform.SetAsLastSibling();
            if (panelRect != null) panelRect.SetAsLastSibling();
        }

        /// <summary>
        /// 떠다니는 패널의 앵커와 피벗을 표준화합니다.
        /// anchorMin = anchorMax = (0,0), pivot = (0,1) : 좌하단 앵커, 좌상단 피벗.
        /// </summary>
        private static void NormalizeFloatingRect(RectTransform rect)
        {
            if (rect == null) return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot     = new Vector2(0f, 1f);
        }

        /// <summary>패널 내 모든 Graphic의 raycastTarget을 false로 설정합니다.</summary>
        private static void DisableRaycastForUI(RectTransform root)
        {
            if (root == null) return;

            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic != null)
                    graphic.raycastTarget = false;
            }
        }

        /// <summary>텍스트 컴포넌트의 RectTransform을 패널 패딩에 맞게 설정합니다.</summary>
        private void SyncTextLayout()
        {
            if (tooltipText == null) return;

            var r = tooltipText.rectTransform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2( padding.x,  padding.y);
            r.offsetMax = new Vector2(-padding.x, -padding.y);
        }

        private bool IsReady()
        {
            return panelRect != null && tooltipText != null;
        }
    }
}
