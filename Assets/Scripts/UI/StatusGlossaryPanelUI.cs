using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI
{
    public class StatusGlossaryPanelUI : MonoBehaviour
    {
        private RectTransform rootRect;
        private TextMeshProUGUI bodyText;

        private readonly Vector2 padding = new Vector2(12f, 10f);
        private readonly Vector2 offset = new Vector2(0f, -14f);

        public void Initialize(Transform parent, TMP_FontAsset font)
        {
            transform.SetParent(parent, false);

            rootRect = gameObject.AddComponent<RectTransform>();
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.9f);
            bg.raycastTarget = false;

            var textGo = new GameObject("StatusText");
            textGo.transform.SetParent(transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            bodyText = textGo.AddComponent<TextMeshProUGUI>();
            bodyText.fontSize = 17f;
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.raycastTarget = false;
            bodyText.color = new Color(0.92f, 0.97f, 1f, 1f);
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

        public void Show(IReadOnlyList<TooltipKeywordFormatter.StatusDisplayData> statuses, Vector2 tooltipPos, Vector2 tooltipSize)
        {
            if (rootRect == null || bodyText == null)
            {
                return;
            }

            if (statuses == null || statuses.Count == 0)
            {
                Hide();
                return;
            }

            var lines = new List<string> { "<b>상태 이상</b>" };
            for (int i = 0; i < statuses.Count; i++)
            {
                var status = statuses[i];
                string colorHex = ColorUtility.ToHtmlStringRGB(status.Color);
                string header = $"• <color=#{colorHex}>{status.Name}</color>";

                if (!string.IsNullOrWhiteSpace(status.StackText))
                {
                    header += $"  {status.StackText}";
                }

                if (!string.IsNullOrWhiteSpace(status.DurationText))
                {
                    header += $"  {status.DurationText}";
                }

                lines.Add(header);

                if (!string.IsNullOrWhiteSpace(status.Description))
                {
                    lines.Add($"  {status.Description}");
                }
            }

            bodyText.text = string.Join("\n", lines);
            var preferred = bodyText.GetPreferredValues(bodyText.text, 430f, 0f);
            rootRect.sizeDelta = new Vector2(Mathf.Clamp(preferred.x + padding.x * 2f, 240f, 470f), preferred.y + padding.y * 2f);
            rootRect.gameObject.SetActive(true);

            Reposition(tooltipPos, tooltipSize);
        }

        public void Reposition(Vector2 tooltipPos, Vector2 tooltipSize)
        {
            if (rootRect == null || !rootRect.gameObject.activeSelf)
            {
                return;
            }

            float x = tooltipPos.x;
            float y = tooltipPos.y - tooltipSize.y - offset.y;

            var size = rootRect.sizeDelta;
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
