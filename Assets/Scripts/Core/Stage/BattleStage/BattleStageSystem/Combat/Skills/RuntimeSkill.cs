using UnityEngine;

namespace DiceOrbit.Data.Skills
{
    [System.Serializable]
    public class RuntimeSkill
    {
        public CharacterSkill BaseSkill;
        public int CurrentLevel;

        public RuntimeSkill(CharacterSkill skill, int initialLevel = 1)
        {
            BaseSkill = skill;
            CurrentLevel = initialLevel;
        }

        /// <summary>
        /// 현재 레벨의 스킬 데이터 반환
        /// </summary>
        public SkillData CurrentSkillData => BaseSkill?.GetSkillData(CurrentLevel);

        public SkillLevelData GetCurrentLevelData()
        {
            return BaseSkill?.GetLevelData(CurrentLevel);
        }

        public SkillLevelData GetNextLevelData()
        {
            return BaseSkill?.GetLevelData(CurrentLevel + 1);
        }

        public bool IsMaxLevel => CurrentLevel >= BaseSkill.MaxLevel;

        public void Upgrade()
        {
            if (!IsMaxLevel)
            {
                CurrentLevel++;
            }
        }
    }
}
