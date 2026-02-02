using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Button startButton;
        [SerializeField] private Button quitButton;

        private void Awake()
        {
            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartClicked);
            }
            if (quitButton != null)
            {
                quitButton.onClick.AddListener(OnQuitClicked);
            }
        }

        public void Show()
        {
            if (panel != null) panel.SetActive(true);
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void OnStartClicked()
        {
            Debug.Log("[MainMenuUI] Start button clicked");
            if (Core.GameFlowManager.Instance != null)
            {
                Core.GameFlowManager.Instance.StartGame();
            }
            else
            {
                Debug.LogWarning("[MainMenuUI] GameFlowManager.Instance is NULL");
            }
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
