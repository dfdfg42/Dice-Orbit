using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.Data.Tile
{
    public class VineTrapTraverse : IOnTraverse
    {
        private readonly TileData tile;
        private readonly TileAttribute attribute;
        private readonly Color highlightColor;
        private bool triggered;

        public string TooltipDescription => "속박 함정: 이 타일을 지나가면 이동이 즉시 중단되고 함정은 제거됩니다.";

        public VineTrapTraverse(TileData tile, TileAttribute attribute, Color highlightColor)
        {
            this.tile = tile;
            this.attribute = attribute;
            this.highlightColor = highlightColor;
        }

        public void OnTraverse(Character character)
        {
            if (triggered || character == null) return;
            triggered = true;

            character.RequestStopMovement();

            if (tile != null)
            {
                tile.Highlight(highlightColor);
            }

            if (attribute != null)
            {
                attribute.RemoveTraverse(this);
                if (attribute.IsEmpty)
                {
                    Object.Destroy(attribute);
                }
            }

            if (tile != null)
            {
                tile.ClearHighlight();
            }

            Debug.Log($"[VineBind] {character.Stats.CharacterName} rooted on tile {tile?.TileIndex}");
        }
    }
}
