using System.Linq;
using UnityEngine;
using DiceOrbit.Data.Modifiers;

namespace DiceOrbit.Core.Run
{
    /// <summary>보상 굴림 설정 — RewardUI 인스펙터 값이 그대로 넘어온다.</summary>
    public struct RewardRollSettings
    {
        public int GoldPerReward;
        public int ModifierChoiceCount;
        public float PotionDropChance;
    }

    /// <summary>
    /// 전투 보상을 매니저들에서 한 번 굴려 <see cref="RewardBundle"/>로 묶는다 (보상 리워크 2026-10-03).
    /// 규칙 자체는 이전과 같다 — 골드(+유물 보너스), 엘리트 유물, 포션 확률 드랍, 특수 주사위 드로우.
    /// 달라진 것은 모디파이어: 캐릭터를 고른 뒤 뽑던 것을, 파티 공용 3택으로 진입 시 한 번만 뽑는다 (재굴림 불가).
    /// </summary>
    public static class RewardRoller
    {
        public static RewardBundle Roll(RewardRollSettings settings)
        {
            var bundle = new RewardBundle();

            bundle.Gold = settings.GoldPerReward + (ArtifactManager.Instance?.BattleGoldBonus ?? 0);

            var node = RunManager.Instance?.CurrentNode;
            if (node != null && node.Type == MapNodeType.Elite)
                bundle.Artifact = ArtifactManager.EnsureInstance().GetShopOfferings(1).FirstOrDefault();

            if (Random.value < settings.PotionDropChance)
                bundle.Potion = PotionManager.EnsureInstance().GetShopOfferings(1).FirstOrDefault();

            bundle.ModifierOffers = ModifierRegistry.GetRandomChoicesForParty(
                PartyManager.Instance?.Party, settings.ModifierChoiceCount);

            bundle.NewDie = DiceDeckManager.EnsureInstance()?.DrawRandomSpecial();

            return bundle;
        }
    }
}
