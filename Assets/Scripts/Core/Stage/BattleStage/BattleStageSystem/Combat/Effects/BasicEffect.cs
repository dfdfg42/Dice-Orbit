using UnityEngine;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;

namespace DiceOrbit.Systems.Effects
{
    /// <summary>
    /// 공격력 버프 (데미지 계산 시 추가)
    /// </summary>
    public class BuffAttackStatus : StatusEffect
    {
        public BuffAttackStatus(int value, int duration) : base(EffectType.BuffAttack, value, duration)
        {
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            base.OnReact(trigger, context);

            if (Owner == null) return;

            // OnCalculateOutput: 데미지 계산 시점에 개입
            if (trigger == CombatTrigger.OnCalculateOutput)
            {
                // 소유자가 공격자이며, 공격 액션일 때
                if (context.SourceUnit == Owner && context.Action.Type == ActionType.Attack)
                {
                    context.OutputValue += Value;
                    // Debug.Log($"[BuffAttack] Added {Value} damage to {context.Action.Name}");
                }
            }
        }
    }
}