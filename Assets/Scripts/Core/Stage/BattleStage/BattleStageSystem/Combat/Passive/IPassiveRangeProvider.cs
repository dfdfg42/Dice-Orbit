using System.Collections.Generic;
using DiceOrbit.Data;

namespace DiceOrbit.Data.Passives
{
    /// <summary>
    /// 타일 범위를 갖는 패시브가 구현하는 인터페이스.
    /// PassiveRangeIndicator가 캐릭터 조회 시 이 타일들에 모서리 브래킷을 표시한다.
    /// 예: 전사 전우애(인접 좌우 타일). 범위가 없는 패시브는 구현하지 않는다.
    /// </summary>
    public interface IPassiveRangeProvider
    {
        /// <summary>현재 시점에 패시브가 영향을 주는 타일 목록 (없으면 빈 목록).</summary>
        IReadOnlyList<TileData> GetRangeTiles();
    }
}
