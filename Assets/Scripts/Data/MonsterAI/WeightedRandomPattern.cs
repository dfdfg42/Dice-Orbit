using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.Data.MonsterAI
{
    [System.Serializable]
    public class WeightedSkill
    {
        public MonsterSkill Skill;
        [Range(1, 100)] public int Weight = 1;
    }

    [CreateAssetMenu(fileName = "Weighted Random Pattern", menuName = "Dice Orbit/Monster AI/Pattern (Weighted Random)")]
    public class WeightedRandomPattern : MonsterPattern
    {
        [SerializeField] private List<WeightedSkill> skills = new List<WeightedSkill>();

        public override MonsterSkill GetNextSkill(Monster monster)
        {
            if (skills.Count == 0) return null;

            int totalWeight = 0;
            foreach (var ws in skills)
            {
                totalWeight += ws.Weight;
            }

            int randomValue = Random.Range(0, totalWeight);
            int currentWeight = 0;

            foreach (var ws in skills)
            {
                currentWeight += ws.Weight;
                if (randomValue < currentWeight)
                {
                    return ws.Skill;
                }
            }

            return skills[0].Skill; // Fallback
        }
    }
}
