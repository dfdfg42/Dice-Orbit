using UnityEngine;
using System.Collections.Generic;

namespace DiceOrbit.Data
{
    /// <summary>
    /// 캐릭터 스탯 데이터
    /// </summary>
    [System.Serializable]
    public class CharacterStats : Stats
    {
        [Header("Basic Info")]
        public string CharacterName = "Hero";
        public int Level = 1;
        
        
        [Header("Secondary Stats")]
        public float CritRate = 0f;      // 0.0 ~ 1.0
        public float CritDamage = 1.5f;  // 배율 (기본 1.5배)
        public float Evasion = 0f;       // 0.0 ~ 1.0
        public int MoveBonus = 0;        // 이동 거리 추가
        
        [Header("Skills")]
        // Legacy: public List<SkillData> ActiveSkills = new List<SkillData>();
        // Legacy: public List<SkillData> PassiveSkills = new List<SkillData>();
        
        // New System
        public List<Skills.RuntimeSkill> RuntimeActiveSkills = new List<Skills.RuntimeSkill>();
        public List<Skills.RuntimeSkill> RuntimePassiveSkills = new List<Skills.RuntimeSkill>();
        
        // Reference to the source preset for Draft Pool
        public CharacterPreset SourcePreset;
        public Passives.PassiveAbility StartingPassive; // Fixed Passiver = 1.0f; // Multiplier for skill effects (Levels up)
        
        // Refactor 2.0: Scaling
        public float SkillPower = 1.0f; // Multiplier for skill effects (Levels up)
        
        [Header("Visual")]
        public Sprite CharacterSprite;
        public Color SpriteColor = Color.white;
        

        
        /// <summary>
        /// 레벨업 (스탯 증가 & 스킬 강화)
        /// </summary>
        public void LevelUp()
        {
            Level++;
            MaxHP += 5; // Simple HP growth
            CurrentHP = MaxHP;
            
            // Skill Scaling Logic
            SkillPower += 0.2f; // +20% power per level
            
            Debug.Log($"[LevelUp] {CharacterName} reached Lv.{Level}! SkillPower: {SkillPower}");
        }
    }
}
