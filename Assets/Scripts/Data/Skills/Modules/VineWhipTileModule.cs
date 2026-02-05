using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Skills.Modules
{
    [CreateAssetMenu(fileName = "Vine Whip Module", menuName = "Dice Orbit/Skills/Modules/Vine Whip")]
    public class VineWhipTileModule : SkillActionModule, IMonsterActionModule, IMonsterTileActionModule
    {
        [Header("Damage Settings")]
        [SerializeField] private int damage = 2;
        [SerializeField] private int lateralRange = 1;
        [SerializeField] private bool avoidLevelUpTile = true;

        public override void Execute(Character source, GameObject targetObj, int diceValue)
        {
            // Monster-only module. No action for player skills.
        }

        public void Execute(Monster source, int diceValue)
        {
            Execute(source, diceValue, null);
        }

        public Data.TileData[] GetPreviewTiles(Monster source)
        {
            return SelectTargetTiles();
        }

        public void Execute(Monster source, int diceValue, Data.TileData[] tiles)
        {
            var selectedTiles = tiles != null && tiles.Length > 0
                ? new HashSet<Data.TileData>(tiles)
                : SelectTargetTilesSet();

            if (selectedTiles == null || selectedTiles.Count == 0) return;

            var partyManager = PartyManager.Instance;
            if (partyManager == null) return;

            var aliveCharacters = partyManager.GetAliveCharacters();
            if (aliveCharacters.Count == 0) return;

            foreach (var character in aliveCharacters)
            {
                if (character != null && character.CurrentTile != null && selectedTiles.Contains(character.CurrentTile))
                {
                    if (CombatPipeline.Instance != null)
                    {
                        var action = new CombatAction("Vine Whip", DiceOrbit.Core.Pipeline.ActionType.Attack, damage);
                        action.AddTag("VineWhip");
                        var context = new CombatContext(source, character, action);
                        CombatPipeline.Instance.Process(context);
                    }
                    else
                    {
                        character.TakeDamage(damage);
                    }
                }
            }

            Debug.Log($"[VineWhip] Targeted tiles: {selectedTiles.Count}");
        }

        private Data.TileData[] SelectTargetTiles()
        {
            var tiles = SelectTargetTilesSet();
            return tiles == null ? null : new List<Data.TileData>(tiles).ToArray();
        }

        private HashSet<Data.TileData> SelectTargetTilesSet()
        {
            var orbitManager = Object.FindAnyObjectByType<OrbitManager>();
            if (orbitManager == null || orbitManager.Tiles.Count == 0) return null;

            var candidates = new List<Data.TileData>(orbitManager.Tiles);
            if (avoidLevelUpTile && orbitManager.LevelUpTile != null)
            {
                candidates.Remove(orbitManager.LevelUpTile);
            }

            if (candidates.Count == 0) return null;

            var centerTile = candidates[Random.Range(0, candidates.Count)];
            if (centerTile == null) return null;

            return CollectTiles(centerTile, lateralRange);
        }

        private HashSet<Data.TileData> CollectTiles(Data.TileData center, int range)
        {
            var tiles = new HashSet<Data.TileData> { center };
            var next = center;
            var prev = center;

            for (int i = 0; i < range; i++)
            {
                if (next?.NextTile != null)
                {
                    next = next.NextTile;
                    tiles.Add(next);
                }

                if (prev?.PreviousTile != null)
                {
                    prev = prev.PreviousTile;
                    tiles.Add(prev);
                }
            }

            return tiles;
        }
    }
}
