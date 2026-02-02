using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DiceOrbit.UI
{
    public class RewardUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Button continueButton;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI itemsText;

        private void Awake()
        {
            if (continueButton != null)
            {
                continueButton.onClick.AddListener(OnContinueClicked);
            }
        }

        public void Show()
        {
            if (panel != null) panel.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void OnContinueClicked()
        {
            if (Core.RewardManager.Instance != null)
            {
                Core.RewardManager.Instance.ClaimReward();
            }
            if (Core.GameFlowManager.Instance != null)
            {
                Core.GameFlowManager.Instance.OnRewardComplete();
            }
        }

        private void Refresh()
        {
            var reward = Core.RewardManager.Instance;
            if (reward == null) return;

            if (goldText != null)
            {
                goldText.text = $"Gold: {reward.PendingGold}";
            }

            if (itemsText != null)
            {
                if (reward.PendingItems.Count == 0)
                {
                    itemsText.text = "Items: None";
                }
                else
                {
                    string names = string.Join(", ", System.Linq.Enumerable.Select(reward.PendingItems, i => i != null ? i.ItemName : "Unknown"));
                    itemsText.text = $"Items: {names}";
                }
            }
        }
    }
}
