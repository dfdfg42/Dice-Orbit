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
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.TriggerLevelUp(character.Stats);
            }
            else
            {
                Debug.LogWarning("GameFlowManager instance not found, LevelUp UI will not show.");
            }
        }
    }
}

