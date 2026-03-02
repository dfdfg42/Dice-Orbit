using DiceOrbit.Core;
using DiceOrbit.Data;
using DiceOrbit.Data.Tile;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    public class TileVfxManager : MonoBehaviour
    {
        private const string DefaultDatabasePath = "VFX/TileVfxDatabase";

        public static TileVfxManager Instance { get; private set; }

        [SerializeField] private TileVfxDatabase database;
        [SerializeField] private Transform vfxRoot;
        [SerializeField] private float defaultLifetime = 2f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (database == null)
            {
                database = Resources.Load<TileVfxDatabase>(DefaultDatabasePath);
            }
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            var go = new GameObject("TileVfxManager");
            go.AddComponent<TileVfxManager>();
        }

        public static void PlayTileEvent(TileData tile, TileVfxTrigger trigger, Character actor = null)
        {
            if (tile == null) return;
            EnsureInstance();
            if (Instance == null || Instance.database == null) return;

            if (Instance.TryPlayAttributeVfx(tile, trigger)) return;
            Instance.TryPlayTileTypeVfx(tile, trigger);
        }

        private bool TryPlayAttributeVfx(TileData tile, TileVfxTrigger trigger)
        {
            foreach (var attribute in tile.GetAttributes())
            {
                if (attribute == null) continue;
                if (!database.TryGet(attribute.Type, trigger, out var entry)) continue;
                if (entry == null || entry.prefab == null) continue;

                Spawn(entry.prefab, tile.Position + entry.offset, entry.lifetime);
                return true;
            }

            return false;
        }

        private bool TryPlayTileTypeVfx(TileData tile, TileVfxTrigger trigger)
        {
            TileType tileType = ResolveTileType(tile);
            if (!database.TryGet(tileType, trigger, out var entry)) return false;
            if (entry == null || entry.prefab == null) return false;

            Spawn(entry.prefab, tile.Position + entry.offset, entry.lifetime);
            return true;
        }

        private void Spawn(GameObject prefab, Vector3 position, float lifetime)
        {
            if (prefab == null) return;

            Transform parent = vfxRoot != null ? vfxRoot : null;
            var instance = Instantiate(prefab, position, Quaternion.identity, parent);
            float ttl = lifetime > 0f ? lifetime : defaultLifetime;
            if (ttl > 0f)
            {
                Destroy(instance, ttl);
            }
        }

        private static TileType ResolveTileType(TileData tile)
        {
            if (tile.TileIndex == 0) return TileType.LevelUp;
            return TileType.Normal;
        }
    }
}

