using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Tile
{
    public class EndTurnDamageTrapArrive : IOnArrive
    {
        private readonly TileData tile;
        private readonly TileAttribute attribute;
        private readonly Monster sourceMonster;
        private readonly int damage;
        private readonly Color highlightColor;

        private Character occupant;
        private bool subscribed;

        public EndTurnDamageTrapArrive(TileData tile, TileAttribute attribute, Monster sourceMonster, int damage, Color highlightColor)
        {
            this.tile = tile;
            this.attribute = attribute;
            this.sourceMonster = sourceMonster;
            this.damage = damage;
            this.highlightColor = highlightColor;
            Subscribe();
        }

        public void OnArrive(Character character)
        {
            if (character == null) return;
            occupant = character;
            tile?.Highlight(highlightColor);
        }

        private void Subscribe()
        {
            if (subscribed) return;
            if (CombatManager.Instance == null) return;

            CombatManager.Instance.OnMonsterTurnStart += OnPlayerTurnEnd;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.OnMonsterTurnStart -= OnPlayerTurnEnd;
            }
            subscribed = false;
        }

        private void OnPlayerTurnEnd()
        {
            if (occupant != null && occupant.IsAlive && occupant.CurrentTile == tile)
            {
                if (CombatPipeline.Instance != null)
                {
                    var action = new CombatAction("Trap", DiceOrbit.Core.Pipeline.ActionType.Attack, damage);
                    action.AddTag("Trap");
                    var context = new CombatContext(sourceMonster, occupant, action);
                    CombatPipeline.Instance.Process(context);
                }
                else
                {
                    occupant.TakeDamage(damage);
                }

                Debug.Log($"[Trap] {occupant.Stats.CharacterName} took {damage} damage on end turn.");
            }

            RemoveTrap();
        }

        private void RemoveTrap()
        {
            Unsubscribe();

            if (attribute != null)
            {
                attribute.RemoveArrive(this);
                if (attribute.IsEmpty)
                {
                    Object.Destroy(attribute);
                }
            }

            tile?.ClearHighlight();
        }
    }
}
