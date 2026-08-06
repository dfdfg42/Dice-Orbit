using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 행동하는 몬스터의 머리 위에 스킬명 말풍선을 띄우는 오버레이 UI.
    /// 화면 좌표로 대상 몬스터를 매 프레임 따라다니며, 씬에 없으면 자동 생성한다.
    /// (TurnAnnouncementUI의 자동 생성/페이드 패턴을 미러. 한글 폰트는 씬의 기존 TMP에서 빌려온다.)
    /// </summary>
    public class MonsterActionLabel : MonoBehaviour
    {
        public static MonsterActionLabel Instance { get; private set; }

        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform bubbleRT;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private float fadeIn  = 0.12f;
        [SerializeField] private float fadeOut = 0.12f;

        private Core.Monster target;
        private Camera cam;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (group != null) group.alpha = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            var existing = FindAnyObjectByType<MonsterActionLabel>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return; }
            Build();
        }

        /// <summary>대상 몬스터 머리 위에 text 말풍선을 페이드 인. Hide() 전까지 대상을 따라다닌다.</summary>
        public IEnumerator Show(Core.Monster monster, string text)
        {
            if (monster == null) yield break;
            target = monster;
            if (label != null) label.text = text;
            Reposition();
            yield return Fade(0f, 1f, fadeIn);
        }

        /// <summary>말풍선을 페이드 아웃하고 추적을 해제한다.</summary>
        public IEnumerator Hide()
        {
            yield return Fade(group != null ? group.alpha : 1f, 0f, fadeOut);
            target = null;
        }

        private void LateUpdate()
        {
            if (target != null) Reposition();
        }

        private void Reposition()
        {
            if (bubbleRT == null) return;
            if (target == null) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            Vector3 sp = cam.WorldToScreenPoint(target.GetHeadTopWorld());
            if (sp.z < 0f) { if (group != null) group.alpha = 0f; return; }
            bubbleRT.position = sp;
        }

        private IEnumerator Fade(float from, float to, float dur)
        {
            if (group == null) yield break;
            group.alpha = from;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, dur <= 0f ? 1f : t / dur);
                yield return null;
            }
            group.alpha = to;
        }

        private static void Build()
        {
            // 한글 지원 폰트를 씬의 기존 TMP에서 미리 확보(우리 라벨 생성 전에).
            TMP_FontAsset borrowed = null;
            var anyText = FindAnyObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include);
            if (anyText != null) borrowed = anyText.font;

            var root = new GameObject("_MonsterActionLabelCanvas");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            root.AddComponent<GraphicRaycaster>();

            var ui = root.AddComponent<MonsterActionLabel>();

            var group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;

            // 말풍선 배경 — pivot을 하단 중앙으로 두어 머리 지점 '위'에 뜨게 한다.
            var bubbleGO = new GameObject("Bubble", typeof(RectTransform));
            bubbleGO.transform.SetParent(root.transform, false);
            var brt = (RectTransform)bubbleGO.transform;
            brt.sizeDelta = new Vector2(260f, 60f);
            brt.pivot = new Vector2(0.5f, 0f);
            var bg = bubbleGO.AddComponent<Image>();
            bg.color = new Color(0.09f, 0.09f, 0.12f, 0.86f);
            bg.raycastTarget = false;

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(bubbleGO.transform, false);
            var lrt = (RectTransform)labelGO.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(12f, 6f); lrt.offsetMax = new Vector2(-12f, -6f);
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 16f;
            tmp.fontSizeMax = 32f;
            if (borrowed != null) tmp.font = borrowed;

            ui.group = group;
            ui.bubbleRT = brt;
            ui.label = tmp;
            Instance = ui;
        }
    }
}
