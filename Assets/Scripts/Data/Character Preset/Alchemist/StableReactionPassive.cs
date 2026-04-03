using DiceOrbit.Core.Pipeline;
using UnityEngine;

namespace DiceOrbit.Data.Passives
{
    [System.Serializable]
    public class StableReactionPassive : PassiveAbility
    {
        [Header("Designer Tuning")]
        [Tooltip("레벨별 피해 증가율(%). 체력 조건 충족 시 적용")]
        [SerializeField] private float[] damageBonusPercentByLevel = { 10f, 15f, 20f, 25f, 30f };

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

        private float ResolveBonusPercent(int level)
        {
            if (damageBonusPercentByLevel == null || damageBonusPercentByLevel.Length == 0)
            {
                return (damageMultiplier - 1f) * 100f;
            }

            int index = Mathf.Clamp(level - 1, 0, damageBonusPercentByLevel.Length - 1);
            return damageBonusPercentByLevel[index];
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
