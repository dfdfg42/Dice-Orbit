using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Systems.Effects
{
    /// <summary>쇠약 — 가하는 피해 Value% 감소 (포션 등으로 부여, 스펙 2026-07-29).</summary>
    public class WeakStatus : StatusEffect
    {
        public WeakStatus(int value, int duration)
            : base(EffectType.Weak, value, duration, isStackable: false) { }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (Owner == null) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.SourceUnit != Owner) return;

            context.OutputValue *= 1f - Value / 100f;
        }
    }

    /// <summary>파워 — 가하는 피해 Value% 증가.</summary>
    public class PowerStatus : StatusEffect
    {
        public PowerStatus(int value, int duration)
            : base(EffectType.Power, value, duration, isStackable: false) { }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (Owner == null) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.SourceUnit != Owner) return;

            context.OutputValue *= 1f + Value / 100f;
        }
    }

    /// <summary>독 — 본인 턴 시작 시 최대체력 Value% 피해 (파이프라인 경유, 중첩 가능).</summary>
    public class PoisonStatus : StatusEffect
    {
        public PoisonStatus(int value, int duration)
            : base(EffectType.Poison, value, duration, isStackable: true) { }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            base.OnTurnEvent(trigger, context);   // 공통 지속시간 감소 (TurnStart + OnPostAction)

            if (Owner == null || Owner.Stats == null) return;
            if (context.Phase != EventPhase.TurnStart) return;
            if (trigger != CombatTrigger.OnPreAction) return;   // 틱 1회 (지속 감소보다 먼저)
            if (context.SourceUnit != Owner) return;
            if (context.IsSimulation) return;

            int damage = Mathf.Max(1, Mathf.RoundToInt(Owner.Stats.MaxHP * (Value / 100f)));
            var tick = new AttackContext(null, Owner, "독", damage);
            CombatPipeline.Instance?.Process(tick);
        }
    }
}
