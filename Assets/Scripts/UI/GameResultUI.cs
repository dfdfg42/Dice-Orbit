using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 게임 결과(게임오버 / 승리) 전체 화면 오버레이 + 다시 시작 버튼.
    /// 씬에 미배치 시 런타임 자동 생성. 씬 종속(재시작 시 함께 파괴됨).
    /// </summary>
    public class GameResultUI : MonoBehaviour
    {
        public static GameResultUI Instance { get; private set; }

        private CanvasGroup group;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI messageText;
        private Button restartButton;

        private static readonly Color GameOverColor = new Color(1f, 0.4f, 0.4f, 1f);
        private static readonly Color VictoryColor = new Color(1f, 0.85f, 0.35f, 1f);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static GameResultUI EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<GameResultUI>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            return Build();
        }

        public static void ShowGameOver()
        {
            EnsureInstance().Show("게임 오버", "파티가 전멸했습니다.", GameOverColor);
        }

        public static void ShowVictory()
        {
            EnsureInstance().Show("클리어!",
                "프로토타입은 여기까지입니다.\n플레이해주셔서 감사합니다!", VictoryColor);
        }

        public void Show(string title, string message, Color titleColor)
        {
            if (titleText != null) { titleText.text = title; titleText.color = titleColor; }
            if (messageText != null) messageText.text = message;
            SetVisible(true);
            BattleInfoPanelUI.SetVisible(false);   // 결과 화면 동안 정보 패널 숨김
        }

        public void Hide() => SetVisible(false);

        private void SetVisible(bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;
        }

        private void OnRestartClicked()
        {
            Hide();
            Core.GameFlowManager.Instance?.RestartGame();
        }

        // ─────────────────────────────────────────────
        // 자동 생성
        // ─────────────────────────────────────────────
        private static GameResultUI Build()
        {
            var root = new GameObject("_GameResultCanvas");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            root.AddComponent<GraphicRaycaster>();

            var group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var ui = root.AddComponent<GameResultUI>();
            ui.group = group;

            // 씬의 기존 TMP에서 한글 지원 폰트를 빌려온다 (자동 생성 텍스트의 글리프 깨짐 방지)
            TMP_FontAsset koreanFont = null;
            var sample = FindAnyObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include);
            if (sample != null) koreanFont = sample.font;

            // 어두운 배경
            var dim = CreateChild(root.transform, "Dim");
            StretchFull(dim);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.82f);

            // 타이틀
            var titleGO = CreateChild(root.transform, "Title");
            titleGO.anchorMin = titleGO.anchorMax = new Vector2(0.5f, 0.5f);
            titleGO.anchoredPosition = new Vector2(0, 140);
            titleGO.sizeDelta = new Vector2(1400, 200);
            ui.titleText = titleGO.gameObject.AddComponent<TextMeshProUGUI>();
            ui.titleText.alignment = TextAlignmentOptions.Center;
            ui.titleText.fontSize = 110;
            ui.titleText.fontStyle = FontStyles.Bold;
            ui.titleText.raycastTarget = false;
            if (koreanFont != null) ui.titleText.font = koreanFont;

            // 메시지
            var msgGO = CreateChild(root.transform, "Message");
            msgGO.anchorMin = msgGO.anchorMax = new Vector2(0.5f, 0.5f);
            msgGO.anchoredPosition = new Vector2(0, -10);
            msgGO.sizeDelta = new Vector2(1400, 240);
            ui.messageText = msgGO.gameObject.AddComponent<TextMeshProUGUI>();
            ui.messageText.alignment = TextAlignmentOptions.Center;
            ui.messageText.fontSize = 48;
            ui.messageText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            ui.messageText.raycastTarget = false;
            if (koreanFont != null) ui.messageText.font = koreanFont;

            // 다시 시작 버튼
            var btnGO = CreateChild(root.transform, "RestartButton");
            btnGO.anchorMin = btnGO.anchorMax = new Vector2(0.5f, 0.5f);
            btnGO.anchoredPosition = new Vector2(0, -260);
            btnGO.sizeDelta = new Vector2(420, 110);
            var btnImg = btnGO.gameObject.AddComponent<Image>();
            btnImg.color = new Color(0.25f, 0.55f, 0.95f, 1f);
            ui.restartButton = btnGO.gameObject.AddComponent<Button>();
            ui.restartButton.targetGraphic = btnImg;
            ui.restartButton.onClick.AddListener(ui.OnRestartClicked);

            var labelGO = CreateChild(btnGO, "Label");
            StretchFull(labelGO);
            var label = labelGO.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = "다시 시작";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 44;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.raycastTarget = false;
            if (koreanFont != null) label.font = koreanFont;

            Instance = ui;
            return ui;
        }

        private static RectTransform CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
