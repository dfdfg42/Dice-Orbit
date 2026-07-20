using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Core;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 환경설정 모달 — BGM/SFX 볼륨 + 전체화면. PlayerPrefs에 저장, 씬 로드 시 자동 적용.
    /// 메인메뉴/인게임 어디서든 Open()으로 열 수 있다.
    /// 레이아웃은 씬에서 배치하고 아래 슬롯에 배선한다.
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        public static SettingsUI Instance { get; private set; }

        private const string KeyBgm = "opt_bgm_volume";
        private const string KeySfx = "opt_sfx_volume";
        private const string KeyFullscreen = "opt_fullscreen";

        [Header("슬롯 (씬에서 배치하고 배선)")]
        [SerializeField] private GameObject rootCanvas;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Button closeButton;

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

        public bool IsOpen => rootCanvas != null && rootCanvas.activeSelf;

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
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
    }
}
