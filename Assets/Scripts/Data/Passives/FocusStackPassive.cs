using UnityEngine;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Passives
{
    [CreateAssetMenu(fileName = "Focus Stack", menuName = "Dice Orbit/Passives/Focus Stack")]
    public class FocusStackPassive : PassiveAbility
    {
        public int DamagePerStack = 2;
        public int MaxStacks = 5;
        
        private Core.Character owner;
        private int currentStacks = 0;
        
        public override void Initialize(Core.Character owner)
        {
            this.owner = owner;
            base.Initialize(owner);
            currentStacks = 0;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            // 턴 시작 시 스택 충전
            if (trigger == CombatTrigger.OnTurnStart)
            {
                // 내 턴일 때
                if (context.SourceUnit == owner)
                {
                    if (currentStacks < MaxStacks)
                    {
                        currentStacks++;
                        Debug.Log($"[FocusStack] Stack added. Current: {currentStacks}");
                    }
                }
            }
            
            // 데미지 계산 시 적용
            if (trigger == CombatTrigger.OnCalculateOutput)
            {
                if (context.SourceUnit == owner && context.Action.Type == Core.Pipeline.ActionType.Attack)
                {
                    int bonus = currentStacks * DamagePerStack;
                    if (bonus > 0)
                    {
                        context.OutputValue += bonus;
                        Debug.Log($"[FocusStack] Applied +{bonus} damage ({currentStacks} stacks)");
                    }
                }
            }
            
            // 이동 시 리셋? (Legacy logic: Resets on Move)
            if (trigger == CombatTrigger.OnPostAction)
            {
                if (context.SourceUnit == owner && context.Action.HasTag("Move"))
                {
                    currentStacks = 0;
                    Debug.Log("[FocusStack] Stacks reset due to movement.");
                }
            }
        }
    }
}
