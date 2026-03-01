using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using UnityEngine;
using static Unity.VisualScripting.Member;
using static UnityEngine.GraphicsBuffer;
using static UnityEngine.Rendering.DebugUI;

namespace DiceOrbit.Data.Tile
{
    public class BoneTile : TileAttribute
    {
        public BoneTile(TileAttributeType type, int value, int duration, bool isStackable = false) : base(type, value, duration, isStackable)
        {

        }
        public override void OnTraverse(Core.Character character)
        {
            Activate();
        }

        public override void OnEndTurn(Core.Character character)
        {
            Activate();
        }

        public void Activate()
        {
            var monsters=CombatManager.Instance?.ActiveMonsters;
            foreach(var monster in monsters)
            {
                monster.Stats.TempArmor += Value;
            }
        }

        public override string GetDescription()
        {
            string durationText = Duration < 0 ? "영구" : $"{Duration}턴";
            return $"지나가거나 턴 종료 시 몬스터에게 방어도 +{Value}, 지속 {durationText}";
        }
    }
}

