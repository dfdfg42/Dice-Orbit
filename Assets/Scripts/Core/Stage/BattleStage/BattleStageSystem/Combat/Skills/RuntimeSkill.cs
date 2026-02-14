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

        public SkillLevelData GetCurrentLevelData()
        {
            return BaseSkill.GetLevelData(CurrentLevel);
        }

        public SkillLevelData GetNextLevelData()
        {
            return BaseSkill.GetLevelData(CurrentLevel + 1);
        }

        public bool IsMaxLevel => CurrentLevel >= BaseSkill.MaxLevel;

        public void Upgrade()
        {
            if (!IsMaxLevel)
            {
                CurrentLevel++;
            }
        }

        public SkillData ToSkillData()
        {
            var levelData = GetCurrentLevelData();
            if (levelData == null) return null;

            return new SkillData
            {
                SkillName = BaseSkill.SkillName,
                Description = string.IsNullOrWhiteSpace(levelData.Description) ? BaseSkill.Description : levelData.Description,
                Type = BaseSkill.Type == CharacterSkillType.Active ? SkillType.Active : SkillType.Passive,
                TargetType = BaseSkill.TargetType,
                Effects = levelData.Effects ?? new System.Collections.Generic.List<EffectData>(),
                Requirement = levelData.Requirement ?? new DiceRequirement(),
                ActionModules = levelData.ActionModules ?? new System.Collections.Generic.List<Skills.Modules.SkillActionModule>(),
                
                DamageMultiplier = levelData.DamageMultiplier,
                BonusDamage = levelData.BonusDamage,
                IgnoreDefense = levelData.IgnoreDefense
            };
        }
    }
}
