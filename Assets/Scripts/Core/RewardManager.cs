using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Data.Items;
using DiceOrbit.Data.Waves;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 웨이브 보상 관리자 (골드/포션 등)
    /// </summary>
    public class RewardManager : MonoBehaviour
    {
        public static RewardManager Instance { get; private set; }

        private int pendingGold = 0;
        private readonly List<ConsumableItem> pendingItems = new List<ConsumableItem>();

        public int PendingGold => pendingGold;
        public IReadOnlyList<ConsumableItem> PendingItems => pendingItems;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void PrepareReward(WaveDefinition waveDef)
        {
            pendingItems.Clear();
            pendingGold = 0;

            if (waveDef == null) return;

            int min = Mathf.Min(waveDef.RewardGoldMin, waveDef.RewardGoldMax);
            int max = Mathf.Max(waveDef.RewardGoldMin, waveDef.RewardGoldMax);
            pendingGold = Random.Range(min, max + 1);

            int count = Mathf.Max(0, waveDef.RewardPotionCount);
            if (waveDef.PotionPool != null && waveDef.PotionPool.Count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    var item = waveDef.PotionPool[Random.Range(0, waveDef.PotionPool.Count)];
                    if (item != null) pendingItems.Add(item);
                }
            }
        }

        public void ClaimReward()
        {
            if (pendingGold > 0)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.AddGold(pendingGold);
                }
            }

            if (pendingItems.Count > 0)
            {
                if (InventoryManager.Instance != null)
                {
                    foreach (var item in pendingItems)
                    {
                        InventoryManager.Instance.AddItem(item);
                    }
                }
            }

            pendingGold = 0;
            pendingItems.Clear();
        }
    }
}
