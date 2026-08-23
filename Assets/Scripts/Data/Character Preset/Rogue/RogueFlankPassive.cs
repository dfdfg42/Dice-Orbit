using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Zones;
using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Data.Passives
{
    /// <summary>
    /// [협공] 같은 구역에 다른 아군이 있으면 도적의 공격이 급소를 노린다.
    /// 개성이 주사위 눈이 아니라 '누구 옆에 서느냐'에서 나오게 하는 패시브 —
    /// 구 자리잡기(이동 1칸당 피해 증가)가 되살리던 눈→딜 커플링을 대체한다.
    /// </summary>
    [System.Serializable]
    public class RogueFlankPassive : CharacterPassiveSkill, IPassiveRangeProvider
    {
        [Header("Designer Tuning")]
        [Tooltip("같은 구역에 아군이 있을 때 피해 증가율(%). 예: 100은 2배")]
        [SerializeField] private float bonusPercent = 100f;

        public override int Priority => 99;

        public override string GetDynamicDescription()
            => $"같은 구역에 다른 아군이 있으면 도적이 주는 피해가 {(bonusPercent + GetFlankBonus()):0.#}% 증가합니다.";

        /// <summary>패시브 영향 범위 = 도적이 선 구역의 타일들.</summary>
        public IReadOnlyList<TileData> GetRangeTiles()
        {
            var zones = CombatZoneManager.Instance;
            if (zones == null || !(owner is Character rogue)) return new List<TileData>();
            return zones.GetTilesInZone(zones.GetZoneOf(rogue));
        }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.SourceUnit != owner) return;
            if (!HasAllyInSameZone()) return;

            float percent = bonusPercent + GetFlankBonus();
            context.OutputValue *= 1f + percent / 100f;
            if (!context.IsSimulation) Notify();
        }

        private bool HasAllyInSameZone()
        {
            var zones = CombatZoneManager.Instance;
            var party = PartyManager.Instance;
            if (zones == null || party == null || !(owner is Character rogue)) return false;

            int myZone = zones.GetZoneOf(rogue);
            if (myZone < 0) return false;

            foreach (var ally in party.GetAliveCharacters())
            {
                if (ally == null || ally == rogue) continue;
                if (zones.GetZoneOf(ally) == myZone) return true;
            }
            return false;
        }

        /// <summary>장착된 시그니처 모디파이어가 더해주는 추가 협공 계수(%) 합산.</summary>
        private float GetFlankBonus()
        {
            if (!(owner is Character ch)) return 0f;
            var mods = ch.Stats?.Modifiers?.Modifiers;
            if (mods == null) return 0f;

            float bonus = 0f;
            foreach (var m in mods)
                if (m is Modifiers.Rogue.RoguePositioningBoost r)
                    bonus += r.BonusPercent;
            return bonus;
        }
    }
}
