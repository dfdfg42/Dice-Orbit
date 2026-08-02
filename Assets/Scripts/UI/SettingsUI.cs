using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Core;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 환경설정 모달 — 그래픽(언어/해상도/전체화면) + 오디오(BGM/SFX). PlayerPrefs에 저장, 씬 로드 시 자동 적용.
    /// 메인메뉴/인게임 어디서든 Open()으로 열 수 있다.
    /// 레이아웃은 씬(Setting UI.prefab)에서 배치하고 아래 슬롯에 배선한다.
    /// 언어는 지금은 선택값만 저장한다 — 실제 번역 전환은 로컬라이제이션 도입 시 연결.
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        public static SettingsUI Instance { get; private set; }

        private const string KeyBgm = "opt_bgm_volume";
        private const string KeySfx = "opt_sfx_volume";
        private const string KeyFullscreen = "opt_fullscreen";
        private const string KeyLanguage = "opt_language";
        private const string KeyResolution = "opt_resolution";

        [Header("공통")]
        [SerializeField] private GameObject rootCanvas;

        [Header("오디오")]
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private TMP_Text bgmValueText;   // "100%"
        [SerializeField] private TMP_Text sfxValueText;   // "50%"

        [Header("그래픽")]
        [SerializeField] private TMP_Dropdown languageDropdown;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Toggle fullscreenToggle;

        [Header("버튼")]
        [SerializeField] private Button saveButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button closeButton;

        // 해상도 목록(고정) — 저장되는 건 이 배열의 인덱스다.
        private static readonly Vector2Int[] Resolutions =
        {
            new Vector2Int(1920, 1080),
            new Vector2Int(1600, 900),
            new Vector2Int(1366, 768),
            new Vector2Int(1280, 720),
        };

        // 언어 목록 — 표시용. 실제 번역은 아직 없음(선택값만 저장).
        private static readonly string[] Languages = { "한국어", "English" };

        private bool _listenersWired;

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

        // ── 저장값 조회/적용 (씬 로드 시 GameFlowManager가 호출) ──────

        public static float SavedBgmVolume => PlayerPrefs.GetFloat(KeyBgm, 1f);
        public static float SavedSfxVolume => PlayerPrefs.GetFloat(KeySfx, 1f);
        public static bool SavedFullscreen => PlayerPrefs.GetInt(KeyFullscreen, 1) == 1;
        public static int SavedLanguageIndex => Mathf.Clamp(PlayerPrefs.GetInt(KeyLanguage, 0), 0, Languages.Length - 1);
        public static int SavedResolutionIndex => Mathf.Clamp(PlayerPrefs.GetInt(KeyResolution, 0), 0, Resolutions.Length - 1);

        public static void ApplySavedSettings()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetBGMVolume(SavedBgmVolume);
                AudioManager.Instance.SetSFXVolume(SavedSfxVolume);
            }

            // 해상도는 빌드에서만 실제 적용 (에디터 게임뷰를 건드리지 않게).
#if UNITY_EDITOR
            Screen.fullScreen = SavedFullscreen;
#else
            var r = Resolutions[SavedResolutionIndex];
            Screen.SetResolution(r.x, r.y, SavedFullscreen);
#endif
        }

        // ── 공개 API ──────────────────────────────────────────

        public bool IsOpen => rootCanvas != null && rootCanvas.activeSelf;

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            WireControls();
            EnsureDropdownOptions();

            // 현재 저장값으로 컨트롤 동기화 (이벤트 없이)
            if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(SavedBgmVolume);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(SavedSfxVolume);
            if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(SavedFullscreen);
            if (languageDropdown != null) languageDropdown.SetValueWithoutNotify(SavedLanguageIndex);
            if (resolutionDropdown != null) resolutionDropdown.SetValueWithoutNotify(SavedResolutionIndex);
            UpdateVolumeLabels();

            gameObject.SetActive(true);
            if (rootCanvas != null) rootCanvas.SetActive(true);
        }

        public void Close()
        {
            PlayerPrefs.Save();
            if (rootCanvas != null) rootCanvas.SetActive(false);
        }

        // ── 내부 ──────────────────────────────────────────────

        private void EnsureDropdownOptions()
        {
            if (languageDropdown != null && languageDropdown.options.Count == 0)
            {
                languageDropdown.ClearOptions();
                languageDropdown.AddOptions(new List<string>(Languages));
            }
            if (resolutionDropdown != null && resolutionDropdown.options.Count == 0)
            {
                resolutionDropdown.ClearOptions();
                var opts = new List<string>();
                foreach (var r in Resolutions) opts.Add($"{r.x}X{r.y}");
                resolutionDropdown.AddOptions(opts);
            }
        }

        private void UpdateVolumeLabels()
        {
            if (bgmValueText != null && bgmSlider != null)
                bgmValueText.text = Mathf.RoundToInt(bgmSlider.value * 100f) + "%";
            if (sfxValueText != null && sfxSlider != null)
                sfxValueText.text = Mathf.RoundToInt(sfxSlider.value * 100f) + "%";
        }

        private void WireControls()
        {
            if (_listenersWired) return;

            bgmSlider?.onValueChanged.AddListener(v =>
            {
                PlayerPrefs.SetFloat(KeyBgm, v);
                AudioManager.Instance?.SetBGMVolume(v);
                if (bgmValueText != null) bgmValueText.text = Mathf.RoundToInt(v * 100f) + "%";
            });
            sfxSlider?.onValueChanged.AddListener(v =>
            {
                PlayerPrefs.SetFloat(KeySfx, v);
                AudioManager.Instance?.SetSFXVolume(v);
                if (sfxValueText != null) sfxValueText.text = Mathf.RoundToInt(v * 100f) + "%";
            });
            fullscreenToggle?.onValueChanged.AddListener(on =>
            {
                PlayerPrefs.SetInt(KeyFullscreen, on ? 1 : 0);
                Screen.fullScreen = on;
            });
            languageDropdown?.onValueChanged.AddListener(idx =>
            {
                // 선택값만 저장 — 실제 번역 전환은 로컬라이제이션 도입 시 여기서 적용.
                PlayerPrefs.SetInt(KeyLanguage, idx);
            });
            resolutionDropdown?.onValueChanged.AddListener(idx =>
            {
                idx = Mathf.Clamp(idx, 0, Resolutions.Length - 1);
                PlayerPrefs.SetInt(KeyResolution, idx);
                var r = Resolutions[idx];
                Screen.SetResolution(r.x, r.y, Screen.fullScreen);
            });

            saveButton?.onClick.AddListener(() =>
            {
                PlayerPrefs.Save();
                Close();
            });
            quitButton?.onClick.AddListener(QuitGame);
            closeButton?.onClick.AddListener(Close);

            _listenersWired = true;
        }

        private void QuitGame()
        {
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
