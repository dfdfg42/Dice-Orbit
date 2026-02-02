using UnityEngine;

namespace DiceOrbit.Data.Dice
{
    [CreateAssetMenu(fileName = "New Dice Config", menuName = "Dice Orbit/Dice Config")]
    public class DiceConfig : ScriptableObject
    {
        public string ConfigName;
        public int MinValue = 1;
        public int MaxValue = 6;
        
        [Header("Filters")]
        public bool OnlyOdd = false;
        public bool OnlyEven = false;
        
        [Header("Shop")]
        public int Price = 100;
        public Sprite Icon;

        /// <summary>
        /// 설정에 따른 랜덤 값 생성
        /// </summary>
        public int GetRandomValue()
        {
            if (OnlyOdd)
            {
                // 홀수만: 1, 3, 5...
                while(true)
                {
                    int val = Random.Range(MinValue, MaxValue + 1);
                    if (val % 2 != 0) return val;
                }
            }
            else if (OnlyEven)
            {
                // 짝수만: 2, 4, 6...
                while(true)
                {
                    int val = Random.Range(MinValue, MaxValue + 1);
                    if (val % 2 == 0) return val;
                }
            }
            else
            {
                return Random.Range(MinValue, MaxValue + 1);
            }
        }
    }
}
