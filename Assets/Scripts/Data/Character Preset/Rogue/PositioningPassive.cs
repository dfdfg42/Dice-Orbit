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
            return $"이동한 타일 1칸당 다음 공격 피해 +{(bonusPercentPerTile + GetPositioningBonus()):0.#}%";
        }

        public override void Initialize(DiceOrbit.Core.Unit Owner)
        {
            base.Initialize(Owner);
            movedDistanceThisTurn = 0;
        }

        // 턴 시작 시 이동 거리 초기화
        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (trigger == CombatTrigger.OnPreAction && context.Phase == EventPhase.TurnStart)
                movedDistanceThisTurn = 0;
        }

        // 이동 누적
        public override void OnMove(CombatTrigger trigger, MoveContext context)
        {
            if (trigger == CombatTrigger.OnPostAction && context.SourceUnit == owner)
                movedDistanceThisTurn += context.Steps;
        }

        // 다음 공격에 누적 이동량만큼 피해 증가, 이후 소모
        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (trigger == CombatTrigger.OnCalculateOutput &&
                context.SourceUnit == owner &&
                movedDistanceThisTurn > 0)
            {
                float effectivePercent = bonusPercentPerTile + GetPositioningBonus();
                float multiplier = 1f + (effectivePercent / 100f) * movedDistanceThisTurn;
                context.OutputValue *= multiplier;
                if (!context.IsSimulation)
                {
                    Notify();
                    movedDistanceThisTurn = 0;
                }
            }
        }

        /// <summary>장착된 시그니처 모디파이어가 더해주는 이동 1칸당 추가 피해율(%) 합산.</summary>
        private float GetPositioningBonus()
        {
            if (!(owner is DiceOrbit.Core.Character ch)) return 0f;
            var mods = ch.Stats?.Modifiers?.Modifiers;
            if (mods == null) return 0f;
            float bonus = 0f;
            foreach (var m in mods)
                if (m is DiceOrbit.Data.Modifiers.Rogue.RoguePositioningBoost r)
                    bonus += r.BonusPercentPerTile;
            return bonus;
        }
    }
}
