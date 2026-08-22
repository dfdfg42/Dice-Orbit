using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Zones;
using DiceOrbit.Data.Skills;
using UnityEngine;

namespace DiceOrbit.Data.CharacterActives
{
    /// <summary>
    /// [연쇄 마탄] 홀수 턴에 마탄이 갈라져 사거리 안의 몬스터 둘을 함께 친다.
    /// 마법사만 중립지대에서도 표적을 찾으므로(원거리 패시브) 이 강화 공격도 그 사거리를 그대로 쓴다.
    /// </summary>
    [System.Serializable]
    public class MageEnergyBallActive : CharacterActiveSkill
    {
        [Header("Designer Tuning")]
        [Tooltip("피해 = 주사위 눈 x 배율 (대상마다 각각). 게이트가 홀수(평균 3)라 중간 배율.")]
        [SerializeField] private float multiplier = 2f;
        [Tooltip("동시에 칠 최대 몬스터 수")]
        [SerializeField] private int maxTargets = 2;

        public override int CalculateRawDamage(Character source, ActiveSkillSlot ability, int diceValue)
        {
            return Mathf.Max(1, Mathf.RoundToInt(diceValue * Mathf.Max(0.1f, multiplier)));
        }

        public override string BuildPreview(Character source, ActiveSkillSlot ability, int diceValue)
            => $"예상 피해: 최대 {Mathf.Max(1, maxTargets)}체에게 각 {CalculateRawDamage(source, ability, diceValue)}";

        public override string GetDynamicDescription()
            => $"사거리 안 몬스터 최대 {Mathf.Max(1, maxTargets)}체에게 주사위 눈 x{multiplier:0.##} 피해";

        public override string GetTargetLabel() => $"사거리 안 최대 {Mathf.Max(1, maxTargets)}체";

        /// <summary>자기 구역부터 가까운 순으로 사거리 안의 주인들을 모은다.</summary>
        public override List<Unit> ResolveTargets(Character source, IReadOnlyList<int> passedZones)
        {
            var result = new List<Unit>();
            var zones = CombatZoneManager.Instance;
            if (zones == null || source == null) return result;

            int myZone = zones.GetZoneOf(source);
            if (myZone < 0) return result;

            int reach = ResolveReach(source);
            int n = zones.ZoneCount;
            int limit = Mathf.Clamp(reach, 0, n / 2);
            int cap = Mathf.Max(1, maxTargets);

            for (int d = 0; d <= limit && result.Count < cap; d++)
            {
                var forward = zones.GetOwner((myZone + d) % n);
                if (forward != null && !result.Contains(forward)) result.Add(forward);
                if (result.Count >= cap) break;

                var backward = zones.GetOwner((myZone - d + n) % n);
                if (backward != null && !result.Contains(backward)) result.Add(backward);
            }
            return result;
        }

        /// <summary>원거리 패시브가 주는 구역 사거리. 없으면 0(자기 구역만).</summary>
        private static int ResolveReach(Character source)
        {
            var passives = source != null && source.Stats != null ? source.Stats.PassiveInstances : null;
            if (passives == null) return 0;

            int reach = 0;
            foreach (var p in passives)
                if (p is Passives.IZoneReachProvider provider)
                    reach = Mathf.Max(reach, provider.ExtraZoneReach);
            return reach;
        }
    }
}
