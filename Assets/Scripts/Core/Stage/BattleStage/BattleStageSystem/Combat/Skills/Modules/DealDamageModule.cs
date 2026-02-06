using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Skills.Modules
{
    [CreateAssetMenu(fileName = "Deal Damage Module", menuName = "Dice Orbit/Skills/Modules/Deal Damage")]
    public class DealDamageModule : SkillActionModule
    {
    [Header("Damage Settings")]
    public int BaseDamage;
    public float AttackMultiplier = 1f;
    public float DiceMultiplier = 1f; // 주사위 값 계수
    public bool IgnoreDefense;

        public override void Execute(Character source, GameObject targetObj, int diceValue)
        {
            if (source == null || targetObj == null) return;

            // 타겟 식별
            var monster = targetObj.GetComponentInParent<Monster>();
            var character = targetObj.GetComponentInParent<Character>();
            
            int finalBaseDamage = BaseDamage + Mathf.RoundToInt(diceValue * DiceMultiplier);
            // 소스 공격력 추가 로직은? 보통 SkillData에서 처리하지만, 모듈 단독일 경우 여기서 계산
            // ActionModule은 SkillData 맥락을 잘 모름 (Signature가 Execute(source, target, dice) 뿐)
            // 따라서 심플하게 계산하거나, Source.Stats.Attack을 여기서 더해줄 수도 있음.
            // 일단은 BaseDamage + Dice 만 계산하여 파이프라인에 태움.
            // (만약 Source.Stats.Attack을 반영해야 한다면 여기서 더해야 함)
            
            if (source.Stats != null)
            {
                finalBaseDamage += Mathf.RoundToInt(source.Stats.Attack * AttackMultiplier);
            }

            Unit target = null;
            if (monster != null) target = monster;
            else if (character != null) target = character;

            if (target != null)
            {
                var action = new CombatAction("Module Damage", Core.Pipeline.ActionType.Attack, finalBaseDamage);
                action.IgnoreDefense = IgnoreDefense;
                action.AddTag("Module");
                
                var context = new CombatContext(source, target, action);
                CombatPipeline.Instance.Process(context);
                
                Debug.Log($"[Module] DealDamage processed via Pipeline. Base: {finalBaseDamage}");
            }
        }
    }
}
