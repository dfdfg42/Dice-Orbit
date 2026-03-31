using DiceOrbit.Core;
using DiceOrbit.Data.Skills;
using UnityEngine;

namespace DiceOrbit.Data.CharacterActives
{
    [System.Serializable]
    public class AlchemistThrowActive : CharacterActiveTemplate
    {
        [SerializeField] private int baseMultiplier = 12;

        public override int CalculateRawDamage(Character source, RuntimeAbility ability, int diceValue)
        {
            int level = Mathf.Max(1, ability?.CurrentLevel ?? 1);
            int multiplier = baseMultiplier + (level - 1);
            return diceValue * multiplier;
        }

        public override string BuildPreview(Character source, RuntimeAbility ability, int diceValue)
        {
            int level = Mathf.Max(1, ability?.CurrentLevel ?? 1);
            int multiplier = baseMultiplier + (level - 1);
            int damage = diceValue * multiplier;
            return $"예상 피해: ({diceValue} x {multiplier}) = {damage}";
        }
    }
}
