using UnityEngine;

namespace DiceOrbit.Data
{
    /// <summary>
    /// 스킬 데이터
    /// </summary>
    [System.Serializable]
    public class SkillData
    {
        [Header("Basic Info")]
        public string SkillName = "Basic Attack";
        public string Description = "Attack the enemy";
        
        [Header("Stats")]
        public int DamageMultiplier = 1; // 공격력 배수 (1 = 100%)
        public int MinDiceValue = 1; // 최소 주사위 값 요구량
        
        [Header("Visual")]
        public Sprite SkillIcon;
        public Color SkillColor = Color.red;
        
        /// <summary>
        /// 주사위 값으로 데미지 계산
        /// </summary>
        public int CalcuateDamage(int baseAttack, int diceValue)
        {
            return baseAttack * DamageMultiplier + diceValue;
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
