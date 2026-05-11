using System;
using System.Collections.Generic;
using DiceOrbit.Data.Passives;
using DiceOrbit.Core;
using UnityEngine;

namespace DiceOrbit.Data.Skills
{
    [Serializable]
    public class RuntimeAbility
    {
        public SkillAsset BaseSkill;
        public int CurrentLevel;

        [NonSerialized] public CharacterActiveTemplate RuntimeActiveInstance;
        [NonSerialized] public IPassive RuntimePassiveInstance;

        public RuntimeAbility(SkillAsset skill, int initialLevel = 1)
        {
            BaseSkill = skill;
            int max = skill != null ? Mathf.Max(1, skill.MaxLevel) : 1;
            CurrentLevel = Mathf.Clamp(initialLevel, 1, max);

            if (skill is ActiveSkillAsset activeAsset && activeAsset.ActiveTemplate != null)
            {
                RuntimeActiveInstance = UnityEngine.Object.Instantiate(activeAsset.ActiveTemplate);
            }
            else if (skill is PassiveSkillAsset passiveAsset && passiveAsset.PassiveTemplate != null)
            {
                RuntimePassiveInstance = UnityEngine.Object.Instantiate(passiveAsset.PassiveTemplate);
            }
        }

        public CharacterSkillType AbilityType => BaseSkill != null ? BaseSkill.Type : CharacterSkillType.Active;

        public CharacterSkillTargetType TargetType
        {
            get
            {
                if (BaseSkill is ActiveSkillAsset activeAsset)
                    return activeAsset.TargetType;
                return CharacterSkillTargetType.None;
            }
        }

        public Visuals.TilePreviewStyle PreviewStyle
        {
            get
            {
                if (BaseSkill is ActiveSkillAsset activeAsset)
                    return activeAsset.PreviewStyle;
                return Visuals.TilePreviewStyle.Neutral;
            }
        }

        public string GetDescription() => BaseSkill?.GetDescription(CurrentLevel) ?? string.Empty;

        public DiceRequirement GetRequirement() => BaseSkill?.GetRequirement(CurrentLevel) ?? BaseSkill?.Requirement;

        public SkillLevelData GetCurrentLevelData() => BaseSkill?.GetLevelData(CurrentLevel);
        public SkillLevelData GetNextLevelData() => BaseSkill?.GetLevelData(CurrentLevel + 1);
        public bool IsMaxLevel => BaseSkill == null || CurrentLevel >= BaseSkill.MaxLevel;

        public bool TryUpgrade()
        {
            if (BaseSkill == null || IsMaxLevel) return false;
            CurrentLevel++;
            return true;
        }

        public bool CanUse(int diceValue)
        {
            if (BaseSkill == null || !BaseSkill.CanUse(diceValue))
                return false;
            
            // 향후 RuntimeActiveInstance 내부의 쿨타임, 스택 등 검사 추가 가능
            return true;
        }

        public bool Execute(Character source, List<Unit> targets, List<TileData> targetTiles, int diceValue)
        {
            if (RuntimeActiveInstance != null)
            {
                return RuntimeActiveInstance.Execute(source, this, targets, targetTiles, diceValue);
            }
            return false;
        }
        
        public string BuildPreview(Character source, int diceValue)
        {
            if (RuntimeActiveInstance != null)
            {
                return RuntimeActiveInstance.BuildPreview(source, this, diceValue);
            }
            return "예상: -";
        }
    }
}
