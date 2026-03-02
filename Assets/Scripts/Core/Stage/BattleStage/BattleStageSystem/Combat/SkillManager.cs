using UnityEngine;
using DiceOrbit.Data;
using System.Collections.Generic;

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
            var targets = ResolveTargetsByType(source, target, skill.skillTargetType);
            var targetTiles = ResolveTargetTiles(source, skill);
            skill.Execute(source, targets, targetTiles, diceValue);
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
