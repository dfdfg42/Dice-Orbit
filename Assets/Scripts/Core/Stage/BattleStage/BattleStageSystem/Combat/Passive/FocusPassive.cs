using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Passives
{
    [CreateAssetMenu(fileName = "FocusPassive", menuName = "Dice Orbit/Passives/Focus (Mage)")]
    public class FocusPassive : PassiveAbility
    {
        [Header("Focus Settings")]
        [Tooltip("턴 종료 시 획득할 집중 스택 수")]
        public int stacksPerTurn = 1;

        public override int Priority => 50;

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (owner == null || context == null || context.Action == null) return;

            if (trigger != CombatTrigger.OnPostAction) return;
            if (context.SourceUnit != owner) return;
            if (context.Action.Type != ActionType.OnEndTurn) return;

            owner.StatusEffects?.AddEffect(DiceOrbit.Data.EffectType.Focus, stacksPerTurn, -1);
        }
    }
}
