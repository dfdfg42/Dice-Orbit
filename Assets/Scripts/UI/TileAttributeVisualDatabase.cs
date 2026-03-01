using System;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Data.Tile;
using UnityEngine;

namespace DiceOrbit.UI
{
    [CreateAssetMenu(fileName = "TileAttributeVisualDatabase", menuName = "DiceOrbit/UI/Tile Attribute Visual Database")]
    public class TileAttributeVisualDatabase : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public TileAttributeType type;
            public Sprite icon;
            public Color iconTint = Color.white;
            public string shortLabel;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        private Dictionary<TileAttributeType, Entry> cache;

        public bool TryGet(TileAttributeType type, out Entry entry)
        {
            BuildCacheIfNeeded();
            return cache.TryGetValue(type, out entry);
        }

        private void BuildCacheIfNeeded()
        {
            if (cache != null) return;
            cache = entries
                .GroupBy(e => e.type)
                .Select(g => g.First())
                .ToDictionary(e => e.type, e => e);
        }
    }
}

