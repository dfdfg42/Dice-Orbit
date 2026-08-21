namespace DiceOrbit.Data.Passives
{
    /// <summary>
    /// 자동 기본공격의 구역 사거리를 넓히는 패시브가 구현한다.
    /// 0이면 자기 구역만, 1이면 인접 구역까지 표적을 찾는다 (표적 수는 늘지 않고 탐색 범위만 넓어진다).
    /// AutoAttackSystem이 캐릭터의 패시브를 훑어 가장 큰 값을 사거리로 쓴다.
    /// </summary>
    public interface IZoneReachProvider
    {
        int ExtraZoneReach { get; }
    }
}
