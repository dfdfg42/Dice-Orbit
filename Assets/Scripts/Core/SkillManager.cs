using UnityEngine;
using DiceOrbit.Data;
using DiceOrbit.UI;
using System.Collections.Generic;
using DiceOrbit.Core.Pipeline; // Pipeline Namespace

namespace DiceOrbit.Core
{
    /// <summary>
    /// 스킬 관리자 (싱글톤)
    /// - 스킬 사용 조건 확인
    /// - 타겟 선택 요청
    /// - 스킬 실행 (파이프라인 위임)
    /// </summary>
    public class SkillManager : MonoBehaviour
    {
        public static SkillManager Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// 스킬 사용 준비 (UI에서 호출)
        /// </summary>
        public void PrepareSkill(Character source, int skillIndex, int diceValue)
        {
            if (source == null) return;
            
            // 1. 유효성 검사
            if (skillIndex < 0 || skillIndex >= source.Stats.RuntimeActiveSkills.Count)
            {
                Debug.LogWarning($"[SkillManager] Invalid skill index: {skillIndex}");
                return;
            }
            
            var runtimeSkill = source.Stats.RuntimeActiveSkills[skillIndex];
            var skillData = runtimeSkill.ToSkillData();
            
            if (skillData == null) return;
            
            // 2. 상태 이상 체크
            if (source.StatusEffects != null)
            {
                // TODO: Check Stun/Silence
            }
            
            // 3. 주사위 조건 확인
            if (!skillData.CanUse(diceValue))
            {
                Debug.LogWarning($"[SkillManager] Cannot use {skillData.SkillName}. Requirement not met.");
                return;
            }
            
            Debug.Log($"[SkillManager] Preparing {skillData.SkillName} for {source.Stats.CharacterName}");
            
            // 4. 타겟 선택 시작
            var targetSelector = SkillTargetSelector.Instance;
            if (targetSelector != null)
            {
                targetSelector.StartTargetSelection(source, skillData, diceValue);
            }
            else
            {
                Debug.LogError("[SkillManager] SkillTargetSelector not found!");
            }
        }
        
        /// <summary>
        /// 타겟 선택 완료 시 호출 (TargetSelector에서 호출)
        /// </summary>
        public void OnTargetSelected(Character source, object target, SkillData skill, int diceValue)
        {
            if (source == null || skill == null) return;
            
            ExecuteSkill(source, target, skill, diceValue);
        }
        
        /// <summary>
        /// 스킬 실제 실행
        /// </summary>
        private void ExecuteSkill(Character source, object target, SkillData skill, int diceValue)
        {
            var combatManager = CombatManager.Instance;
            if (combatManager == null) return;

            Debug.Log($"[SkillManager] Executing {skill.SkillName} via Pipeline...");

            // 1. 모듈 실행 (모듈 내부 동작)
            bool handledByModules = ExecuteModules(source, target, skill, diceValue);
            
            // 모듈이 처리했어도 기본 데미지가 있다면 파이프라인 태울 수 있음.
            // 여기서는 기존 로직대로 모듈 처리 시 종료
            if (handledByModules)
            {
                ApplyStatusEffects(source, target, skill);
                return;
            }

            // 2. 기본 스킬 로직 -> Pipeline Action으로 변환
            int baseDamage = skill.CalculateDamage(source.Stats.Attack, diceValue);
            
            // 타겟 해결
            var monsterTargets = new List<Monster>();
            var characterTargets = new List<Character>();

            ResolveTargets(target, skill.TargetType, monsterTargets, characterTargets);

            // 3. 파이프라인 실행
            foreach (var mTarget in monsterTargets)
            {
                var action = new Pipeline.CombatAction(skill.SkillName, Pipeline.ActionType.Attack, baseDamage);
                action.IgnoreDefense = skill.IgnoreDefense;
                action.AddTag("Skill");

                var context = new Pipeline.CombatContext(source, mTarget, action);
                Pipeline.CombatPipeline.Instance.Process(context);
            }

            foreach (var cTarget in characterTargets)
            {
                // 아군 타겟은 주로 힐 (TargetType.Ally / Self)
                var actionType = (skill.TargetType == SkillTargetType.Self || skill.TargetType == SkillTargetType.Ally) 
                                 ? Pipeline.ActionType.Heal 
                                 : Pipeline.ActionType.Attack;

                var action = new Pipeline.CombatAction(skill.SkillName, actionType, baseDamage);
                action.AddTag("Skill");
                
                var context = new Pipeline.CombatContext(source, cTarget, action);
                Pipeline.CombatPipeline.Instance.Process(context);
            }

            // 4. 상태 이상 부여 (Status Effects)
            // 추후 Effect 부착도 Action으로 만들 수 있음.
            ApplyStatusEffects(source, target, skill);
        }
        
