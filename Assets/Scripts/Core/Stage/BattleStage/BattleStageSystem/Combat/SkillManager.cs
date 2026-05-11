using UnityEngine;
using DiceOrbit.Data;
using System.Collections.Generic;
using DiceOrbit.Data.Skills;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 스킬 관리자 (싱글톤)
    /// - 스킬 사용 조건 확인
    /// - 타겟 선택 요청
    /// - 스킬 실행 (ActiveTemplate 경로 단일화)
    /// </summary>
    public class SkillManager : MonoBehaviour
    {
        public static SkillManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
        }

        /// <summary>
        /// 스킬 사용 준비 (UI에서 호출)
        /// </summary>
        public void PrepareSkill(Character source, int skillIndex, DiceData dice)
        {
            if (source == null) return;

            // 1. 유효성 검사
            if (skillIndex < 0 || skillIndex >= source.Stats.ActiveAbilityCount)
            {
                Debug.LogWarning($"[SkillManager] Invalid skill index: {skillIndex}");
                source.OnSkillResolved();
                return;
            }

            RuntimeAbility runtimeAbility = source.Stats.GetActiveAbilityByIndex(skillIndex);
            if (runtimeAbility?.BaseSkill == null)
            {
                source.OnSkillResolved();
                return;
            }

            // 2. 상태 이상 체크
            if (source.StatusEffects != null)
            {
                // TODO: Check Stun/Silence
            }

            // 3. 주사위 조건 확인
            if (!runtimeAbility.BaseSkill.CanUse(dice.Value))
            {
                Debug.LogWarning($"[SkillManager] Cannot use {runtimeAbility.BaseSkill.SkillName}. Requirement not met.");
                source.OnSkillResolved();
                return;
            }

            Debug.Log($"[SkillManager] Preparing {runtimeAbility.BaseSkill.SkillName} for {source.Stats.CharacterName}");

            // 4. TargetType.None: 타겟 없이 즉시 실행
            if (runtimeAbility.TargetType == CharacterSkillTargetType.None)
            {
                ExecuteNoTarget(source, runtimeAbility, dice.Value);
                return;
            }

            // 5. 타겟 선택 시작
            var targetSelector = SkillTargetSelector.Instance;
            if (targetSelector != null)
            {
                targetSelector.StartTargetSelection(source, runtimeAbility, dice);
            }
            else
            {
                Debug.LogError("[SkillManager] SkillTargetSelector not found!");
                source.OnSkillResolved();
            }
        }

        private void ExecuteNoTarget(Character source, RuntimeAbility runtimeAbility, int diceValue)
        {
            source.OnSkillExecutionStarted();
            var activeTemplate = runtimeAbility.BaseSkill?.ActiveTemplate;
            if (activeTemplate != null)
                activeTemplate.Execute(source, runtimeAbility, new List<Unit>(), new List<TileData>(), diceValue);
            source.OnSkillResolved();
        }

        /// <summary>
        /// 타겟 선택 완료 시 호출 (TargetSelector에서 호출)
        /// </summary>
        public void OnTargetSelected(Character source, Unit target, RuntimeAbility runtimeAbility, int diceValue)
        {
            if (source == null || runtimeAbility == null) return;
            ExecuteTargetingSkill(source, target, runtimeAbility, diceValue);
        }

        /// <summary>
        /// 스킬 실제 실행 — ActiveTemplate 단일 경로
        /// </summary>
        private void ExecuteTargetingSkill(Character source, Unit target, RuntimeAbility runtimeAbility, int diceValue)
        {
            if (runtimeAbility?.BaseSkill?.ActiveTemplate == null)
            {
                Debug.LogWarning($"[SkillManager] No ActiveTemplate on skill '{runtimeAbility?.BaseSkill?.SkillName}'. Skill not executed.");
                source.OnSkillResolved();
                return;
            }

            source.OnSkillExecutionStarted();

            var targets = ResolveTargetsByType(source, target, runtimeAbility.TargetType);
            runtimeAbility.BaseSkill.ActiveTemplate.Execute(source, runtimeAbility, targets, new List<TileData>(), diceValue);

            source.OnSkillResolved();
        }

        private List<Unit> ResolveTargetsByType(Character source, Unit initialTarget, CharacterSkillTargetType type)
        {
            var resolved = new List<Unit>();
            switch (type)
            {
                case CharacterSkillTargetType.OneEnemy:
                    if (initialTarget != null) resolved.Add(initialTarget);
                    break;
                case CharacterSkillTargetType.None:
                    break;
                default:
                    break;
            }
            return resolved;
        }
    }
}
