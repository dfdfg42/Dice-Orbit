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
        public float DiceMultiplier = 1f;
        public int BonusDamage = 0;
        public bool IgnoreDefense = false;
        
        public int Cooldown;
        
        public DiceRequirement Requirement;

        public List<EffectData> Effects;
        
        [Header("Modular Actions (New System)")]
        public List<Modules.SkillActionModule> ActionModules = new List<Modules.SkillActionModule>();
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
        
        /// <summary>
        /// 모듈 실행
        /// </summary>
        public void ExecuteModules(int level, Core.Character source, object target, int diceValue)
        {
            var levelData = GetLevelData(level);
            if(levelData == null || levelData.ActionModules == null) return;
            
            foreach(var module in levelData.ActionModules)
            {
                if(module != null)
                {
                    GameObject targetObj = target as GameObject;
                    if (target is MonoBehaviour mb) targetObj = mb.gameObject;
                    
                    if (targetObj != null)
                    {
                        module.Execute(source, targetObj, diceValue);
                        Debug.Log($"Executed Module: {module.name}");
                    }
                }
            }
        }
    }
}
