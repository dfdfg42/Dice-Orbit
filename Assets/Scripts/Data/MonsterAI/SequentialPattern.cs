using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.Data.MonsterAI
{
    [CreateAssetMenu(fileName = "Sequential Pattern", menuName = "Dice Orbit/Monster AI/Pattern (Sequential)")]
    public class SequentialPattern : MonsterPattern
    {
        [SerializeField] private List<MonsterSkill> skills = new List<MonsterSkill>();
        
        // Runtime state tracking per monster instance
        private Dictionary<int, int> monsterIndices = new Dictionary<int, int>();

        public override void Initialize(Monster monster)
        {
            int id = monster.GetInstanceID();
            if (monsterIndices.ContainsKey(id))
            {
                monsterIndices[id] = 0;
            }
            else
            {
                monsterIndices.Add(id, 0);
            }
        }

        public override MonsterSkill GetNextSkill(Monster monster)
        {
            if (skills.Count == 0) return null;

            int id = monster.GetInstanceID();
            if (!monsterIndices.ContainsKey(id))
            {
                monsterIndices.Add(id, 0);
            }

            int index = monsterIndices[id];
            MonsterSkill skill = skills[index];

            // Advance index and loop
            monsterIndices[id] = (index + 1) % skills.Count;

            return skill;
        }
        
        private void OnDisable()
        {
            monsterIndices.Clear();
        }
    }
}
