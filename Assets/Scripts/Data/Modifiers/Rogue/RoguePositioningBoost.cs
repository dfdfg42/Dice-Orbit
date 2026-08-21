using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Data.Passives;

namespace DiceOrbit.Data.Modifiers.Rogue
{
    /// <summary>
    /// [도적 시그니처] 협공(RogueFlankPassive) 계수 +N%.
    /// 패시브가 유효 계수를 계산할 때 이 보너스를 합산한다(풀 방식).
    /// 클래스명은 세이브 ID(GetType().Name)라 유지한다 — 저장된 런의 복원이 깨지지 않도록.
    /// </summary>
    [System.Serializable]
    public class RoguePositioningBoost : CharacterModifier
    {
        [SerializeField] private float bonusPercent = 10f;
        public float BonusPercent => bonusPercent;

        public override ModifierCategory Category => ModifierCategory.Signature;
        public override string ModifierName => "급소 감각";
        public override string Description => $"협공 피해 계수 +{bonusPercent:0.#}%";

        // 협공(RogueFlankPassive)을 가진 캐릭터에게만 제시/장착 가능
        public override bool CanApplyTo(Character character)
        {
            var passives = character?.Stats?.PassiveInstances;
            if (passives == null) return false;
            foreach (var p in passives)
                if (p is RogueFlankPassive) return true;
            return false;
        }
    }
}
