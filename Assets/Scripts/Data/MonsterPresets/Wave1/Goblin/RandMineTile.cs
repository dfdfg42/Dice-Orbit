using DiceOrbit.Core.Pipeline;
using UnityEngine;
using static Unity.VisualScripting.Member;
using static UnityEngine.GraphicsBuffer;
using static UnityEngine.Rendering.DebugUI;

namespace DiceOrbit.Data.Tile
{
    public class RandMineTile : TileAttribute
    {
        public RandMineTile(TileAttributeType type, int value, int duration, bool isStackable = false) : base(type, value, duration, isStackable)
        {

        }

        public override void OnTraverse(Core.Character character)
        {
            Explosion(character);
        }

        public override void OnEndTurn(Core.Character character)
        {
            Explosion(character);
        }

        public void Explosion(Core.Character target)
        {
            if (target == null || !target.IsAlive) return;

            var context = new Core.Pipeline.CombatContext(
                null,
                target,
                new Core.Pipeline.CombatAction("Mine Explosion", Core.Pipeline.ActionType.Attack, Value)
            );
            Core.Pipeline.CombatPipeline.Instance?.Process(context);
            Owner.RemoveAttribute(this);
        }
    }
}