using UnityEngine;
using System.Collections.Generic;

namespace DiceOrbit.Data
{
    /// <summary>
    /// 캐릭터 스탯 데이터
    /// </summary>
    [System.Serializable]
    public class CharacterStats
    {
        [Header("Basic Info")]
        public string CharacterName = "Hero";
        public int Level = 1;
        
        [Header("Combat Stats")]
        public int MaxHP = 30;
        public int CurrentHP = 30;
        public int Attack = 5;
        public int Defense = 0;
        
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
        /// HP 증가
        /// </summary>
        public void Heal(int amount)
        {
            CurrentHP = Mathf.Min(CurrentHP + amount, MaxHP);
        }
        
        /// <summary>
        /// 데미지 받기
        /// </summary>
        public void TakeDamage(int damage)
        {
            int actualDamage = Mathf.Max(1, damage - Defense); // 최소 1 데미지
            CurrentHP = Mathf.Max(0, CurrentHP - actualDamage);
            
            Debug.Log($"{CharacterName} took {actualDamage} damage! (HP: {CurrentHP}/{MaxHP})");
        }
        
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
        
        /// <summary>
        /// 생존 확인
        /// </summary>
        public bool IsAlive => CurrentHP > 0;
        
        /// <summary>
        /// HP 비율
        /// </summary>
        public float HPRatio => MaxHP > 0 ? (float)CurrentHP / MaxHP : 0f;
    }
}
