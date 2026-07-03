using UnityEngine;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Modifiers.Generic
{
    /// <summary>
    /// [예리한 칼날] 모든 공격 피해 +고정값. (Generic: 파이프라인 훅)
    /// </summary>
    [System.Serializable]
    public class SharpBladeModifier : CharacterModifier
    {
        [SerializeField] private int bonusDamage = 1;

        public override ModifierCategory Category => ModifierCategory.Generic;
        public override string ModifierName => "예리한 칼날";
        public override string Description => $"모든 공격 피해 +{bonusDamage}";

        protected override void OnAttackWithActive(AttackContext context)
        {
            context.OutputValue += bonusDamage;
        }
    }

    /// <summary>
    /// [광폭화] 모든 공격 피해 +%. (Generic)
    /// </summary>
    [System.Serializable]
    public class BerserkModifier : CharacterModifier
    {
        [SerializeField] private int bonusPercent = 5;

        public override ModifierCategory Category => ModifierCategory.Generic;
        public override string ModifierName => "광폭화";
        public override string Description => $"모든 공격 피해 +{bonusPercent}%";

        protected override void OnAttackWithActive(AttackContext context)
        {
            context.OutputValue *= 1f + (bonusPercent / 100f);
        }
    }

    /// <summary>
    /// [거인의 힘] 모든 공격 피해 +고정값(대). (Generic)
    /// </summary>
    [System.Serializable]
    public class GiantStrengthModifier : CharacterModifier
    {
        [SerializeField] private int bonusDamage = 2;

        public override ModifierCategory Category => ModifierCategory.Generic;
        public override string ModifierName => "거인의 힘";
        public override string Description => $"모든 공격 피해 +{bonusDamage}";

        protected override void OnAttackWithActive(AttackContext context)
        {
            context.OutputValue += bonusDamage;
        }
    }
}
