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

        public void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (Owner == null) return;

            // OnCalculateOutput: 데미지 계산 시점에 개입. 소유자가 공격자일 때.
            if (trigger == CombatTrigger.OnCalculateOutput && context.SourceUnit == Owner)
            {
                context.OutputValue += Value;
            }
        }
    }
}