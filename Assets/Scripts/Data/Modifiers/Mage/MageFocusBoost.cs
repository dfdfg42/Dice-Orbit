using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Data.Passives;

namespace DiceOrbit.Data.Modifiers.Mage
{
    /// <summary>
    /// [마법사 시그니처] 원거리(MageRangedPassive) 구역 사거리 +N.
    /// 패시브가 유효 사거리를 계산할 때 이 보너스를 합산한다(풀 방식).
    /// 클래스명은 세이브 ID(GetType().Name)라 유지한다 — 저장된 런의 복원이 깨지지 않도록.
    /// </summary>
    [System.Serializable]
    public class MageFocusBoost : CharacterModifier
    {
        [SerializeField] private int bonusZoneReach = 1;
        public int BonusZoneReach => bonusZoneReach;

        public override ModifierCategory Category => ModifierCategory.Signature;
        public override string ModifierName => "먼 시야";
        public override string Description => $"원거리 공격의 사거리가 {bonusZoneReach}구역 증가합니다.";

        // 원거리(MageRangedPassive)를 가진 캐릭터에게만 제시/장착 가능
        public override bool CanApplyTo(Character character)
        {
            var passives = character?.Stats?.PassiveInstances;
            if (passives == null) return false;
            foreach (var p in passives)
                if (p is MageRangedPassive) return true;
            return false;
        }
    }
}
