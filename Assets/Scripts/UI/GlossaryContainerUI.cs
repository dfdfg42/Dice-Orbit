using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 툴팁 보조 패널 컨테이너입니다.
    /// 키워드 카드와 상태이상 카드를 수직으로 쌓아서 메인 툴팁 패널 오른쪽에 표시합니다.
    ///
    /// ── 동작 방식 ────────────────────────────────────────────────
    ///   - Show() 호출 시 기존 카드를 모두 삭제하고 새 카드를 생성합니다.
    ///   - Unity의 VerticalLayoutGroup + ContentSizeFitter로 높이를 자동 계산합니다.
    ///   - 카드가 많아져도 스크롤 없이 화면 경계에서 클램프합니다.
    ///   - 오른쪽 화면 공간이 부족하면 메인 패널 왼쪽으로 자동 전환합니다.
    ///
    /// ── Unity Inspector 설정 ─────────────────────────────────────
    ///   이 컴포넌트가 붙은 오브젝트에 다음 컴포넌트가 필요합니다:
    ///     - VerticalLayoutGroup (spacing, padding 설정)
    ///     - ContentSizeFitter (VerticalFit = PreferredSize)
    ///   cardPrefab: GlossaryCardUI가 붙은 카드 프리팹
    /// </summary>
    [RequireComponent(typeof(VerticalLayoutGroup))]
    [RequireComponent(typeof(ContentSizeFitter))]
    public class GlossaryContainerUI : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════
        // 인스펙터 참조
        // ═══════════════════════════════════════════════════════

        [Header("UI 참조")]
        [SerializeField] private GlossaryCardUI cardPrefab; // 카드 프리팹 (GlossaryCardUI 포함)

        [Header("레이아웃")]
        [Tooltip("메인 패널 오른쪽 끝 기준 수평 오프셋")]
        [SerializeField] private float sideOffset = 14f;    // 메인 패널과 카드 컨테이너 사이 간격

        [Tooltip("카드 컨테이너의 최소 너비")]
        [SerializeField] private float minWidth = 220f;

        [Tooltip("카드 컨테이너의 최대 너비")]
        [SerializeField] private float maxWidth = 420f;

        // ═══════════════════════════════════════════════════════
        // 런타임 상태
        // ═══════════════════════════════════════════════════════

        // 현재 활성 카드 목록 (Hide/ClearCards 시 전부 Destroy)
        private readonly List<GlossaryCardUI> _activeCards = new List<GlossaryCardUI>();

        // 이 컨테이너의 RectTransform 캐시
        private RectTransform _rect;

        // ═══════════════════════════════════════════════════════
        // 초기화
        // ═══════════════════════════════════════════════════════

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();

            // 앵커를 좌하단 고정, 피벗을 좌상단으로 설정합니다
            // 이 설정이 없으면 위치 계산이 어긋납니다
            _rect.anchorMin = Vector2.zero;
            _rect.anchorMax = Vector2.zero;
            _rect.pivot     = new Vector2(0f, 1f);

            // 컨테이너 너비를 고정합니다 (높이는 ContentSizeFitter가 자동 계산)
            float clampedWidth = Mathf.Clamp(maxWidth, minWidth, maxWidth);
            _rect.sizeDelta = new Vector2(clampedWidth, _rect.sizeDelta.y);

            // ContentSizeFitter: 높이만 자동으로 늘어나게 설정합니다
            var fitter = GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; // 너비는 수동 고정
                fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize; // 높이는 카드에 맞게 자동
            }

            // Raycast 차단: 컨테이너 배경이 Raycast를 방해하지 않도록 합니다
            var bg = GetComponent<Image>();
            if (bg != null) bg.raycastTarget = false;

            // 시작 시 숨겨둡니다
            gameObject.SetActive(false);
        }

        // ═══════════════════════════════════════════════════════
        // 공개 API
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 키워드 목록과 상태이상 목록으로 카드를 생성하고 컨테이너를 표시합니다.
        /// 항목이 하나도 없으면 컨테이너를 숨깁니다.
        /// </summary>
        /// <param name="keywords">본문에서 매칭된 키워드 목록</param>
        /// <param name="statuses">유닛의 현재 상태이상 목록</param>
        /// <param name="tooltipPos">메인 툴팁 패널의 스크린 좌표</param>
        /// <param name="tooltipSize">메인 툴팁 패널의 sizeDelta</param>
        public void Show(
            IReadOnlyList<TooltipKeywordFormatter.KeywordDisplayData> keywords,
            IReadOnlyList<TooltipKeywordFormatter.StatusDisplayData>  statuses,
            IReadOnlyList<TooltipKeywordFormatter.KeywordDisplayData> passives,
            Vector2 tooltipPos,
            Vector2 tooltipSize)
        {
            if (_rect == null || cardPrefab == null) return;

            // 표시할 항목이 없으면 숨깁니다
            int keywordCount = keywords?.Count ?? 0;
            int statusCount  = statuses?.Count ?? 0;
            int passiveCount = passives?.Count ?? 0;
            if (keywordCount + statusCount + passiveCount == 0)
            {
                Debug.Log("[GlossaryContainerUI] 표시할 키워드/상태/패시브 카드가 없어 컨테이너를 숨깁니다.");
                Hide();
                return;
            }

            // 이전 카드를 전부 삭제합니다
            ClearCards();

            // 부모 컨테이너를 먼저 활성화한 뒤 카드를 생성하면,
            // Hierarchy에서 카드가 비활성으로 보이는 혼란을 줄일 수 있습니다.
            gameObject.SetActive(true);

            // 키워드 카드 생성
            if (keywords != null)
            {
                foreach (var kw in keywords)
                {
                    var card = Instantiate(cardPrefab, transform);
                    card.gameObject.SetActive(true); // 비활성 프리팹도 강제로 켭니다
                    card.SetKeyword(kw.Key, kw.Description, kw.Color, kw.Icon);
                    _activeCards.Add(card);
                }
            }

            // 패시브 카드 생성 (키워드 포맷과 동일한 UI 사용)
            if (passives != null)
            {
                foreach (var p in passives)
                {
                    var card = Instantiate(cardPrefab, transform);
                    card.gameObject.SetActive(true);
                    card.SetKeyword(p.Key, p.Description, p.Color, p.Icon);
                    _activeCards.Add(card);
                }
            }

            // 상태이상 카드 생성: 상태이상 하나당 카드 하나
            if (statuses != null)
            {
                foreach (var st in statuses)
                {
                    var card = Instantiate(cardPrefab, transform);
                    card.gameObject.SetActive(true); // 비활성 프리팹도 강제로 켭니다
                    card.SetStatus(st.Name, st.StackText, st.DurationText, st.Description, st.Color);
                    _activeCards.Add(card);
                }
            }

            // 레이아웃 강제 갱신
            // ForceRebuildLayoutImmediate를 호출해야 ContentSizeFitter가 즉시 높이를 계산합니다
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);

            // 위치 재조정
            Reposition(tooltipPos, tooltipSize);
        }

        /// <summary>
        /// 모든 카드를 삭제하고 컨테이너를 숨깁니다.
        /// </summary>
        public void Hide()
        {
            ClearCards();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 메인 툴팁 패널 위치를 기준으로 컨테이너를 재배치합니다.
        /// 오른쪽 화면 공간이 부족하면 왼쪽으로 자동 전환하고,
        /// 상하 방향은 화면 경계에서 클램프합니다.
        /// </summary>
        /// <param name="tooltipPos">메인 툴팁 패널의 스크린 좌표</param>
        /// <param name="tooltipSize">메인 툴팁 패널의 sizeDelta</param>
        public void Reposition(Vector2 tooltipPos, Vector2 tooltipSize)
        {
            if (_rect == null || !gameObject.activeSelf) return;

            var size = _rect.sizeDelta;

            // 기본 위치: 메인 패널 오른쪽에 붙여서 배치합니다
            float x = tooltipPos.x + tooltipSize.x + sideOffset;
            float y = tooltipPos.y; // 메인 패널 상단과 y를 맞춥니다

            // 오른쪽 화면 밖으로 나가면 메인 패널 왼쪽으로 이동합니다
            if (x + size.x > Screen.width)
                x = tooltipPos.x - size.x - sideOffset;

            // 화면 경계 클램프 (카드가 많아서 높이가 길어져도 클램프로 처리합니다)
            x = Mathf.Clamp(x, 0f, Mathf.Max(0f, Screen.width  - size.x));
            y = Mathf.Clamp(y, size.y, Screen.height);

            _rect.position = new Vector2(x, y);
        }

        // ═══════════════════════════════════════════════════════
        // 내부 헬퍼
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 현재 생성된 카드를 모두 Destroy하고 목록을 비웁니다.
        /// Show() 시작 시와 Hide() 시 호출됩니다.
        /// </summary>
        private void ClearCards()
        {
            foreach (var card in _activeCards)
            {
                // 씬 전환 등으로 이미 파괴된 경우를 처리합니다
                if (card != null)
                    Destroy(card.gameObject);
            }
            _activeCards.Clear();
        }
    }
}
