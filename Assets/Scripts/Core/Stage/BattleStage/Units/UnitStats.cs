using UnityEngine;

namespace DiceOrbit.Data
{
    [System.Serializable]
    public class UnitStats
    {
        [Header("Combat Stats")]
        public int MaxHP = 50;
        public int CurrentHP = 50;
        public int Attack = 8;
        public int Defense = 2;

        /// <summary>
        /// HP 증가
        /// </summary>
        public void Heal(int amount)
        {
            CurrentHP = Mathf.Min(CurrentHP + amount, MaxHP);
        }

        /// <summary>
        /// 데미지 받기
        /// </summary>
        public void TakeDamage(int damage)
        {
            int actualDamage = Mathf.Max(1, damage - Defense); // 최소 1 데미지
            CurrentHP = Mathf.Max(0, CurrentHP - actualDamage);

            Debug.Log($" took {actualDamage} damage! (HP: {CurrentHP}/{MaxHP})");
        }

        /// <summary>
        /// 생존 확인
        /// </summary>
        public bool IsAlive => CurrentHP > 0;

        /// <summary>
        /// HP 비율
        /// </summary>
        public float HPRatio => MaxHP > 0 ? (float)CurrentHP / MaxHP : 0f;

        /// <summary>
        /// 기본 스탯 복사 (자식 클래스에서 사용)
        /// </summary>
        protected void CopyBaseTo(UnitStats target)
        {
            target.MaxHP = this.MaxHP;
            target.CurrentHP = this.CurrentHP;
            target.Attack = this.Attack;
            target.Defense = this.Defense;
        }
    }
}

