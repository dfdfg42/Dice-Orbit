using DiceOrbit.Core;
using DiceOrbit.Data.Skills;
using UnityEngine;

namespace DiceOrbit.Data.CharacterActives
{
    [System.Serializable]
    public class RogueAmbushActive : CharacterActiveSkill
    {
        [Header("Designer Tuning")]
        [Tooltip("피해 = 주사위 눈금 x 배율")]
        [SerializeField] private int multiplier = 2;

        public override int CalculateRawDamage(Character source, ActiveSkillSlot ability, int diceValue)
        {
            return diceValue * Mathf.Max(1, multiplier);
        }

        public override string BuildPreview(Character source, ActiveSkillSlot ability, int diceValue)
        {
            int mult = Mathf.Max(1, multiplier);
            int damage = diceValue * mult;
            return $"예상 피해: ({diceValue} x {mult}) = {damage}";
        }

        public override string GetDynamicDescription()
            => $"주사위 눈금 × {Mathf.Max(1, multiplier)} 피해";
    }
}
