using UnityEngine;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// 유물 효과 종류. 유물 = 파티 전역 상시, "런의 규칙"을 바꾼다 (스펙 §5).
    /// 효과 적용은 소비처가 RelicManager에 질의하는 풀 방식 (패시브의 프로퍼티 조회 패턴과 동일).
    /// </summary>
    public enum RelicEffectType
    {
        ShopDiscountPercent,     // 상점 가격 -N%
        RestHealBonusPercent,    // 휴식 회복 비율 +N%p
        BattleGoldBonusFlat,     // 전투 보상 골드 +N
        ReviveHpBonusPercent,    // 점감 부활 HP +N%p
        BattleStartHealFlat,     // 전투 시작 시 파티 전원 +N 회복
    }

    [CreateAssetMenu(fileName = "Relic", menuName = "DiceOrbit/Relic Definition")]
    public class RelicDefinition : ScriptableObject
    {
        public string RelicName = "유물";
        [TextArea(2, 4)] public string Description = "";
        public Sprite Icon;
        public RelicEffectType EffectType;
        public float Value;
        [Min(1)] public int ShopPrice = 120;
    }
}
