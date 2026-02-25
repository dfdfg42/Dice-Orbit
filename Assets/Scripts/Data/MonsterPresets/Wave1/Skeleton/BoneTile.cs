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
    }
}

