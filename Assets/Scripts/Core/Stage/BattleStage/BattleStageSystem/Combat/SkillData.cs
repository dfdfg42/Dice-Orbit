using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
        
        [Header("Modules")]
        public List<Skills.Modules.SkillActionModule> ActionModules = new List<Skills.Modules.SkillActionModule>();
        
        [Header("Legacy (deprecated)")]
        public int DamageMultiplier = 1;
        public int BonusDamage = 0;
        public bool IgnoreDefense = false;
        
        /// <summary>
        /// 주사위 값으로 스킬 사용 가능한지 확인
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
            return baseAttack * DamageMultiplier + diceValue + BonusDamage;
        }

        /// <summary>
        /// 런타임에서 독립적으로 사용할 수 있도록 깊은 복사
        /// </summary>
        public SkillData DeepCopy()
        {
            var copiedRequirement = new DiceRequirement
            {
                MinDiceCount = Requirement != null ? Requirement.MinDiceCount : 1,
                MinDiceValue = Requirement != null ? Requirement.MinDiceValue : 1,
                MaxDiceValue = Requirement != null ? Requirement.MaxDiceValue : null,
                ExactDiceValue = Requirement != null ? Requirement.ExactDiceValue : null,
                Pattern = Requirement != null ? Requirement.Pattern : DicePattern.None
            };

            var copiedEffects = Effects != null
                ? Effects.Select(e => new EffectData(e.Type, e.Value, e.Duration)).ToList()
                : new List<EffectData>();

            return new SkillData
            {
                SkillName = SkillName,
                Type = Type,
                TargetType = TargetType,
                Effects = copiedEffects,
                Requirement = copiedRequirement,
                ActionModules = ActionModules != null ? new List<Skills.Modules.SkillActionModule>(ActionModules) : new List<Skills.Modules.SkillActionModule>(),
                DamageMultiplier = DamageMultiplier,
                BonusDamage = BonusDamage,
                IgnoreDefense = IgnoreDefense
            };
        }
    }
}
