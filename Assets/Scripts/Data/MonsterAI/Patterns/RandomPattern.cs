using UnityEngine;
using System.Collections.Generic;

namespace DiceOrbit.Data.MonsterAI.Patterns
{
    /// <summary>
    /// 랜덤 패턴 (가중치 없이 단순 랜덤 선택)
    /// </summary>
    [System.Serializable]
    public class RandomPattern : MonsterAI
    {
        public override MonsterSkill GetNextSkill()
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;
            return availableSkills[Random.Range(0, availableSkills.Count)];
        }
    }
}
