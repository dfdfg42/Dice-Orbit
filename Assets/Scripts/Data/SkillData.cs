using UnityEngine;
using System;
using System.Collections.Generic;

namespace DiceOrbit.Data
{
    /// <summary>
    /// 스킬 타입
    /// </summary>
    public enum SkillType
    {
        Active,     // 사용 스킬
        Passive     // 패시브 (자동 적용)
    }
    
    /// <summary>
    /// 스킬 타겟 타입
    /// </summary>
    public enum SkillTargetType
    {
        SingleEnemy,
        AllEnemies,
        Self,
        Ally,
        AllAllies
    }
    
    /// <summary>
    /// 스킬 데이터
    /// </summary>
    [System.Serializable]
    public class SkillData
    {
        [Header("Basic Info")]
        public string SkillName = "Basic Attack";
        public SkillType Type = SkillType.Active;
        public SkillTargetType TargetType = SkillTargetType.SingleEnemy;
        
        [Header("Effects")]
        public List<EffectData> Effects = new List<EffectData>();
        
        [Header("Requirements")]
        public DiceRequirement Requirement = new DiceRequirement();
        
        [Header("Legacy (deprecated)")]
        public int DamageMultiplier = 1;
        public int BonusDamage = 0;
        public bool IgnoreDefense = false;
        
        [Header("Modular System")]
        public List<Skills.Modules.SkillActionModule> ActionModules = new List<Skills.Modules.SkillActionModule>();
        
        /// <summary>
        /// 주사위 요구사항 충족 여부
        /// </summary>
        public bool CanUse(int diceValue)
        {
            return Requirement.CanUse(diceValue);
        }
        
        /// <summary>
        /// 레거시 데미지 계산 (하위 호환)
        /// </summary>
        public int CalculateDamage(int baseAttack, int diceValue)
        {
            if (!string.IsNullOrWhiteSpace(SkillName) &&
                SkillName.Trim().Equals("Basic Attack", StringComparison.OrdinalIgnoreCase))
            {
                return (diceValue * DamageMultiplier) + BonusDamage;
            }

            return baseAttack * DamageMultiplier + diceValue + BonusDamage;
        }
    }
}
