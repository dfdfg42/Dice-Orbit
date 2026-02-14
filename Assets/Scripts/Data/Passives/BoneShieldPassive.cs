using UnityEngine;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Passives
{
    [CreateAssetMenu(fileName = "BoneShield", menuName = "Dice Orbit/Passives/Bone Shield")]
    public class BoneShieldPassive : PassiveAbility
    {
        [Header("Settings")]
        [SerializeField] private int defenseGain = 1;

        private Core.Monster owner;
        private int temporaryDefense;

        public override void Initialize(Core.Monster owner)
        {
            this.owner = owner;
            temporaryDefense = 0;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (owner == null || context == null || context.Action == null) return;

            if (trigger == CombatTrigger.OnHit
                && context.Target == owner
                && context.Action.Type == Core.Pipeline.ActionType.Attack)
            {
                owner.Stats.Defense += defenseGain;
                temporaryDefense += defenseGain;
                Debug.Log($"[BoneShield] {owner.Stats.MonsterName} gained +{defenseGain} temporary DEF.");
                return;
            }

            // Remove temporary defense right before owner's next action starts.
            if (trigger == CombatTrigger.OnPreAction
                && temporaryDefense > 0
                && context.SourceUnit == owner)
            {
                owner.Stats.Defense = Mathf.Max(0, owner.Stats.Defense - temporaryDefense);
                Debug.Log($"[BoneShield] {owner.Stats.MonsterName} temporary DEF expired ({temporaryDefense}).");
                temporaryDefense = 0;
            }
        }
    }
}
