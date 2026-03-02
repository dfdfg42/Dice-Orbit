using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Passives
{
    [System.Serializable]
    public class BattleCryPassive : PassiveAbility
    {
        public float damageMultiplier = 1.05f;
        private bool hasPartyAttackedThisTurn;

        public override int Priority => 100;

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            hasPartyAttackedThisTurn = false;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (context == null || context.Action == null) return;

            if (trigger == CombatTrigger.OnPreAction && context.Action.Type == ActionType.OnStartTurn)
            {
                hasPartyAttackedThisTurn = false;
            }

            if (trigger == CombatTrigger.OnCalculateOutput &&
                !hasPartyAttackedThisTurn &&
                context.Action.Type == ActionType.Attack &&
                context.SourceUnit is Character)
            {
                context.OutputValue *= damageMultiplier;
                hasPartyAttackedThisTurn = true;
            }
        }
    }
}
