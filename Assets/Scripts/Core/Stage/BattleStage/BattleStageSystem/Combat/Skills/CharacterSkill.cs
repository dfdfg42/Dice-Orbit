using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Data;

namespace DiceOrbit.Data.Skills
{
    public enum CharacterSkillType
    {
        Active,
        Passive
    }

    [System.Serializable]
    public class SkillLevelData
    {
        public int Level;
        public string Description;
        
        [Header("Level-Specific Data")]
        public int DamageMultiplier = 1;
        public int BonusDamage = 0;
        public bool IgnoreDefense = false;
        
        public DiceRequirement Requirement;
        public List<EffectData> Effects;
        public List<Modules.SkillActionModule> ActionModules;
    }

    [CreateAssetMenu(fileName = "New Character Skill", menuName = "Dice Orbit/Skills/Character Skill")]
    public class CharacterSkill : ScriptableObject
    {
        [Header("Skill Data")]
        public SkillData BaseData = new SkillData();
        
        [Header("Character Skill Info")]
        public Sprite Icon;
        public CharacterSkillType Type;
        
        [Header("Requirements")]
        public DiceRequirement Requirement = new DiceRequirement();
        
        [Header("Progression")]
        public List<SkillLevelData> Levels = new List<SkillLevelData>();

        // 편의 프로퍼티 (BaseData의 필드에 직접 접근)
        public string SkillName
        {
            get => BaseData.SkillName;
            set => BaseData.SkillName = value;
        }

        public string Description
        {
            get => BaseData.Description;
            set => BaseData.Description = value;
        }

        public SkillTargetType TargetType
        {
            get => BaseData.TargetType;
            set => BaseData.TargetType = value;
        }

        /// <summary>
        /// 주사위 값으로 스킬 사용 가능한지 확인
        /// </summary>
        public bool CanUse(int diceValue)
        {
            return Requirement.CanUse(diceValue);
        }

        public SkillLevelData GetLevelData(int level)
        {
            int index = level - 1;
            if (index >= 0 && index < Levels.Count)
            {
                return Levels[index];
            }
            if (Levels.Count > 0) return Levels[Levels.Count - 1];
            return null;
        }

        public int MaxLevel => Levels.Count;
        
        /// <summary>
        /// 현재 레벨의 SkillData 반환 (레벨별 데이터 적용)
        /// </summary>
        public SkillData GetSkillData(int level)
        {
            var levelData = GetLevelData(level);
            if (levelData == null) return BaseData;
            
            // BaseData 복사 후 레벨별 데이터 적용
            var skillData = new SkillData
            {
                SkillName = BaseData.SkillName,
                Description = string.IsNullOrWhiteSpace(levelData.Description) ? BaseData.Description : levelData.Description,
                Type = Type == CharacterSkillType.Active ? SkillType.Active : SkillType.Passive,
                TargetType = BaseData.TargetType,
                DamageMultiplier = levelData.DamageMultiplier,
                BonusDamage = levelData.BonusDamage,
                IgnoreDefense = levelData.IgnoreDefense,
                Effects = levelData.Effects ?? new List<EffectData>(),
                ActionModules = levelData.ActionModules ?? new List<Modules.SkillActionModule>()
            };
            
            return skillData;
        }
    }
}