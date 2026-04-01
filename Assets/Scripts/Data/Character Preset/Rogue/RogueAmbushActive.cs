using DiceOrbit.Core;
using DiceOrbit.Data.Skills;
using UnityEngine;

namespace DiceOrbit.Data.CharacterActives
{
    [System.Serializable]
    public class RogueAmbushActive : CharacterActiveTemplate
    {
        [Header("Designer Tuning")]
        [Tooltip("레벨별 배율값 (주사위값 x 배율)")]
        [SerializeField] private int[] multiplierByLevel = { 20, 21, 22, 23, 24 };
        [SerializeField] private int baseMultiplier = 20;

        public override int CalculateRawDamage(Character source, RuntimeAbility ability, int diceValue)
        {
            int level = Mathf.Max(1, ability?.CurrentLevel ?? 1);
            int multiplier = ResolveMultiplier(level);
            return diceValue * multiplier;
        }

        public override string BuildPreview(Character source, RuntimeAbility ability, int diceValue)
        {
            int level = Mathf.Max(1, ability?.CurrentLevel ?? 1);
            int multiplier = ResolveMultiplier(level);
            int damage = diceValue * multiplier;
            return $"예상 피해: ({diceValue} x {multiplier}) = {damage}";
        }

        private int ResolveMultiplier(int level)
        {
            if (multiplierByLevel == null || multiplierByLevel.Length == 0)
            {
                return Mathf.Max(1, baseMultiplier + (level - 1));
            }

            int index = Mathf.Clamp(level - 1, 0, multiplierByLevel.Length - 1);
            return Mathf.Max(1, multiplierByLevel[index]);
        }
    }
}
