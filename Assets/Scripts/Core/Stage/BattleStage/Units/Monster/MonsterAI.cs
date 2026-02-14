using UnityEngine;
using DiceOrbit.Core;
using System.Collections.Generic;

namespace DiceOrbit.Data.MonsterAI
{
    /// <summary>
    /// Base class for all Monster AI patterns.
    /// </summary>
    public abstract class MonsterAI : ScriptableObject
    {
        protected Monster owner;
        protected List<SkillData> availableSkills = new List<SkillData>();

        public abstract SkillData GetNextSkill();

        /// <summary>
        /// Initialize AI with Monster reference and load initial skills.
        /// </summary>
        public virtual void Initialize(Monster monster) 
        { 
            owner = monster;
            RefreshSkills();
        }

        /// <summary>
        /// Refresh available skills from the owner Monster.
        /// Call this when Monster's skill list changes.
        /// </summary>
        public virtual void RefreshSkills()
        {
            if (owner == null) return;

            availableSkills.Clear();

            var monsterSkills = owner.AvailableSkills;
            if (monsterSkills != null)
            {
                availableSkills.AddRange(monsterSkills);
            }
        }
    }
}
