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
        public void OnTargetSelected(Character source, Unit target, CharacterSkillData skill, int diceValue)
        {
            if (source == null || skill == null) return;

            ExecuteTargetingSkill(source, target, skill, diceValue);
        }
        
        /// <summary>
        /// 스킬 실제 실행
        /// </summary>
        private void ExecuteTargetingSkill(Character source, Unit target, CharacterSkillData skill, int diceValue)
        {
            List<Unit> targets = new List<Unit> { target };
            skill.Execute(source, targets, new List<TileData>(), diceValue);
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
