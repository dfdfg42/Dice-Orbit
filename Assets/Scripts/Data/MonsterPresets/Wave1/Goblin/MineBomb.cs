using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Tile;
using UnityEngine;

namespace DiceOrbit.Data.MonsterPresets.Wave1.Goblin
{
    [CreateAssetMenu(fileName = "MineBomb", menuName = "DiceOrbit/Skill Effects/Mine Bomb")]
    public class MineBomb : SkillEffectBase
    {
        [SerializeField] private int damage = 20;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles)
        {
            var orbitManager = GameManager.Instance?.GetOrbitManager();
            var partyManager = PartyManager.Instance;
            if (orbitManager == null || partyManager == null) return;

            // Find all tiles that currently have a mine attribute.
            var mineTiles = orbitManager.Tiles
                .Where(tile => tile != null && tile.GetAttributes().Any(attr => attr != null && attr.Type == TileAttributeType.RandMine))
                .ToList();

            if (mineTiles.Count == 0) return;

            // Affected area = mine tile + one tile on each side.
            var affectedTiles = new HashSet<TileData>();
            foreach (var mineTile in mineTiles)
            {
                affectedTiles.Add(mineTile);
                if (mineTile.PreviousTile != null) affectedTiles.Add(mineTile.PreviousTile);
                if (mineTile.NextTile != null) affectedTiles.Add(mineTile.NextTile);
            }

            var aliveCharacters = partyManager.GetAliveCharacters();
            foreach (var character in aliveCharacters)
            {
                if (character == null || !character.IsAlive) continue;
                if (character.CurrentTile == null || !affectedTiles.Contains(character.CurrentTile)) continue;

                var context = new CombatContext(
                    source,
                    character,
                    new CombatAction("Mine Bomb", ActionType.Attack, damage)
                );
                CombatPipeline.Instance?.Process(context);
            }

            // Remove only mine attributes from mine tiles after detonation.
            foreach (var mineTile in mineTiles)
            {
                var mines = mineTile.GetAttributes()
                    .Where(attr => attr != null && attr.Type == TileAttributeType.RandMine)
                    .ToList();

                foreach (var mine in mines)
                {
                    mineTile.RemoveAttribute(mine);
                }
            }
        }
    }
}
