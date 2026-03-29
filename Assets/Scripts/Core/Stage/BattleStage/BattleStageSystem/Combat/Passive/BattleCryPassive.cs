using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Passives
{
    [System.Serializable]
    public class BattleCryPassive : PassiveAbility
    {
        public float damageMultiplier = 1.05f;

        public override int Priority => 100;

        protected override void ApplyLevel(int level)
        {
            float bonusPercent = CharacterStats.GetPassivePercentFromCurveB(level, 5f);
            damageMultiplier = 1f + (bonusPercent / 100f);
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (owner == null || context == null || context.Action == null) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.Action.Type != ActionType.Attack) return;
            if (context.SourceUnit != owner) return;

            context.OutputValue *= damageMultiplier;
        }
    }
}
