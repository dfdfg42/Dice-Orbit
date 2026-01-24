using DiceOrbit.Core;
using UnityEngine;
namespace DiceOrbit.Data.Tile
{
    public class LevelUpTraverse : IOnTraverse
    {
        void IOnTraverse.OnTraverse(Character character)
        {
            character.Stats.LevelUp();
        }
    }
}

