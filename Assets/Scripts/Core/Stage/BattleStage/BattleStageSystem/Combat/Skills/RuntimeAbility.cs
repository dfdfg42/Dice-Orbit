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
        [SerializeReference] public CharacterSkillBase BaseSkill;
        public int CurrentLevel;

        [NonSerialized] public CharacterActiveTemplate RuntimeActiveInstance;
        [NonSerialized] public IPassive                RuntimePassiveInstance;

        public RuntimeAbility(CharacterSkillBase skill, int initialLevel = 1)
        {
            BaseSkill    = skill;
            int max      = skill != null ? Mathf.Max(1, skill.MaxLevel) : 1;
            CurrentLevel = Mathf.Clamp(initialLevel, 1, max);

            if (skill is CharacterActiveTemplate active)
                RuntimeActiveInstance = active.Clone();
            else if (skill is CharacterPassive passive)
                RuntimePassiveInstance = passive.Clone() as CharacterPassive;
        }

        public CharacterSkillType AbilityType =>
            BaseSkill != null ? BaseSkill.SkillType : CharacterSkillType.Active;

        public CharacterSkillTargetType TargetType
        {
            get
            {
                if (BaseSkill is CharacterActiveTemplate a) return a.TargetType;
                return CharacterSkillTargetType.None;
            }
        }

        public Visuals.TilePreviewStyle PreviewStyle
        {
            get
            {
                if (BaseSkill is CharacterActiveTemplate a) return a.PreviewStyle;
                return Visuals.TilePreviewStyle.Neutral;
            }
        }

        public string GetDescription()         => BaseSkill?.GetDescription(CurrentLevel) ?? string.Empty;
        public DiceRequirement GetRequirement() => BaseSkill?.GetRequirement(CurrentLevel) ?? BaseSkill?.requirement;
        public SkillLevelData  GetCurrentLevelData() => BaseSkill?.GetLevelData(CurrentLevel);
        public SkillLevelData  GetNextLevelData()    => BaseSkill?.GetLevelData(CurrentLevel + 1);
        public bool IsMaxLevel => BaseSkill == null || CurrentLevel >= BaseSkill.MaxLevel;

        public bool TryUpgrade()
        {
            if (BaseSkill == null || IsMaxLevel) return false;
            CurrentLevel++;
            return true;
        }

        public bool CanUse(int diceValue)
        {
            if (BaseSkill == null || !BaseSkill.CanUse(diceValue)) return false;
            return true;
        }

        public bool Execute(Character source, List<Unit> targets, List<TileData> targetTiles, int diceValue)
        {
            if (RuntimeActiveInstance != null)
                return RuntimeActiveInstance.Execute(source, this, targets, targetTiles, diceValue);
            return false;
        }

        public string BuildPreview(Character source, int diceValue)
        {
            if (RuntimeActiveInstance != null)
                return RuntimeActiveInstance.BuildPreview(source, this, diceValue);
            return "예상: -";
        }
    }
}
