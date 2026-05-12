using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.Data.Skills
{
    public enum CharacterSkillType { Active, Passive }

    public enum CharacterSkillTargetType
    {
        OneEnemy,
        None,
        OneTile,
        AllTiles,
    }

    [System.Serializable]
    public class SkillLevelData
    {
        public int Level;
        public string Description;
        public DiceRequirement Requirement;
    }

    [System.Serializable]
    public abstract class CharacterSkillBase
    {
        [SerializeField] protected string skillName = "";
        [SerializeField, TextArea(2, 4)] protected string description = "";
        [SerializeField] public Sprite icon;
        [SerializeField] public DiceRequirement requirement = new DiceRequirement();
        [SerializeField] public int maxLevelOverride = 1;
        [SerializeField] public List<SkillLevelData> levels = new List<SkillLevelData>();

        public string SkillName    => skillName;
        public string Description  => description;
        public abstract CharacterSkillType SkillType { get; }

        public int MaxLevel => Mathf.Max(1, maxLevelOverride, levels != null ? levels.Count : 0);

        public bool CanUse(int diceValue) => requirement.CanUse(diceValue);

        public string GetDescription(int level)
        {
            var data = GetLevelData(level);
            if (data != null && !string.IsNullOrWhiteSpace(data.Description))
                return data.Description;
            return description;
        }

        public DiceRequirement GetRequirement(int level)
        {
            var data = GetLevelData(level);
            if (data?.Requirement != null) return data.Requirement;
            return requirement;
        }

        public SkillLevelData GetLevelData(int level)
        {
            int idx = level - 1;
            if (idx >= 0 && idx < levels.Count) return levels[idx];
            if (levels.Count > 0) return levels[levels.Count - 1];
            return null;
        }
    }
}
