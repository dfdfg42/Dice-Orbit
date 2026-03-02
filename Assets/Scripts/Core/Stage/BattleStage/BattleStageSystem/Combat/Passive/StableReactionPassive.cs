//using UnityEngine;
//using DiceOrbit.Core;
//using DiceOrbit.Core.Pipeline;

//namespace DiceOrbit.Data.Passives
//{
//    [CreateAssetMenu(fileName = "StableReactionPassive", menuName = "Dice Orbit/Passives/Stable Reaction (Alchemist)")]
//    public class StableReactionPassive : PassiveAbility
//    {
//        [Header("Stable Reaction Settings")]
//        [Tooltip("증가할 데미지 배율 (예: 1.1 = 10% 증가)")]
//        public float DamageMultiplier = 1.1f;
//        [Tooltip("체력 비율 기준점 (0.0 ~ 1.0)")]
//        public float HealthThresholdRatio = 0.6f;

//        public override int Priority => 98;

//        public override void OnReact(CombatTrigger trigger, CombatContext context)
//        {
//            // 데미지 계산 시점이 왔을 때
//            if (trigger == CombatTrigger.OnCalculateOutput)
//            {
//                // 공격 액션이고, 액션 주체가 본인일 경우
//                if (context.Action.Type == ActionType.Attack && context.SourceUnit == owner && owner.Stats != null)
//                {
//                    // (현재 체력 / 최대 체력) 비율 계산
//                    float currentHPRatio = (float)owner.Stats.CurrentHP / owner.Stats.MaxHP;

//                    if (currentHPRatio >= HealthThresholdRatio)
//                    {
//                        // 10% 데미지 증폭
//                        context.OutputValue *= DamageMultiplier;
                        
//                        Debug.Log($"[Passive:안정 반응] {owner.name}의 체력이 60% 이상! 데미지 {DamageMultiplier}배 증가 적용됨.");
//                    }
//                }
//            }
//        }
//    }
//}
