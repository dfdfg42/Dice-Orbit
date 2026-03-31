using DiceOrbit.Core;
using DiceOrbit.Data;
using DiceOrbit.Data.Skills;
using UnityEngine;

namespace DiceOrbit.Data.CharacterActives
{
    [System.Serializable]
    public class MageEnergyBallActive : CharacterActiveTemplate
    {
        [SerializeField] private int baseMultiplier = 12;
        [SerializeField] private float baseBonusRatioPerStack = 0.05f;

        public override int CalculateRawDamage(Character source, RuntimeAbility ability, int diceValue)
        {
            int level = Mathf.Max(1, ability?.CurrentLevel ?? 1);
            int resolvedBaseMultiplier = baseMultiplier + (level - 1);
            float resolvedBonusRatio = baseBonusRatioPerStack + ((level - 1) * 0.01f);
            int focusStacks = source?.StatusEffects != null ? source.StatusEffects.GetEffectValue(EffectType.Focus) : 0;

            int baseDamage = diceValue * resolvedBaseMultiplier;
            return Mathf.RoundToInt(baseDamage * (1.0f + (focusStacks * resolvedBonusRatio)));
        }

        public override string BuildPreview(Character source, RuntimeAbility ability, int diceValue)
        {
            int level = Mathf.Max(1, ability?.CurrentLevel ?? 1);
            int resolvedBaseMultiplier = baseMultiplier + (level - 1);
            float resolvedBonusRatio = baseBonusRatioPerStack + ((level - 1) * 0.01f);
            int focusStacks = source?.StatusEffects != null ? source.StatusEffects.GetEffectValue(EffectType.Focus) : 0;

            int baseDamage = diceValue * resolvedBaseMultiplier;
            float totalMultiplier = 1.0f + (focusStacks * resolvedBonusRatio);
            int finalDamage = Mathf.RoundToInt(baseDamage * totalMultiplier);
            float bonusPercent = focusStacks * resolvedBonusRatio * 100f;

            return $"예상 피해: ({diceValue} x {resolvedBaseMultiplier}) x (1 + {focusStacks} x {resolvedBonusRatio:0.##})\n= {baseDamage} x {totalMultiplier:0.##} = {finalDamage} (집중 +{bonusPercent:0.#}%)";
        }

        public override void OnAfterResolved(Character source, RuntimeAbility ability)
        {
            int focusStacks = source?.StatusEffects != null ? source.StatusEffects.GetEffectValue(EffectType.Focus) : 0;
            if (focusStacks > 0)
            {
                source.StatusEffects?.RemoveEffect(EffectType.Focus);
            }
        }
    }
}
