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
                source.OnSkillResolved();
                return;
            }
            
            var runtimeSkill = source.Stats.RuntimeActiveSkills[skillIndex];
            var skillData = runtimeSkill.CurrentSkillData;
            
            if (skillData == null)
            {
                source.OnSkillResolved();
                return;
            }
            
            // 2. 상태 이상 체크
            if (source.StatusEffects != null)
            {
                // TODO: Check Stun/Silence
            }
            
            // 3. 주사위 조건 확인 (CharacterSkill의 Requirement 사용)
            if (!runtimeSkill.BaseSkill.CanUse(diceValue))
            {
                Debug.LogWarning($"[SkillManager] Cannot use {skillData.SkillName}. Requirement not met.");
                source.OnSkillResolved();
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
                source.OnSkillResolved();
            }
        }
        
        /// <summary>
        /// 타겟 선택 완료 시 호출 (TargetSelector에서 호출)
        /// </summary>
        public void OnTargetSelected(Character source, Unit target, SkillData skill, int diceValue)
        {
            if (source == null || skill == null) return;
            
            ExecuteSkill(source, target, skill, diceValue);
        }
        
        /// <summary>
        /// 스킬 실제 실행
        /// </summary>
        private void ExecuteSkill(Character source, Unit target, SkillData skill, int diceValue)
        {
            var combatManager = CombatManager.Instance;
            if (combatManager == null)
            {
                source?.OnSkillResolved();
                return;
            }

            Debug.Log($"[SkillManager] Executing {skill.SkillName} via Pipeline...");

            // 2. 기본 스킬 로직 -> Pipeline Action으로 변환
            int baseDamage = skill.CalculateDamage(source.Stats.Attack, diceValue);
            Debug.Log($"[SkillManager] BaseDamage={baseDamage}, Dice={diceValue}, Skill={skill.SkillName}");
            
            // 타겟 해결
            var monsterTargets = new List<Monster>();
            var characterTargets = new List<Character>();

            ResolveTargets(target, skill.TargetType, monsterTargets, characterTargets);
            Debug.Log($"[SkillManager] Targets -> Monsters:{monsterTargets.Count}, Characters:{characterTargets.Count}");

            // 3. 파이프라인 실행
            var pipeline = Pipeline.CombatPipeline.Instance;
            if (pipeline == null)
            {
                Debug.LogWarning("[SkillManager] CombatPipeline not found, using fallback damage.");
            }
            foreach (var mTarget in monsterTargets)
            {
                var action = new Pipeline.CombatAction(skill.SkillName, Pipeline.ActionType.Attack, baseDamage);
                action.IgnoreDefense = skill.IgnoreDefense;
                action.AddTag("Skill");

                // 효과 추가
                if (skill.Effects != null)
                {
                    foreach (var eff in skill.Effects)
                    {
                        action.AddEffect(eff.Type, eff.Value, eff.Duration);
                    }
                }

                if (pipeline != null)
                {
                    var context = new Pipeline.CombatContext(source, mTarget, action);
                    pipeline.Process(context);
                }
                else
                {
                    combatManager.AttackMonster(mTarget, baseDamage, skill.IgnoreDefense);
                }
            }

            foreach (var cTarget in characterTargets)
            {
                // 아군 타겟은 주로 힐 (TargetType.Ally / Self)
                var actionType = (skill.TargetType == SkillTargetType.Self || skill.TargetType == SkillTargetType.Ally) 
                                 ? Pipeline.ActionType.Heal 
                                 : Pipeline.ActionType.Attack;

                var action = new Pipeline.CombatAction(skill.SkillName, actionType, baseDamage);
                action.AddTag("Skill");

                // 효과 추가
                if (skill.Effects != null)
                {
                    foreach (var eff in skill.Effects)
                    {
                        action.AddEffect(eff.Type, eff.Value, eff.Duration);
                    }
                }

                if (pipeline != null)
                {
                    var context = new Pipeline.CombatContext(source, cTarget, action);
                    pipeline.Process(context);
                }
                else
                {
                    if (actionType == Pipeline.ActionType.Heal)
                    {
                        cTarget.Stats.Heal(baseDamage);
                    }
                    else
                    {
                        cTarget.TakeDamage(baseDamage);
                    }
                }
            }

            // 4. 상태 이상 부여 (Status Effects)
            // 추후 Effect 부착도 Action으로 만들 수 있음.
            // Action에 포함시켜 처리했으므로 별도 호출 제거
            // ApplyStatusEffects(source, target, skill);
            source?.OnSkillResolved();
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
        /// 상태 이상 적용 로직 (모듈 처리 시 등 별도 호출용)
        /// </summary>
        private void ApplyStatusEffects(Character source, object target, SkillData skill)
        {
            if (skill.Effects == null || skill.Effects.Count == 0) return;

            var monsterTargets = new List<Monster>();
            var characterTargets = new List<Character>();

            ResolveTargets(target, skill.TargetType, monsterTargets, characterTargets);

            var pipeline = Pipeline.CombatPipeline.Instance;

            // 몬스터 타겟
            foreach (var mTarget in monsterTargets)
            {
                if (pipeline != null)
                {
                    var action = new Pipeline.CombatAction("Effect Applying", Pipeline.ActionType.Utility, 0);
                    action.AddTag("Effect");
                    foreach (var eff in skill.Effects)
                    {
                        action.AddEffect(eff.Type, eff.Value, eff.Duration);
                    }
                    var context = new Pipeline.CombatContext(source, mTarget, action);
                    pipeline.Process(context);
                }
                else
                {
                    // Fallback: 직접 매니저 호출 (임시)
                    if (mTarget.StatusEffects != null)
                    {
                         foreach (var eff in skill.Effects)
                            mTarget.StatusEffects.AddEffect(eff.Type, eff.Value, eff.Duration);
                    }
                }
            }

            // 캐릭터 타겟
            foreach (var cTarget in characterTargets)
            {
                if (pipeline != null)
                {
                    var action = new Pipeline.CombatAction("Effect Applying", Pipeline.ActionType.Utility, 0);
                    action.AddTag("Effect");
                    foreach (var eff in skill.Effects)
                    {
                        action.AddEffect(eff.Type, eff.Value, eff.Duration);
                    }
                    var context = new Pipeline.CombatContext(source, cTarget, action);
                    pipeline.Process(context);
                }
                else
                {
                     if (cTarget.StatusEffects != null)
                    {
                         foreach (var eff in skill.Effects)
                            cTarget.StatusEffects.AddEffect(eff.Type, eff.Value, eff.Duration);
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
