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
        
        [Header("Skills")]
        public List<SkillData> ActiveSkills = new List<SkillData>();
        public List<SkillData> PassiveSkills = new List<SkillData>();
        
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
