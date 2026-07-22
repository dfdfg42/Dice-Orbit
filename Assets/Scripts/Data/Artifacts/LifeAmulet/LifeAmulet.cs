using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Run;

namespace DiceOrbit.Data.Artifacts
{
    /// <summary>생명의 부적 — 전투 시작 시 파티 전원 +N 회복 (CombatStart 방송에 반응, 파이프라인 경유).</summary>
    [System.Serializable]
    public class LifeAmulet : RuntimeArtifact
    {
        public int amount = 5;

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (context.Phase != EventPhase.CombatStart) return;
            if (trigger != CombatTrigger.OnPreAction) return;   // 방송당 1회만 (트리거 4회 방지)
            if (context.IsSimulation) return;
            if (context.Target == null) return;

            var heal = new HealContext(null, context.Target, "생명의 부적", amount);
            CombatPipeline.Instance?.Process(heal);
        }
    }
}
