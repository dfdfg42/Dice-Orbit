using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core.Run;

namespace DiceOrbit.Data.Waves
{
    // 구 웨이브 DB — Act 풀로 이관 후 삭제 예정 (읽기 전용 존치)
    [CreateAssetMenu(fileName = "New Wave Database", menuName = "Dice Orbit/Waves/Wave Database")]
    public class WaveDatabase : ScriptableObject
    {
        public List<EncounterDefinition> Waves = new List<EncounterDefinition>();
    }
}
