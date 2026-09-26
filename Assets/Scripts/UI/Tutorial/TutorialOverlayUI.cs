using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DiceOrbit.UI.Skin;

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
        private TextMeshProUGUI nextButtonLabel;
        private Button skipButton;
        private Action onSkip;
        private Vector2 _hlOffset;   // 현재 단계 하이라이트 이동
        private Vector4 _hlPad;      // 현재 단계 하이라이트 변별 확장 (x=좌,y=우,z=상,w=하)
        private bool _centerBubble;  // 대상이 있어도 말풍선을 화면 중앙(하단)에

        private static UiSkin Skin => UiSkin.Current;   // 룩은 UiSkin 단일 권위 (2026-09-25, TutorialSkin 철거)

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
                var img = NewImage(transform, Skin.Scrim, "Dim" + i);
                img.raycastTarget = true;   // 대상 외 클릭 차단
                dim[i] = img;
            }

            var bubbleImg = NewImage(transform, Color.white, "Bubble");
            Skin.Apply(bubbleImg, SkinPart.TutorialBubble);
            bubble = bubbleImg.rectTransform;
            bubble.anchorMin = bubble.anchorMax = bubble.pivot = new Vector2(0.5f, 0.5f);
            bubble.sizeDelta = new Vector2(560, 150);

            bubbleText = NewText(bubble, "", 26);
            var brt = bubbleText.rectTransform;
            brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 1);
            brt.offsetMin = new Vector2(24, 54); brt.offsetMax = new Vector2(-24, -18);

            nextButton = MakeButton(bubble, "다음", new Vector2(-24, 16), new Vector2(1, 0), new Vector2(170, 52), SkinButton.TutorialStart);   // 전용 4상태 스프라이트(배지 포함)가 40px 높이에선 짜부돼 52로 (2026-09-26)
            nextButtonLabel = nextButton.GetComponentInChildren<TextMeshProUGUI>();
            skipButton = MakeButton(transform, "튜토리얼 스킵", new Vector2(-16, -16), new Vector2(1, 1), new Vector2(190, 52), SkinButton.TutorialSkip);
            skipButton.onClick.AddListener(() => onSkip?.Invoke());

            gameObject.SetActive(false);
        }

        /// <param name="onNext">Confirm 진행 단계에서만 non-null → "다음" 버튼 노출.</param>
        public void ShowStep(string instruction, RectTransform screenTarget, bool gateInput, bool noSpotlight, Action onNext, Action onSkipAction)
            => ShowStep(instruction, screenTarget, gateInput, noSpotlight, onNext, onSkipAction, Vector2.zero, Vector4.zero);

        public void ShowStep(string instruction, RectTransform screenTarget, bool gateInput, bool noSpotlight, Action onNext, Action onSkipAction, Vector2 hlOffset, Vector4 hlPad, string nextLabel = "다음", bool centerBubble = false)
        {
            gameObject.SetActive(true);
            bubbleText.text = instruction;
            onSkip = onSkipAction;
            _hlOffset = hlOffset;
            _hlPad = hlPad;
            _centerBubble = centerBubble;

            nextButton.gameObject.SetActive(onNext != null);
            if (nextButtonLabel != null) nextButtonLabel.text = nextLabel;
            nextButton.onClick.RemoveAllListeners();
            if (onNext != null) nextButton.onClick.AddListener(() => onNext());

            if (noSpotlight)
            {
                // 딤 없이 화면 전체 밝게. gateInput=true면 dim[0]을 전체화면 투명막으로 만들어
                // 클릭만 차단(화면은 그대로 보임). 말풍선의 "다음"/"스킵"은 상위 형제라 통과된다.
                foreach (var d in dim) d.color = new Color(0f, 0f, 0f, 0f);
                dim[0].raycastTarget = gateInput;   // 전체 덮개(아래 HighlightScreenRect가 전체화면으로 배치)
                dim[1].raycastTarget = false;
                dim[2].raycastTarget = false;
                dim[3].raycastTarget = false;
                HighlightScreenRect(new Rect(-9999, -9999, 0, 0));
                return;
            }

            foreach (var d in dim) { d.color = Skin.Scrim; d.raycastTarget = gateInput; } // 잠금 아니면 클릭 통과

            if (screenTarget != null) HighlightScreenRect(AdjustRect(GetScreenRect(screenTarget)));
            else HighlightScreenRect(new Rect(-9999, -9999, 0, 0)); // 대상 없음 → 전체 딤
        }

        /// <summary>대상 RectTransform → 스크린 rect + 현재 단계 오프셋/패딩 적용해 하이라이트.</summary>
        public void HighlightTarget(RectTransform target) => HighlightScreenRect(AdjustRect(GetScreenRect(target)));

        /// <summary>현재 단계의 오프셋/패딩을 rect에 적용 (x=좌,y=우,z=상,w=하 확장 + 오프셋 이동).</summary>
        private Rect AdjustRect(Rect r)
        {
            return Rect.MinMaxRect(
                r.xMin - _hlPad.x + _hlOffset.x,
                r.yMin - _hlPad.w + _hlOffset.y,
                r.xMax + _hlPad.y + _hlOffset.x,
                r.yMax + _hlPad.z + _hlOffset.y);
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
            if (hasTarget && !_centerBubble)
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

        private Button MakeButton(Transform parent, string text, Vector2 pos, Vector2 anchor, Vector2 size, SkinButton kind)
        {
            var img = NewImage(parent, Color.white, "Btn");
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            var btn = img.gameObject.AddComponent<Button>();
            Skin.ApplyButton(btn, kind);
            var t = NewText(img.rectTransform, text, 20); Stretch(t.rectTransform);
            t.margin = kind == SkinButton.TutorialStart ? new Vector4(40, 0, 6, 0) : new Vector4(6, 0, 40, 0);   // 배지 자리 비우기 (2026-09-26)
            t.alignment = TextAlignmentOptions.Center;
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
            t.color = Skin.Ink; t.raycastTarget = false; t.enableWordWrapping = true;
            return t;
        }

        private static void Stretch(RectTransform r)
        { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }
    }
}
