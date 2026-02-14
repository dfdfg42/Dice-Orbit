using DiceOrbit.Core;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;
namespace DiceOrbit.Data.Tile
{
    public class LevelUpTraverse : IOnTraverse
    {
        public string TooltipDescription => "이 타일을 지나가면 레벨업합니다.";

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

