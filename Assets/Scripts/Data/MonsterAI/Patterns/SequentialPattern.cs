using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Data.MonsterAI.Patterns
{
    /// <summary>
    /// 순차 패턴 (스킬 목록을 순서대로 사용)
    /// </summary>
    [CreateAssetMenu(fileName = "SequencePattern", menuName = "DiceOrbit/Monster/Pattern/Sequence")]
    public class SequentialPattern : MonsterAI
    {
        [SerializeField] List<SkillData> availableSkills = new List<SkillData>();
        [SerializeField] private int currentIndex = 0;

        protected override void InitializeRuntimeState()
        {
            base.InitializeRuntimeState();
            currentIndex = 0;
        }

        public override void RefreshSkills()
        {
            if (owner == null) return;

            availableSkills.Clear();

            var monsterSkills = owner.AvailableSkills;
            if (monsterSkills != null)
            {
                availableSkills.AddRange(monsterSkills);
            }
        }

        public override SkillData GetNextSkill()
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;

            var skill = availableSkills[currentIndex % availableSkills.Count];
            currentIndex++;

            return skill;
        }
    }
}
