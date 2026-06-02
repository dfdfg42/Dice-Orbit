using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.Passives
{
    [System.Serializable]
    public class FocusPassive : CharacterPassiveSkill
    {
        [Header("Designer Tuning")]
        [Tooltip("피해를 입지 않은 턴 종료 시 부여할 집중 스택")]
        [SerializeField] private int stacksGainedWhenSafe = 2;
        [Tooltip("피해를 입은 턴 종료 시 집중 스택을 이 값으로 나눔 (예: 4 = 1/4로 감소)")]
        [SerializeField] private int penaltyDivisor = 4;

        private int hpAtTurnStart = -1;

        public override int Priority => 50;

        public override string GetDynamicDescription()
        {
            return $"피해 없이 턴 종료 시 집중 +{stacksGainedWhenSafe}, 피해 입으면 1/{Mathf.Max(2, penaltyDivisor)}로 감소";
        }

        public override void Initialize(Unit ownerUnit)
        {
            base.Initialize(ownerUnit);

            // 웨이브가 넘어갈 때만 집중 스택 초기화 (공격으로는 소비되지 않음)
            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.OnWaveStart -= HandleWaveStart;
                WaveManager.Instance.OnWaveStart += HandleWaveStart;
            }
        }

        private void HandleWaveStart(int wave)
        {
            owner?.StatusEffects?.RemoveEffect(EffectType.Focus);
            hpAtTurnStart = -1;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (owner == null || context == null || context.Action == null) return;
            if (context.SourceUnit != owner) return;
            if (owner.Stats == null) return;

            // 턴 종료 시 체력 손실 여부로 분기
            if (trigger == CombatTrigger.OnPostAction && context.Action.Type == ActionType.OnEndTurn)
            {
                bool lostHP = hpAtTurnStart >= 0 && owner.Stats.CurrentHP < hpAtTurnStart;
                if (lostHP)
                {
                    ReduceFocus();
                }
                else
                {
                    GainFocus();
                }

                hpAtTurnStart = owner.Stats.CurrentHP;
            }
        }

        private void GainFocus()
        {
            owner.StatusEffects?.AddEffect(new StatusEffect(EffectType.Focus, Mathf.Max(1, stacksGainedWhenSafe), -1, isStackable: true));
            Notify();
        }

        private void ReduceFocus()
        {
            if (owner.StatusEffects == null) return;

            int current = owner.StatusEffects.GetEffectValue(EffectType.Focus);
            if (current <= 0) return;

            owner.StatusEffects.RemoveEffect(EffectType.Focus);

            int reduced = current / Mathf.Max(2, penaltyDivisor);
            if (reduced > 0)
            {
                owner.StatusEffects.AddEffect(new StatusEffect(EffectType.Focus, reduced, -1, isStackable: true));
            }
            Notify();
        }
    }
}