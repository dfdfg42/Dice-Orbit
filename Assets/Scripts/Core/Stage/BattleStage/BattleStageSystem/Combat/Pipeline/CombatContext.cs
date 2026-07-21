using System.Collections.Generic;
using DiceOrbit.Data; // EffectType

namespace DiceOrbit.Core.Pipeline
{
    public enum EventPhase { TurnStart, TurnEnd, TileTick, CombatStart }

    /// <summary>
    /// 파이프라인을 통과하는 봉투(본체). NotifyReactors가 나르는 타입.
    /// </summary>
    public abstract class CombatContext
    {
        public Unit SourceUnit;
        public Unit Target;
        public bool IsCancelled;
        public bool IsSimulation;

        protected CombatContext(Unit source, Unit target)
        {
            SourceUnit = source;
            Target = target;
        }
    }

    /// <summary>효과 행위 공통 (공격/힐) — 레시피 + 계산상태.</summary>
    public abstract class EffectContext : CombatContext
    {
        public string Name;
        public float BaseValue;
        public float OutputValue;
        public HashSet<string> Tags = new HashSet<string>();
        public List<ActionEffectInfo> Effects = new List<ActionEffectInfo>();

        /// <summary>
        /// 이 행위에 쓸 VFX 프로필 — 실행부(스킬)가 지정만 하고,
        /// 재생 판단은 파이프라인 ApplyAction 한 곳에서 한다 (히트/힐 프리팹 없으면 전역 기본).
        /// </summary>
        public Visuals.CombatVfxProfile VfxProfile;

        protected EffectContext(Unit source, Unit target, string name, float baseValue)
            : base(source, target)
        {
            Name = name;
            BaseValue = baseValue;
            OutputValue = baseValue;
        }

        public void AddTag(string tag) => Tags.Add(tag);
        public bool HasTag(string tag) => Tags.Contains(tag);
        public void AddEffect(EffectType type, int value, int duration)
            => Effects.Add(new ActionEffectInfo(type, value, duration));
    }

    public sealed class AttackContext : EffectContext
    {
        public bool IsEffected;
        public AttackContext(Unit source, Unit target, string name, float baseValue)
            : base(source, target, name, baseValue) { }
    }

    public sealed class HealContext : EffectContext
    {
        public HealContext(Unit source, Unit target, string name, float baseValue)
            : base(source, target, name, baseValue) { }
    }

    /// <summary>이동 사건 — 걸음 수만 운반.</summary>
    public sealed class MoveContext : CombatContext
    {
        public int Steps;
        public MoveContext(Unit source, Unit target, int steps)
            : base(source, target) { Steps = steps; }
    }

    /// <summary>턴시작/종료/타일틱 사건 — 숫자 없음.</summary>
    public sealed class TurnEventContext : CombatContext
    {
        public EventPhase Phase;
        public TurnEventContext(Unit source, Unit target, EventPhase phase)
            : base(source, target) { Phase = phase; }
    }
}
