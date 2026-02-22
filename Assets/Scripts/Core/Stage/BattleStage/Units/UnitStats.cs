using UnityEngine;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data
{
    [System.Serializable]
    public class UnitStats : ICombatReactor
    {
        [Header("Combat Stats")]
        public int MaxHP = 50;
        public int CurrentHP = 50;
        public int Attack = 8;
        public int Defense = 2;
        [HideInInspector] public int TempArmor = 0; // 임시 방어도 (턴마다 초기화)

        // ICombatReactor implementation
        public virtual int Priority => 20;

        public virtual void OnReact(CombatTrigger trigger, CombatContext context)
        {
            //턴 시작 시 임시 방어도를 깎는다
            if (trigger == CombatTrigger.OnTurnStart)
            {
                TempArmor = 0;
            }
        }

        /// <summary>
        /// HP 증가
        /// </summary>
        public void Heal(int amount)
        {
            CurrentHP = Mathf.Min(CurrentHP + amount, MaxHP);
        }

        /// <summary>
        /// 데미지 받기, 받은 데미지만큼 리턴
        /// </summary>
        public int TakeDamage(int damage)
        {
            int actualDamage = Mathf.Max(1, damage - Defense); // 최소 1 데미지

            // 임시 방어도가 있다면 데미지를 우선 흡수
            if (TempArmor > 0)
            {
                int absorbed = Mathf.Min(actualDamage, TempArmor);
                TempArmor -= absorbed;
                actualDamage -= absorbed;
                Debug.Log($"TempArmor absorbed {absorbed} dmg");
            }
            
            CurrentHP = Mathf.Max(0, CurrentHP - actualDamage);
            Debug.Log($" took {actualDamage} damage! (HP: {CurrentHP}/{MaxHP})");
            return actualDamage;
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

