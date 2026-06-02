using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Data.Passives;

namespace DiceOrbit.Data.Modifiers.Rogue
{
    /// <summary>
    /// [도적 시그니처] 자리잡기(PositioningPassive) 계수 +N% — 이동 1칸당 피해 증가율을 끌어올린다.
    /// 패시브가 유효 계수를 계산할 때 이 보너스를 합산한다(풀 방식).
    /// </summary>
    [System.Serializable]
    public class RoguePositioningBoost : CharacterModifier
    {
        [SerializeField] private float bonusPercentPerTile = 10f;
        public float BonusPercentPerTile => bonusPercentPerTile;

        public override ModifierCategory Category => ModifierCategory.Signature;
        public override string ModifierName => "민첩한 발놀림";
        public override string Description => $"이동 1칸당 피해 계수 +{bonusPercentPerTile:0.#}%";

        // 자리잡기(PositioningPassive)를 가진 캐릭터에게만 제시/장착 가능
        public override bool CanApplyTo(Character character)
        {
            var passives = character?.Stats?.PassiveInstances;
            if (passives == null) return false;
            foreach (var p in passives)
                if (p is PositioningPassive) return true;
            return false;
        }
    }
}
