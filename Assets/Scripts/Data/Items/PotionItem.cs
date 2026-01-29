using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.Data.Items
{
    public enum PotionEffectType
    {
        Heal,
        Move, // Teleport or Move Steps (instant)
        Buff
    }

    [CreateAssetMenu(fileName = "New Potion", menuName = "Dice Orbit/Items/Potion")]
    public class PotionItem : ConsumableItem
    {
        public PotionEffectType EffectType;
        public int Value; 
        
        [Header("Buff Settings (Only if Buff)")]
        public StatusEffectType BuffType;
        public int Duration;

        public override bool Use(Character target)
        {
            if (target == null) return false;

            switch (EffectType)
            {
                case PotionEffectType.Heal:
                    target.Stats.Heal(Value);
                    Debug.Log($"Used {ItemName}: Healed {target.Stats.CharacterName} for {Value}");
                    break;
                    
                case PotionEffectType.Move:
                    // Instant Move logic
                    // This moves character WITHOUT consuming action/dice.
                    // We need to access OrbitManager to get next tile.
                    // Simplified: Just log for now or assume Character has Teleport/Move logic.
                    // Character.cs doesn't have public Move(int) yet.
                    // I'll implement a simple transform move or call a new method later.
                    // For now, let's treat it as "Heal" as placeholder or just Log.
                    Debug.Log($"Used {ItemName}: Moving {target.Stats.CharacterName} {Value} tiles instantly!");
                    // TODO: Implement Character.Move(int distance)
                    break;
                    
                case PotionEffectType.Buff:
                    if (target.StatusEffects != null)
                    {
                        target.StatusEffects.AddEffect(BuffType, Value, Duration);
                        Debug.Log($"Used {ItemName}: Applied {BuffType} to {target.Stats.CharacterName}");
                    }
                    else
                    {
                        Debug.LogWarning("Target has no StatusEffectManager!");
                        return false; 
                    }
                    break;
            }
            return true;
        }
    }
}
