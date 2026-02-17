using UnityEngine;
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
        AllAllies,
        Tiles
    }
    
    /// <summary>
    /// 스킬 데이터
    /// </summary>
    [System.Serializable]
    public class SkillData
    {
        [Header("Basic Info")]
        public string SkillName = "Basic Attack";
        [TextArea] public string Description;
        public SkillType Type = SkillType.Active;
        public SkillTargetType TargetType = SkillTargetType.SingleEnemy;
        
        [Header("Effects")]
        public List<EffectData> Effects = new List<EffectData>();
        
        [Header("Damage")]
        public int DamageMultiplier = 1;
        public int BonusDamage = 0;
        public bool IgnoreDefense = false;
        
        /// <summary>
        /// 데미지 계산
        /// </summary>
        public int CalculateDamage(int baseAttack, int diceValue)
        {
            return baseAttack * DamageMultiplier + diceValue + BonusDamage;
        }

    }
}
