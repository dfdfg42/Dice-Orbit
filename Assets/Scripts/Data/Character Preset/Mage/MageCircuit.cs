using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Zones;
using UnityEngine;

namespace DiceOrbit.Data.Passives
{
    /// <summary>
    /// 마력 회로 구역 계산 (2026-08-28). 마법사의 공격 가능 구역 = 자기 구역 + 살아 있는 다른 아군들의 구역.
    /// 같은 구역에 아군이 몰려 있어도 구역 하나로만 센다. 저장하지 않고 질의 시점에 계산한다 —
    /// 아군 사망·이동을 즉시 반영하기 위해서다 (구역 소유권과 같은 원칙).
    /// 액티브(연쇄 번개·궤도 붕괴)·패시브(기본공격 타게팅)가 공유한다.
    /// </summary>
    public static class MageCircuit
    {
        /// <summary>연결된 구역 번호들 (중복 없음, 순서 보장 없음). 마법사가 구역 밖이면 빈 목록.</summary>
        public static List<int> GetCircuitZones(Character mage)
        {
            var result = new List<int>();
            var zones = CombatZoneManager.Instance;
            var party = PartyManager.Instance;
            if (zones == null || mage == null) return result;

            int myZone = zones.GetZoneOf(mage);
            if (myZone < 0) return result;
            result.Add(myZone);

            if (party == null) return result;
            foreach (var ally in party.GetAliveCharacters())
            {
                if (ally == null || ally == mage) continue;
                int zone = zones.GetZoneOf(ally);
                if (zone >= 0 && !result.Contains(zone)) result.Add(zone);
            }
            return result;
        }

        /// <summary>원형 구역 거리 (0~n/2).</summary>
        public static int CircularDistance(int a, int b, int zoneCount)
        {
            int n = Mathf.Max(1, zoneCount);
            int diff = Mathf.Abs(((a - b) % n + n) % n);
            return Mathf.Min(diff, n - diff);
        }

        /// <summary>
        /// 회로 구역들의 몬스터(주인)를 마법사 구역에서 가까운 순으로 최대 maxCount명.
        /// maxCount가 0 이하면 전원.
        /// </summary>
        public static List<Unit> OwnersInCircuit(Character mage, int maxCount)
        {
            var result = new List<Unit>();
            var zones = CombatZoneManager.Instance;
            if (zones == null || mage == null) return result;

            int myZone = zones.GetZoneOf(mage);
            if (myZone < 0) return result;

            var circuit = GetCircuitZones(mage);
            circuit.Sort((a, b) =>
                CircularDistance(a, myZone, zones.ZoneCount).CompareTo(
                    CircularDistance(b, myZone, zones.ZoneCount)));

            foreach (var zone in circuit)
            {
                var owner = zones.GetOwner(zone);
                if (owner == null || result.Contains(owner)) continue;
                result.Add(owner);
                if (maxCount > 0 && result.Count >= maxCount) break;
            }
            return result;
        }
    }
}
