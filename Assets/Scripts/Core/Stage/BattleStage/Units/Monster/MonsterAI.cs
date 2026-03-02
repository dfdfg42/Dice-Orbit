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

        /// <summary>
        /// AI가 사용할 스킬 리스트
        /// </summary>
        [Header("Available Skills")]
        public List<MonsterSkill> availableSkills = new List<MonsterSkill>();

        public abstract MonsterSkill GetNextSkill();

        /// <summary>
        /// Initialize AI with Monster reference.
        /// </summary>
        public virtual void Initialize(Monster monster) 
        { 
            owner = monster;
            ValidateSkills();
        }

        /// <summary>
        /// 스킬 유효성 검사
        /// </summary>
        protected virtual void ValidateSkills()
        {
            if (availableSkills == null || availableSkills.Count == 0)
            {
                Debug.LogWarning($"[MonsterAI] {GetType().Name} has no available skills!");
            }

            // Null 또는 skillData가 없는 스킬 제거
            availableSkills?.RemoveAll(skill => skill == null || skill.skillData == null);
        }
    }
}
