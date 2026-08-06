using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 행동하는 몬스터의 머리 위에 [아이콘 + 스킬명] 말풍선을 띄우는 오버레이 UI.
    /// 정보 패널(BattleInfoPanelUI)과 같은 톤 — 크림 종이 배경 + 잉크 텍스트(InfoPanelRows 팔레트 재사용).
    /// 화면 좌표로 대상 몬스터를 매 프레임 따라다니며, 씬에 없으면 자동 생성한다.
    /// </summary>
    public class MonsterActionLabel : MonoBehaviour
    {
        public static MonsterActionLabel Instance { get; private set; }

        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform bubbleRT;
        [SerializeField] private Image iconImage;
        [SerializeField] private GameObject iconGO;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private float fadeIn  = 0.12f;
        [SerializeField] private float fadeOut = 0.12f;
        [Tooltip("머리 위 앵커에서 추가로 위로 올리는 화면 픽셀(해상도 일관, 클수록 더 위)")]
        [SerializeField] private float screenYOffset = 48f;

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

        /// <summary>대상 몬스터 머리 위에 [아이콘 + text] 말풍선을 페이드 인. icon이 null이면 텍스트만. Hide() 전까지 추적.</summary>
        public IEnumerator Show(Core.Monster monster, string text, Sprite icon = null)
        {
            if (monster == null) yield break;
            target = monster;
            if (label != null) label.text = text;

            if (iconGO != null)
            {
                bool has = icon != null;
                if (iconImage != null && has) iconImage.sprite = icon;
                if (iconGO.activeSelf != has) iconGO.SetActive(has);
            }

            RebuildLayout();
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

        private void RebuildLayout()
        {
            if (bubbleRT != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(bubbleRT);
        }

        private void Reposition()
        {
            if (bubbleRT == null || target == null) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            Vector3 sp = cam.WorldToScreenPoint(target.GetHeadTopWorld());
            if (sp.z < 0f) { if (group != null) group.alpha = 0f; return; }
            sp.y += screenYOffset;
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

            // ── 말풍선 배경 (크림 종이 패널, 하단 중앙 pivot → 머리 지점 '위'에 뜸) ──
            var bubbleGO = new GameObject("Bubble", typeof(RectTransform));
            bubbleGO.transform.SetParent(root.transform, false);
            var brt = (RectTransform)bubbleGO.transform;
            brt.pivot = new Vector2(0.5f, 0f);

            var bg = bubbleGO.AddComponent<Image>();
            bg.color = InfoPanelRows.PaperColor;
            bg.raycastTarget = false;

            // UI 테두리 (잉크색 아웃라인) — 정보 패널과 같은 잉크 톤
            var outline = bubbleGO.AddComponent<Outline>();
            var ink = InfoPanelRows.InkDark;
            outline.effectColor = new Color(ink.r, ink.g, ink.b, 1f);
            outline.effectDistance = new Vector2(3f, 3f);
            outline.useGraphicAlpha = false;

            // 카드 느낌의 옅은 그림자
            var shadow = bubbleGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
            shadow.effectDistance = new Vector2(2f, -2f);

            var hlg = bubbleGO.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(12, 14, 6, 6);
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;  hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            var fitter = bubbleGO.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            // ── 아이콘 (스킬 IntentIcon) ──
            var iconGO = new GameObject("Icon", typeof(RectTransform));
            iconGO.transform.SetParent(bubbleGO.transform, false);
            var img = iconGO.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            var iconLE = iconGO.AddComponent<LayoutElement>();
            iconLE.preferredWidth = 30f; iconLE.preferredHeight = 30f;

            // ── 스킬명 라벨 (잉크색 볼드) ──
            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(bubbleGO.transform, false);
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.fontSize = 28f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = InfoPanelRows.InkDark;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            if (borrowed != null) tmp.font = borrowed;

            ui.group = group;
            ui.bubbleRT = brt;
            ui.iconImage = img;
            ui.iconGO = iconGO;
            ui.label = tmp;
            Instance = ui;
        }
    }
}
