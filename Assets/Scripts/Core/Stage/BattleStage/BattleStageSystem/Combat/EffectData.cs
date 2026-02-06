using UnityEngine;
using DiceOrbit.Systems;

namespace DiceOrbit.Data
{
    /// <summary>
    /// 효과 타입
    /// </summary>
    public enum EffectType
    {
        Damage,         // 데미지
        Heal,           // 회복
        BuffAttack,     // 공격력 버프
        BuffDefense,    // 방어력 버프
        DebuffAttack,   // 공격력 디버프
        DebuffDefense,  // 방어력 디버프
        Dot,            // 지속 데미지
        Shield          // 보호막
    }
    
    /// <summary>
    /// 효과 데이터
    /// </summary>
    [System.Serializable]
    public class EffectData
    {
        public EffectType Type;
        public int Value;
        public int Duration = 0; // 0 = 즉시, 1+ = 턴 지속
        public string Description;
        
        public EffectData(EffectType type, int value, int duration = 0)
        {
            Type = type;
            Value = value;
            Duration = duration;
            Description = GenerateDescription();
        }
        
        private string GenerateDescription()
        {
            switch (Type)
            {
                case EffectType.Damage:
                    return $"Deal {Value} damage";
                case EffectType.Heal:
                    return $"Heal {Value} HP";
                case EffectType.BuffAttack:
                    return $"+{Value} ATK for {Duration} turns";
                case EffectType.BuffDefense:
                    return $"+{Value} DEF for {Duration} turns";
                case EffectType.Dot:
                    return $"{Value} damage for {Duration} turns";
                default:
                    return Type.ToString();
            }
        }
    }
}
