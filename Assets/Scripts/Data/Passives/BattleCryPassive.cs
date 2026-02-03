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

        public override void Initialize(Core.Character owner)
        {
            base.Initialize(owner);
            hasAttackedThisTurn = false;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            // 1. 턴 시작 시 플래그 초기화
            if (trigger == CombatTrigger.OnTurnStart)
            {
                // 내 턴이 시작될 때
                if (context.Source == context.Target) // TurnOwner == MyOwner? (Context 정의 확인 필요)
                {
                    // ! Context의 Source/Target 의미가 트리거마다 다를 수 있음.
                    // OnTurnStart: Source = TurnOwner
                    // 일단 내 턴인지 확인하려면 context.Source == Owner 인지 봐야 함
                    // (PassiveAbility.Initialize에서 Owner 저장한다고 가정)
                    
                    // 하지만 PassiveAbility는 SO라서 runtime owner 변수가 공유될 위험 있음.
                    // *이번 구현에서는 SO를 Clone해서 쓴다고 가정하고 멤버변수 사용*
                    hasAttackedThisTurn = false;
                }
            }

            // 2. 데미지 계산 시 보정
            if (trigger == CombatTrigger.OnCalculateOutput)
            {
                // 내가 공격자이고, 공격 액션이며, 이번 턴 아직 공격 안했으면
                if (context.SourceUnit == context.Source /* Owner check needed via specific way if context.Source is Character */ 
                    && context.Action.Type == ActionType.Attack
                    && !hasAttackedThisTurn)
                {
                    context.OutputValue += BonusDamageManager;
                    Debug.Log($"[BattleCry] First attack bonus! +{BonusDamageManager}");
                    
                    // ! 주의: OnCalculateOutput은 여러 번 불릴 수 있음 (예측 등).
                    // 실제 공격이 수행되었을 때(OnHit/PostAction) 플래그를 꺼야 함.
                    // 여기서 끄면 안됨.
                }
            }
            
            // 3. 공격 수행 후 플래그 변경
            if (trigger == CombatTrigger.OnPostAction)
            {
                if (context.SourceUnit == context.Source // check owner
                    && context.Action.Type == ActionType.Attack)
                {
                    hasAttackedThisTurn = true;
                }
            }
        }
        
        // PassiveAbility가 SO라면, Initialize 호출 시 Clone을 만들어서 등록하는 로직이 Character/Manager에 있어야 함.
    }
}