        private void ResolveTargets(object initialTarget, SkillTargetType type, List<Monster> mList, List<Character> cList)
        {
            if (type == SkillTargetType.AllEnemies)
            {
                mList.AddRange(CombatManager.Instance.GetAliveMonsters());
            }
            else if (type == SkillTargetType.AllAllies)
            {
                cList.AddRange(PartyManager.Instance.GetAliveCharacters());
            }
            else
            {
                // Single Target
                if (initialTarget is Monster m) mList.Add(m);
                else if (initialTarget is Character c) cList.Add(c);
                else if (initialTarget is GameObject go)
                {
                    var mC = go.GetComponentInParent<Monster>();
                    if (mC) mList.Add(mC);
                    var cC = go.GetComponentInParent<Character>();
                    if (cC) cList.Add(cC);
                }
            }
        }
        
        /// <summary>
        /// 상태 이상 적용 로직 (Legacy Wrapper or Pipeline Action)
        /// </summary>
        private void ApplyStatusEffects(Character source, object target, SkillData skill)
        {
            if (skill.Effects == null || skill.Effects.Count == 0) return;

            // EffectManager는 "즉발 효과(IEffect)" 레지스트리임.
            // StatusEffect는 "지속 효과".
            // SkillData.Effects가 무엇을 담고 있는지에 따라 다름.
            // EffectManager.ApplyEffects는 IEffect를 찾아서 Apply.
            // 만약 지속효과(Buff)라면 IEffect 구현체(BuffAttackEffect 등)가 StatusEffectManager를 호출해야 함.
            
            // 기존 EffectManager 사용 유지
            // (가장 깔끔한 방법: EffectManager의 IEffect 구현체를 수정해서 StatusEffectManager를 호출하게 만듦)
            
            // 여기서는 타겟 분기 후 적용
            if (target is Monster mTarget)
            {
                 Systems.EffectManager.ApplyEffects(skill.Effects, mTarget);
            }
            else if (target is Character cTarget)
            {
                 Systems.EffectManager.ApplyEffects(skill.Effects, cTarget);
            }
            else if (skill.TargetType == SkillTargetType.AllEnemies)
            {
                 foreach(var m in CombatManager.Instance.GetAliveMonsters())
                     Systems.EffectManager.ApplyEffects(skill.Effects, m);
            }
            // ... 생략된 타겟 타입 처리
        }

        private bool ExecuteModules(Character source, object target, SkillData skill, int diceValue)
        {
            if (skill.ActionModules == null || skill.ActionModules.Count == 0) return false;

            if (skill.TargetType == SkillTargetType.AllEnemies)
            {
                var combatManager = CombatManager.Instance;
                if (combatManager != null)
                {
                    foreach (var monster in combatManager.GetAliveMonsters())
                    {
                        ExecuteModulesOnTarget(source, monster, skill, diceValue);
                    }
                }
                return true;
            }

            if (skill.TargetType == SkillTargetType.AllAllies)
            {
                var partyManager = PartyManager.Instance;
                if (partyManager != null)
                {
                    foreach (var ally in partyManager.GetAliveCharacters())
                    {
                        ExecuteModulesOnTarget(source, ally, skill, diceValue);
                    }
                }
                return true;
            }

            ExecuteModulesOnTarget(source, target, skill, diceValue);
            return true;
        }

        private void ExecuteModulesOnTarget(Character source, object target, SkillData skill, int diceValue)
        {
            foreach (var module in skill.ActionModules)
            {
                if (module != null)
                {
                    GameObject targetObj = GetTargetGameObject(target);
                    if (targetObj != null)
                    {
                        module.Execute(source, targetObj, diceValue);
                    }
                }
            }
        }

        private GameObject GetTargetGameObject(object target)
        {
             if (target is MonoBehaviour mb) return mb.gameObject;
             if (target is GameObject go) return go;
             return null;
        }

        private Monster ResolveMonsterTarget(object target)
        {
            if (target is Monster monster) return monster;
            if (target is GameObject go) return go.GetComponentInParent<Monster>();
            if (target is MonoBehaviour mb) return mb.GetComponentInParent<Monster>();
            return null;
        }

        private Character ResolveCharacterTarget(object target)
        {
            if (target is Character character) return character;
            if (target is GameObject go) return go.GetComponentInParent<Character>();
            if (target is MonoBehaviour mb) return mb.GetComponentInParent<Character>();
            return null;
        }
    }
}
