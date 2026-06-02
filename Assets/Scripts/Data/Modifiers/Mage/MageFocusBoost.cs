using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Data.Passives;

namespace DiceOrbit.Data.Modifiers.Mage
{
    /// <summary>
    /// [마법사 시그니처] 정신 집중(FocusPassive) 획득 스택 +N.
    /// 패시브가 안전한 턴 종료 시 부여할 스택을 계산할 때 이 보너스를 합산한다(풀 방식).
    /// </summary>
    [System.Serializable]
    public class MageFocusBoost : CharacterModifier
    {
        [SerializeField] private int bonusFocusStacks = 1;
        public int BonusFocusStacks => bonusFocusStacks;

        public override ModifierCategory Category => ModifierCategory.Signature;
        public override string ModifierName => "깊은 집중";
        public override string Description => $"안전한 턴 종료 시 집중 스택 +{bonusFocusStacks}";

        // 정신 집중(FocusPassive)을 가진 캐릭터에게만 제시/장착 가능
        public override bool CanApplyTo(Character character)
        {
            var passives = character?.Stats?.PassiveInstances;
            if (passives == null) return false;
            foreach (var p in passives)
                if (p is FocusPassive) return true;
            return false;
        }
    }
}
