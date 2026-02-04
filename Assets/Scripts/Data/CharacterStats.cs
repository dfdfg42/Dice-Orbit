using UnityEngine;
using System.Collections.Generic;

namespace DiceOrbit.Data
{
    /// <summary>
    /// 캐릭터 스탯 데이터
    /// </summary>
    [System.Serializable]
    public class CharacterStats : UnitStats
    {
        [Header("Basic Info")]
        public string CharacterName = "Hero";
        public int Level = 1;
        
        [Header("Skills")]
        [Header("Skills")]
        // Legacy: public List<SkillData> ActiveSkills = new List<SkillData>();
        // Legacy: public List<SkillData> PassiveSkills = new List<SkillData>();
        
        // New System
        public List<Skills.RuntimeSkill> RuntimeActiveSkills = new List<Skills.RuntimeSkill>();
        public List<Skills.RuntimeSkill> RuntimePassiveSkills = new List<Skills.RuntimeSkill>();
        
        // Reference to the source preset for Draft Pool
        public Core.CharacterPreset SourcePreset;
        
        [Header("Visual")]
        public Sprite CharacterSprite;
        public Color SpriteColor = Color.white;
        
        /// <summary>
        /// 레벨업
        /// </summary>
        public void LevelUp()
        {
            Level++;
            MaxHP += 5;
            CurrentHP = MaxHP; // 풀 회복
            Attack += 2;
            Defense += 1;
            
            Debug.Log($"{CharacterName} leveled up to {Level}! HP: {MaxHP}, ATK: {Attack}, DEF: {Defense}");
        }
    }
}
