using UnityEngine;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Passives
{
    [CreateAssetMenu(fileName = "LifestealOnHit", menuName = "Dice Orbit/Passives/Lifesteal On Hit")]
    public class LifestealOnHitPassive : PassiveAbility
    {
        [Header("Settings")]
        [SerializeField] private int healAmount = 1;

        private Core.Monster owner;

        public override void Initialize(Core.Monster owner)
        {
            this.owner = owner;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (owner == null || context == null || context.Action == null) return;
            if (trigger != CombatTrigger.OnHit) return;
            if (context.Action.Type != Core.Pipeline.ActionType.Attack) return;

            if (context.SourceUnit == owner)
            {
                owner.Stats.Heal(healAmount);
                Debug.Log($"[Lifesteal] {owner.Stats.MonsterName} healed {healAmount}.");
            }
        }
    }
}
