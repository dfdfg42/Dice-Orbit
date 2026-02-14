using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.Skills.Modules
{
    [CreateAssetMenu(fileName = "End Turn Trap Module", menuName = "Dice Orbit/Skills/Modules/End Turn Trap")]
    public class EndTurnTrapTileModule : SkillActionModule, IMonsterActionModule, IMonsterTileActionModule
    {
        [Header("Trap Settings")]
        [SerializeField] private int trapCount = 3;
        [SerializeField] private int trapDamage = 5;
        [SerializeField] private bool avoidLevelUpTile = true;
        [SerializeField] private Color highlightColor = new Color(0.2f, 0.8f, 0.2f, 0.6f);

        public override void Execute(Character source, GameObject targetObj, int diceValue)
        {
            // Monster-only module.
        }

        public void Execute(Monster source, int diceValue)
        {
            Execute(source, diceValue, null);
        }

        public TileData[] GetPreviewTiles(Monster source)
        {
            return SelectTrapTiles();
        }

        public void Execute(Monster source, int diceValue, TileData[] tiles)
        {
            var selectedTiles = tiles ?? SelectTrapTiles();
            if (selectedTiles == null || selectedTiles.Length == 0) return;

            foreach (var tile in selectedTiles)
            {
                if (tile == null) continue;

                var attribute = tile.GetComponent<TileAttribute>();
                if (attribute == null)
                {
                    attribute = tile.gameObject.AddComponent<TileAttribute>();
                    tile.AddAttribute(attribute);
                }

                var trap = new EndTurnDamageTrapArrive(tile, attribute, source, trapDamage, highlightColor);
                attribute.AddArrive(trap);
                tile.Highlight(highlightColor);
            }

            Debug.Log($"[EndTurnTrap] Placed {selectedTiles.Length} traps.");
        }

        private TileData[] SelectTrapTiles()
        {
            var orbitManager = Object.FindAnyObjectByType<OrbitManager>();
            if (orbitManager == null || orbitManager.Tiles.Count == 0) return null;

            var candidates = new List<TileData>(orbitManager.Tiles);
            if (avoidLevelUpTile && orbitManager.LevelUpTile != null)
            {
                candidates.Remove(orbitManager.LevelUpTile);
            }

            if (candidates.Count == 0) return null;

            var selected = new List<TileData>();
            for (int i = 0; i < trapCount; i++)
            {
                selected.Add(candidates[Random.Range(0, candidates.Count)]);
            }

            return selected.ToArray();
        }
    }
}
