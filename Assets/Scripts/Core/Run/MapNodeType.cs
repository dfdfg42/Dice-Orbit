namespace DiceOrbit.Core.Run
{
    /// <summary>노드맵의 노드 종류. (스펙 §1 — 모집/공방 노드는 없음)</summary>
    public enum MapNodeType
    {
        Battle,   // 일반 전투 → 골드 + 모디파이어 3택1
        Elite,    // 강화 전투 → 유물 드랍 (유물 시스템은 후속)
        Shop,     // 상점 (v1 스텁)
        Rest,     // 휴식 — 파티 회복
        Event,    // 주사위 이벤트 (v1 스텁)
        Boss,     // 막 최종전
    }
}
