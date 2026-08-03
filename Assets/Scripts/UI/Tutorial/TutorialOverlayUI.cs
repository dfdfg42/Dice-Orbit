using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DiceOrbit.UI.Tutorial
{
    /// <summary>
    /// 튜토리얼 오버레이 — 대상 rect만 밝게 남기는 4분할 딤 스포트라이트 + 말풍선 + 스킵.
    /// 런타임 생성(씬 배치 불필요). 최상위 sortingOrder로 항상 위.
    /// </summary>
    public class TutorialOverlayUI : MonoBehaviour
    {
        public static TutorialOverlayUI Instance { get; private set; }

        private Canvas canvas;
        private UnityEngine.UI.Image[] dim = new UnityEngine.UI.Image[4]; // top/bottom/left/right
        private RectTransform bubble;
        private TextMeshProUGUI bubbleText;
        private Button nextButton;
        private Button skipButton;
        private Action onSkip;

        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.72f);
        private static readonly Color Card = new Color(0.118f, 0.133f, 0.200f, 0.98f);
        private static readonly Color Ink = new Color(0.910f, 0.894f, 0.847f);
        private static readonly Color Gold = new Color(0.878f, 0.702f, 0.341f);

        public static TutorialOverlayUI EnsureInstance()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("TutorialOverlayUI");
            Instance = go.AddComponent<TutorialOverlayUI>();
            Instance.Build();
            return Instance;
        }

        private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Build()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4000;
            gameObject.AddComponent<GraphicRaycaster>();

            for (int i = 0; i < 4; i++)
            {
                var img = NewImage(transform, Dim, "Dim" + i);
                img.raycastTarget = true;   // 대상 외 클릭 차단
                dim[i] = img;
            }

            bubble = NewImage(transform, Card, "Bubble").rectTransform;
            bubble.anchorMin = bubble.anchorMax = bubble.pivot = new Vector2(0.5f, 0.5f);
            bubble.sizeDelta = new Vector2(560, 150);

            bubbleText = NewText(bubble, "", 26);
            var brt = bubbleText.rectTransform;
            brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 1);
            brt.offsetMin = new Vector2(24, 54); brt.offsetMax = new Vector2(-24, -18);

            nextButton = MakeButton(bubble, "다음", new Vector2(-24, 16), new Vector2(1, 0), new Vector2(140, 40), Gold);
            skipButton = MakeButton(transform, "튜토리얼 스킵", new Vector2(-16, -16), new Vector2(1, 1), new Vector2(160, 40),
                new Color(0.35f, 0.30f, 0.32f, 0.9f));
            skipButton.onClick.AddListener(() => onSkip?.Invoke());

            gameObject.SetActive(false);
        }

        /// <param name="onNext">Confirm 진행 단계에서만 non-null → "다음" 버튼 노출.</param>
        public void ShowStep(string instruction, RectTransform screenTarget, bool gateInput, bool noSpotlight, Action onNext, Action onSkipAction)
        {
            gameObject.SetActive(true);
            bubbleText.text = instruction;
            onSkip = onSkipAction;

            nextButton.gameObject.SetActive(onNext != null);
            nextButton.onClick.RemoveAllListeners();
            if (onNext != null) nextButton.onClick.AddListener(() => onNext());

            if (noSpotlight)
            {
                // 딤 없이 화면 전체 밝게 — 클릭도 전부 통과. 말풍선만 하단-중앙에.
                foreach (var d in dim) { d.color = new Color(0f, 0f, 0f, 0f); d.raycastTarget = false; }
                HighlightScreenRect(new Rect(-9999, -9999, 0, 0));
                return;
            }

            foreach (var d in dim) { d.color = Dim; d.raycastTarget = gateInput; } // 잠금 아니면 클릭 통과

            if (screenTarget != null) HighlightScreenRect(GetScreenRect(screenTarget));
            else HighlightScreenRect(new Rect(-9999, -9999, 0, 0)); // 대상 없음 → 전체 딤
        }

        /// <summary>대상 rect(스크린 좌표)만 남기고 4방향 딤 배치 + 말풍선 위치 조정.</summary>
        public void HighlightScreenRect(Rect r)
        {
            float w = Screen.width, h = Screen.height;
            float yMin = Mathf.Max(0, r.yMin), yMax = Mathf.Min(h, r.yMax);
            float bandH = Mathf.Max(0, yMax - yMin);

            SetRect(dim[0], 0, r.yMax, w, h - r.yMax);                 // top
            SetRect(dim[1], 0, 0, w, yMin);                            // bottom
            SetRect(dim[2], 0, yMin, Mathf.Max(0, r.xMin), bandH);     // left
            SetRect(dim[3], r.xMax, yMin, Mathf.Max(0, w - r.xMax), bandH); // right

            // 말풍선 위치. 유효 대상이 있으면 그 아래(공간 없으면 위), 없으면(전체 딤) 화면 하단-중앙.
            float bubbleH = bubble.sizeDelta.y, bubbleW = bubble.sizeDelta.x;
            bool hasTarget = r.xMax > 0f && r.yMax > 0f && r.width > 0f && r.height > 0f;
            float bx, by;
            if (hasTarget)
            {
                bx = r.center.x;
                by = r.yMin - 24 - bubbleH * 0.5f;                                  // 대상 아래
                if (by - bubbleH * 0.5f < 24) by = r.yMax + 24 + bubbleH * 0.5f;    // 아래 공간 없으면 위
            }
            else
            {
                bx = w * 0.5f;
                by = h * 0.30f;
            }
            // 항상 화면 안에 완전히 들어오도록 클램프
            bx = Mathf.Clamp(bx, bubbleW * 0.5f + 16, w - bubbleW * 0.5f - 16);
            by = Mathf.Clamp(by, bubbleH * 0.5f + 16, h - bubbleH * 0.5f - 16);
            bubble.anchorMin = bubble.anchorMax = new Vector2(0, 0);
            bubble.pivot = new Vector2(0.5f, 0.5f);
            bubble.anchoredPosition = new Vector2(bx, by);
        }

        public void Hide() { onSkip = null; gameObject.SetActive(false); }

        public static Rect GetScreenRect(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            // 대상 캔버스 모드에 맞춰 스크린 픽셀로 변환 (Overlay면 cam=null → 그대로 스크린).
            var canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
            for (int i = 0; i < 4; i++)
                corners[i] = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
            float xMin = Mathf.Min(corners[0].x, corners[2].x);
            float yMin = Mathf.Min(corners[0].y, corners[2].y);
            float xMax = Mathf.Max(corners[0].x, corners[2].x);
            float yMax = Mathf.Max(corners[0].y, corners[2].y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private void SetRect(UnityEngine.UI.Image img, float x, float y, float w, float h)
        {
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(Mathf.Max(0, w), Mathf.Max(0, h));
        }

        private Button MakeButton(Transform parent, string text, Vector2 pos, Vector2 anchor, Vector2 size, Color color)
        {
            var img = NewImage(parent, color, "Btn");
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
            var t = NewText(img.rectTransform, text, 20); Stretch(t.rectTransform);
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.14f, 0.11f, 0.055f);
            return btn;
        }

        private static UnityEngine.UI.Image NewImage(Transform parent, Color c, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<UnityEngine.UI.Image>(); img.color = c;
            return img;
        }

        private static TextMeshProUGUI NewText(Transform parent, string s, float size)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = s; t.fontSize = size; t.alignment = TextAlignmentOptions.Left;
            t.color = Ink; t.raycastTarget = false; t.enableWordWrapping = true;
            return t;
        }

        private static void Stretch(RectTransform r)
        { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }
    }
}
