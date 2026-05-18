using System;
using System.Collections.Generic;
using DiceOrbit.Core;
using UnityEngine;

namespace DiceOrbit.Data.Skills
{
    [Serializable]
    public class ActiveSkillSlot
    {
        [SerializeReference] public CharacterActiveSkill BaseSkill;
        public int CurrentLevel;

        [NonSerialized] public CharacterActiveSkill RuntimeInstance;

        public ActiveSkillSlot(CharacterActiveSkill skill, int initialLevel = 1)
        {
            BaseSkill       = skill;
            int max         = skill != null ? Mathf.Max(1, skill.MaxLevel) : 1;
            CurrentLevel    = Mathf.Clamp(initialLevel, 1, max);
            RuntimeInstance = skill?.Clone();
        }

        public CharacterSkillTargetType TargetType   => BaseSkill?.TargetType   ?? CharacterSkillTargetType.None;
        public Visuals.TilePreviewStyle PreviewStyle => BaseSkill?.PreviewStyle ?? Visuals.TilePreviewStyle.Neutral;

        public string          GetDescription() => BaseSkill?.Description ?? string.Empty;
        public DiceRequirement GetRequirement() => BaseSkill?.requirement;

        public bool IsMaxLevel => BaseSkill == null || CurrentLevel >= BaseSkill.MaxLevel;

        public bool TryUpgrade()
        {
            if (BaseSkill == null || IsMaxLevel) return false;
            CurrentLevel++;
            return true;
        }

        public bool CanUse(int diceValue) => BaseSkill?.CanUse(diceValue) ?? false;

        public bool Execute(Character source, List<Unit> targets, List<TileData> targetTiles, int diceValue)
        {
            if (RuntimeInstance != null)
                return RuntimeInstance.Execute(source, this, targets, targetTiles, diceValue);
            return false;
        }

        public string BuildPreview(Character source, int diceValue)
        {
            if (RuntimeInstance != null)
                return RuntimeInstance.BuildPreview(source, this, diceValue);
            return "예상: -";
        }
    }
}
