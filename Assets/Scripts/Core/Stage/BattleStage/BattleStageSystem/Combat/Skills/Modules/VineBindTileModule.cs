using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.Skills.Modules
{
    [CreateAssetMenu(fileName = "Vine Bind Module", menuName = "Dice Orbit/Skills/Modules/Vine Bind")]
    public class VineBindTileModule : SkillActionModule, IMonsterActionModule, IMonsterTileActionModule
    {
        [Header("Trap Settings")]
        [SerializeField] private int trapCount = 1;
        [SerializeField] private bool avoidLevelUpTile = true;
        [SerializeField] private Color highlightColor = new Color(0.2f, 0.8f, 0.2f, 0.6f);

        public override void Execute(Character source, GameObject targetObj, int diceValue)
        {
            // Monster-only module. No action for player skills.
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
                }

                var trap = new DiceOrbit.Data.Tile.VineTrapTraverse(tile, attribute, highlightColor);
                attribute.AddTraverse(trap);

                tile.Highlight(highlightColor);
                Debug.Log($"[VineBind] Trap placed on tile {tile.TileIndex}");
            }
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

        public override string GetTooltipDescription()
        {
            if (!string.IsNullOrWhiteSpace(TooltipDescription))
            {
                return TooltipDescription;
            }

            return $"무작위 타일 {trapCount}곳에 속박 함정을 설치합니다. 해당 타일을 지나가면 즉시 이동이 중단됩니다.";
        }
    }
}
