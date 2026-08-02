using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Core;

namespace DiceOrbit.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Button startButton;
        [SerializeField] private Button continueButton;    // 이어하기 — 세이브 있을 때만 활성
        [SerializeField] private Button settingsButton;    // 환경설정 모달
        [SerializeField] private Button quitButton;
        [SerializeField] private MainMenuCharacterDisplay characterDisplay;

        private void Awake()
        {
            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartGameButtonClicked);
            }

            if (continueButton != null)
            {
                continueButton.onClick.AddListener(OnContinueButtonClicked);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(OnSettingsButtonClicked);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(OnQuitButtonClicked);
            }
        }

        public void Show()
        {
            if (panel != null)
            {
                panel.SetActive(true);
            }
            else
            {
                gameObject.SetActive(true);
            }

            // 이어하기: 세이브가 있을 때만 누를 수 있게
            if (continueButton != null)
                continueButton.interactable = Core.Run.Save.RunSaveService.HasSave();

            characterDisplay?.PlayEntranceAnimation();
        }

        public void Hide()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        public void OnStartGameButtonClicked()
        {
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.StartGame();
            }
        }
        public void OnContinueButtonClicked()
        {
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.ContinueGame();
            }
        }

        public void OnSettingsButtonClicked()
        {
            SettingsUI.EnsureInstance();
            SettingsUI.Instance?.Open();
        }

        public void OnQuitButtonClicked()
        {
            Debug.Log("Quit Game");
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
