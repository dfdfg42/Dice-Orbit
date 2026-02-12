using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.Data.MonsterAI
{
    /// <summary>
    /// Base class for all Monster AI patterns.
    /// </summary>
    public abstract class MonsterAI : ScriptableObject
    {
        public abstract SkillData GetNextSkill(Monster monster, System.Collections.Generic.List<SkillData> availableSkills);

        /// <summary>
        /// Optional hook for initialization or resetting state when a battle starts or phase changes.
        /// </summary>
        public virtual void Initialize(Monster monster) { }
    }
}
