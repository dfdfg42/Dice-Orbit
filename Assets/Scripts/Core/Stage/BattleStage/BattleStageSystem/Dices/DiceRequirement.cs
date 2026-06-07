using UnityEngine;

namespace DiceOrbit.Data
{
    /// <summary>
    /// 주사위 패턴
    /// </summary>
    public enum DicePattern
    {
        None,       // 제한 없음
        Even,       // 짝수만
        Odd,        // 홀수만
        High,       // 4 이상
        Low         // 3 이하
    }

    /// <summary>
    /// 주사위 요구사항
    /// </summary>
    [System.Serializable]
    public class DiceRequirement
    {
        public int MinDiceCount = 1;        // 필요한 최소 주사위 개수 (나중에 구현)
        public int MinDiceValue = 1;        // 최소 주사위 값

        // Unity는 int? 를 직렬화하지 못하므로 sentinel(0 = 미사용) int 필드로 저장하고
        // 기존 호출부(.HasValue/.Value)와 호환되도록 nullable 프로퍼티로 노출한다.
        [Tooltip("최대 주사위 값. 0이면 상한 없음")]
        [SerializeField] private int maxDiceValue = 0;
        [Tooltip("정확한 주사위 값. 0이면 미사용")]
        [SerializeField] private int exactDiceValue = 0;

        public DicePattern Pattern = DicePattern.None; // 패턴

        public int? MaxDiceValue   => maxDiceValue   > 0 ? maxDiceValue   : (int?)null;
        public int? ExactDiceValue => exactDiceValue > 0 ? exactDiceValue : (int?)null;

        /// <summary>
        /// 주사위 값이 요구사항을 만족하는지 확인
        /// </summary>
        public bool CanUse(int diceValue)
        {
            // 정확한 값 체크
            if (ExactDiceValue.HasValue)
            {
                return diceValue == ExactDiceValue.Value;
            }
            
            // 최소 값 체크
            if (diceValue < MinDiceValue)
            {
                return false;
            }

            // 최대 값 체크
            if (MaxDiceValue.HasValue && diceValue > MaxDiceValue.Value)
            {
                return false;
            }
            
            // 패턴 체크
            switch (Pattern)
            {
                case DicePattern.Even:
                    return diceValue % 2 == 0;
                case DicePattern.Odd:
                    return diceValue % 2 == 1;
                case DicePattern.High:
                    return diceValue >= 4;
                case DicePattern.Low:
                    return diceValue <= 3;
                case DicePattern.None:
                default:
                    return true;
            }
        }
        
        /// <summary>
        /// 요구사항 설명
        /// </summary>
        public string GetDescription()
        {
            if (ExactDiceValue.HasValue)
            {
                return $"Requires dice value: {ExactDiceValue.Value}";
            }
            
            string desc = $"Min value: {MinDiceValue}";

            if (MaxDiceValue.HasValue)
            {
                desc += $", Max value: {MaxDiceValue.Value}";
            }
            
            if (Pattern != DicePattern.None)
            {
                desc += $", {Pattern} only";
            }
            
            return desc;
        }
    }
}
