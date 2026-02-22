using UnityEngine;
using DiceOrbit.Core;
using System.Collections.Generic;

namespace DiceOrbit.Data.MonsterAI
{
    /// <summary>
    /// Base class for all Monster AI patterns.
    /// </summary>
    [System.Serializable]
    public abstract class MonsterAI
    {
        [System.NonSerialized]
        protected Monster owner;
        public abstract MonsterSkill GetNextSkill();

        /// <summary>
        /// Initialize AI with Monster reference.
        /// </summary>
        public virtual void Initialize(Monster monster) 
        { 
            owner = monster;
        }
    }
}
