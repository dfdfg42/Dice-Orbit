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
        
        [Header("Legacy / Mapping")]
        public int Damage; // Used as Base Damage
        public int DamageMultiplier = 1;
        public int BonusDamage = 0;
        public bool IgnoreDefense = false;
        
        public int Cooldown;
        
        public DiceRequirement Requirement;
        public List<EffectData> Effects;
        public List<Modules.SkillActionModule> ActionModules;
    }

    [CreateAssetMenu(fileName = "New Character Skill", menuName = "Dice Orbit/Skills/Character Skill")]
    public class CharacterSkill : ScriptableObject
    {
        [Header("Basic Info")]
        public string SkillName;
        [TextArea] public string Description;
        public Sprite Icon;
        public CharacterSkillType Type;
        public SkillTargetType TargetType; // Legacy Target Type
        
        [Header("Progression")]
        public List<SkillLevelData> Levels = new List<SkillLevelData>();

        public SkillLevelData GetLevelData(int level)
        {
            // Level is 1-based index
            int index = level - 1;
            if (index >= 0 && index < Levels.Count)
            {
                return Levels[index];
            }
            // Return max level if overflow
            if (Levels.Count > 0) return Levels[Levels.Count - 1];
            return null;
        }

        public int MaxLevel => Levels.Count;
    }
}