using UnityEngine;

namespace DiceOrbit.Data
{
    /// <summary>
    /// 공격 의도 타입
    /// </summary>
    public enum IntentType
    {
        Attack,      // 공격
        Defend,      // 방어 버프
        Buff,        // 공격력 버프
        Special,     // 특수 행동
        Multi        // 다중 공격
    }
    
    /// <summary>
    /// 몬스터 공격 의도 데이터
    /// </summary>
    [System.Serializable]
    public class AttackIntent
    {
        public IntentType Type;
        public int Damage;           // 예정 데미지
        public int HitCount = 1;     // 공격 횟수 (다중 공격용)
        public string Description;
        
        public AttackIntent(IntentType type, int damage = 0, string desc = "")
        {
            Type = type;
            Damage = damage;
            Description = desc;
        }
        
        /// <summary>
        /// 의도 설명
        /// </summary>
        public override string ToString()
        {
            switch (Type)
            {
                case IntentType.Attack:
                    return $"Attack ({Damage} damage)";
                case IntentType.Multi:
                    return $"Attack x{HitCount} ({Damage} each)";
                case IntentType.Defend:
                    return "Defend (+Defense)";
                case IntentType.Buff:
                    return "Power Up (+Attack)";
                case IntentType.Special:
                    return Description;
                default:
                    return "Unknown";
            }
        }
    }
}
