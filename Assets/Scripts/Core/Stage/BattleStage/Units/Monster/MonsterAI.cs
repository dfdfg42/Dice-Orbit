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

        public abstract SkillData GetNextSkill();

        /// <summary>
        /// Initialize AI with Monster reference and load initial skills.
        /// </summary>
        public virtual void Initialize(Monster monster) 
        { 
            owner = monster;
            InitializeRuntimeState();
            RefreshSkills();
        }

        /// <summary>
        /// Initialize runtime-specific state for deep copy of fields.
        /// Override this in derived classes to deep copy SerializedField collections.
        /// </summary>
        protected virtual void InitializeRuntimeState()
        {
            // Base implementation: nothing to copy
            // Derived classes should override to deep copy their [SerializeField] collections
        }

        /// <summary>
        /// Refresh available skills from the owner Monster.
        /// Override this in derived classes if pattern needs to cache skills.
        /// </summary>
        public virtual void RefreshSkills()
        {
            // Base implementation: do nothing
            // Derived classes override if they need to cache owner.AvailableSkills
        }
    }
}
