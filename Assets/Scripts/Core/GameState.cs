namespace DiceOrbit.Core
{
    /// <summary>
    /// 게임 상태
    /// </summary>
    public enum GameState
    {
        MainMenu,           // 메인 메뉴
        CharacterSelection, // 캐릭터 선택
        Combat,             // 전투 중
        Shop,               // 상점
        Victory,            // 승리
        GameOver,           // 패배
        Recruit,            // 모집
        Reward,             // 보상
        Map,                // 노드맵 — 다음 노드 선택 (2026-07-07 런 구조 개편)
        Event               // 이벤트 노드 — 주사위 도박
        // (LevelUp 상태는 유산으로 철거 — 성장은 웨이브 클리어 보상 모디파이어로 일원화)
    }
}
