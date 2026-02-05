using UnityEngine;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Passives
{
    [CreateAssetMenu(fileName = "RegenerationOnHit", menuName = "Dice Orbit/Passives/Regeneration (On Hit)")]
    public class RegenerationOnHitPassive : PassiveAbility
    {
        [Header("Settings")]
        [SerializeField] private int healAmount = 2;

        private Core.Monster owner;

        public override void Initialize(Core.Monster owner)
        {
            this.owner = owner;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnHit) return;
            if (context.Action.Type != DiceOrbit.Core.Pipeline.ActionType.Attack) return;

            if (context.Target == owner)
            {
                owner.Stats.Heal(healAmount);
                Debug.Log($"[Regeneration] {owner.Stats.MonsterName} healed {healAmount} after being hit.");
            }
        }
    }
}
