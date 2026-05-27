using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Core;

namespace DiceOrbit.UI
{
    public class RewardUI : MonoBehaviour
    {
        [SerializeField] private Button confirmButton;

        private void Awake()
        {
            if (confirmButton == null)
                confirmButton = GetComponentInChildren<Button>(includeInactive: true);

            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        private void OnDestroy()
        {
            if (confirmButton != null)
                confirmButton.onClick.RemoveListener(OnConfirmClicked);
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnConfirmClicked()
        {
            GameFlowManager.Instance?.OnRewardComplete();
        }
    }
}
