using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
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
