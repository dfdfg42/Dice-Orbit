using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Data;
using DiceOrbit.Core;

namespace DiceOrbit.Data
{
    public class CharacterSkillData : SkillData
    {
        [HideInInspector] public SkillTargetType skillTargetType = SkillTargetType.SingleEnemy;

        [Header("Legacy Effects - For Compatibility")]
        public List<EffectData> Effects = new List<EffectData>();

        [Header("Legacy Damage - For Compatibility")]
        [HideInInspector] public int DamageMultiplier = 1;
        [HideInInspector] public int BonusDamage = 0;
        [HideInInspector] public bool IgnoreDefense = false;

        /// <summary>
        /// 데미지 계산 (DamageMultiplier를 주사위 눈금 배수로 적용)
        /// </summary>
        public int CalculateDamage(int baseAttack, int diceValue)
        {
            return baseAttack + (diceValue * DamageMultiplier) + BonusDamage;
        }
    }
}

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
    }

    [CreateAssetMenu(fileName = "New Character Skill", menuName = "Dice Orbit/Skills/Character Skill")]
    public class CharacterSkill : ScriptableObject
    {
        [Header("Skill Data")]
        public CharacterSkillData BaseData = new CharacterSkillData();
        
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
            set => BaseData.description = value;
        }

        public SkillTargetType TargetType
        {
            get => BaseData.skillTargetType;
            set => BaseData.skillTargetType = value;
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
        public CharacterSkillData GetSkillData(int level)
        {
            var levelData = GetLevelData(level);
            if (levelData == null) return BaseData;
            
            // BaseData 복사 후 레벨별 데이터 적용
            var skillData = new CharacterSkillData
            {
                SkillName = BaseData.SkillName,
                description = string.IsNullOrWhiteSpace(levelData.Description) ? BaseData.description : levelData.Description,
                skillTargetType = BaseData.skillTargetType,
                DamageMultiplier = levelData.DamageMultiplier,
                BonusDamage = levelData.BonusDamage,
                IgnoreDefense = levelData.IgnoreDefense,
                Effects = levelData.Effects ?? new List<EffectData>(),
            };
            
            return skillData;
        }
    }
}