using DiceOrbit.Core.Pipeline;
using UnityEngine;
using static UnityEngine.UI.GridLayoutGroup;

namespace DiceOrbit.Data
{
    [System.Serializable]
    public class UnitStats : ICombatReactor
    {
        [Header("Combat Stats")]
        public int MaxHP = 50;
        public int CurrentHP = 50;
        public int Attack = 0;
        // Legacy field: fixed defense is not used in current combat rule.
        public int Defense = 0;
        public int TempArmor = 0; // 임시 방어도 (턴마다 초기화)

        // ICombatReactor implementation
        public virtual int Priority => 20;

        public virtual void OnReact(CombatTrigger trigger, CombatContext context)
        {
            //턴 시작 시 임시 방어도를 깎는다
            if (context.Action.Type == ActionType.OnStartTurn && trigger == CombatTrigger.OnPreAction)
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
            // Slay-the-Spire style:
            // 1) 고정 방어력(Defense)은 사용하지 않음
            // 2) TempArmor가 먼저 소모되고 남은 값만 HP에 적용
            int remainingDamage = Mathf.Max(0, damage);

            // 임시 방어도가 있다면 데미지를 우선 흡수
            if (TempArmor > 0 && remainingDamage > 0)
            {
                int absorbed = Mathf.Min(remainingDamage, TempArmor);
                TempArmor -= absorbed;
                remainingDamage -= absorbed;
                Debug.Log($"TempArmor absorbed {absorbed} dmg");
            }

            int actualDamage = remainingDamage;
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

