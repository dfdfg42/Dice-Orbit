using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.Data.Skills.Modules
{
    /// <summary>
    /// 데미지 부여 모듈
    /// </summary>
    [CreateAssetMenu(fileName = "DealDamageModule", menuName = "Dice Orbit/Skills/Modules/Deal Damage")]
    public class DealDamageModule : SkillActionModule
    {
        [Header("Damage Settings")]
        [SerializeField] private float attackMultiplier = 1.0f;
        [SerializeField] private int baseBonus = 0;
        [SerializeField] private bool ignoreDefense = false;
        
        // 추가: 주사위 계수 (주사위 값의 몇 배를 더할지)
        [SerializeField] private float diceMultiplier = 1.0f; 

        public override void Execute(Character source, GameObject target, int diceValue)
        {
            var combatManager = CombatManager.Instance;
            if (combatManager == null) return;
            
            // 데미지 계산
            // Formula: (Attack * Multiplier) + (Dice * DiceMult) + Bonus
            int attackDamage = Mathf.RoundToInt(source.Stats.Attack * attackMultiplier);
            int diceDamage = Mathf.RoundToInt(diceValue * diceMultiplier);
            int totalDamage = attackDamage + diceDamage + baseBonus;
            
            // 타겟 확인 (단일/전체 로직은 타겟 셀렉터가 이미 처리해서 단일 타겟만 넘어오거나, 
            // 전체 공격인 경우 각각 Execute가 호출될 수도 있음.
            // 하지만 현재 구조상 SkillManager는 하나의 target 객체를 넘김.
            // 전체 공격일 경우 SkillManager에서 Loop를 돌거나 여기서 CombatManager.AttackAll을 호출해야 함.
            // 보통 모듈은 단일 타겟에 대한 효과를 정의하는 것이 재사용성이 높음.
            
            var monster = target.GetComponent<Monster>();
            if (monster != null)
            {
                // 패시브 적용 (SkillManager에서 이미 했을 수도 있지만, 모듈 단독 실행 시 필요할 수 있음)
                // 하지만 SkillManager가 전역적으로 보정을 해주므로 여기선 Raw Damage 전달
                // *주의*: SkillManager의 "기본 로직"과 중복 실행되지 않도록 SkillData 설정 시 주의 필요.
                
                combatManager.AttackMonster(monster, totalDamage, ignoreDefense);
                Debug.Log($"[DealDamageModule] Executed on {monster.Stats.MonsterName}: {totalDamage} Damage");
            }
            else
            {
                // 혹시 전체 공격 타겟 타입인데 target이 null로 넘어오거나 특수한 경우?
                // SkillManager 로직상 타겟이 지정됨.
            }
        }
    }
}
