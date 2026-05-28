using DiceOrbit.Core;
using DiceOrbit.Data;
using DiceOrbit.Data.Skills;
using UnityEngine;

namespace DiceOrbit.Data.CharacterActives
{
    [System.Serializable]
    public class MageEnergyBallActive : CharacterActiveSkill
    {
        [Header("Designer Tuning")]
        [Tooltip("피해 = (주사위 눈금 x 배율) + 집중 스택")]
        [SerializeField] private int multiplier = 1;

        public override int CalculateRawDamage(Character source, ActiveSkillSlot ability, int diceValue)
        {
            int focusStacks = source?.StatusEffects != null ? source.StatusEffects.GetEffectValue(EffectType.Focus) : 0;
            return diceValue * Mathf.Max(1, multiplier) + focusStacks;
        }

        public override string BuildPreview(Character source, ActiveSkillSlot ability, int diceValue)
        {
            int mult = Mathf.Max(1, multiplier);
            int focusStacks = source?.StatusEffects != null ? source.StatusEffects.GetEffectValue(EffectType.Focus) : 0;
            int damage = diceValue * mult + focusStacks;
            return $"예상 피해: ({diceValue} x {mult}) + 집중 {focusStacks} = {damage}";
        }

        public override string GetDynamicDescription()
            => $"{FormatDiceCondition()} X {Mathf.Max(1, multiplier)} + 집중 스택 피해";

        public override void OnAfterResolved(Character source, ActiveSkillSlot ability)
        {
            int focusStacks = source?.StatusEffects != null ? source.StatusEffects.GetEffectValue(EffectType.Focus) : 0;
            if (focusStacks > 0)
            {
                source.StatusEffects?.RemoveEffect(EffectType.Focus);
            }
        }
    }
}
