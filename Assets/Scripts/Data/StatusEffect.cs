using UnityEngine;

namespace DiceOrbit.Data
{
    public enum StatusEffectType
    {
        // Buffs
        AttackUp,       // 공격력 증가
        DefenseUp,      // 방어력 증가
        EvasionUp,      // 회피율 증가
        MoveSpeedUp,    // 이동 거리 증가
        Regeneration,   // 매 턴 회복
        Stealth,        // 은신 (회피 증가, 특정 스킬 조건)
        Focus,          // 집중 (마법사 스택)
        
        // Debuffs
        AttackDown,     // 공격력 감소
        DefenseDown,    // 방어력 감소
        Poison,         // 독 (지속 피해)
        Burn,           // 화상 (지속 피해 + @)
        Stun,           // 기절 (행동 불가)
        Weakness,       // 약화 (받는 피해 증가)
        
        // Special
        Shield,         // 보호막 (체력 대신 소모)
        Invincible      // 무적
    }

    [System.Serializable]
    public class StatusEffect
    {
        public StatusEffectType Type;
        public int Value;           // 스택 수 or 수치 (공격력 +10 등)
        public int Duration;        // 남은 턴 수 (-1이면 영구)
        public bool IsStackable;    // 중첩 가능 여부
        
        public StatusEffect(StatusEffectType type, int value, int duration, bool isStackable = true)
        {
            Type = type;
            Value = value;
            Duration = duration;
            IsStackable = isStackable;
        }
        
        public void AddStack(int amount)
        {
            if (IsStackable)
            {
                Value += amount;
            }
        }
        
        public void RefreshDuration(int newDuration)
        {
            if(newDuration > Duration)
                Duration = newDuration;
        }
    }
}
