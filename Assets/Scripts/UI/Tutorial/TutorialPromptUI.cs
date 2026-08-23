using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DiceOrbit.UI.Tutorial
{
    /// <summary>"튜토리얼 하시겠어요?" 런타임 모달. 씬 배치 불필요 — Show()가 캔버스째 생성.
    /// 오버레이 말풍선과 동일한 스킨(TutorialSkin: 크림 패널 + 둥근 버튼)을 공유한다.</summary>
    public class TutorialPromptUI : MonoBehaviour
    {
        private static readonly Color Card    = new Color(0.118f, 0.133f, 0.200f, 1f);   // 스킨 없을 때 패널
        private static readonly Color Ink     = new Color(0.910f, 0.894f, 0.847f);       // 밝은 글자(단색 폴백)
        private static readonly Color InkDark = new Color(0.16f, 0.12f, 0.08f);          // 크림 배경용 어두운 글자
        private static readonly Color BtnInk  = new Color(0.14f, 0.11f, 0.055f);         // 버튼 글자(크림 위)
        private static readonly Color Gold    = new Color(0.878f, 0.702f, 0.341f);       // "예"
        private static readonly Color Tan     = new Color(0.72f, 0.66f, 0.56f);          // "아니오"(크림 스킨)
        private static readonly Color Navy    = new Color(0.200f, 0.255f, 0.368f);       // "아니오"(단색 폴백)

        private Sprite _panel, _button;

        public static void Show(Action onYes, Action onNo)
        {
            var go = new GameObject("TutorialPromptUI");
            var self = go.AddComponent<TutorialPromptUI>();
            self.Build(onYes, onNo);
        }

        private void Build(Action onYes, Action onNo)
        {
            var skin = TutorialSkin.Get();
            _panel  = skin != null ? skin.panelSprite  : null;
            _button = skin != null ? skin.buttonSprite : null;
            bool cream = _panel != null;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            gameObject.AddComponent<GraphicRaycaster>();

            var dim = NewImage(transform, new Color(0f, 0f, 0f, 0.72f));
            Stretch(dim.rectTransform);

            var panel = NewImage(transform, cream ? Color.white : Card);
            if (cream) { panel.sprite = _panel; panel.type = UnityEngine.UI.Image.Type.Sliced; }
            var prt = panel.rectTransform;
            prt.sizeDelta = new Vector2(560, 260);
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = Vector2.zero;

            var label = NewText(panel.transform, "튜토리얼을 시작할까요?", 34);
            label.color = cream ? InkDark : Ink;
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0, 0.5f); lrt.anchorMax = new Vector2(1, 1f);
            lrt.offsetMin = new Vector2(20, 0); lrt.offsetMax = new Vector2(-20, -20);

            MakeButton(panel.transform, "시작", new Vector2(-130, -70), Gold, cream,
                () => { Close(); onYes?.Invoke(); });
            MakeButton(panel.transform, "건너뛰기", new Vector2(130, -70), cream ? Tan : Navy, cream,
                () => { Close(); onNo?.Invoke(); });
        }

        private void Close() => Destroy(gameObject);

        private void MakeButton(Transform parent, string text, Vector2 pos, Color color, bool cream, Action onClick)
        {
            var img = NewImage(parent, color);
            if (_button != null) { img.sprite = _button; img.type = UnityEngine.UI.Image.Type.Sliced; }
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(200, 66);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());
            var t = NewText(img.transform, text, 28);
            t.color = cream ? BtnInk : Ink;
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
