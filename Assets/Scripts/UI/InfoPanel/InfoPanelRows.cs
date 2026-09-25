using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.UI.Skin;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 정보 패널 내부 행(제목/본문/구분선) 생성 헬퍼. 패널 스타일 상수의 단일 출처.
    /// 테마: 크림색 점수지(scoresheet) — 밝은 종이 위 진한 잉크 (보상 화면의 다크 테이블과 한 세트).
    /// </summary>
    internal static class InfoPanelRows
    {
        // 팔레트는 UiSkin이 단일 권위 (2026-09-25 리스킨 3단계) — 파일에 색을 복사하지 않는다.
        private static UiSkin Skin => UiSkin.Current;

        /// <summary>모디파이어 효과 라인용 리치텍스트 색 (스킬 설명에 인라인 삽입 시).</summary>
        public static string ModifierColorHex => "#" + ColorUtility.ToHtmlStringRGB(Skin.Modifier);

        /// <summary>보조 정보(주사위 조건·대상 등 메타)용 리치텍스트 색.</summary>
        public static string MutedColorHex => "#" + ColorUtility.ToHtmlStringRGB(Skin.InkMuted);

        // ── 항목 타이포 (2026-09-25 정리) — 제목 볼드 > 설명 보통 > 메타 작게. 씬 섹션 제목 칩(24 볼드)보다 한 단계 아래.
        public const float EntryTitleSize = 23f;
        public const float EntryBodySize = 21f;
        public const float EntryMetaSize = 19f;
        public const float EntryInnerSpacing = 2f;      // 제목 행 ↔ 설명

        /// <summary>
        /// 항목 1개(제목 행 + 설명)를 묶는 세로 그룹. 컨테이너의 spacing이 항목 사이 간격, 이 그룹의 spacing이 제목↔설명 간격이라
        /// 항목끼리는 띄고 안쪽은 붙는다 (구: 행을 컨테이너에 직접 넣어 모든 줄 간격이 균일 → 항목 경계가 안 보였다).
        /// </summary>
        public static RectTransform AddEntry(Transform parent)
        {
            var go = new GameObject("Entry", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = EntryInnerSpacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return (RectTransform)go.transform;
        }

        /// <summary>
        /// DB에서 온 색(다크 배경용으로 설계됨)을 밝은 배경 위에서 읽히게 어둡게 보정.
        /// 상태이상/키워드/타일 틴트 등 외부 색 소스에 사용.
        /// </summary>
        public static Color OnLight(Color c)
            => Color.Lerp(c, new Color(0.08f, 0.09f, 0.12f), 0.45f);

        public static TextMeshProUGUI AddText(Transform parent, string text, float size,
            Color color, FontStyles style = FontStyles.Normal, bool linkKeywords = false)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();

            // 키워드 링크: DB 키워드를 <link>로 감싸고 호버 감지에 등록 → 커서 옆 툴팁으로 정의 표시
            if (linkKeywords)
            {
                tmp.text = TooltipKeywordFormatter.InsertKeywordLinks(text, onLightBackground: true);
                KeywordLinkHover.Register(tmp);
            }
            else
            {
                tmp.text = text;
            }

            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>섹션 헤더 서식: 크림 칩 위 텍스트만 (핍 없음). 칩·글자 크기는 씬(TitleChip)이 소유.</summary>
        public static string FormatSectionTitle(string title)
            => title;

        /// <summary>아이콘 + 텍스트 가로 행 (타일 속성 등). icon이 null이면 텍스트만.</summary>
        public static void AddIconTextRow(Transform parent, Sprite icon, Color iconTint,
            string text, float size, Color color, FontStyles style = FontStyles.Normal, float iconSize = 22f)
        {
            if (icon == null)
            {
                AddText(parent, text, size, color, style);
                return;
            }

            var row = new GameObject("IconRow", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(row.transform, false);
            var img = iconGo.AddComponent<Image>();
            img.sprite = icon;
            img.color = iconTint;
            img.preserveAspect = true;
            img.raycastTarget = false;
            var le = iconGo.AddComponent<LayoutElement>();
            le.preferredWidth = iconSize;
            le.preferredHeight = iconSize;

            var tmp = AddText(row.transform, text, size, color, style);
            tmp.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }

        public static void Clear(Transform content)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
                Object.Destroy(content.GetChild(i).gameObject);
        }
    }
}
