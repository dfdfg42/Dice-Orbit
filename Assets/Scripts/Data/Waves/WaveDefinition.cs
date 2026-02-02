using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Data.Items;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Data.Waves
{
    [CreateAssetMenu(fileName = "WaveDefinition", menuName = "Dice Orbit/Waves/Wave Definition")]
    public class WaveDefinition : ScriptableObject
    {
        [Header("Spawn Settings")]
        public int SpawnCount = 3;

        [Header("Monster Pool")]
        public List<MonsterPreset> MonsterPresets = new List<MonsterPreset>();

        [Header("Reward")]
        public int RewardGoldMin = 20;
        public int RewardGoldMax = 50;
        public int RewardPotionCount = 1;
        public List<ConsumableItem> PotionPool = new List<ConsumableItem>();
    }
}
