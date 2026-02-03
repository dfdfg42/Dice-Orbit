using UnityEngine;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;

namespace DiceOrbit.Systems.Effects
{
    /// <summary>
    /// 실행 중인 상태 이상 인스턴스
    /// </summary>
    public class StatusEffect : ICombatReactor
    {
        public EffectType Type;
        public int Value;
        public int Duration;
        public bool IsStackable;
        
        public int Priority => 10; // 효과는 패시브보다 후순위? 혹은 상위? (설계에 따라 다름)

        public StatusEffect(EffectType type, int value, int duration, bool isStackable = false)
        {
            Type = type;
            Value = value;
            Duration = duration;
            IsStackable = isStackable;
        }

        public void AddStack(int value)
        {
            Value += value;
        }

        public void RefreshDuration(int duration)
        {
            Duration = Mathf.Max(Duration, duration);
        }

        public void OnReact(CombatTrigger trigger, CombatContext context)
        {
            // 예시 로직
            
            // 1. 턴 시작 시: 도트뎀 (Source가 자신일 경우)
            if (trigger == CombatTrigger.OnTurnStart)
            {
                // 여기서 context.Source와 Target의 관계를 잘 봐야 함.
                // OnTurnStart 트리거는 "누구의 턴"인지 context.Source에 담겨 온다고 가정.
                // 혹은 Pipeline에서 별도 처리.
                
                // 간단히: EffectOwner가 context.Source==Context.Target(TurnOwner) 일 때
                // 도트뎀 적용은 조금 복잡하므로 보통 Pipeline에서 "TurnStartAction"을 만들어서 돌림.
            }

            // 2. 데미지 계산 시: 버프/디버프
            if (trigger == CombatTrigger.OnCalculateOutput)
            {
                // 내가 타겟(방어자)이고, 방어 버프가 있다면
                if (context.Target == Owner && Type == EffectType.BuffDefense)
                {
                    // context.OutputValue -= Value; ???
                    // context.Action이 뭔지 확인 필요. AttackAction이어야 함.
                }
                
                // 내가 공격자이고, 공격 버프가 있다면
                if (context.SourceUnit == Owner && Type == EffectType.BuffAttack) 
                {
                    // 이 논리는 Context 구현에 따라 달라짐.
                    // 단순화: Context에는 Source와 Target이 명확함.
                    // 이 Effect가 "누구"에게 붙어있는지 알아야 함. (StatusEffect는 Owner 정보가 없음 현재)
                }
            }
        }
        
        // Owner를 주입받아야 함
        public Core.Character Owner { get; set; }
        
        public void SetOwner(Core.Character owner)
        {
            Owner = owner;
        }
        
        // 실제 구현 보강
        public void ProcessReaction(CombatTrigger trigger, CombatContext context)
        {
             if (Owner == null) return;

             // 공격력 증가 버프
             if (trigger == CombatTrigger.OnCalculateOutput && Type == EffectType.BuffAttack)
             {
                 // 내가 공격자일 때
                 if (context.SourceUnit == Owner && context.Action.Type == Core.Pipeline.ActionType.Attack)
                 {
                     context.OutputValue += Value;
                     Debug.Log($"[Effect] {Type} applied: +{Value} damage");
                 }
             }
             
             // 방어력 증가 버프
             if (trigger == CombatTrigger.OnCalculateOutput && Type == EffectType.BuffDefense)
             {
                 // 내가 타겟일 때 (Target은 object이므로 비교 가능)
                 if (context.Target == Owner && context.Action.Type == Core.Pipeline.ActionType.Attack)
                 {
                     context.OutputValue -= Value;
                     Debug.Log($"[Effect] {Type} applied: -{Value} damage received");
                 }
             }
        }
    }
}
