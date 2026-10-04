using System.Text;
using TMPro;
using UnityEngine;
using DiceOrbit.UI.Skin;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 타일 정보 패널 — 정보 패널 왼쪽 위에 도킹되는 **한 장짜리 평평한 패널** (2026-10-04 단순화).
    /// 타일에 붙은 속성마다 [아이콘 + 이름 (값·지속)] 한 줄과 설명을 세로로 쌓는다. 속성 사이는 간격으로만 구분한다.
    ///
    /// 예전에는 타일 그림 액자 + 속성마다 따로 뜨는 카드였다 — 패널이 겹겹이 붙어 정작 읽을 내용(속성 설명)이 묻혔다.
    /// 타일 그림은 보드에 이미 있고, 속성 아이콘은 제목 줄 옆에 한 번이면 충분하다.
    ///
    /// 표시 주체는 BattleInfoPanelUI가 결정 (캐릭터 조회 시 밟은 타일 / 타일 직접 호버·핀).
    /// 씬 구조: _TileInfoCanvas/Stack (세로 레이아웃, 첫 자식 Bg = PlainPanel 배경) — 줄은 런타임에 쌓는다.
    /// </summary>
    public class TileInfoPanelUI : MonoBehaviour
    {
        public static TileInfoPanelUI Instance { get; private set; }

        [Header("슬롯 (씬에서 배치)")]
        [SerializeField] private GameObject rootCanvas;
        [Tooltip("패널 본체 — 첫 자식은 배경(Bg), 그 아래로 속성 줄이 세로로 쌓인다")]
        [SerializeField] private RectTransform stack;

        [Header("스킨")]
        [SerializeField] private TileAttributeVisualDatabase attributeVisuals;
        [SerializeField] private float iconSize = 30f;      // 제목 줄 옆 속성 아이콘
        [SerializeField] private float titleSize = 21f;     // 오른쪽 정보 패널 항목(23/21)보다 한 단계 작게
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

        /// <summary>패널을 띄울 가치가 있는 타일인가 — 속성이 하나라도 붙은 타일. 속성 없는 타일은 말할 거리가 없다.</summary>
        public static bool HasContent(in TileInfoData t)
            => t.Attributes != null && t.Attributes.Count > 0;

        public void Show(TileInfoData t)
        {
            rootCanvas.SetActive(true);

            string signature = Signature(t);
            if (signature == _shownSignature) return;
            _shownSignature = signature;

            Rebuild(t);
        }

        public void Hide()
        {
            if (rootCanvas != null) rootCanvas.SetActive(false);
        }

        // ── 렌더링 ────────────────────────────────────────────────

        /// <summary>속성 줄을 다시 쌓는다. 배경(첫 자식)은 남긴다.</summary>
        private void Rebuild(TileInfoData t)
        {
            if (stack == null) return;

            for (int i = stack.childCount - 1; i >= 1; i--)
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
            sb.Append(t.TileIndex);
            foreach (var a in t.Attributes)
                sb.Append('|').Append((int)a.Type).Append(':').Append(a.Value).Append(':').Append(a.Duration).Append(':').Append(a.Description);
            return sb.ToString();
        }
    }
}
