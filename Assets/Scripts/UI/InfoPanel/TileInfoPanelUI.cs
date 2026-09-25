using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.UI.Skin;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 화면 왼쪽 위의 독립 타일 정보 패널.
    /// 타일 그림(안 위쪽에 인게임 버블과 같은 속성 아이콘) + 아래로 속성당 설명 카드가 세로로 쌓인다.
    /// (구: 오른쪽 정보 패널의 타일 섹션 — 분리해 독립)
    ///
    /// 표시 주체는 BattleInfoPanelUI가 결정 (캐릭터 조회 시 밟은 타일 / 타일 직접 호버·핀).
    /// 크림 점수지 테마 공유 (InfoPanelRows 팔레트).
    /// </summary>
    public class TileInfoPanelUI : MonoBehaviour
    {
        public static TileInfoPanelUI Instance { get; private set; }

        [Header("슬롯 (씬에서 배치)")]
        [SerializeField] private GameObject rootCanvas;
        [SerializeField] private RectTransform stack;               // 타일 카드 + 속성 카드들이 쌓이는 곳
        [SerializeField] private Image tileImage;                   // 타일 그림
        [SerializeField] private RectTransform tileIconRow;         // 그림 안 위쪽 아이콘 행
        // (타일 인덱스/타입 메타 텍스트는 제거 — 개발자용 정보라 유저에게 불필요)

        [Header("스킨")]
        [SerializeField] private Sprite normalTileSprite;
        [SerializeField] private Sprite levelUpTileSprite;
        [SerializeField] private TileAttributeVisualDatabase attributeVisuals;
        [SerializeField] private float panelWidth = 300f;
        [SerializeField] private float iconSize = 22f;              // 타일 그림 안 속성 아이콘 크기
        [SerializeField] private float cardIconSize = 34f;          // 속성 카드 제목 옆 아이콘 크기
        [Tooltip("정보 패널 왼쪽 경계의 화면 X 비율 (BattleInfoPanelUI 폭 30% 기준 = 0.70)")]
        [SerializeField, Range(0.4f, 1f)] private float dockAnchorX = 0.70f;
        [SerializeField] private Vector2 screenOffset = new Vector2(-16f, -16f);   // 도킹 지점 기준 (왼쪽/아래로)

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (attributeVisuals == null)
                attributeVisuals = Resources.Load<TileAttributeVisualDatabase>("UI/TileAttributeVisualDatabase");
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            Instance = FindFirstObjectByType<TileInfoPanelUI>(FindObjectsInactive.Include);
            if (Instance == null)
            {
                var go = new GameObject("TileInfoPanelUI");
                Instance = go.AddComponent<TileInfoPanelUI>();
            }
        }

        // ── 공개 API ──────────────────────────────────────────────

        public void Show(TileInfoData t)
        {
            rootCanvas.SetActive(true);

            SetTileVisual(t);
            RebuildAttributeCards(t);
        }

        public void Hide()
        {
            if (rootCanvas != null) rootCanvas.SetActive(false);
        }

        // ── 렌더링 ────────────────────────────────────────────────

        private void SetTileVisual(TileInfoData t)
        {
            if (tileImage != null)
            {
                var sprite = t.Type == Data.TileType.LevelUp ? levelUpTileSprite : normalTileSprite;
                if (sprite != null)
                {
                    tileImage.sprite = sprite;
                    tileImage.type = Image.Type.Simple;
                    tileImage.color = Color.white;
                }
                else
                {
                    UiSkin.Current.ApplySlot(tileImage);   // 스프라이트 미지정: 스킨 슬롯 (아이콘의 홈)
                }
            }

            // 그림 안 위쪽 아이콘 (인게임 타일 버블과 같은 감각)
            if (tileIconRow != null)
            {
                // 씬에 옛 설정이 박제돼 있어도 크기 제어가 먹도록 런타임에 강제 교정
                var rowLayout = tileIconRow.GetComponent<HorizontalLayoutGroup>();
                if (rowLayout != null)
                {
                    rowLayout.childControlWidth = true;
                    rowLayout.childControlHeight = true;
                    rowLayout.childForceExpandWidth = false;
                    rowLayout.childForceExpandHeight = false;
                }

                InfoPanelRows.Clear(tileIconRow);
                foreach (var a in t.Attributes)
                {
                    if (attributeVisuals == null || !attributeVisuals.TryGet(a.Type, out var e) || e.icon == null) continue;

                    var iconGo = new GameObject("AttrIcon", typeof(RectTransform));
                    iconGo.transform.SetParent(tileIconRow, false);

                    // 크기를 RectTransform에도 직접 지정 (레이아웃 설정과 무관하게 보장)
                    var rect = (RectTransform)iconGo.transform;
                    rect.sizeDelta = new Vector2(iconSize, iconSize);

                    var img = iconGo.AddComponent<Image>();
                    img.sprite = e.icon;
                    img.color = e.iconTint;
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                    var le = iconGo.AddComponent<LayoutElement>();
                    le.preferredWidth = iconSize;
                    le.preferredHeight = iconSize;
                }
            }
        }

        /// <summary>속성당 설명 카드 1장씩 세로로 쌓기 (구 툴팁 글로서리 카드 감각).</summary>
        private void RebuildAttributeCards(TileInfoData t)
        {
            if (stack == null) return;

            // 타일 카드(첫 자식)는 남기고 속성 카드만 재생성
            for (int i = stack.childCount - 1; i >= 1; i--)
                Destroy(stack.GetChild(i).gameObject);

            foreach (var a in t.Attributes)
            {
                string label = !string.IsNullOrWhiteSpace(a.DisplayName) ? a.DisplayName : a.Type.ToString();
                string desc = a.Description ?? "";
                Color tint = Color.white;
                Sprite icon = null;
                if (attributeVisuals != null && attributeVisuals.TryGet(a.Type, out var e))
                {
                    tint = e.iconTint;
                    icon = e.icon;
                    if (!string.IsNullOrWhiteSpace(e.description)) desc = e.description;   // DB 수동 오버라이드
                }

                string dur = a.Duration < 0 ? "" : $"{a.Duration}T";
                string stackText = a.Value > 0 ? a.Value.ToString() : "";
                string meta = $"{stackText} {dur}".Trim();
                string title = meta.Length > 0 ? $"{label}  {meta}" : label;

                CreateAttributeCard(icon, tint, title, desc);
            }
        }

        private void CreateAttributeCard(Sprite icon, Color tint, string title, string desc)
        {
            // 크림 카드 한 장
            var card = new GameObject("AttrCard", typeof(RectTransform));
            card.transform.SetParent(stack, false);
            var bg = card.AddComponent<Image>();
            UiSkin.Current.ApplyCard(bg);
            bg.raycastTarget = true;                                // 키워드 링크 호버 영역 확보

            var shadow = card.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            shadow.effectDistance = new Vector2(0f, -4f);

            var layout = card.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 4f;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            card.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 제목 행 (아이콘 + 이름 + 스택/지속) — 라이트 배경 색 보정
            InfoPanelRows.AddIconTextRow(card.transform, icon, tint, title, 19f, InfoPanelRows.OnLight(tint), FontStyles.Bold, cardIconSize);

            // 설명 (키워드 링크 → 커서 옆 정의 툴팁)
            if (!string.IsNullOrWhiteSpace(desc))
                InfoPanelRows.AddText(card.transform, desc, 15f, UiSkin.Current.InkMuted, FontStyles.Normal, linkKeywords: true);
        }
    }
}
