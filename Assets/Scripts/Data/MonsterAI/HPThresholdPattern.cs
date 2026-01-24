using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.Data.MonsterAI
{
    [CreateAssetMenu(fileName = "HP Threshold Pattern", menuName = "Dice Orbit/Monster AI/Pattern (HP Threshold)")]
    public class HPThresholdPattern : MonsterPattern
    {
        [Header("Settings")]
        [Range(0f, 1f)] public float ThresholdPercent = 0.5f;

        [Header("Patterns")]
        public MonsterPattern NormalPattern;
        public MonsterPattern CriticalPattern;

        public override void Initialize(Monster monster)
        {
            if (NormalPattern != null) NormalPattern.Initialize(monster);
            if (CriticalPattern != null) CriticalPattern.Initialize(monster);
        }

        public override MonsterSkill GetNextSkill(Monster monster)
        {
            if (monster.Stats.HPRatio <= ThresholdPercent)
            {
                if (CriticalPattern != null)
                {
                    return CriticalPattern.GetNextSkill(monster);
                }
            }

            if (NormalPattern != null)
            {
                return NormalPattern.GetNextSkill(monster);
            }

            return null;
        }
    }
}
