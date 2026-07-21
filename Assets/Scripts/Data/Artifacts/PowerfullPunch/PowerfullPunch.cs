using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Run;
using UnityEngine;

namespace DiceOrbit.Data.Artifacts
{
    /// <summary>강력한 주먹 (디버그) — 캐릭터 공격의 출력을 고정값으로.</summary>
    [System.Serializable]
    public class PowerfullPunch : RuntimeArtifact
    {
        public int fixedOutput = 1000;

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.SourceUnit is not Character) return;
            Debug.Log("[Artifact] PowerfullPunch react");
            context.OutputValue = fixedOutput;
        }
    }
}
