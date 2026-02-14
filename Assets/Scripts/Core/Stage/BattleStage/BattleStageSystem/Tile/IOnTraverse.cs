namespace DiceOrbit.Data.Tile
{
    public interface IOnTraverse
    {
        string TooltipDescription { get; }

        /// <summary>
        /// 타일을 경유할 때 호출
        /// </summary>
        void OnTraverse(Core.Character character);
    }
}

