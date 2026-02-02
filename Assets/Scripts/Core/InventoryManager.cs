using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Data.Items;

namespace DiceOrbit.Core
{
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        [Header("Inventory")]
        [SerializeField] private List<ConsumableItem> items = new List<ConsumableItem>();
        [SerializeField] private int maxSlots = 10;

        // Events
        public System.Action OnInventoryChanged;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public List<ConsumableItem> GetItems() => items;

        public bool AddItem(ConsumableItem item)
        {
            if (items.Count >= maxSlots)
            {
                Debug.LogWarning("[Inventory] Full!");
                return false;
            }
            
            items.Add(item);
            OnInventoryChanged?.Invoke();
            Debug.Log($"[Inventory] Added {item.ItemName}");
            return true;
        }

        public void RemoveItem(ConsumableItem item)
        {
            if (items.Contains(item))
            {
                items.Remove(item);
                OnInventoryChanged?.Invoke();
            }
        }

        public void UseItem(int index, Character target)
        {
            if (index < 0 || index >= items.Count) return;
            
            var item = items[index];
            if (item.Use(target))
            {
                RemoveItem(item);
                Debug.Log($"[Inventory] Used {item.ItemName}");
            }
        }
    }
}
