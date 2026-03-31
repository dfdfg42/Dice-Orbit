using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Passives
{
    [System.Serializable]
    public class FocusPassive : PassiveAbility
    {
        // 레벨 1~5 스택당 추가 피해(%)
        private static readonly float[] BonusPercentPerStackByLevel = { 5f, 6f, 7f, 8f, 10f };

        public int stacksPerTurn = 1;
        private float runtimeBonusDamageRatioPerStack = 0.05f;

        public float BonusDamageRatioPerStack => runtimeBonusDamageRatioPerStack;

        public override int Priority => 50;

        protected override void ApplyLevel(int level)
        {
            float bonusPercent = ResolveBonusPercent(level);
            runtimeBonusDamageRatioPerStack = bonusPercent / 100f;
        }

        private static float ResolveBonusPercent(int level)
        {
            int index = Mathf.Clamp(level - 1, 0, BonusPercentPerStackByLevel.Length - 1);
            return BonusPercentPerStackByLevel[index];
        }

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
