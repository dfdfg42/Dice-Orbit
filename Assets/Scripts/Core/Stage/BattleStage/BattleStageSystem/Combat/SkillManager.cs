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
            if (skillIndex < 0 || skillIndex >= source.Stats.ActiveAbilityCount)
            {
                Debug.LogWarning($"[SkillManager] Invalid skill index: {skillIndex}");
                source.OnSkillResolved();
                return;
            }
            
            RuntimeAbility runtimeAbility = source.Stats.GetActiveAbilityByIndex(skillIndex);
            // CurrentSkillData는 RuntimeAbility.CurrentLevel 기준으로 계산됩니다.
            var skillData = runtimeAbility?.CurrentSkillData;
            
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
            if (runtimeAbility.BaseSkill == null || !runtimeAbility.BaseSkill.CanUse(diceValue))
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
                targetSelector.StartTargetSelection(source, runtimeAbility, diceValue);
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
        public void OnTargetSelected(Character source, Unit target, RuntimeAbility runtimeAbility, int diceValue)
        {
            if (source == null || runtimeAbility == null) return;

            ExecuteTargetingSkill(source, target, runtimeAbility, diceValue);
        }
        
        /// <summary>
        /// 스킬 실제 실행
        /// </summary>
        private void ExecuteTargetingSkill(Character source, Unit target, RuntimeAbility runtimeAbility, int diceValue)
        {
            var skill = runtimeAbility?.CurrentSkillData;
            if (skill == null) return;

            source.OnSkillExecutionStarted();

            var targets = ResolveTargetsByType(source, target, skill.skillTargetType);
            var targetTiles = ResolveTargetTiles(source, skill);

            bool executedByTemplate = runtimeAbility.BaseSkill?.ActiveTemplate != null
                && runtimeAbility.BaseSkill.ActiveTemplate.Execute(source, runtimeAbility, targets, targetTiles, diceValue);

            if (!executedByTemplate)
            {
                // 하위 호환: ActiveTemplate 미설정 스킬은 기존 Effect 순회 경로를 유지합니다.
                skill.Execute(source, targets, targetTiles, diceValue);
            }

            source.OnSkillResolved();
        }

        private List<Unit> ResolveTargetsByType(Character source, Unit initialTarget, SkillTargetType type)
        {
            var resolved = new List<Unit>();

            switch (type)
            {
                case SkillTargetType.SingleEnemy:
                case SkillTargetType.Ally:
                    if (initialTarget != null && initialTarget.IsAlive)
                    {
                        resolved.Add(initialTarget);
                    }
                    break;
                case SkillTargetType.AllEnemies:
                    var enemies = CombatManager.Instance?.GetAliveMonsters();
                    if (enemies != null)
                    {
                        foreach (var enemy in enemies)
                        {
                            if (enemy != null && enemy.IsAlive)
                            {
                                resolved.Add(enemy);
                            }
                        }
                    }
                    break;
                case SkillTargetType.Self:
                    if (source != null && source.IsAlive)
                    {
                        resolved.Add(source);
                    }
                    break;
                case SkillTargetType.AllAllies:
                    var allies = PartyManager.Instance?.GetAliveCharacters();
                    if (allies != null)
                    {
                        foreach (var ally in allies)
                        {
                            if (ally != null && ally.IsAlive && ally != source)
                            {
                                resolved.Add(ally);
                            }
                        }
                    }
                    break;
                case SkillTargetType.Tiles:
                    break;
            }

            return resolved;
        }

        private List<TileData> ResolveTargetTiles(Character source, CharacterSkillData skill)
        {
            var tiles = new List<TileData>();
            if (skill?.Effects == null) return tiles;

            foreach (var effect in skill.Effects)
            {
                if (effect == null) continue;
                var previewTiles = effect.GetTargetTilesPreview(source);
                if (previewTiles == null || previewTiles.Count == 0) continue;

                foreach (var tile in previewTiles)
                {
                    if (tile == null || tiles.Contains(tile)) continue;
                    tiles.Add(tile);
                }
            }

            return tiles;
        }
    }
}
