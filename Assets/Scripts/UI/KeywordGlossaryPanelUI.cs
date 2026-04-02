using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI
{
    public class KeywordGlossaryPanelUI : MonoBehaviour
    {
        private RectTransform rootRect;
        private TextMeshProUGUI bodyText;

        private readonly Vector2 padding = new Vector2(12f, 10f);
        private readonly Vector2 offset = new Vector2(14f, 0f);

        public void Initialize(Transform parent, TMP_FontAsset font)
        {
            transform.SetParent(parent, false);

            rootRect = gameObject.AddComponent<RectTransform>();
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.9f);
            bg.raycastTarget = false;

            var textGo = new GameObject("GlossaryText");
            textGo.transform.SetParent(transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            bodyText = textGo.AddComponent<TextMeshProUGUI>();
            bodyText.fontSize = 18f;
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.raycastTarget = false;
            bodyText.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            if (font != null) bodyText.font = font;

            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(padding.x, padding.y);
            textRect.offsetMax = new Vector2(-padding.x, -padding.y);

            Hide();
        }

        public void SetFont(TMP_FontAsset font)
        {
            if (font != null && bodyText != null)
            {
                bodyText.font = font;
            }
        }

        public void Show(IReadOnlyList<TooltipKeywordFormatter.KeywordDisplayData> keywords, Vector2 tooltipPos, Vector2 tooltipSize)
        {
            if (rootRect == null || bodyText == null)
            {
                return;
            }

            if (keywords == null || keywords.Count == 0)
            {
                Hide();
                return;
            }

            var lines = new List<string>();
            for (int i = 0; i < keywords.Count; i++)
            {
                var keyword = keywords[i];
                string colorHex = ColorUtility.ToHtmlStringRGB(keyword.Color);
                lines.Add($"• <color=#{colorHex}>{keyword.Key}</color>");
                lines.Add($"  {keyword.Description}");
            }

            bodyText.text = string.Join("\n", lines);
            var preferred = bodyText.GetPreferredValues(bodyText.text, 380f, 0f);
            rootRect.sizeDelta = new Vector2(Mathf.Clamp(preferred.x + padding.x * 2f, 220f, 420f), preferred.y + padding.y * 2f);
            rootRect.gameObject.SetActive(true);

            Reposition(tooltipPos, tooltipSize);
        }

        public void Reposition(Vector2 tooltipPos, Vector2 tooltipSize)
        {
            if (rootRect == null || !rootRect.gameObject.activeSelf)
            {
                return;
            }

            float x = tooltipPos.x + tooltipSize.x + offset.x;
            float y = tooltipPos.y;

            var size = rootRect.sizeDelta;
            if (x + size.x > Screen.width)
            {
                x = tooltipPos.x - size.x - offset.x;
            }

            x = Mathf.Clamp(x, 0f, Mathf.Max(0f, Screen.width - size.x));
            y = Mathf.Clamp(y, size.y, Screen.height);
            rootRect.position = new Vector2(x, y);
        }

        public void Hide()
        {
            if (rootRect != null)
            {
                rootRect.gameObject.SetActive(false);
            }
        }
    }
}
