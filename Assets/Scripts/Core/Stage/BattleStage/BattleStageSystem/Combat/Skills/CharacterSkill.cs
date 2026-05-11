using DiceOrbit.Core;
using DiceOrbit.Data;
using DiceOrbit.Data.Passives;
using DiceOrbit.Visuals;
using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Data.Skills
{
    public enum CharacterSkillTargetType
    {
        OneEnemy,
        None,
        OneTile,    // 타일 하나 클릭으로 선택
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

    [CreateAssetMenu(fileName = "New Character Skill", menuName = "Dice Orbit/Skills/Character Skill")]
    public class CharacterSkill : ScriptableObject
    {
        [Header("Skill Data")]
        [SerializeField] private string skillName = "";
        [SerializeField, TextArea(2, 4)] private string description = "";

        public string SkillName => skillName;
        public string Description => description;

        [Header("Character Skill Info")]
        public Sprite Icon;
        public CharacterSkillType Type;

        [Header("Active Binding (Type=Active)")]
        [SerializeReference] public CharacterActiveTemplate ActiveTemplate;
        public CharacterSkillTargetType TargetType = CharacterSkillTargetType.OneEnemy;

        [Header("Tile Targeting Preview")]
        public TilePreviewStyle PreviewStyle = TilePreviewStyle.Neutral;

        [Header("Passive Binding (Type=Passive)")]
        // 런타임에서 복제되어 PassiveManager에 등록될 패시브 템플릿입니다.
        [SerializeReference] public PassiveAbility PassiveTemplate;

        [Header("Level")]
        [Min(1)] public int MaxLevelOverride = 1;

        [Header("Requirements")]
        public DiceRequirement Requirement = new DiceRequirement();

        [Header("Progression")]
        public List<SkillLevelData> Levels = new List<SkillLevelData>();

        public int MaxLevel => Mathf.Max(1, MaxLevelOverride, Levels != null ? Levels.Count : 0);

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
                return Levels[index];
            if (Levels.Count > 0)
                return Levels[Levels.Count - 1];
            return null;
        }

        /// <summary>
        /// 현재 레벨의 설명 반환 (레벨별 오버라이드 지원)
        /// </summary>
        public string GetDescription(int level)
        {
            var levelData = GetLevelData(level);
            if (levelData != null && !string.IsNullOrWhiteSpace(levelData.Description))
                return levelData.Description;
            return description;
        }

        /// <summary>
        /// 현재 레벨의 DiceRequirement 반환 (레벨별 오버라이드 지원)
        /// </summary>
        public DiceRequirement GetRequirement(int level)
        {
            var levelData = GetLevelData(level);
            if (levelData?.Requirement != null)
                return levelData.Requirement;
            return Requirement;
        }
    }
}
