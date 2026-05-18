using DiceOrbit.Core.Pipeline;
using DiceOrbit.Systems.Artifact;
using DiceOrbit.Core;
using UnityEngine;

using static UnityEngine.UI.GridLayoutGroup;

public class PowerfullPunch : RuntimeArtifact
{
    public PowerfullPunch(ArtifactData data) : base(data)
    {
    }

    public override void OnReact(CombatTrigger trigger, CombatContext context)
    {
        if (trigger != CombatTrigger.OnCalculateOutput) return;
        if (context.Action.Type != ActionType.Attack) return;
        if (context.SourceUnit is not Character) return;
        Debug.Log("artifact react: Powerfull Punch");
        context.OutputValue = 1000;
    }
}
