using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Zones;
using DiceOrbit.Data.Skills;
using UnityEngine;

namespace DiceOrbit.Data.CharacterActives
{
    /// <summary>
    /// [돌파] 높은 눈으로 멀리 달리는 턴에, 지나쳐 온 모든 구역의 주인을 함께 벤다.
    /// 이동량이 곧 위력이 되는 경로형이라 높낮이 게이트(4 이상)와 한 몸이다(불변식 5).
    /// </summary>
    [System.Serializable]
    public class WarriorGreatswordActive : CharacterActiveSkill
    {
        [Header("Designer Tuning")]
        [Tooltip("피해 = 주사위 눈 x 배율 (지나친 구역마다 각각). 게이트가 높은 눈(4~6)이라 배율은 낮게 잡는다.")]
        [SerializeField] private float multiplier = 1.4f;

        public override Core.Pipeline.CharacterModfierContext GenerateContext(Character source, ActiveSkillSlot ability)
        {
            return new Core.Pipeline.WarriorGreatswordModifiedContext(source, this);
        }

        public override int CalculateRawDamage(Character source, ActiveSkillSlot ability, int diceValue)
        {
            return Mathf.Max(1, Mathf.RoundToInt(diceValue * Mathf.Max(0.1f, multiplier)));
        }

        public override string BuildPreview(Character source, ActiveSkillSlot ability, int diceValue)
            => $"예상 피해: 지나친 구역마다 {CalculateRawDamage(source, ability, diceValue)}";

        public override string GetDynamicDescription()
            => $"지나쳐 온 모든 구역의 몬스터에게 주사위 눈 x{multiplier:0.##} 피해";

        public override string GetTargetLabel() => "지나친 구역 전부";

        /// <summary>이동으로 지나온 구역들의 주인 전원.</summary>
        public override List<Unit> ResolveTargets(Character source, IReadOnlyList<int> passedZones)
        {
            var result = new List<Unit>();
            var zones = CombatZoneManager.Instance;
            if (zones == null || source == null) return result;

            if (passedZones == null || passedZones.Count == 0)
                return base.ResolveTargets(source, passedZones);

            foreach (var zone in passedZones)
            {
                var owner = zones.GetOwner(zone);
                if (owner != null && !result.Contains(owner)) result.Add(owner);
            }
            return result;
        }
    }
}
