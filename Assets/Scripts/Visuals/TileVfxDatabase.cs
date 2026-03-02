using System;
using System.Collections.Generic;
using DiceOrbit.Data;
using DiceOrbit.Data.Tile;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    public enum TileVfxTrigger
    {
        OnTraverse,
        OnArrive,
        OnEndTurn
    }

    [CreateAssetMenu(fileName = "TileVfxDatabase", menuName = "Dice Orbit/VFX/Tile VFX Database")]
    public class TileVfxDatabase : ScriptableObject
    {
        [Serializable]
        public class TileTypeEntry
        {
            public TileType tileType = TileType.Normal;
            public TileVfxTrigger trigger = TileVfxTrigger.OnTraverse;
            public GameObject prefab;
            public Vector3 offset = new Vector3(0f, 0.2f, 0f);
            public float lifetime = 2.0f;
        }

        [Serializable]
        public class TileAttributeEntry
        {
            public TileAttributeType attributeType = TileAttributeType.None;
            public TileVfxTrigger trigger = TileVfxTrigger.OnTraverse;
            public GameObject prefab;
            public Vector3 offset = new Vector3(0f, 0.2f, 0f);
            public float lifetime = 2.0f;
        }

        [SerializeField] private List<TileTypeEntry> tileTypeEntries = new List<TileTypeEntry>();
        [SerializeField] private List<TileAttributeEntry> attributeEntries = new List<TileAttributeEntry>();

        private Dictionary<(TileType, TileVfxTrigger), TileTypeEntry> tileTypeCache;
        private Dictionary<(TileAttributeType, TileVfxTrigger), TileAttributeEntry> attributeCache;

        public bool TryGet(TileType tileType, TileVfxTrigger trigger, out TileTypeEntry entry)
        {
            BuildCacheIfNeeded();
            return tileTypeCache.TryGetValue((tileType, trigger), out entry);
        }

        public bool TryGet(TileAttributeType attributeType, TileVfxTrigger trigger, out TileAttributeEntry entry)
        {
            BuildCacheIfNeeded();
            return attributeCache.TryGetValue((attributeType, trigger), out entry);
        }

        private void BuildCacheIfNeeded()
        {
            if (tileTypeCache == null)
            {
                tileTypeCache = new Dictionary<(TileType, TileVfxTrigger), TileTypeEntry>();
                foreach (var entry in tileTypeEntries)
                {
                    if (entry == null) continue;
                    tileTypeCache[(entry.tileType, entry.trigger)] = entry;
                }
            }

            if (attributeCache == null)
            {
                attributeCache = new Dictionary<(TileAttributeType, TileVfxTrigger), TileAttributeEntry>();
                foreach (var entry in attributeEntries)
                {
                    if (entry == null) continue;
                    attributeCache[(entry.attributeType, entry.trigger)] = entry;
                }
            }
        }

        private void OnValidate()
        {
            tileTypeCache = null;
            attributeCache = null;
        }
    }
}

