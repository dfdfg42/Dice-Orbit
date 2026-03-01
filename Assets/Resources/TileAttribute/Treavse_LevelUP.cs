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
            character.Stats.LevelUp();
        }

        public override string GetDescription()
        {
            return "지나가면 레벨이 상승합니다.";
        }
    }
}
