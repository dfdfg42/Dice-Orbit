using System.Collections.Generic;
using DiceOrbit.Data;
using DiceOrbit.Data.Modifiers;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// 한 전투의 보상 묶음 — 보상 화면 진입 시 <see cref="RewardRoller"/>가 한 번만 굴린다 (보상 리워크 2026-10-03).
    /// 데이터만 담는다. 수령·선택 규칙은 RewardUI/RewardFlow.
    /// </summary>
    public sealed class RewardBundle
    {
        /// <summary>자동 수령 골드 (기본 + 유물 보너스).</summary>
        public int Gold;

        /// <summary>엘리트 클리어 유물. 없으면 null.</summary>
        public ArtifactData Artifact;

        /// <summary>드랍된 포션. 없으면 null.</summary>
        public Potion Potion;

        /// <summary>공용 모디파이어 제시 (생존 파티원 중 1명 이상이 장착 가능한 종류, 중복 없음). 비면 강화 박자가 없다.</summary>
        public List<CharacterModifier> ModifierOffers = new List<CharacterModifier>();

        /// <summary>새 특수 주사위. 없으면 null — 주사위 박자가 없다.</summary>
        public DieDefinitionSO NewDie;
    }
}
