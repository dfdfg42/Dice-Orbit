using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Zones;
using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Data.Passives
{
    /// <summary>
    /// [수호] 전사와 같은 구역에 선 아군(자신 포함)이 받는 피해를 줄인다.
    /// 전사의 자리가 곧 '안전지대'가 되어, 도적 협공처럼 뭉쳐야 이득인 패시브와 맞물린다.
    /// </summary>
    [System.Serializable]
    public class WarriorGuardPassive : CharacterPassiveSkill, IPassiveRangeProvider
    {
        [Header("Designer Tuning")]
        [Tooltip("같은 구역 아군(자신 포함)이 받는 피해 감소율(%). 예: 30은 -30%")]
        [SerializeField] private float damageReductionPercent = 30f;

        public override int Priority => 100;

        public override string GetDynamicDescription()
            => $"전사와 같은 구역에 있는 아군이 받는 피해가 {damageReductionPercent:0.#}% 감소합니다.";

        /// <summary>패시브 영향 범위 = 전사가 선 구역의 타일들. 조회 시 범위 표시에 쓰인다.</summary>
        public IReadOnlyList<TileData> GetRangeTiles()
        {
            var zones = CombatZoneManager.Instance;
            if (zones == null || !(owner is Character guard)) return new List<TileData>();
            return zones.GetTilesInZone(zones.GetZoneOf(guard));
        }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (!(owner is Character guard)) return;

            // 아군이 맞을 때만 개입한다 (전사 자신의 공격에는 관여하지 않는다).
            if (!(context.Target is Character victim)) return;

            var zones = CombatZoneManager.Instance;
            if (zones == null) return;

            int guardZone = zones.GetZoneOf(guard);
            if (guardZone < 0 || zones.GetZoneOf(victim) != guardZone) return;

            context.OutputValue *= Mathf.Max(0f, 1f - damageReductionPercent / 100f);
            if (!context.IsSimulation) Notify();
        }
    }
}
