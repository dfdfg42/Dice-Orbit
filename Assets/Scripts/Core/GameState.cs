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
        Recruit,            // 동료 영입 (New)
        Reward,             // 보상 획득 (New)
        Victory,            // 승리
        GameOver            // 패배
    }
}
