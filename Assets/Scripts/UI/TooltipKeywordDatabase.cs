using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.UI
{
    [CreateAssetMenu(fileName = "TooltipKeywordDatabase", menuName = "Dice Orbit/UI/Tooltip Keyword Database")]
    public class TooltipKeywordDatabase : ScriptableObject
    {
        [System.Serializable]
        public class KeywordDefinition
        {
            public string key;
            [TextArea] public string description;
            public Color color = new Color(1f, 0.83f, 0.42f, 1f);
            public Sprite icon;
        }

        [Header("Keyword Definitions")]
        public List<KeywordDefinition> entries = new List<KeywordDefinition>();

        public bool TryGet(string keyword, out KeywordDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(keyword) || entries == null) return false;

            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key)) continue;
                if (!string.Equals(entry.key, keyword, System.StringComparison.OrdinalIgnoreCase)) continue;

                definition = entry;
                return true;
            }

            return false;
        }
    }
}
