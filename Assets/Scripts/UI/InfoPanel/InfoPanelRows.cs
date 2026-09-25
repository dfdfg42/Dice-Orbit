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

        /// <summary>섹션 헤더 서식: Figma 배틀 UI는 크림 칩 위 텍스트만 (핍 없음).</summary>
        public static string FormatSectionTitle(string title)
            => title;

        /// <summary>섹션 헤더: 골드 핍 + 검정 잉크 볼드, 큼직하게.</summary>
        public static void AddSectionTitle(Transform parent, string title)
            => AddText(parent, FormatSectionTitle(title), 26f, Skin.Ink, FontStyles.Bold);

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

        public static void AddDivider(Transform parent)
        {
            var go = new GameObject("Divider", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.10f, 0.11f, 0.16f, 0.12f);   // 종이 위 옅은 잉크 선
            img.raycastTarget = false;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 2f;
            le.preferredHeight = 2f;
            le.flexibleWidth = 1f;
        }

        public static void Clear(Transform content)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
                Object.Destroy(content.GetChild(i).gameObject);
        }
    }
}
