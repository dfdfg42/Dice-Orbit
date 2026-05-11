using DiceOrbit.Core;
using DiceOrbit.Data;
using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Data.Skills
{
    public enum CharacterSkillTargetType
    {
        OneEnemy,
        None,
        OneTile,    // 특정 타일 하나 클릭으로 선택
        AllTiles,   // 모든 타일 자동 선택 (확인 클릭만 필요)
    }

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
        public DiceRequirement Requirement;
    }

    public abstract class SkillAsset : ScriptableObject
    {
        [Header("Skill Data")]
        [SerializeField] private string skillName = "";
        [SerializeField, TextArea(2, 4)] private string description = "";

        public string SkillName => skillName;
        public string Description => description;

        [Header("Character Skill Info")]
        public Sprite Icon;

        [Header("Level")]
        [Min(1)] public int MaxLevelOverride = 1;

        [Header("Requirements")]
        public DiceRequirement Requirement = new DiceRequirement();

        [Header("Progression")]
        public List<SkillLevelData> Levels = new List<SkillLevelData>();

        public int MaxLevel => Mathf.Max(1, MaxLevelOverride, Levels != null ? Levels.Count : 0);

        public abstract CharacterSkillType Type { get; }

        public bool CanUse(int diceValue)
        {
            return Requirement.CanUse(diceValue);
        }

        public SkillLevelData GetLevelData(int level)
        {
            int index = level - 1;
            if (index >= 0 && index < Levels.Count)
                return Levels[index];
            if (Levels.Count > 0)
                return Levels[Levels.Count - 1];
            return null;
        }

        public string GetDescription(int level)
        {
            var levelData = GetLevelData(level);
            if (levelData != null && !string.IsNullOrWhiteSpace(levelData.Description))
                return levelData.Description;
            return description;
        }

        public DiceRequirement GetRequirement(int level)
        {
            var levelData = GetLevelData(level);
            if (levelData?.Requirement != null)
                return levelData.Requirement;
            return Requirement;
        }
    }
}
