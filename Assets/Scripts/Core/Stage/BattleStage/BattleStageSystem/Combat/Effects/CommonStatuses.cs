using DiceOrbit.Core;
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

    /// <summary>
    /// 중독 — StS식 정수 중첩 (2026-08-28 재설계). 지속시간 없음(Duration=-1), Value가 곧 남은 중첩.
    /// 소유자 턴 시작 시(행동 전) 현재 중첩만큼 직접 체력 손실을 주고 중첩을 1 줄인다. 0이 되면 제거.
    /// 예: 중첩 6 → 턴마다 6, 5, 4, 3, 2, 1 피해. 방어도·보호막·공격 보정 전부 무시(IsDirectHpLoss).
    /// </summary>
    public class PoisonStatus : StatusEffect
    {
        public PoisonStatus(int stacks)
            : base(EffectType.Poison, Mathf.Max(0, stacks), -1, isStackable: true) { }

        /// <summary>이번 틱의 피해량을 반환하고 중첩을 1 줄인다. (순수 로직 — 자가 테스트 대상)</summary>
        public int ConsumeTick()
        {
            int damage = Mathf.Max(0, Value);
            Value = Mathf.Max(0, Value - 1);
            return damage;
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            base.OnTurnEvent(trigger, context);   // Duration=-1이라 공통 감소는 일어나지 않는다

            if (Owner == null || Owner.Stats == null) return;
            if (context.Phase != EventPhase.TurnStart) return;
            if (trigger != CombatTrigger.OnPreAction) return;   // 행동(OnPostAction 계열)보다 먼저 틱
            if (context.SourceUnit != Owner) return;
            if (context.IsSimulation) return;

            int damage = ConsumeTick();
            if (damage > 0)
            {
                var tick = new AttackContext(null, Owner, "중독", damage) { IsDirectHpLoss = true };
                CombatPipeline.Instance?.Process(tick);
            }

            if (Value <= 0) Owner.StatusEffects?.RemoveEffect(EffectType.Poison);
        }
    }

    /// <summary>기절 — 행동 불가(지속시간만큼). 효과별 하드코딩 없이 상태이상 스스로 처리:
    /// - 플레이어: 적용 시 CharacterStats.StunDebuff++/만료 시 -- → canAct() 게이트가 UI/스킬을 막음.
    /// - 몬스터: 소유자 턴 시작마다 예약 행동을 취소(CancelIntent) → 그 턴은 idle.
    /// Frozen/BindDebuff 패턴 미러.</summary>
    public class StunDebuff : StatusEffect
    {
        public StunDebuff(int duration) : base(EffectType.Stunned, 0, duration)
        {
            IsStackable = false;
        }

        public override void EffectApplied()
        {
            if (Owner != null && Owner.Stats is CharacterStats c) c.StunDebuff++;
            Debug.Log($"[StunDebuff] {Owner?.name} 기절! (지속: {Duration}턴)");
        }

        public override void EffectExpired()
        {
            if (Owner != null && Owner.Stats is CharacterStats c) c.StunDebuff--;
            Debug.Log($"[StunDebuff] {Owner?.name} 기절 해제.");
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            // 몬스터: 자기 턴 시작마다 예약 행동을 취소해 스킵(지속시간만큼). 플레이어는 canAct() 경로.
            if (Owner is Monster m
                && context.Phase == EventPhase.TurnStart
                && trigger == CombatTrigger.OnPostAction
                && context.SourceUnit == Owner)
            {
                m.CancelIntent();
            }

            base.OnTurnEvent(trigger, context); // 공통 지속시간 감소(→0이면 매니저가 정리)
        }
    }

    /// <summary>둔화 — 이동 감소(Value만큼). 적용 시 CharacterStats.MoveDebuff += Value, 만료 시 -=.
    /// OrbitManager가 이동칸에 (MoveBuff - MoveDebuff)를 반영한다. Frozen/BindDebuff 패턴 미러.</summary>
    public class SlowStatus : StatusEffect
    {
        public SlowStatus(int value, int duration) : base(EffectType.Slowed, value, duration)
        {
            IsStackable = false;
        }

        public override void EffectApplied()
        {
            if (Owner != null && Owner.Stats is CharacterStats c) c.MoveDebuff += Value;
        }

        public override void EffectExpired()
        {
            if (Owner != null && Owner.Stats is CharacterStats c) c.MoveDebuff -= Value;
        }
    }
}
