using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Core;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 환경설정 모달 — BGM/SFX 볼륨 + 전체화면. PlayerPrefs에 저장, 씬 로드 시 자동 적용.
    /// 메인메뉴/인게임 어디서든 Open()으로 열 수 있다.
    ///
    /// ── 에디터 소유 레이아웃 ─────────────────────────────────────
    /// 컴포넌트 우클릭 → [기본 레이아웃 생성] → 씬에서 자유롭게 스타일링.
    /// 슬롯이 비어 있으면 런타임 폴백 생성.
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        public static SettingsUI Instance { get; private set; }

        private const string KeyBgm = "opt_bgm_volume";
        private const string KeySfx = "opt_sfx_volume";
        private const string KeyFullscreen = "opt_fullscreen";

        [Header("슬롯 (씬에서 배치 — [기본 레이아웃 생성]으로 자동 배선)")]
        [SerializeField] private GameObject rootCanvas;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Button closeButton;

        private bool _listenersWired;
        private TMP_FontAsset _font;

        // 보드게임의 밤 팔레트
        private static readonly Color Felt     = new Color(0.043f, 0.051f, 0.078f, 0.85f);
        private static readonly Color Card     = new Color(0.118f, 0.133f, 0.200f);
        private static readonly Color CardEdge = new Color(0.239f, 0.271f, 0.400f);
        private static readonly Color CardWell = new Color(0.082f, 0.094f, 0.153f);
        private static readonly Color Ink      = new Color(0.910f, 0.894f, 0.847f);
        private static readonly Color Gold     = new Color(0.878f, 0.702f, 0.341f);
        private static readonly Color GoldInk  = new Color(0.140f, 0.110f, 0.055f);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            Instance = FindFirstObjectByType<SettingsUI>(FindObjectsInactive.Include);
            if (Instance == null)
            {
                var go = new GameObject("SettingsUI");
                Instance = go.AddComponent<SettingsUI>();
            }
        }

        // ── 저장값 적용 (씬 로드 시 GameFlowManager가 호출) ──────

        public static float SavedBgmVolume => PlayerPrefs.GetFloat(KeyBgm, 1f);
        public static float SavedSfxVolume => PlayerPrefs.GetFloat(KeySfx, 1f);
        public static bool SavedFullscreen => PlayerPrefs.GetInt(KeyFullscreen, 1) == 1;

        public static void ApplySavedSettings()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetBGMVolume(SavedBgmVolume);
                AudioManager.Instance.SetSFXVolume(SavedSfxVolume);
            }
            Screen.fullScreen = SavedFullscreen;
        }

        // ── 공개 API ──────────────────────────────────────────

        public void Open()
        {
            if (rootCanvas == null) BuildDefaultLayout();
            WireControls();

            // 현재 저장값으로 컨트롤 동기화 (이벤트 없이)
            if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(SavedBgmVolume);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(SavedSfxVolume);
            if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(SavedFullscreen);

            gameObject.SetActive(true);
            rootCanvas.SetActive(true);
        }

        public void Close()
        {
            PlayerPrefs.Save();
            if (rootCanvas != null) rootCanvas.SetActive(false);
        }

        private void WireControls()
        {
            if (_listenersWired) return;
            if (bgmSlider == null && closeButton == null) return;

            bgmSlider?.onValueChanged.AddListener(v =>
            {
                PlayerPrefs.SetFloat(KeyBgm, v);
                AudioManager.Instance?.SetBGMVolume(v);
            });
            sfxSlider?.onValueChanged.AddListener(v =>
            {
                PlayerPrefs.SetFloat(KeySfx, v);
                AudioManager.Instance?.SetSFXVolume(v);
            });
            fullscreenToggle?.onValueChanged.AddListener(on =>
            {
                PlayerPrefs.SetInt(KeyFullscreen, on ? 1 : 0);
                Screen.fullScreen = on;
            });
            closeButton?.onClick.AddListener(Close);
            _listenersWired = true;
        }

        // ─────────────────────────────────────────────
        // [에디터] 기본 레이아웃 생성 (런타임 폴백 겸용)
        // ─────────────────────────────────────────────
        [ContextMenu("기본 레이아웃 생성")]
        private void BuildDefaultLayout()
        {
            if (transform.Find("_SettingsCanvas") != null)
            {
                Debug.LogWarning("[SettingsUI] _SettingsCanvas가 이미 있습니다. 다시 생성하려면 기존 것을 삭제하세요.");
                return;
            }

            _font = FindAnyObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include)?.font;

            var canvasGo = new GameObject("_SettingsCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;   // 모든 게임 UI 위 (커서 툴팁 30000 아래)
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();
            rootCanvas = canvasGo;

            // 딤
            var dim = new GameObject("Dim", typeof(RectTransform));
            dim.transform.SetParent(canvasGo.transform, false);
            var dimRect = (RectTransform)dim.transform;
            dimRect.anchorMin = Vector2.zero; dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero; dimRect.offsetMax = Vector2.zero;
            dim.AddComponent<Image>().color = Felt;

            // 패널
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(canvasGo.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(620, 480);
            var edge = panel.AddComponent<Image>();
            edge.sprite = UiRoundedSprite.Get(26);
            edge.type = Image.Type.Sliced;
            edge.color = CardEdge;
            var shadow = panel.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(0f, -10f);

            var bg = new GameObject("BG", typeof(RectTransform));
            bg.transform.SetParent(panel.transform, false);
            var bgRect = (RectTransform)bg.transform;
            bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = new Vector2(3, 3); bgRect.offsetMax = new Vector2(-3, -3);
            var bgImg = bg.AddComponent<Image>();
            bgImg.sprite = UiRoundedSprite.Get(23);
            bgImg.type = Image.Type.Sliced;
            bgImg.color = Card;
            bgImg.raycastTarget = false;

            var title = CreateText(panel, "환경설정", 40, FontStyles.Bold);
            PlaceTop(title, 30, 56);

            bgmSlider = CreateSliderRow(panel, "배경음", -130);
            sfxSlider = CreateSliderRow(panel, "효과음", -210);
            fullscreenToggle = CreateToggleRow(panel, "전체화면", -290);

            closeButton = CreateButton(panel, "닫기", new Vector2(0, -400));

            rootCanvas.SetActive(false);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
            Debug.Log("[SettingsUI] 기본 레이아웃 생성 완료 — 계층을 자유롭게 스타일링한 뒤 씬을 저장하세요.");
        }

        private Slider CreateSliderRow(GameObject parent, string label, float y)
        {
            var labelTmp = CreateText(parent, label, 26, FontStyles.Bold);
            PlaceAt(labelTmp.rectTransform, new Vector2(-170, y), new Vector2(160, 40));
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;

            var sliderGo = new GameObject($"Slider_{label}", typeof(RectTransform));
            sliderGo.transform.SetParent(parent.transform, false);
            var rect = (RectTransform)sliderGo.transform;
            PlaceAt(rect, new Vector2(80, y), new Vector2(320, 28));

            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;

            // 트랙
            var track = new GameObject("Track", typeof(RectTransform));
            track.transform.SetParent(sliderGo.transform, false);
            var trackRect = (RectTransform)track.transform;
            trackRect.anchorMin = new Vector2(0f, 0.35f); trackRect.anchorMax = new Vector2(1f, 0.65f);
            trackRect.offsetMin = Vector2.zero; trackRect.offsetMax = Vector2.zero;
            var trackImg = track.AddComponent<Image>();
            trackImg.sprite = UiRoundedSprite.Get(6);
            trackImg.type = Image.Type.Sliced;
            trackImg.color = CardWell;

            // 채움
            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            var fillAreaRect = (RectTransform)fillArea.transform;
            fillAreaRect.anchorMin = new Vector2(0f, 0.35f); fillAreaRect.anchorMax = new Vector2(1f, 0.65f);
            fillAreaRect.offsetMin = Vector2.zero; fillAreaRect.offsetMax = Vector2.zero;
            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(fillArea.transform, false);
            var fillImg = fill.AddComponent<Image>();
            fillImg.sprite = UiRoundedSprite.Get(6);
            fillImg.type = Image.Type.Sliced;
            fillImg.color = Gold;
            slider.fillRect = (RectTransform)fill.transform;

            // 핸들
            var handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            var handleAreaRect = (RectTransform)handleArea.transform;
            handleAreaRect.anchorMin = Vector2.zero; handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = Vector2.zero; handleAreaRect.offsetMax = Vector2.zero;
            var handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(handleArea.transform, false);
            var handleRect = (RectTransform)handle.transform;
            handleRect.sizeDelta = new Vector2(26, 26);
            var handleImg = handle.AddComponent<Image>();
            handleImg.sprite = UiRoundedSprite.Get(13);
            handleImg.type = Image.Type.Sliced;
            handleImg.color = Ink;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImg;

            return slider;
        }

        private Toggle CreateToggleRow(GameObject parent, string label, float y)
        {
            var labelTmp = CreateText(parent, label, 26, FontStyles.Bold);
            PlaceAt(labelTmp.rectTransform, new Vector2(-170, y), new Vector2(200, 40));
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;

            var toggleGo = new GameObject($"Toggle_{label}", typeof(RectTransform));
            toggleGo.transform.SetParent(parent.transform, false);
            PlaceAt((RectTransform)toggleGo.transform, new Vector2(210, y), new Vector2(44, 44));

            var box = toggleGo.AddComponent<Image>();
            box.sprite = UiRoundedSprite.Get(10);
            box.type = Image.Type.Sliced;
            box.color = CardWell;

            var check = new GameObject("Check", typeof(RectTransform));
            check.transform.SetParent(toggleGo.transform, false);
            var checkRect = (RectTransform)check.transform;
            checkRect.anchorMin = Vector2.zero; checkRect.anchorMax = Vector2.one;
            checkRect.offsetMin = new Vector2(9, 9); checkRect.offsetMax = new Vector2(-9, -9);
            var checkImg = check.AddComponent<Image>();
            checkImg.sprite = UiRoundedSprite.Get(6);
            checkImg.type = Image.Type.Sliced;
            checkImg.color = Gold;

            var toggle = toggleGo.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = checkImg;
            toggle.isOn = true;
            return toggle;
        }

        private Button CreateButton(GameObject parent, string label, Vector2 pos)
        {
            var go = new GameObject($"Button_{label}", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(240, 66);

            var img = go.AddComponent<Image>();
            img.sprite = UiRoundedSprite.Get(18);
            img.type = Image.Type.Sliced;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = ColorBlock.defaultColorBlock;
            cb.normalColor = Gold;
            cb.highlightedColor = Color.Lerp(Gold, Color.white, 0.12f);
            cb.pressedColor = Color.Lerp(Gold, Color.black, 0.2f);
            cb.selectedColor = Gold;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            var txt = CreateText(go, label, 28, FontStyles.Bold);
            txt.color = GoldInk;
            var r = txt.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            return btn;
        }

        private TextMeshProUGUI CreateText(GameObject parent, string text, float size, FontStyles style)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Ink;
            tmp.raycastTarget = false;
            if (_font != null) tmp.font = _font;
            return tmp;
        }

        private static void PlaceTop(TextMeshProUGUI tmp, float topOffset, float height)
        {
            var r = tmp.rectTransform;
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -topOffset);
            r.sizeDelta = new Vector2(0f, height);
        }

        private static void PlaceAt(RectTransform r, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }
    }
}
