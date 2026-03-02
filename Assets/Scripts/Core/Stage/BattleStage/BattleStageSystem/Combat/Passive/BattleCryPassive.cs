//using UnityEngine;
//using DiceOrbit.Core;
//using DiceOrbit.Core.Pipeline;

//namespace DiceOrbit.Data.Passives
//{
//    [CreateAssetMenu(fileName = "BattleCryPassive", menuName = "Dice Orbit/Passives/Battle Cry (Greatsword)")]
//    public class BattleCryPassive : PassiveAbility
//    {
//        [Header("Battle Cry Settings")]
//        [Tooltip("증가할 데미지 배율 (예: 1.05 = 5% 증가)")]
//        public float DamageMultiplier = 1.05f;

//        // 이번 턴에 어떤 아군이라도 공격을 했는지 여부
//        private bool hasPartyAttackedThisTurn = false;

//        public override int Priority => 100; // 버프/디버프 연산 시 가장 나중에(마지막으로 크게) 영향력을 주기 위한 임의 우선순위

//        public override void Initialize(Unit Owner)
//        {
//            base.Initialize(Owner);
//            hasPartyAttackedThisTurn = false;
//        }

//        public override void OnReact(CombatTrigger trigger, CombatContext context)
//        {
//            // 1. 턴이 시작되면 첫 번째 타격 여부 초기화
//            if (trigger == CombatTrigger.OnPreAction && context.Action.Type == ActionType.OnStartTurn)
//            {
//                hasPartyAttackedThisTurn = false;
//            }

//            // 2. 데미지 계산 시점이 왔을 때
//            if (trigger == CombatTrigger.OnCalculateOutput)
//            {
//                // 아직 이번 턴에 누군가의 첫 공격이 발생하지 않은 상태이고, 액션 타입이 Attack 이라면
//                if (!hasPartyAttackedThisTurn && context.Action.Type == ActionType.Attack)
//                {
//                    // 아군(Character)의 공격인 경우에만
//                    if (context.SourceUnit is Character)
//                    {
//                        // 5% 데미지 증폭
//                        context.OutputValue *= DamageMultiplier;

//                        // 한 번 적용되었으므로 이번 턴에는 다시 발동하지 않도록 플래그 설정
//                        hasPartyAttackedThisTurn = true;
                        
//                        Debug.Log($"[Passive:전투의 함성] 파티의 턴 첫 공격! 데미지 {DamageMultiplier}배 증가 적용됨. (대상: {context.Target?.name})");
//                    }
//                }
//            }
//        }
//    }
//}
