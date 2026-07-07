using UnityEngine;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// 포션 효과 종류. 포션 = 1회성 순간 개입, 중심축은 주사위 조작 (스펙 §5).
    /// </summary>
    public enum PotionEffectType
    {
        HealLowestAlly,   // 가장 다친 아군 +N 회복
        HealParty,        // 파티 전원 +N 회복
        RerollDice,       // 남은(미사용) 주사위 전부 재굴림 — 전투 중에만
        CleanseParty,     // 파티 이동 디버프/속박 해제
    }

    [CreateAssetMenu(fileName = "Potion", menuName = "DiceOrbit/Potion Definition")]
    public class PotionDefinition : ScriptableObject
    {
        public string PotionName = "물약";
        [TextArea(2, 4)] public string Description = "";
        public Sprite Icon;
        public PotionEffectType EffectType;
        public int Value;
        [Min(1)] public int ShopPrice = 40;
        [Tooltip("전투 중에만 사용 가능 (주사위 조작류)")]
        public bool CombatOnly;
    }
}
