using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Data.Passives;

namespace DiceOrbit.Data.Modifiers.Alchemist
{
    /// <summary>
    /// [연금술사 시그니처] 웨이브 시작 시 설치되는 시약 타일 수 +N.
    /// ReagentPrepPassive가 유효 설치 수를 계산할 때 이 보너스를 합산해서 읽는다(풀 방식).
    /// </summary>
    [System.Serializable]
    public class AlchemistExtraReagent : CharacterModifier
    {
        [SerializeField] private int bonusReagentTiles = 1;
        public int BonusReagentTiles => bonusReagentTiles;

        public override ModifierCategory Category => ModifierCategory.Signature;
        public override string ModifierName => "시약 과잉";
        public override string Description => $"전투 시작 시 시약 타일을 {bonusReagentTiles}개 더 설치합니다.";

        // 시약 준비(ReagentPrepPassive)를 가진 캐릭터에게만 제시/장착 가능
        public override bool CanApplyTo(Character character)
        {
            var passives = character?.Stats?.PassiveInstances;
            if (passives == null) return false;
            foreach (var p in passives)
                if (p is ReagentPrepPassive) return true;
            return false;
        }
    }
}
