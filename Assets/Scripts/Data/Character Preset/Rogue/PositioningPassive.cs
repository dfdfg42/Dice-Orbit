using UnityEngine;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Passives
{
    [System.Serializable]
    public class PositioningPassive : PassiveAbility
    {
        [Header("Designer Tuning")]
        [Tooltip("레벨별 다음 공격 피해 배율. 예: 1.05는 +5%")]
        [SerializeField] private float[] damageMultiplierByLevel = { 1.10f, 1.15f, 1.20f, 1.25f, 1.30f };
        [Tooltip("레벨별 활성화 이동 거리 조건")]
        [SerializeField] private int[] thresholdDistanceByLevel = { 5, 5, 4, 4, 3 };

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
            runtimeDamageMultiplier = ResolveDamageMultiplier(level);
            runtimeThresholdDistance = ResolveThresholdDistance(level);
        }

        private float ResolveDamageMultiplier(int level)
        {
            if (damageMultiplierByLevel == null || damageMultiplierByLevel.Length == 0)
            {
                return Mathf.Max(1f, damageMultiplier);
            }

            int index = Mathf.Clamp(level - 1, 0, damageMultiplierByLevel.Length - 1);
            return Mathf.Max(1f, damageMultiplierByLevel[index]);
        }

        private int ResolveThresholdDistance(int level)
        {
            if (thresholdDistanceByLevel == null || thresholdDistanceByLevel.Length == 0)
            {
                return Mathf.Max(1, thresholdDistance);
            }

            int index = Mathf.Clamp(level - 1, 0, thresholdDistanceByLevel.Length - 1);
            return Mathf.Max(1, thresholdDistanceByLevel[index]);
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
