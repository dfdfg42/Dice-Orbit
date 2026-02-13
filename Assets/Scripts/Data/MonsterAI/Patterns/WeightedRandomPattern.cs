using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.Data.MonsterAI.Patterns
{
    [System.Serializable]
    public class WeightedSkillIndex
    {
        [Tooltip("Index into the Available Skills list (0 = first skill)")]
        public int SkillIndex;
        [Range(1, 100)] public int Weight = 1;
    }

    [CreateAssetMenu(fileName = "Weighted Random Pattern", menuName = "Dice Orbit/Monster AI/Pattern (Weighted Random)")]
    public class WeightedRandomPattern : MonsterAI
    {
        [SerializeField] private List<WeightedSkillIndex> weights = new List<WeightedSkillIndex>();

        public override SkillData GetNextSkill(Monster monster, System.Collections.Generic.List<SkillData> availableSkills)
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;
            
            // If configuration is empty, fallback to simple random
            if (weights.Count == 0)
            {
                return availableSkills[Random.Range(0, availableSkills.Count)];
            }

            int totalWeight = 0;
            var validWeights = new List<WeightedSkillIndex>();
            
            // Filter weights that are valid for current available skills count
            foreach (var w in weights)
            {
                if (w.SkillIndex < availableSkills.Count)
                {
                    totalWeight += w.Weight;
                    validWeights.Add(w);
                }
            }
            
            if (totalWeight == 0) return availableSkills[0];

            int randomValue = Random.Range(0, totalWeight);
            int currentWeight = 0;

            foreach (var w in validWeights)
            {
                currentWeight += w.Weight;
                if (randomValue < currentWeight)
                {
                    return availableSkills[w.SkillIndex];
                }
            }

            return availableSkills[validWeights[0].SkillIndex];
        }
    }
}
