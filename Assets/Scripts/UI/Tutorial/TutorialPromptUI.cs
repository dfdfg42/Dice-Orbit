using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DiceOrbit.UI.Tutorial
{
    /// <summary>"튜토리얼 하시겠어요?" 런타임 모달. 씬 배치 불필요 — Show()가 캔버스째 생성.</summary>
    public class TutorialPromptUI : MonoBehaviour
    {
        public static void Show(Action onYes, Action onNo)
        {
            var go = new GameObject("TutorialPromptUI");
            var self = go.AddComponent<TutorialPromptUI>();
            self.Build(onYes, onNo);
        }

        private void Build(Action onYes, Action onNo)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            gameObject.AddComponent<GraphicRaycaster>();

            var dim = NewImage(transform, new Color(0f, 0f, 0f, 0.6f));
            Stretch(dim.rectTransform);

            var panel = NewImage(transform, new Color(0.118f, 0.133f, 0.200f, 1f));
            var prt = panel.rectTransform;
            prt.sizeDelta = new Vector2(560, 260);
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = Vector2.zero;

            var label = NewText(panel.transform, "튜토리얼을 진행하시겠어요?", 34);
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0, 0.5f); lrt.anchorMax = new Vector2(1, 1f);
            lrt.offsetMin = new Vector2(20, 0); lrt.offsetMax = new Vector2(-20, -20);

            MakeButton(panel.transform, "예", new Vector2(-130, -70), new Color(0.878f, 0.702f, 0.341f),
                () => { Close(); onYes?.Invoke(); });
            MakeButton(panel.transform, "아니오", new Vector2(130, -70), new Color(0.200f, 0.255f, 0.368f),
                () => { Close(); onNo?.Invoke(); });
        }

        private void Close() => Destroy(gameObject);

        private void MakeButton(Transform parent, string text, Vector2 pos, Color color, Action onClick)
        {
            var img = NewImage(parent, color);
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(200, 66);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());
            var t = NewText(img.transform, text, 28);
            Stretch(t.rectTransform);
        }

        private static UnityEngine.UI.Image NewImage(Transform parent, Color c)
        {
            var go = new GameObject("Img", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.color = c;
            return img;
        }

        private static TextMeshProUGUI NewText(Transform parent, string s, float size)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = s; t.fontSize = size; t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.910f, 0.894f, 0.847f); t.raycastTarget = false;
            return t;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }
    }
}
