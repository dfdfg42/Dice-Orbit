using System.Collections.Generic;

namespace DiceOrbit.Data.Passives
{
    /// <summary>
    /// 구역 단위로 작동하는 패시브가 구현한다 (2026-10-04 — 타일 단위 IPassiveRangeProvider를 대체).
    /// 캐릭터 조회 시 PassiveZoneIndicator가 이 구역들을 통째로 감싸는 테두리를 그리고,
    /// 정보 패널이 패시브 제목 옆에 지금의 효과를 적는다.
    /// 저장하지 않고 질의 시점에 계산한다 — 아군 이동·사망이 조회 중에도 바로 반영된다.
    /// </summary>
    public interface IPassiveZoneProvider
    {
        /// <summary>
        /// 지금 이 패시브가 걸려 있는 구역 번호들을 zones에 더한다. 소유자가 구역 밖이면 아무것도 더하지 않는다.
        /// </summary>
        void CollectPassiveZones(List<int> zones);

        /// <summary>지금의 효과 요약 — 정보 패널에 적을 한 줄과 발동 여부.</summary>
        PassiveZoneStatus GetPassiveZoneStatus();
    }

    /// <summary>구역 패시브의 현재 상태. Effect 예: "받는 피해 -20%" (패시브 이름은 넣지 않는다).</summary>
    public readonly struct PassiveZoneStatus
    {
        public readonly string Effect;
        /// <summary>지금 효과가 실제로 걸려 있는가. false면(같은 구역 아군 없음 등) 흐리게 표시한다.</summary>
        public readonly bool Active;

        public PassiveZoneStatus(string effect, bool active)
        {
            Effect = effect;
            Active = active;
        }
    }
}
