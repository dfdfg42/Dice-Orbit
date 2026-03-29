using UnityEngine;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Passives
{
    [System.Serializable]
    public class PositioningPassive : PassiveAbility
    {
        public float damageMultiplier = 1.05f;
        public int thresholdDistance = 5;
        public float damageMultiplierPerLevel = 0.02f;
        public int thresholdDistanceReductionPerLevel = 0;

        private float runtimeDamageMultiplier;
        private int runtimeThresholdDistance;

        private int movedDistanceThisTurn;
        private bool isConditionMet;

        public override int Priority => 99;

        public override void Initialize(Core.Unit Owner)
        {
            base.Initialize(Owner);
            ResetTurnData();
        }

        protected override void ApplyLevel(int level)
        {
            int bonusLevel = Mathf.Max(0, level - 1);
            float bonusPercent = CharacterStats.GetPassivePercentFromCurveB(level, 5f);
            runtimeDamageMultiplier = 1f + (bonusPercent / 100f);
            runtimeThresholdDistance = Mathf.Max(1, thresholdDistance - (thresholdDistanceReductionPerLevel * bonusLevel));
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

                if (movedDistanceThisTurn >= runtimeThresholdDistance && !isConditionMet)
                {
                    isConditionMet = true;
                    Debug.Log("패시브 활성화");
                }
            }

            if (trigger == CombatTrigger.OnCalculateOutput &&
                isConditionMet &&
                context.Action.Type == ActionType.Attack &&
                context.SourceUnit == owner)
            {
                context.OutputValue *= runtimeDamageMultiplier;
                isConditionMet = false;
            }
        }
    }
}
