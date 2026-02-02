using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Data.Waves
{
    [CreateAssetMenu(fileName = "WaveDatabase", menuName = "Dice Orbit/Waves/Wave Database")]
    public class WaveDatabase : ScriptableObject
    {
        public List<WaveDefinition> Waves = new List<WaveDefinition>();
    }
}
