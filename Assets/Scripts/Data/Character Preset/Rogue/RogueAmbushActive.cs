using DiceOrbit.Core;
using DiceOrbit.Core.Zones;
using DiceOrbit.Data.Skills;
using UnityEngine;

namespace DiceOrbit.Data.CharacterActives
{
    /// <summary>
    /// [급습] 낮은 눈으로 짧게 파고든 턴에, 자기 구역의 주인에게 급소 일격을 넣는다.
    /// 제자리형이라 낮은 눈 게이트(3 이하)와 한 몸이며, 협공(같은 구역 아군)이면 더 깊이 파고든다.
    /// </summary>
    [System.Serializable]
    public class RogueAmbushActive : CharacterActiveSkill
    {
        [Header("Designer Tuning")]
        [Tooltip("피해 = 공격력 x 배율")]
        [SerializeField] private float multiplier = 2f;
        [Tooltip("같은 구역에 아군이 있을 때 곱해지는 추가 배율")]
        [SerializeField] private float flankMultiplier = 1.5f;

        public override int CalculateRawDamage(Character source, ActiveSkillSlot ability, int diceValue)
        {
            int attack = source != null && source.Stats != null ? source.Stats.Attack : 0;
            float mult = Mathf.Max(0.1f, multiplier) * (HasAllyInSameZone(source) ? Mathf.Max(1f, flankMultiplier) : 1f);
            return Mathf.Max(1, Mathf.RoundToInt(attack * mult));
        }

        public override string BuildPreview(Character source, ActiveSkillSlot ability, int diceValue)
            => $"예상 피해: {CalculateRawDamage(source, ability, diceValue)}" + (HasAllyInSameZone(source) ? " (협공)" : "");

        public override string GetDynamicDescription()
            => $"자기 구역 몬스터에게 공격력 x{multiplier:0.##} 피해 (협공 시 x{flankMultiplier:0.##} 추가)";

        private static bool HasAllyInSameZone(Character source)
        {
            var zones = CombatZoneManager.Instance;
            var party = PartyManager.Instance;
            if (zones == null || party == null || source == null) return false;

            int myZone = zones.GetZoneOf(source);
            if (myZone < 0) return false;

            foreach (var ally in party.GetAliveCharacters())
            {
                if (ally == null || ally == source) continue;
                if (zones.GetZoneOf(ally) == myZone) return true;
            }
            return false;
        }
    }
}
