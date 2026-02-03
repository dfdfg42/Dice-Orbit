using UnityEngine;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Passives
{
    [CreateAssetMenu(fileName = "Battle Cry", menuName = "Dice Orbit/Passives/Battle Cry")]
    public class BattleCryPassive : PassiveAbility
    {
        [Header("Settings")]
        public int BonusDamageManager = 3;
        
        // 런타임 상태 (ScriptableObject는 상태 저장에 주의해야 하지만, 
        // 1인용 게임 파시브라면 임시로 여기서 관리하거나, 
        // PassiveManager가 Runtime Instance를 관리하도록 구조를 잡아야 함.
        // 여기서는 간단히 'RuntimePassive' 래퍼가 없으므로
        // 상태를 Owner(Character) 어딘가에 저장하거나, 
        // 이 SO가 'Instance'로 복제되어 사용된다고 가정합니다.
        // *Dice Orbit 프로젝트 규칙: Skill/Passive SO는 Instantiate해서 씀? -> Character.InitializeStats에서 확인 필요.
        // 보통은 Runtime Wrapper가 필요. 일단 변수 사용.)
        
        private bool hasAttackedThisTurn = false;

        private Core.Character owner;

        public override void Initialize(Core.Character owner)
        {
            this.owner = owner;
            base.Initialize(owner);
            hasAttackedThisTurn = false;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            // 1. 턴 시작 시 플래그 초기화
            if (trigger == CombatTrigger.OnTurnStart)
            {
                // 내 턴이 시작될 때 (Source가 나 자신일 때)
                if (context.SourceUnit == owner) 
                {
                    hasAttackedThisTurn = false;
                }
            }

            // 2. 데미지 계산 시 보정
            if (trigger == CombatTrigger.OnCalculateOutput)
            {
                // 내가 공격자이고, 공격 액션이며, 이번 턴 아직 공격 안했으면
                if (context.SourceUnit == owner 
                    && context.Action.Type == Core.Pipeline.ActionType.Attack
                    && !hasAttackedThisTurn)
                {
                    context.OutputValue += BonusDamageManager;
                    Debug.Log($"[BattleCry] First attack bonus! +{BonusDamageManager}");
                }
            }
            
            // 3. 공격 수행 후 플래그 변경
            if (trigger == CombatTrigger.OnPostAction)
            {
                if (context.SourceUnit == owner
                    && context.Action.Type == Core.Pipeline.ActionType.Attack)
                {
                    hasAttackedThisTurn = true;
                }
            }
        }
        
        // PassiveAbility가 SO라면, Initialize 호출 시 Clone을 만들어서 등록하는 로직이 Character/Manager에 있어야 함.
    }
}
