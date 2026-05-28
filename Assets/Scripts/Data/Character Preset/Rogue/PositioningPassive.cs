using UnityEngine;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Passives
{
    [System.Serializable]
    public class PositioningPassive : CharacterPassiveSkill
    {
        [Header("Designer Tuning")]
        [Tooltip("한 턴에 이동한 타일 1칸당 다음 공격 피해 증가율(%). 예: 25는 +25%")]
        [SerializeField] private float bonusPercentPerTile = 25f;

        private int movedDistanceThisTurn;

        public override int Priority => 99;

        public override string GetDynamicDescription()
        {
            return $"이동한 타일 1칸당 다음 공격 피해 +{bonusPercentPerTile:0.#}%";
        }

        public override void Initialize(DiceOrbit.Core.Unit Owner)
        {
            base.Initialize(Owner);
            movedDistanceThisTurn = 0;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (context == null || context.Action == null) return;

            // 턴 시작 시 이동 거리 초기화
            if (trigger == CombatTrigger.OnPreAction && context.Action.Type == ActionType.OnStartTurn)
            {
                movedDistanceThisTurn = 0;
                return;
            }

            // 이동 누적
            if ((trigger == CombatTrigger.OnPostAction || trigger == CombatTrigger.OnActionSuccess) &&
                context.Action.Type == ActionType.Move &&
                context.SourceUnit == owner)
            {
                movedDistanceThisTurn += Mathf.RoundToInt(context.Action.BaseValue);
                return;
            }

            // 다음 공격에 누적 이동량만큼 피해 증가, 이후 소모
            if (trigger == CombatTrigger.OnCalculateOutput &&
                context.Action.Type == ActionType.Attack &&
                context.SourceUnit == owner &&
                movedDistanceThisTurn > 0)
            {
                float multiplier = 1f + (bonusPercentPerTile / 100f) * movedDistanceThisTurn;
                context.OutputValue *= multiplier;
                if (!context.IsSimulation)
                {
                    Notify();
                    movedDistanceThisTurn = 0;
                }
            }
        }
    }
}
