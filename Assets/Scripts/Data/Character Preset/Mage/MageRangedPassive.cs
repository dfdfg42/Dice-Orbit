using DiceOrbit.Core;
using UnityEngine;

namespace DiceOrbit.Data.Passives
{
    /// <summary>
    /// [원거리] 자기 구역에 몬스터가 없으면 인접 구역의 몬스터를 대신 때린다.
    /// 마법사만 중립지대(주인 없는 구역)에 숨어서도 계속 일할 수 있게 하는 패시브 —
    /// 안전과 딜을 맞바꿔야 하는 다른 캐릭터와 달리 둘 다 가진다.
    /// 표적 수는 늘지 않는다(사거리만 넓어진다) — 근접 캐릭터를 압도하지 않게.
    /// </summary>
    [System.Serializable]
    public class MageRangedPassive : CharacterPassiveSkill, IZoneReachProvider
    {
        [Header("Designer Tuning")]
        [Tooltip("표적을 찾을 때 넓힐 구역 수. 1이면 인접 구역까지.")]
        [SerializeField] private int extraZoneReach = 1;

        public override int Priority => 50;

        public int ExtraZoneReach => Mathf.Max(0, extraZoneReach + GetReachBonus());

        public override string GetDynamicDescription()
            => $"자기 구역이 비어 있으면 {ExtraZoneReach}칸 이내 구역의 몬스터를 공격";

        /// <summary>장착된 시그니처 모디파이어가 더해주는 추가 사거리 합산.</summary>
        private int GetReachBonus()
        {
            if (!(owner is Character ch)) return 0;
            var mods = ch.Stats?.Modifiers?.Modifiers;
            if (mods == null) return 0;

            int bonus = 0;
            foreach (var m in mods)
                if (m is Modifiers.Mage.MageFocusBoost f)
                    bonus += f.BonusZoneReach;
            return bonus;
        }
    }
}
