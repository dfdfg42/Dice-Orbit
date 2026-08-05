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
        Frozen,          // 빙결 상태이상
        Dodge,           // 회피율 (%)
        Frostbite,       // 동상: 입는 피해량 증가 (서리토템)
        Weak,            // 쇠약: 가하는 피해 -V% (포션)
        Poison,          // 독: 매턴 최대체력 V% 피해 (포션)
        Power,           // 파워: 가하는 피해 +V% (포션)
        FireExtinguishMark, // 불꽃 소화 마커: 이번 턴 이미 불을 껐음 (Wave5)
        FireDamageTaken,    // 불꽃 요정용: 이번 턴 받은 누적 피해 (Wave5)
        FireGuard,          // 불의 가호: 방어도 보유 시 받는 피해 감소 (Wave5)
        BoneMark,           // 뼈무덤 발동 마커: 이번 라운드 해골병사가 뼈 방어도 획득함 (Wave1)
        Stunned,            // 기절: 다음 턴 행동 불가(이동+스킬) (Wave3 수정)
        CrystalStack,       // 수정 중첩: 수정 핵 스택 카운터(가시화용 상태) (Wave3 수정)
        SnowDamageTaken,    // 받은 피해: 눈사람 진창눈 취소 판정용 누적 피해(가시화) (Wave3 눈사람)
        Slowed,             // 둔화: 이동 감소(MoveDebuff) (Wave0 슬라임)
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

