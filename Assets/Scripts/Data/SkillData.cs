using UnityEngine;

namespace DiceOrbit.Data
{
    /// <summary>
    /// 스킬 타겟 타입
    /// </summary>
    public enum SkillTargetType
    {
        SingleEnemy,    // 단일 적
        AllEnemies,     // 모든 적
        Self,           // 자신
        Ally,           // 아군 1명
        AllAllies       // 모든 아군
    }
    
    /// <summary>
    /// 스킬 데이터
    /// </summary>
    [System.Serializable]
    public class SkillData
    {
        [Header("Basic Info")]
        public string SkillName = "Basic Attack";
        public string Description = "Attack the enemy";
        
        [Header("Targeting")]
        public SkillTargetType TargetType = SkillTargetType.SingleEnemy;
        
        [Header("Stats")]
        public int DamageMultiplier = 1; // 공격력 배수 (1 = 100%)
        public int MinDiceValue = 1; // 최소 주사위 값 요구량
        
        [Header("Special Effects")]
        public bool IgnoreDefense = false;  // 방어 무시
        public int BonusDamage = 0;         // 추가 고정 데미지
        
        [Header("Visual")]
        public Sprite SkillIcon;
        public Color SkillColor = Color.red;
        
        /// <summary>
        /// 주사위 값으로 데미지 계산
        /// </summary>
        public int CalculateDamage(int baseAttack, int diceValue)
        {
            return (baseAttack * DamageMultiplier) + diceValue + BonusDamage;
        }
        
        /// <summary>
        /// 사용 가능 여부
        /// </summary>
        public bool CanUse(int diceValue)
        {
            return diceValue >= MinDiceValue;
        }
    }
}
