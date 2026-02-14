using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.Data.MonsterAI.Patterns
{
    /// <summary>
    /// SkillData와 가중치를 쌍으로 저장하는 구조체
    /// </summary>
    [System.Serializable]
    public class WeightedSkill
    {
        [Tooltip("Skill to use")]
        public SkillData Skill;

        [Tooltip("Weight of this skill (higher = more likely to be selected)")]
        [Range(0.1f, 100f)] 
        public float Weight = 1f;
    }

    /// <summary>
    /// 가중치 랜덤 패턴 (SkillData 직접 참조 방식)
    /// </summary>
    [CreateAssetMenu(fileName = "Weighted Random Pattern", menuName = "Dice Orbit/Monster AI/Pattern (Weighted Random)")]
    public class WeightedRandomPattern : MonsterAI
    {
        [SerializeField] private List<WeightedSkill> weightedSkills = new List<WeightedSkill>();

        protected override void InitializeRuntimeState()
        {
            base.InitializeRuntimeState();

            // Deep copy weightedSkills list to avoid sharing with original ScriptableObject
            if (weightedSkills != null && weightedSkills.Count > 0)
            {
                var originalSkills = weightedSkills;
                weightedSkills = new List<WeightedSkill>(originalSkills.Count);

                foreach (var ws in originalSkills)
                {
                    weightedSkills.Add(new WeightedSkill
                    {
                        Skill = ws.Skill, // SkillData는 ScriptableObject이므로 참조 공유 OK
                        Weight = ws.Weight
                    });
                }
            }
        }

        public override SkillData GetNextSkill()
        {
            // If no weighted skills configured, fallback to owner's available skills
            if (weightedSkills == null || weightedSkills.Count == 0)
            {
                if (owner == null || owner.AvailableSkills == null || owner.AvailableSkills.Count == 0)
                    return null;

                return owner.AvailableSkills[Random.Range(0, owner.AvailableSkills.Count)];
            }

            // Calculate total weight (only count valid skills)
            float totalWeight = 0f;

            foreach (var ws in weightedSkills)
            {
                if (ws.Skill != null && ws.Weight > 0)
                {
                    totalWeight += ws.Weight;
                }
            }

            // No valid skills
            if (totalWeight <= 0)
            {
                return owner?.AvailableSkills != null && owner.AvailableSkills.Count > 0 
                    ? owner.AvailableSkills[0] 
                    : null;
            }

            // Weighted random selection - direct iteration without temp list
            float randomValue = Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            foreach (var ws in weightedSkills)
            {
                if (ws.Skill != null && ws.Weight > 0)
                {
                    currentWeight += ws.Weight;
                    if (randomValue < currentWeight)
                    {
                        return ws.Skill;
                    }
                }
            }

            // Fallback: return first valid skill
            foreach (var ws in weightedSkills)
            {
                if (ws.Skill != null)
                    return ws.Skill;
            }

            return null;
        }
    }
}
