using UnityEngine;
using DiceOrbit.Core;   // Character, GoldManager, SubclassPickerAttribute

namespace DiceOrbit.Data
{
    /// <summary>주사위 사용 확정 시 넘어오는 컨텍스트 (최소).</summary>
    public class DieUseContext
    {
        public Character User;      // 이 주사위를 배정해 행동한 캐릭터 (null 가능)
        public int RolledValue;     // 이번에 굴려 나온 면 값
    }

    /// <summary>
    /// 주사위 '사용 시 효과' — 이벤트 결과(EventOutcome)/유물과 같은 [SerializeReference] 다형성.
    /// 새 효과 = 이 클래스를 상속한 클래스 하나 추가.
    /// </summary>
    [System.Serializable]
    public abstract class DieEffect
    {
        public Sprite Icon;                              // 호버 효과 행 아이콘(선택)
        public abstract string Apply(DieUseContext ctx); // 발동 + 사람이 읽을 요약 반환
        public virtual string Preview() => "";           // 호버 라벨용 짧은 설명
    }

    /// <summary>사용 시 골드 +N.</summary>
    [System.Serializable]
    public class GainGoldOnUse : DieEffect
    {
        public int amount = 20;
        public override string Apply(DieUseContext ctx) { GoldManager.EnsureInstance().AddGold(amount); return $"골드 +{amount}"; }
        public override string Preview() => $"골드 +{amount}";
    }

    /// <summary>사용 시 사용한 캐릭터 HP +N.</summary>
    [System.Serializable]
    public class HealUserOnUse : DieEffect
    {
        public int amount = 5;
        public override string Apply(DieUseContext ctx)
        {
            var u = ctx?.User;
            if (u != null && u.IsAlive && u.Stats != null)
                u.Stats.CurrentHP = Mathf.Min(u.Stats.MaxHP, u.Stats.CurrentHP + amount);
            return $"HP +{amount}";
        }
        public override string Preview() => $"HP +{amount}";
    }
}
