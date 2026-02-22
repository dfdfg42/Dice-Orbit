using System;
using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.Data.MonsterAI.Patterns
{
    /// <summary>
    /// 순차 패턴 (스킬 목록을 순서대로 사용)
    /// </summary>
    [System.Serializable]
    public class SequentialPattern : MonsterAI
    {
        [SerializeField] List<MonsterSkill> availableSkills = new List<MonsterSkill>();
        [SerializeField] private int currentIndex = 0;

        public override void Initialize(Monster monster)
        {
            base.Initialize(monster);
            currentIndex = 0;
        }

        public override MonsterSkill GetNextSkill()
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;

            var skill = availableSkills[currentIndex % availableSkills.Count];
            currentIndex++;

            return skill;
        }
    }
}
