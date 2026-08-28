using System.Collections.Generic;

namespace DiceOrbit.Data.Passives
{
    /// <summary>
    /// 공격 가능 '구역 집합'을 제공하는 패시브 (마력 회로 등).
    /// IZoneReachProvider(자기 구역에서 거리 N까지)와 달리 임의의 구역 집합을 돌려준다 —
    /// AutoAttackSystem 기본공격과 강화 공격이 이 집합 안에서 표적을 찾는다.
    /// </summary>
    public interface IAttackZoneProvider
    {
        /// <summary>이 캐릭터가 공격할 수 있는 구역 번호들 (중복 없음). 비어 있으면 공격 불가.</summary>
        List<int> GetAttackableZones(Core.Character source);
    }
}
