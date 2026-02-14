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

        public abstract MonsterSkill GetNextSkill();

        /// <summary>
        /// Initialize AI with Monster reference and load initial skills.
        /// </summary>
        public virtual void Initialize(Monster monster) 
        { 
            owner = monster;
            InitializeRuntimeState();
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
    }
}
