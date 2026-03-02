using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Data;
using DiceOrbit.Core;

namespace DiceOrbit.Data
{
    public class CharacterSkillData : SkillData
    {
        [HideInInspector] public SkillTargetType skillTargetType = SkillTargetType.SingleEnemy;

        [Header("Skill Effects")]
        public List<DiceOrbit.Data.Skills.Effects.SkillEffectBase> Effects = new List<DiceOrbit.Data.Skills.Effects.SkillEffectBase>();
        
        public override void Execute(Core.Unit source, List<Core.Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            if (Effects == null || Effects.Count == 0) return;
            
            foreach (var effect in Effects)
            {
                if (effect == null) continue;
                effect.Execute(source, targetUnits, targetTiles, diceValue);
            }
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
        public DiceRequirement Requirement;
        public List<DiceOrbit.Data.Skills.Effects.SkillEffectBase> Effects;
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
                Effects = levelData.Effects ?? new List<DiceOrbit.Data.Skills.Effects.SkillEffectBase>(),
            };
            
            return skillData;
        }
    }
}
