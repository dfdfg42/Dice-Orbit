using UnityEngine;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;

namespace DiceOrbit.Systems.Effects
{
    /// <summary>
    /// 실행 중인 상태 이상 인스턴스
    /// </summary>
    public class StatusEffect : ICombatReactor
    {
        public EffectType Type;
        public int Value;
        public int Duration;
        public bool IsStackable;
        
        public int Priority => 10; // 효과는 패시브보다 후순위? 혹은 상위? (설계에 따라 다름)

        public StatusEffect(EffectType type, int value, int duration, bool isStackable = false)
        {
            Type = type;
            Value = value;
            Duration = duration;
            IsStackable = isStackable;
        }

        public void AddStack(int value)
        {
            if (IsStackable)
            {
                Value += value;
            }
        }

        public void RefreshDuration(int duration)
        {
            if (Duration == -1 || duration == -1)
            {
                Duration = -1;
            }
            else
            {
                Duration = Mathf.Max(Duration, duration);
            }
        }

        // ICombatReactor Implementation
        public virtual void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (Owner == null) return;

            // 공통 로직: 턴 시작 시 지속시간 감소
            // (SourceUnit == Owner일 때 = 나의 턴 시작)
            if (context.Action.Type == ActionType.OnStartTurn
                && trigger == CombatTrigger.OnPostAction
                && context.SourceUnit == Owner)
            {
                if (Duration > 0)
                {
                    Debug.Log("[StatusEffect] " + Type + "의 지속시간이 감소합니다. 남은 턴: " + (Duration - 1));
                    Duration--;
                }
            }
        }

        // Owner를 주입받아야 함
        public Core.Unit Owner { get; set; }

        public void SetOwner(Core.Unit owner)
        {
            Owner = owner;
        }

        public virtual void EffectApplied()
        {
            // 효과가 적용될 때 추가로 처리할 로직이 있다면 여기에 구현
        }

        public virtual void EffectExpired()
        {
            // 효과가 만료될 때 추가로 처리할 로직이 있다면 여기에 구현
        }
    }
}
