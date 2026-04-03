using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Passives
{
    [System.Serializable]
    public class FocusPassive : PassiveAbility
    {
        [Header("Designer Tuning")]
        [Tooltip("레벨별 집중 1스택당 추가 피해율(%). 예: 5는 +5%")]
        [SerializeField] private float[] bonusPercentPerStackByLevel = {10f, 15f, 20f, 25f, 30f };

        public int stacksPerTurn = 1;
        private float runtimeBonusDamageRatioPerStack = 0.05f;

        public float BonusDamageRatioPerStack => runtimeBonusDamageRatioPerStack;

        public override int Priority => 50;

        protected override void ApplyLevel(int level)
        {
            float bonusPercent = ResolveBonusPercent(level);
            runtimeBonusDamageRatioPerStack = bonusPercent / 100f;
        }

        private float ResolveBonusPercent(int level)
        {
            if (bonusPercentPerStackByLevel == null || bonusPercentPerStackByLevel.Length == 0)
            {
                return runtimeBonusDamageRatioPerStack * 100f;
            }

            int index = Mathf.Clamp(level - 1, 0, bonusPercentPerStackByLevel.Length - 1);
            return bonusPercentPerStackByLevel[index];
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
