using UnityEngine;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Core.Tile.TileAttribute
{
    public class TileAttribute : ICombatReactor
    {
        public int Priority => 5;

        public void OnReact(CombatTrigger trigger, CombatContext context)
        {
            
        }

    }
}