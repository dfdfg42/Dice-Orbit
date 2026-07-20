using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 턴 전환 시 화면 중앙에 "플레이어 턴" / "몬스터 턴" 같은 안내를 띄우는 UI.
    /// 텍스트 또는 이미지 둘 다 지원. 씬에 미배치 시 자동 생성.
    /// </summary>
    public class TurnAnnouncementUI : MonoBehaviour
    {
        public static TurnAnnouncementUI Instance { get; private set; }

        [Header("Refs")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private Image image;

        [Header("턴 이미지 (지정 시 텍스트 대신 아트 이미지로 표시)")]
        [SerializeField] private Sprite playerTurnSprite;
        [SerializeField] private Sprite monsterTurnSprite;

        [Header("텍스트 폴백 색 (이미지 미지정 시)")]
        [SerializeField] private Color playerTurnColor  = new Color(0.55f, 0.85f, 1f, 1f);
        [SerializeField] private Color monsterTurnColor = new Color(1f, 0.45f, 0.45f, 1f);

        [Header("Timing")]
        [SerializeField] private float fadeIn  = 0.18f;
        [SerializeField] private float hold    = 0.65f;
        [SerializeField] private float fadeOut = 0.22f;

        public float TotalDuration => fadeIn + hold + fadeOut;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (group != null) group.alpha = 0f;
            if (label != null) label.gameObject.SetActive(false);
            if (image != null) image.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            var existing = FindAnyObjectByType<TurnAnnouncementUI>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return; }
            BuildDefault();
        }

        /// <summary>플레이어 턴 안내 — 이미지가 지정돼 있으면 이미지, 아니면 텍스트 폴백.</summary>
        public IEnumerator ShowPlayerTurn()
            => playerTurnSprite != null ? ShowImage(playerTurnSprite) : ShowText("플레이어 턴", playerTurnColor);

        /// <summary>몬스터 턴 안내 — 이미지가 지정돼 있으면 이미지, 아니면 텍스트 폴백.</summary>
        public IEnumerator ShowMonsterTurn()
            => monsterTurnSprite != null ? ShowImage(monsterTurnSprite) : ShowText("몬스터 턴", monsterTurnColor);

        public IEnumerator ShowText(string text, Color color)
        {
            if (label != null)
            {
                label.gameObject.SetActive(true);
                label.text  = text;
                label.color = color;
            }
            if (image != null) image.gameObject.SetActive(false);

            yield return RunFade();
        }

        public IEnumerator ShowImage(Sprite sprite)
        {
            if (image != null)
            {
                image.gameObject.SetActive(true);
                image.sprite = sprite;
            }
            if (label != null) label.gameObject.SetActive(false);

            yield return RunFade();
        }

        private IEnumerator RunFade()
        {
            if (group == null) yield break;

            // Fade in
            float t = 0f;
            while (t < fadeIn)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Clamp01(t / fadeIn);
                yield return null;
            }
            group.alpha = 1f;

            yield return new WaitForSecondsRealtime(hold);

            t = 0f;
            while (t < fadeOut)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = 1f - Mathf.Clamp01(t / fadeOut);
                yield return null;
            }
            group.alpha = 0f;

            if (label != null) label.gameObject.SetActive(false);
            if (image != null) image.gameObject.SetActive(false);
        }

        private static void BuildDefault()
        {
            var root = new GameObject("_TurnAnnouncementCanvas");
            var ui = root.AddComponent<TurnAnnouncementUI>();
            BuildHierarchyOn(root, ui);
            Instance = ui;
        }

        private static void BuildHierarchyOn(GameObject root, TurnAnnouncementUI ui)
        {
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            root.AddComponent<GraphicRaycaster>();

            var group = root.AddComponent<CanvasGroup>();
            group.alpha          = 0f;
            group.blocksRaycasts = false;
            group.interactable   = false;

            // Label
            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(root.transform, false);
            var labelRT = (RectTransform)labelGO.transform;
            labelRT.anchorMin = labelRT.anchorMax = new Vector2(0.5f, 0.5f);
            labelRT.sizeDelta = new Vector2(1200f, 240f);
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.alignment   = TextAlignmentOptions.Center;
            tmp.fontSize    = 96f;
            tmp.fontStyle   = FontStyles.Bold;
            tmp.color       = Color.white;
            tmp.raycastTarget = false;

            // Image (optional, swap later)
            var imageGO = new GameObject("Image", typeof(RectTransform));
            imageGO.transform.SetParent(root.transform, false);
            var imageRT = (RectTransform)imageGO.transform;
            imageRT.anchorMin = imageRT.anchorMax = new Vector2(0.5f, 0.5f);
            imageRT.sizeDelta = new Vector2(800f, 240f);
            var img = imageGO.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget  = false;
            imageGO.SetActive(false);

            ui.group = group;
            ui.label = tmp;
            ui.image = img;
        }
    }
}
