using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Passives
{
    [System.Serializable]
    public class FocusPassive : PassiveAbility
    {
        public int stacksPerTurn = 1;

        public override int Priority => 50;

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (owner == null || context == null || context.Action == null) return;

            if (trigger != CombatTrigger.OnPostAction) return;
            if (context.SourceUnit != owner) return;
            if (context.Action.Type != ActionType.OnEndTurn) return;

            owner.StatusEffects?.AddEffect(DiceOrbit.Systems.Effects.StatusEffectManager.CreateEffect(DiceOrbit.Data.EffectType.Focus, stacksPerTurn, -1));
        }
    }
}
