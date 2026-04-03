using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using UnityEngine;

namespace DiceOrbit.Data.Passives
{
    [System.Serializable]
    public class BattleCryPassive : PassiveAbility
    {
        [Header("Designer Tuning")]
        [Tooltip("레벨별 공격 피해 증가율(%). 예: 5는 +5%")]
        [SerializeField] private float[] damageBonusPercentByLevel = { 10f, 15f, 20f, 25f, 30f };

        public float damageMultiplier = 1.05f;
        public float CurrentDamageMultiplier => damageMultiplier;

        public override int Priority => 100;

        protected override void ApplyLevel(int level)
        {
            float bonusPercent = ResolveBonusPercent(level);
            damageMultiplier = 1f + (bonusPercent / 100f);
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
            if (owner == null || context == null || context.Action == null) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.Action.Type != ActionType.Attack) return;
            if (context.SourceUnit != owner) return;

            context.OutputValue *= damageMultiplier;
        }
    }
}
