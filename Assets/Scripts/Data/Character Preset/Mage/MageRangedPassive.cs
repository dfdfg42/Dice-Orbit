using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Zones;
using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Data.Passives
{
    /// <summary>
    /// [마력 회로] 마법사의 공격 가능 구역 = 자기 구역 + 살아 있는 다른 아군들의 구역 (2026-08-28 개편).
    /// 아군이 퍼질수록 회로가 넓어져, 셋이 서로 다른 구역에 서면 전장 전체가 사거리가 된다.
    /// 기본공격도 회로 안에서 가장 가까운 몬스터를 찾는다 (AutoAttackSystem이 IAttackZoneProvider를 읽음).
    /// 저장하지 않고 질의 시점에 계산 — 아군 사망·이동이 즉시 반영된다.
    /// 클래스명은 .asset SerializeReference 호환을 위해 유지한다 (구 [원거리]).
    /// </summary>
    [System.Serializable]
    public class MageRangedPassive : CharacterPassiveSkill, IAttackZoneProvider, IPassiveRangeProvider
    {
        public override int Priority => 50;

        public override string GetDynamicDescription()
            => "마법사는 자기 구역과 살아 있는 아군이 있는 모든 구역을 공격할 수 있습니다. 아군이 여러 구역에 흩어질수록 공격 범위가 넓어집니다.";

        /// <summary>공격 가능 구역 집합 = 마력 회로.</summary>
        public List<int> GetAttackableZones(Character source)
            => MageCircuit.GetCircuitZones(source != null ? source : owner as Character);

        /// <summary>패시브 영향 범위 표시 = 회로로 연결된 모든 구역의 타일들.</summary>
        public IReadOnlyList<TileData> GetRangeTiles()
        {
            var result = new List<TileData>();
            var zones = CombatZoneManager.Instance;
            if (zones == null || !(owner is Character mage)) return result;

            foreach (var zone in MageCircuit.GetCircuitZones(mage))
                result.AddRange(zones.GetTilesInZone(zone));
            return result;
        }
    }
}
