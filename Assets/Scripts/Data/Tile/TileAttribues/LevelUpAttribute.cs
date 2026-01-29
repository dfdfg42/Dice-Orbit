using DiceOrbit.Core;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;
namespace DiceOrbit.Data.Tile
{
    public class LevelUpTraverse : IOnTraverse
    {
        void IOnTraverse.OnTraverse(Character character)
        {
            character.Stats.LevelUp();
            // UI Removed in Refactor 2.0
            Debug.Log($"[LevelUpTraverse] {character.Stats.CharacterName} leveled up!");
        }
    }
}

