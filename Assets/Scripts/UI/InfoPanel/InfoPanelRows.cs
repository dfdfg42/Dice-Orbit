using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI
{
    /// <summary>정보 패널 내부 행(제목/본문/구분선) 생성 헬퍼. 패널 스타일 상수의 단일 출처.</summary>
    internal static class InfoPanelRows
    {
        public static readonly Color SectionTitleColor = new Color(1f, 0.85f, 0.55f);
        public static readonly Color PassiveColor      = new Color(1f, 0.6f, 0.4f);   // 기존 툴팁 관례 계승
        public static readonly Color MutedColor        = new Color(0.7f, 0.7f, 0.7f);
        public static readonly Color HpColor           = new Color(0.95f, 0.5f, 0.5f);
        public static readonly Color DiceColor         = new Color(0.62f, 0.9f, 1f);

        public static TextMeshProUGUI AddText(Transform parent, string text, float size,
            Color color, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static void AddSectionTitle(Transform parent, string title)
            => AddText(parent, title, 20f, SectionTitleColor, FontStyles.Bold);

        /// <summary>아이콘 + 텍스트 가로 행 (타일 속성 등). icon이 null이면 텍스트만.</summary>
        public static void AddIconTextRow(Transform parent, Sprite icon, Color iconTint,
            string text, float size, Color color, FontStyles style = FontStyles.Normal)
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
            le.preferredWidth = 22f;
            le.preferredHeight = 22f;

            var tmp = AddText(row.transform, text, size, color, style);
            tmp.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }

        public static void AddDivider(Transform parent)
        {
            var go = new GameObject("Divider", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.15f);
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
