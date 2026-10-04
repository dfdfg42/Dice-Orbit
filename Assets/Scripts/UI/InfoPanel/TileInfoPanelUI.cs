using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.UI.Skin;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 타일 정보 패널 — 정보 패널 왼쪽 위에 도킹되는 **한 장짜리 평평한 패널** (2026-10-04 단순화).
    /// 맨 위에 타일 미리보기(인게임 타일과 같은 그림 + 그림 아래쪽에 속성 아이콘), 그 아래로 속성마다
    /// [아이콘 + 이름 (값·지속)] 한 줄과 설명이 세로로 쌓인다. 속성 사이는 간격으로만 구분한다.
    ///
    /// 예전에는 타일 그림을 액자에 넣고 속성마다 카드를 따로 띄웠다 — 패널이 겹겹이 붙어 정작 읽을 내용이 묻혔다.
    /// 액자와 카드는 없앴지만 **미리보기 그림은 남긴다** (사용자 결정 2026-10-04: "타일 미리보기는 있어야 한다").
    ///
    /// 표시 주체는 BattleInfoPanelUI가 결정 (캐릭터 조회 시 밟은 타일 / 타일 직접 호버·핀).
    /// 씬 구조: _TileInfoCanvas/Stack (세로 레이아웃) — Bg(PlainPanel 배경) · TilePreview/TileImage/TileIconRow · (런타임) 속성 줄.
    /// </summary>
    public class TileInfoPanelUI : MonoBehaviour
    {
        public static TileInfoPanelUI Instance { get; private set; }

        /// <summary>Stack에서 씬이 소유하는 고정 자식 수 (Bg, TilePreview). 그 뒤는 런타임에 쌓는 속성 줄이다.</summary>
        private const int FixedChildren = 2;

        [Header("슬롯 (씬에서 배치)")]
        [SerializeField] private GameObject rootCanvas;
        [Tooltip("패널 본체 — 자식 순서: Bg, TilePreview, 그 아래로 속성 줄")]
        [SerializeField] private RectTransform stack;
        [Tooltip("타일 미리보기 그림 (액자 없이 그림만)")]
        [SerializeField] private Image tileImage;
        [Tooltip("미리보기 그림 아래쪽의 속성 아이콘 행 (자리는 씬의 앵커가 정한다)")]
        [SerializeField] private RectTransform tileIconRow;

        [Header("스킨")]
        [SerializeField] private Sprite normalTileSprite;
        [SerializeField] private Sprite levelUpTileSprite;
        [SerializeField] private TileAttributeVisualDatabase attributeVisuals;
        [SerializeField] private float previewIconSize = 28f;   // 미리보기 그림 안 속성 아이콘
        [SerializeField] private float iconSize = 30f;          // 속성 줄 제목 옆 아이콘
        [SerializeField] private float titleSize = 21f;         // 오른쪽 정보 패널 항목(23/21)보다 한 단계 작게
        [SerializeField] private float bodySize = 18f;

        private string _shownSignature;   // 같은 내용이면 다시 쌓지 않는다 (정보 패널이 주기적으로 Show를 부른다)

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

        /// <summary>패널을 띄울 가치가 있는 타일인가 — 레벨업 타일이거나 속성이 하나라도 붙은 타일. 맨 일반 타일은 그림뿐이라 소음.</summary>
        public static bool HasContent(in TileInfoData t)
            => t.Type == Data.TileType.LevelUp || (t.Attributes != null && t.Attributes.Count > 0);

        public void Show(TileInfoData t)
        {
            rootCanvas.SetActive(true);

            string signature = Signature(t);
            if (signature == _shownSignature) return;
            _shownSignature = signature;

            SetTilePreview(t);
            RebuildAttributeLines(t);
        }

        public void Hide()
        {
            if (rootCanvas != null) rootCanvas.SetActive(false);
        }

        // ── 미리보기 ──────────────────────────────────────────────

        /// <summary>인게임 타일과 같은 그림 + 그림 아래쪽에 속성 아이콘.</summary>
        private void SetTilePreview(TileInfoData t)
        {
            if (tileImage == null || tileIconRow == null)
            {
                Debug.LogError("[TileInfoPanelUI] 타일 미리보기 슬롯이 비어 있습니다 — 씬 Stack/TilePreview 아래 TileImage·TileIconRow를 배선하세요.", this);
                return;
            }

            // 타일 재질 텍스처(new cardNormal)를 같은 비율로. 비면 에러 — 다른 그림으로 대체하지 않는다
            var sprite = t.Type == Data.TileType.LevelUp ? levelUpTileSprite : normalTileSprite;
            if (sprite == null)
            {
                Debug.LogError($"[TileInfoPanelUI] {t.Type} 타일 스프라이트가 비어 있습니다 — 씬 TileInfoPanelUI의 normalTileSprite/levelUpTileSprite에 타일 재질 텍스처를 배선하세요.", this);
            }
            else
            {
                tileImage.sprite = sprite;
                tileImage.type = Image.Type.Simple;
                tileImage.preserveAspect = true;
                tileImage.color = Color.white;
            }

            InfoPanelRows.Clear(tileIconRow);
            foreach (var a in t.Attributes)
            {
                if (attributeVisuals == null || !attributeVisuals.TryGet(a.Type, out var e) || e.icon == null) continue;

                var iconGo = new GameObject("AttrIcon", typeof(RectTransform));
                iconGo.transform.SetParent(tileIconRow, false);

                var img = iconGo.AddComponent<Image>();
                img.sprite = e.icon;
                img.color = e.iconTint;
                img.preserveAspect = true;
                img.raycastTarget = false;
                var le = iconGo.AddComponent<LayoutElement>();
                le.preferredWidth = previewIconSize;
                le.preferredHeight = previewIconSize;
            }
        }

        // ── 속성 줄 ───────────────────────────────────────────────

        /// <summary>속성 줄을 다시 쌓는다. 씬이 소유하는 고정 자식(배경·미리보기)은 남긴다.</summary>
        private void RebuildAttributeLines(TileInfoData t)
        {
            if (stack == null) return;

            for (int i = stack.childCount - 1; i >= FixedChildren; i--)
                Destroy(stack.GetChild(i).gameObject);

            foreach (var a in t.Attributes)
            {
                Resolve(a, out string title, out string desc, out Sprite icon, out Color tint);

                // 항목 = 제목 줄(아이콘 + 이름 + 값·지속) + 설명. 항목 사이 간격은 Stack의 spacing이 만든다.
                var entry = InfoPanelRows.AddEntry(stack);
                InfoPanelRows.AddIconTextRow(entry, icon, tint, title, titleSize, InfoPanelRows.OnLight(tint), FontStyles.Bold, iconSize);
                if (!string.IsNullOrWhiteSpace(desc))
                    InfoPanelRows.AddText(entry, desc, bodySize, UiSkin.Current.Ink, FontStyles.Normal, linkKeywords: true);   // 키워드 링크 → 커서 옆 정의 툴팁
            }
        }

        /// <summary>속성 1개의 표시 값 — 이름·설명은 속성이, 아이콘·틴트(와 수동 설명 오버라이드)는 비주얼 DB가 준다.</summary>
        private void Resolve(in TileAttributeInfo a, out string title, out string desc, out Sprite icon, out Color tint)
        {
            string label = !string.IsNullOrWhiteSpace(a.DisplayName) ? a.DisplayName : a.Type.ToString();
            desc = a.Description ?? "";
            tint = Color.white;
            icon = null;
            if (attributeVisuals != null && attributeVisuals.TryGet(a.Type, out var e))
            {
                tint = e.iconTint;
                icon = e.icon;
                if (!string.IsNullOrWhiteSpace(e.description)) desc = e.description;   // DB 수동 오버라이드
            }

            string dur = a.Duration < 0 ? "" : $"{a.Duration}T";
            string stackText = a.Value > 0 ? a.Value.ToString() : "";
            string meta = $"{stackText} {dur}".Trim();
            title = meta.Length > 0 ? $"{label}  {meta}" : label;
        }

        private static string Signature(in TileInfoData t)
        {
            var sb = new StringBuilder();
            sb.Append(t.TileIndex).Append('/').Append((int)t.Type);
            foreach (var a in t.Attributes)
                sb.Append('|').Append((int)a.Type).Append(':').Append(a.Value).Append(':').Append(a.Duration).Append(':').Append(a.Description);
            return sb.ToString();
        }
    }
}
