using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Passives
{
    [System.Serializable]
    public class StableReactionPassive : PassiveAbility
    {
        public float damageMultiplier = 1.1f;
        public float healthThresholdRatio = 0.6f;

        public override int Priority => 98;

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (owner == null || context == null || context.Action == null || owner.Stats == null) return;

            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.Action.Type != ActionType.Attack) return;
            if (context.SourceUnit != owner) return;

            float currentHPRatio = (float)owner.Stats.CurrentHP / owner.Stats.MaxHP;
            if (currentHPRatio >= healthThresholdRatio)
            {
                context.OutputValue *= damageMultiplier;
            }
        }
    }
}
