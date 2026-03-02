using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Data;
using DiceOrbit.Data.Tile;
using UnityEngine;

namespace DiceOrbit.UI
{
    public class TileAttributeBubbleManager : MonoBehaviour
    {
        public static TileAttributeBubbleManager Instance { get; private set; }
        private const string DefaultVisualDatabaseResourcePath = "UI/TileAttributeVisualDatabase";
        private const string DefaultBubbleSpriteResourcePath = "UI/TileBubbleBG";

        [Header("References")]
        [SerializeField] private TileAttributeVisualDatabase visualDatabase;
        [SerializeField] private Sprite bubbleSprite;

        [Header("Rules")]
        [SerializeField] private bool hideLevelUpAttribute = true;

        private readonly Dictionary<TileData, TileAttributeBubbleUI> activeBubbles = new Dictionary<TileData, TileAttributeBubbleUI>();
        private readonly HashSet<TileAttributeType> missingMappingLogged = new HashSet<TileAttributeType>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Bootstrap()
        {
            // Do not create a runtime instance before scene objects are available.
            // This prevents losing inspector-assigned references (e.g., bubbleSprite).
            if (Instance == null)
            {
                Instance = FindFirstObjectByType<TileAttributeBubbleManager>();
            }
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;

            var existing = FindFirstObjectByType<TileAttributeBubbleManager>();
            if (existing != null)
            {
                Instance = existing;
                return;
            }

            var go = new GameObject("TileAttributeBubbleManager");
            Instance = go.AddComponent<TileAttributeBubbleManager>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            TryResolveVisualDatabase();
            TryResolveBubbleSprite();
        }

        private void Start()
        {
            RefreshAllTiles();
        }

        public void RefreshAllTiles()
        {
            var allTiles = GameManager.Instance?.GetOrbitManager().Tiles;
            foreach (var tile in allTiles)
            {
                RefreshTile(tile);
            }
        }

        public void RefreshTile(TileData tile)
        {
            if (tile == null) return;
            if (bubbleSprite == null) TryResolveBubbleSprite();
            if (visualDatabase == null) TryResolveVisualDatabase();

            var attrs = tile.GetAttributes();
            if (attrs == null || attrs.Count == 0)
            {
                RemoveBubble(tile);
                return;
            }

            TileAttribute primaryAttribute = attrs.FirstOrDefault(a =>
                a != null &&
                (!hideLevelUpAttribute || a.Type != TileAttributeType.LevelUp));

            if (primaryAttribute == null)
            {
                RemoveBubble(tile);
                return;
            }

            if (visualDatabase == null)
            {
                Debug.LogError("[TileAttributeBubble] TileAttributeVisualDatabase is not assigned.");
                return;
            }

            if (!visualDatabase.TryGet(primaryAttribute.Type, out var visual))
            {
                if (!missingMappingLogged.Contains(primaryAttribute.Type))
                {
                    missingMappingLogged.Add(primaryAttribute.Type);
                    Debug.LogError($"[TileAttributeBubble] Missing visual mapping for {primaryAttribute.Type}.");
                }
                RemoveBubble(tile);
                return;
            }

            if (visual.icon == null)
            {
                Debug.LogError($"[TileAttributeBubble] Icon is not assigned for {primaryAttribute.Type}.");
                RemoveBubble(tile);
                return;
            }

            var bubble = GetOrCreateBubble(tile);
            bubble.Setup(
                tile.transform,
                bubbleSprite,
                visual.icon,
                visual.iconTint,
                visual.shortLabel
            );
        }

        private TileAttributeBubbleUI GetOrCreateBubble(TileData tile)
        {
            if (activeBubbles.TryGetValue(tile, out var existing) && existing != null)
            {
                return existing;
            }

            var go = new GameObject($"TileBubble_{tile.TileIndex}");
            var bubble = go.AddComponent<TileAttributeBubbleUI>();
            activeBubbles[tile] = bubble;
            return bubble;
        }

        private void RemoveBubble(TileData tile)
        {
            if (!activeBubbles.TryGetValue(tile, out var bubble)) return;
            activeBubbles.Remove(tile);
            if (bubble != null)
            {
                Destroy(bubble.gameObject);
            }
        }

        private void TryResolveVisualDatabase()
        {
            if (visualDatabase != null) return;
            visualDatabase = Resources.Load<TileAttributeVisualDatabase>(DefaultVisualDatabaseResourcePath);
            if (visualDatabase == null)
            {
                Debug.LogWarning($"[TileAttributeBubble] Could not load TileAttributeVisualDatabase from Resources/{DefaultVisualDatabaseResourcePath}.asset");
            }
        }

        private void TryResolveBubbleSprite()
        {
            if (bubbleSprite != null) return;
            bubbleSprite = Resources.Load<Sprite>(DefaultBubbleSpriteResourcePath);
            if (bubbleSprite == null)
            {
                Debug.LogWarning($"[TileAttributeBubble] Could not load bubble sprite from Resources/{DefaultBubbleSpriteResourcePath}.");
            }
        }
    }
}
