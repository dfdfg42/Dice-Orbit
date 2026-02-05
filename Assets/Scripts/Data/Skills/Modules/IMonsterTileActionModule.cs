using DiceOrbit.Core;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.Skills.Modules
{
    public interface IMonsterTileActionModule
    {
        TileData[] GetPreviewTiles(Monster source);
        void Execute(Monster source, int diceValue, TileData[] tiles);
    }
}
