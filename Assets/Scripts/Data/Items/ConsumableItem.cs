using UnityEngine;

namespace DiceOrbit.Data.Items
{
    public enum ItemType
    {
        Potion,
        Scroll,
        Artifact // If needed
    }

    public abstract class ConsumableItem : ScriptableObject
    {
        public string ItemName;
        [TextArea] public string Description;
        public Sprite Icon;
        public ItemType Type;
        public int Price; // Shop Price

        /// <summary>
        /// 아이템 사용 효과
        /// </summary>
        public abstract bool Use(Core.Character target);
    }
}
