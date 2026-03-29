using UnityEngine;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core;

namespace DiceOrbit.Data.Tile
{
  
    public class treavse_LevelUP : TileAttribute
    {
        public treavse_LevelUP(TileAttributeType type, int value, int duration, bool isStackable = false) : base(type, value, duration, isStackable)
        {
            
        }

        public override void OnTraverse(Character character)
        {
            // 상태 전환 일관성을 위해 레벨업은 GameFlow를 통해 처리합니다.
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.TriggerLevelUp(character);
                return;
            }

            // GameFlowManager가 없는 씬에서는 직접 레벨업 처리합니다.
            character.LevelUpCharacter();
        }

        public override string GetDescription()
        {
            return "지나가면 레벨이 상승합니다.";
        }
    }
}
