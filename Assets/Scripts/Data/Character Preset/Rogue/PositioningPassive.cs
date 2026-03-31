using UnityEngine;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Passives
{
    [System.Serializable]
    public class PositioningPassive : PassiveAbility
    {
        // 레벨 1~5 다음 공격 피해량 배율
        private static readonly float[] DamageMultiplierByLevel = { 1.05f, 1.07f, 1.09f, 1.11f, 1.13f };
        // 레벨 1~5 활성화 이동 거리 조건
        private static readonly int[] ThresholdDistanceByLevel = { 5, 5, 4, 4, 3 };

        public float damageMultiplier = 1.05f;
        public int thresholdDistance = 5;

        private float runtimeDamageMultiplier;
        private int runtimeThresholdDistance;
    public float CurrentDamageMultiplier => runtimeDamageMultiplier;
    public int CurrentThresholdDistance => runtimeThresholdDistance;

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
            int index = Mathf.Clamp(level - 1, 0, DamageMultiplierByLevel.Length - 1);
            runtimeDamageMultiplier = DamageMultiplierByLevel[index];

            int thresholdIndex = Mathf.Clamp(level - 1, 0, ThresholdDistanceByLevel.Length - 1);
            runtimeThresholdDistance = Mathf.Max(1, ThresholdDistanceByLevel[thresholdIndex]);
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
