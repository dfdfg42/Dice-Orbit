using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.TextCore.Text;
using DiceOrbit.UI;
using DiceOrbit.Visuals;

namespace DiceOrbit.Core.Pipeline
{
    /// <summary>
    /// 액션 처리 엔진. 모든 전투 요청은 여길 통과함.
    /// </summary>
    public class CombatPipeline : MonoBehaviour
    {
        public static CombatPipeline Instance { get; private set; }

        private OrbitManager _orbitManager;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            _orbitManager = FindAnyObjectByType<OrbitManager>();
        }

        /// <summary>
        /// 액션 처리 메인 메서드
        /// 각 단계를 별도 메서드로 분리함
        /// </summary>
        public void Process(CombatContext context)
        {
            if (context == null) return;
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

        /// <summary>
        /// 예상 피해량 시뮬레이션. OnCalculateOutput 단계만 돌려서 최종 OutputValue를 계산하고
        /// 적용/OnHit/OnPostAction은 건너뛴다. 반응자는 context.IsSimulation 체크로 Notify·스택소비 같은
        /// 부수효과를 스킵해야 한다. OnPreAction은 회피 RNG가 있어 미리보기에 부적합하므로 제외.
        /// </summary>
        public int SimulateCalculation(EffectContext context)
        {
            if (context == null) return 0;

            context.IsSimulation = true;
            NotifyReactors(context, CombatTrigger.OnCalculateOutput);

            if (context is AttackContext && context.OutputValue < 0)
                context.OutputValue = 0;

            return Mathf.RoundToInt(context.OutputValue);
        }

        private bool HandlePreAction(CombatContext context)
        {
            NotifyReactors(context, CombatTrigger.OnPreAction);
            if (context.IsCancelled) return false;

            if (context is AttackContext && context.Target?.Stats?.DodgeChance > 0f)
            {
                if (UnityEngine.Random.value < context.Target.Stats.DodgeChance / 100f)
                {
                    context.IsCancelled = true;
                    Debug.Log($"{context.Target.name} 회피! (회피율 {context.Target.Stats.DodgeChance}%)");
                    return false;
                }
            }

            return true;
        }

        private void HandleCalculate(CombatContext context)
        {
            NotifyReactors(context, CombatTrigger.OnCalculateOutput);

            // 억지로 음수가 되지 않도록 보정 (HEAL이면 그대로)
            if (context is AttackContext atk)
            {
                if (atk.OutputValue < 0) atk.OutputValue = 0;
            }
        }

        private void HandlePostAction(CombatContext context)
        {
            // 적중했다면 OnHit, 처치했다면 OnKill 등 세분화 가능
            NotifyReactors(context, CombatTrigger.OnHit); // 일단 OnHit으로 통일
            NotifyReactors(context, CombatTrigger.OnPostAction);
        }

        private void NotifyReactors(CombatContext context, CombatTrigger trigger)
        {
            // 반응할 수 있는 모든 후보 수집 (Source의 패시브, Target의 상태이상 등)
            // 기본적으로 모든 파티원들에서 수집하고, Source나 Target이 몬스터라면 몬스터에서도 수집하는 방식으로 구현.
            var reactors = new List<ICombatReactor>();

            HashSet<Unit> uniqueUnits = new HashSet<Unit>();
            // A. Source의 Reactor 수집
            CollectReactors(context.SourceUnit, reactors);

            // B. Target의 Reactor 수집
            CollectReactors(context.Target, reactors);

            // C. Party 전체에서 Reactor 수집
                if (Core.PartyManager.Instance != null)
                {
                    foreach (var ally in Core.PartyManager.Instance.Party)
                    {
                        if (ally != null && ally.Passives is ICombatReactor allyReactor)
                        {
                            CollectReactors(ally, reactors);
                        }
                    }
            }

            // F. 활성 몬스터 전체에서 Reactor 수집 (반응형 몬스터 패시브 — 서리 갑옷 등)
            if (Core.CombatManager.Instance != null && Core.CombatManager.Instance.ActiveMonsters != null)
            {
                foreach (var m in Core.CombatManager.Instance.ActiveMonsters)
                    if (m != null) CollectReactors(m, reactors);
            }

            // D. 유물에서 Reactor 수집 (ArtifactManager — 보유 런타임 인스턴스 자체가 ICombatReactor)
            if (Core.Run.ArtifactManager.Instance != null)
            {
                foreach (var artifact in Core.Run.ArtifactManager.Instance.Artifacts)
                {
                    reactors.Add(artifact);
                }
            }

            // E. 타일 Reactor 수집 (TileAttribute 포탑/연막 등이 매 전투마다 반응 가능)
            if (_orbitManager != null)
            {
                foreach (var tile in _orbitManager.Tiles)
                    if (tile != null) reactors.Add(tile);
            }

            // 우선순위 정렬 (높은 게 먼저 실행 -> 데미지 계산 시 중요)
            // 예: "데미지 2배" vs "데미지 +10" -> 순서에 따라 결과가 다름.
            // 보통 곱연산이나 고정값 합산을 하려면 합의된 Priority가 필요.
            reactors.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            //중복 제거 (같은 유닛의 패시브와 상태이상이 겹칠 수 있음)
            reactors = reactors.Distinct().ToList();
            // 실행
            foreach (var reactor in reactors)
            {
                reactor.OnReact(trigger, context);
                if (context.IsCancelled) break;
            }
        }

        // 유닛에서 Reactor 수집 (패시브 및 상태이상)
        private void CollectReactors(Unit unit, List<ICombatReactor> list)
        {
            if (unit == null) return;
            // Passives
            unit.CollectReactors(list);
        }

        private void ApplyAction(CombatContext context)
        {
            switch (context)
            {
                case AttackContext atk:
                    if (atk.Target.TakeDamage(Mathf.RoundToInt(atk.OutputValue)) != 0) atk.IsEffected = true;
                    // VFX 재생 판단은 여기 한 곳 — 컨텍스트의 프로필에 hit이 있으면 그걸, 없으면 전역 기본
                    if (atk.IsEffected)
                        VfxManager.PlayAttackHit(atk.VfxProfile, atk.Target);
                    break;
                case HealContext heal:
                    // Unit.Heal을 사용하는 것이 일관성에 좋음 (오버라이드 가능성 고려)
                    heal.Target.Heal(Mathf.RoundToInt(heal.OutputValue));
                    VfxManager.PlayHealEffect(heal.VfxProfile, heal.Target);
                    break;
                // MoveContext / TurnEventContext: 순수 방송 — Apply 없음 (의도적 no-op)
            }

            // 추가 효과 적용 (Effects) — 효과 행위(공격/힐)만 해당
            if (context is EffectContext efx && efx.Effects != null && efx.Effects.Count > 0)
            {
                foreach (var effect in efx.Effects)
                    ApplyEffect(efx.Target, effect);
            }
        }

        private void ApplyEffect(Core.Unit target, ActionEffectInfo effectInfo)
        {
            if (target == null) return;

            switch (effectInfo.Type)
            {
                case DiceOrbit.Data.EffectType.Damage:
                    target.TakeDamage(effectInfo.Value);
                    break;

                case DiceOrbit.Data.EffectType.Heal:
                    target.Heal(effectInfo.Value);
                    break;

                default:
                    // 버프, 디버프, 도트 등은 StatusEffectManager로 위임
                    if (target.StatusEffects != null)
                    {
                        target.StatusEffects.AddEffect(DiceOrbit.Systems.Effects.StatusEffectManager.CreateEffect(effectInfo.Type, effectInfo.Value, effectInfo.Duration));
                        var statusData = TooltipKeywordFormatter.BuildStatusDisplayData(effectInfo.Type.ToString(), effectInfo.Value, effectInfo.Duration);
                        CombatNotifier.NotifyStatus(target, statusData.Name, statusData.Color);
                    }
                    break;
            }
        }
    }
}
