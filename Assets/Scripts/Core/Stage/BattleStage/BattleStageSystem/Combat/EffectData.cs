using UnityEngine;

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
        Shield,         // 보호막
        Focus,           // 마법사 집중 스택
        Honey,           // 꿀 타일 효과
        SlushSnow,      // 눈사람 진창눈 이동 디버프 (몬스터 전용 타일/상태 로직)
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
            string durationText = (Duration == -1) ? "Permanent" : $"{Duration} turns";

            switch (Type)
            {
                case EffectType.Damage:
                    return $"Deal {Value} damage";
                case EffectType.Heal:
                    return $"Heal {Value} HP";
                case EffectType.BuffAttack:
                    return $"+{Value} ATK ({durationText})";
                case EffectType.BuffDefense:
                    return $"+{Value} DEF ({durationText})";
                case EffectType.Dot:
                    return $"{Value} damage ({durationText})";
                default:
                    return Type.ToString();
            }
        }
    }
}

