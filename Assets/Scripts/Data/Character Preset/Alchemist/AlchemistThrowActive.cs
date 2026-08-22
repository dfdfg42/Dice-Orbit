using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Zones;
using DiceOrbit.Data.Skills;
using UnityEngine;

namespace DiceOrbit.Data.CharacterActives
{
    /// <summary>
    /// [살포] 짝수 턴에 시약을 넓게 뿌려 자기 구역과 양옆 구역의 몬스터를 한꺼번에 적신다.
    /// 한 대상당 위력은 낮지만 닿는 범위가 가장 넓다 — 전사 돌파가 '멀리 달린 만큼'이라면
    /// 이쪽은 '서 있는 자리 주변'이라 조건이 겹치지 않는다.
    /// </summary>
    [System.Serializable]
    public class AlchemistThrowActive : CharacterActiveSkill
    {
        [Header("Designer Tuning")]
        [Tooltip("피해 = 주사위 눈 x 배율 (대상마다 각각). 게이트가 짝수(평균 4) + 범위가 넓어 배율은 낮게.")]
        [SerializeField] private float multiplier = 1.2f;
        [Tooltip("양옆으로 몇 구역까지 퍼질지")]
        [SerializeField] private int spreadZones = 1;

        public override int CalculateRawDamage(Character source, ActiveSkillSlot ability, int diceValue)
        {
            return Mathf.Max(1, Mathf.RoundToInt(diceValue * Mathf.Max(0.1f, multiplier)));
        }

        public override string BuildPreview(Character source, ActiveSkillSlot ability, int diceValue)
            => $"예상 피해: 주변 구역마다 {CalculateRawDamage(source, ability, diceValue)}";

        public override string GetDynamicDescription()
            => $"자기 구역과 양옆 {Mathf.Max(0, spreadZones)}구역의 몬스터에게 주사위 눈 x{multiplier:0.##} 피해";

        public override string GetTargetLabel() => $"자기 구역 + 양옆 {Mathf.Max(0, spreadZones)}구역";

        /// <summary>자기 구역 + 양옆 spreadZones구역의 주인 전원.</summary>
        public override List<Unit> ResolveTargets(Character source, IReadOnlyList<int> passedZones)
        {
            var result = new List<Unit>();
            var zones = CombatZoneManager.Instance;
            if (zones == null || source == null) return result;

            int myZone = zones.GetZoneOf(source);
            if (myZone < 0) return result;

            int n = zones.ZoneCount;
            int limit = Mathf.Clamp(spreadZones, 0, n / 2);
            for (int d = 0; d <= limit; d++)
            {
                var forward = zones.GetOwner((myZone + d) % n);
                if (forward != null && !result.Contains(forward)) result.Add(forward);

                var backward = zones.GetOwner((myZone - d + n) % n);
                if (backward != null && !result.Contains(backward)) result.Add(backward);
            }
            return result;
        }
    }
}
