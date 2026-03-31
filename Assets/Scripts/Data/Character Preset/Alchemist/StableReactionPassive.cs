using DiceOrbit.Core.Pipeline;
using UnityEngine;

namespace DiceOrbit.Data.Passives
{
    [System.Serializable]
    public class StableReactionPassive : PassiveAbility
    {
        // 레벨 1~5 피해 증가(%)
        private static readonly float[] DamageBonusPercentByLevel = { 10f, 12f, 14f, 16f, 18f };

        public float damageMultiplier = 1.1f;
        public float healthThresholdRatio = 0.6f;
        private float runtimeDamageMultiplier = 1.1f;
    public float CurrentDamageMultiplier => runtimeDamageMultiplier;

        public override int Priority => 98;

        protected override void ApplyLevel(int level)
        {
            float bonusPercent = ResolveBonusPercent(level);
            runtimeDamageMultiplier = 1f + (bonusPercent / 100f);
        }

        private static float ResolveBonusPercent(int level)
        {
            int index = Mathf.Clamp(level - 1, 0, DamageBonusPercentByLevel.Length - 1);
            return DamageBonusPercentByLevel[index];
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (owner == null || context == null || context.Action == null || owner.Stats == null) return;

            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.Action.Type != ActionType.Attack) return;
            if (context.SourceUnit != owner) return;

            float currentHPRatio = (float)owner.Stats.CurrentHP / owner.Stats.MaxHP;
            if (currentHPRatio >= healthThresholdRatio)
            {
                context.OutputValue *= runtimeDamageMultiplier;
            }
        }
    }
}
