using System;
using DiceOrbit.UI.Skin;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DiceOrbit.UI.Tutorial
{
    /// <summary>"튜토리얼 하시겠어요?" 런타임 모달. 씬 배치 불필요 — Show()가 캔버스째 생성. 룩은 UiSkin (2026-09-25).</summary>
    public class TutorialPromptUI : MonoBehaviour
    {
        private static UiSkin Skin => UiSkin.Current;

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

            var dim = NewImage(transform, Skin.Scrim);
            Stretch(dim.rectTransform);

            var panel = NewImage(transform, Color.white);
            Skin.Apply(panel, SkinPart.TutorialPromptFrame);
            var prt = panel.rectTransform;
            prt.sizeDelta = new Vector2(560, 260);
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = Vector2.zero;

            var label = NewText(panel.transform, "튜토리얼을 시작할까요?", 34);
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0, 0.5f); lrt.anchorMax = new Vector2(1, 1f);
            lrt.offsetMin = new Vector2(20, 0); lrt.offsetMax = new Vector2(-20, -20);

            MakeButton(panel.transform, "시작", new Vector2(-130, -70), SkinButton.TutorialStart, () => { Close(); onYes?.Invoke(); });
            MakeButton(panel.transform, "건너뛰기", new Vector2(130, -70), SkinButton.TutorialSkip, () => { Close(); onNo?.Invoke(); });
        }

        private void Close() => Destroy(gameObject);

        private void MakeButton(Transform parent, string text, Vector2 pos, SkinButton kind, Action onClick)
        {
            var img = NewImage(parent, Color.white);
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(200, 66);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            var btn = img.gameObject.AddComponent<Button>();
            Skin.ApplyButton(btn, kind);
            btn.onClick.AddListener(() => onClick());
            var t = NewText(img.transform, text, 28);
            Stretch(t.rectTransform);
            t.margin = kind == SkinButton.TutorialStart ? new Vector4(44, 0, 8, 0) : new Vector4(8, 0, 44, 0);   // 전용 버튼의 배지(왼쪽/오른쪽) 자리를 비워 글자가 겹치지 않게 (2026-09-26)
        }

        private static Image NewImage(Transform parent, Color c)
        {
            var go = new GameObject("Img", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = c;
            return img;
        }

        private static TextMeshProUGUI NewText(Transform parent, string s, float size)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = s; t.fontSize = size; t.alignment = TextAlignmentOptions.Center;
            t.color = Skin.Ink; t.raycastTarget = false;
            return t;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }
    }
}
