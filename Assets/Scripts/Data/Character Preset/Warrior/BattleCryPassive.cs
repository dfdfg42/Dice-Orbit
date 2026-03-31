using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using UnityEngine;

namespace DiceOrbit.Data.Passives
{
    [System.Serializable]
    public class BattleCryPassive : PassiveAbility
    {
        // 레벨 1~5 공격 피해량 증가(%)
        private static readonly float[] DamageBonusPercentByLevel = { 5f, 6f, 7f, 8f, 10f };

        public float damageMultiplier = 1.05f;
    public float CurrentDamageMultiplier => damageMultiplier;

        public override int Priority => 100;

        protected override void ApplyLevel(int level)
        {
            float bonusPercent = ResolveBonusPercent(level);
            damageMultiplier = 1f + (bonusPercent / 100f);
        }

        private static float ResolveBonusPercent(int level)
        {
            int index = Mathf.Clamp(level - 1, 0, DamageBonusPercentByLevel.Length - 1);
            return DamageBonusPercentByLevel[index];
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
