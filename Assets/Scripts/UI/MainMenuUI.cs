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

        private void Awake()
        {
            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartGameButtonClicked);
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
    }
}
