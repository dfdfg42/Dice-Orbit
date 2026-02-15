using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace DiceOrbit.Core.Pipeline
{
    /// <summary>
    /// 액션 처리 엔진. 모든 전투 요청은 여길 통과함.
    /// </summary>
    public class CombatPipeline : MonoBehaviour
    {
        public static CombatPipeline Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        /// <summary>
        /// 액션 처리 메인 메서드
        /// 각 단계를 별도 메서드로 분리함
        /// </summary>
        public void Process(CombatContext context)
        {
            if (context == null || context.Action == null) return;
            if (context.IsCancelled) return;

            // 1. Pre-Action (준비 단계)
            if (!HandlePreAction(context)) return;

            // 2. Calculate (수치 계산 단계)
            HandleCalculate(context);

            // 3. Apply (실제 적용 단계)
            ApplyAction(context);

            // 4. Post-Action / Reaction (반응 단계)
            HandlePostAction(context);
        }

        private bool HandlePreAction(CombatContext context)
        {
            NotifyReactors(context, CombatTrigger.OnPreAction);
            return !context.IsCancelled;
        }

        private void HandleCalculate(CombatContext context)
        {
            NotifyReactors(context, CombatTrigger.OnCalculateOutput);

            // 억지로 음수가 되지 않도록 보정 (HEAL이면 그대로)
            if (context.Action.Type == ActionType.Attack)
            {
                if (context.OutputValue < 0) context.OutputValue = 0;
            }
        }

        private void HandlePostAction(CombatContext context)
        {
            //Debug.LogWarning($"{context.SourceUnit.name}, {context.Target.name}, {context.Action.Type}");
            // 적중했다면 OnHit, 처치했다면 OnKill 등 세분화 가능
            NotifyReactors(context, CombatTrigger.OnHit); // 일단 OnHit으로 통일
            NotifyReactors(context, CombatTrigger.OnPostAction);
        }

        private void NotifyReactors(CombatContext context, CombatTrigger trigger)
        {
            // 반응할 수 있는 모든 후보 수집 (Source의 패시브, Target의 상태이상 등)
            // 지금은 Source(시전자)와 Target(피격자) 양쪽의 Reactor를 모두 호출
            
            var reactors = new List<ICombatReactor>();

            // A. Source의 Reactor 수집
            CollectReactors(context.SourceUnit, reactors);

            // B. Target의 Reactor 수집
            CollectReactors(context.Target, reactors);

            // 우선순위 정렬 (높은 게 먼저 실행 -> 데미지 계산 시 중요)
            // 예: "데미지 2배" vs "데미지 +10" -> 순서에 따라 결과가 다름.
            // 보통 곱연산이나 고정값 합산을 하려면 합의된 Priority가 필요.
            reactors.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            // 실행
            foreach (var reactor in reactors)
            {
                reactor.OnReact(trigger, context);
                if (context.IsCancelled) break;
            }
        }

        private void CollectReactors(Unit unit, List<ICombatReactor> list)
        {
            if (unit == null) return;
            // Passives
            unit.CollectReactors(list);
            //Unit 리엑터는 여기서 전부 수집. 일단은 패시브에서만 수집하게 했음. 나중에 수정 반드시 해야 함
            //Unit의 CollectReactors도 수정해야 함
            if (unit is Core.Character character)
            {
                // Include party passives so ally passives can react to actions
                if (Core.PartyManager.Instance != null)
                {
                    foreach (var ally in Core.PartyManager.Instance.Party)
                    {
                        if (ally != null && ally.Passives is ICombatReactor allyReactor)
                        {
                            list.Add(allyReactor);
                        }
                    }
                }

                // Status Effects
                if (character.StatusEffects != null)
                {
                    // Similar logic for StatusEffects
                     if (character.StatusEffects is ICombatReactor efReactor)
                        list.Add(efReactor);
                }
            }
            else if (unit is Core.Monster monster)
            {
                // Monster Logic (Monster Passives?) or Status Effects
            }
        }

        private void ApplyAction(CombatContext context)
        {
            int finalValue = Mathf.RoundToInt(context.OutputValue);

            if (context.Action.Type == ActionType.Attack)
            {
                context.Target.TakeDamage(finalValue);
            }
            else if (context.Action.Type == ActionType.Heal)
            {
                context.Target.Stats.Heal(finalValue);
            }
        }
    }
}
