using DiceOrbit.Core;
using DiceOrbit.Data.Skills;
using UnityEngine;

namespace DiceOrbit.Data.CharacterActives
{
    [System.Serializable]
    public class WarriorGreatswordActive : CharacterActiveSkill
    {
        [Header("Designer Tuning")]
        [Tooltip("레벨별 배율값 (주사위값 x 배율)")]
        [SerializeField] private int[] multiplierByLevel = { 4, 6, 8, 10, 12 };
        [SerializeField] private int baseMultiplier = 12;

        public override Core.Pipeline.CharacterModfierContext GenerateContext(Character source, ActiveSkillSlot ability)
        {
            return new Core.Pipeline.WarriorGreatswordModifiedContext(source, this);
        }

        public override int CalculateRawDamage(Character source, ActiveSkillSlot ability, int diceValue)
        {
            int multiplier = ResolveMultiplier(Mathf.Max(1, ability?.CurrentLevel ?? 1));

            // 만약 ability가 들고 있는 RuntimeInstance(나 자신)에 
            // Caching된 데이터 체계가 본격적으로 Slot에 적용되면 여기서 읽어올 수 있습니다.
            // 일단 현재는 기존대로 동작하게 둡니다.
            return diceValue * multiplier;
        }

        public override string BuildPreview(Character source, ActiveSkillSlot ability, int diceValue)
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
