//using UnityEngine;
//using DiceOrbit.Core;
//using DiceOrbit.Core.Pipeline;

//namespace DiceOrbit.Data.Passives
//{
//    [CreateAssetMenu(fileName = "PositioningPassive", menuName = "Dice Orbit/Passives/Positioning (Rogue)")]
//    public class PositioningPassive : PassiveAbility
//    {
//        [Header("Positioning Settings")]
//        [Tooltip("증가할 데미지 배율 (예: 1.05 = 5% 증가)")]
//        public float DamageMultiplier = 1.05f;
//        [Tooltip("조건을 달성하기 위해 이동해야 하는 누적 칸 수")]
//        public int ThresholdDistance = 5;

//        // 현재 턴에 이동한 누적 칸 수 (캐릭터 개인 기준)
//        private int movedDistanceThisTurn = 0;
        
//        // 이동 조건을 만족했는지 여부
//        private bool isConditionMet = false;

//        public override int Priority => 99;

//        public override void Initialize(Unit Owner)
//        {
//            base.Initialize(Owner);
//            ResetTurnData();
//        }

//        private void ResetTurnData()
//        {
//            movedDistanceThisTurn = 0;
//            isConditionMet = false;
//        }

//        public override void OnReact(CombatTrigger trigger, CombatContext context)
//        {
//            // 1. 턴이 시작되면 초기화
//            if (trigger == CombatTrigger.OnPreAction && context.Action.Type == ActionType.OnStartTurn)
//            {
//                ResetTurnData();
//            }

//            // 2. 이동 액션 처리
//            if (context.Action.Type == ActionType.Move)
//            {
//                // OnPostAction 이나 OnActionSuccess 시점에서 이동 완료로 간주
//                if (trigger == CombatTrigger.OnPostAction || trigger == CombatTrigger.OnActionSuccess)
//                {
//                    // 이동한 유닛이 이 패시브의 주인인 경우
//                    if (context.SourceUnit == owner)
//                    {
//                        // 주사위 눈금만큼 이동했다고 가정 (BaseValue에 이동거리가 담김)
//                        // 단말의 이동 액션 파이프라인 구조에 맞게 수정 필요 시 변경
//                        int dist = Mathf.RoundToInt(context.Action.BaseValue);
//                        movedDistanceThisTurn += dist;

//                        Debug.Log($"[Passive:위치 선정] {owner.name} 누적 이동 횟수: {movedDistanceThisTurn}");

//                        if (movedDistanceThisTurn >= ThresholdDistance && !isConditionMet)
//                        {
//                            isConditionMet = true;
//                            Debug.Log($"[Passive:위치 선정] 거리 조건 만족! 다음 공격 5% 강화 준비됨.");
//                        }
//                    }
//                }
//            }

//            // 3. 데미지 계산 시점이 왔을 때
//            if (trigger == CombatTrigger.OnCalculateOutput)
//            {
//                // 5칸 이동 조건을 만족했고, 공격 액션이고, 액션 주체가 본인일 경우
//                if (isConditionMet && context.Action.Type == ActionType.Attack && context.SourceUnit == owner)
//                {
//                    // 5% 데미지 증폭
//                    context.OutputValue *= DamageMultiplier;

//                    // 강화 효과는 한 번 사용 시 소진 (또는 턴 지속될 수도 있으나, "다음 공격" 이라는 텍스트에 따라 소진형으로 구현)
//                    isConditionMet = false;
                    
//                    Debug.Log($"[Passive:위치 선정] {owner.name}의 위치 선정 발동! 데미지 {DamageMultiplier}배 증가 적용됨.");
//                }
//            }
//        }
//    }
//}
