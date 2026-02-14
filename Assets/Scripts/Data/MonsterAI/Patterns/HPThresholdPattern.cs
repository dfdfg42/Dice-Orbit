using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.Data.MonsterAI.Patterns
{
    [CreateAssetMenu(fileName = "HP Threshold Pattern", menuName = "Dice Orbit/Monster AI/Pattern (HP Threshold)")]
    public class HPThresholdPattern : MonsterAI
    {
        [Header("Settings")]
        [Range(0f, 1f)] public float ThresholdPercent = 0.5f;

        [Header("Patterns")]
        public MonsterAI NormalPattern;
        public MonsterAI CriticalPattern;

        private MonsterAI runtimeNormalPattern;
        private MonsterAI runtimeCriticalPattern;

        public override void Initialize(Monster monster)
        {
            RecreateRuntimePatterns();

            if (runtimeNormalPattern != null) runtimeNormalPattern.Initialize(monster);
            if (runtimeCriticalPattern != null) runtimeCriticalPattern.Initialize(monster);
        }

        public override SkillData GetNextSkill(Monster monster, System.Collections.Generic.List<SkillData> availableSkills)
        {
            if (monster.Stats.HPRatio <= ThresholdPercent)
            {
                if (runtimeCriticalPattern != null)
                {
                    return runtimeCriticalPattern.GetNextSkill(monster, availableSkills);
                }
            }

            if (runtimeNormalPattern != null)
            {
                return runtimeNormalPattern.GetNextSkill(monster, availableSkills);
            }

            return null;
        }

        private void RecreateRuntimePatterns()
        {
            if (runtimeNormalPattern != null)
            {
                Destroy(runtimeNormalPattern);
                runtimeNormalPattern = null;
            }

            if (runtimeCriticalPattern != null)
            {
                Destroy(runtimeCriticalPattern);
                runtimeCriticalPattern = null;
            }

            if (NormalPattern != null)
            {
                runtimeNormalPattern = Instantiate(NormalPattern);
                runtimeNormalPattern.name = NormalPattern.name;
            }

            if (CriticalPattern != null)
            {
                runtimeCriticalPattern = Instantiate(CriticalPattern);
                runtimeCriticalPattern.name = CriticalPattern.name;
            }
        }

        private void OnDisable()
        {
            if (runtimeNormalPattern != null)
            {
                Destroy(runtimeNormalPattern);
                runtimeNormalPattern = null;
            }

            if (runtimeCriticalPattern != null)
            {
                Destroy(runtimeCriticalPattern);
                runtimeCriticalPattern = null;
            }
        }
    }
}
