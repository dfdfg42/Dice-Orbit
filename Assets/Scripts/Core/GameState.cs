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
        LevelUp,            // 스킬 선택 (레벨업)
        Victory,            // 승리
        GameOver            // 패배
    }
}
