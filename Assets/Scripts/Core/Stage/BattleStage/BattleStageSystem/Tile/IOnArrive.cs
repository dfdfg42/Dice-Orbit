namespace DiceOrbit.Data.Tile
{
    public interface IOnArrive
    {
        string TooltipDescription { get; }

        /// <summary>
        /// 타일에 도착했을 때(도착점이 그 타일일 때) 호출
        /// </summary>
        void OnArrive(Core.Character character);
    }
}
