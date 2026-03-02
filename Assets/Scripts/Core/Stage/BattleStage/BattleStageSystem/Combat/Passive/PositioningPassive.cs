using UnityEngine;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Passives
{
    [System.Serializable]
    public class PositioningPassive : PassiveAbility
    {
        public float damageMultiplier = 1.05f;
        public int thresholdDistance = 5;

        private int movedDistanceThisTurn;
        private bool isConditionMet;

        public override int Priority => 99;

        public override void Initialize(Core.Unit Owner)
        {
            base.Initialize(Owner);
            ResetTurnData();
        }

        private void ResetTurnData()
        {
            movedDistanceThisTurn = 0;
            isConditionMet = false;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (context == null || context.Action == null) return;

            if (trigger == CombatTrigger.OnPreAction && context.Action.Type == ActionType.OnStartTurn)
            {
                ResetTurnData();
            }

            if ((trigger == CombatTrigger.OnPostAction || trigger == CombatTrigger.OnActionSuccess) &&
                context.Action.Type == ActionType.Move &&
                context.SourceUnit == owner)
            {
                int dist = Mathf.RoundToInt(context.Action.BaseValue);
                movedDistanceThisTurn += dist;

                if (movedDistanceThisTurn >= thresholdDistance && !isConditionMet)
                {
                    isConditionMet = true;
                }
            }

            if (trigger == CombatTrigger.OnCalculateOutput &&
                isConditionMet &&
                context.Action.Type == ActionType.Attack &&
                context.SourceUnit == owner)
            {
                context.OutputValue *= damageMultiplier;
                isConditionMet = false;
            }
        }
    }
}
