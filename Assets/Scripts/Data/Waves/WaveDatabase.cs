using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Data.Waves
{
    [System.Serializable]
    public class WaveDefinition
    {
        public List<MonsterPreset> MonsterPresets;
        public int SpawnCount = 1;
        public Sprite BackgroundSprite;
        // Reward info can be added here
    }

    [CreateAssetMenu(fileName = "New Wave Database", menuName = "Dice Orbit/Waves/Wave Database")]
    public class WaveDatabase : ScriptableObject
    {
        public List<WaveDefinition> Waves = new List<WaveDefinition>();
    }
}
