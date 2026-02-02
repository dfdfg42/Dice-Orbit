using UnityEngine;
using DiceOrbit.Data;
using DiceOrbit.UI;
using System.Collections.Generic;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 스킬 관리자 (싱글톤)
    /// - 스킬 사용 조건 확인
    /// - 타겟 선택 요청
    /// - 스킬 효과 실행 (데미지, 상태이상, 패시브)
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
            
            // 2. 상태 이상 체크 (침묵, 기절 등)
            if (source.StatusEffects != null)
            {
                // TODO: StatusEffectType에 Stun, Silence 등이 추가되면 여기서 체크
                // currently implementing placeholder check logic or extension point
            }
            
            // 3. 주사위 조건 확인
            if (!skillData.CanUse(diceValue))
            {
                Debug.LogWarning($"[SkillManager] Cannot use {skillData.SkillName}. Requirement not met.");
                // TODO: UI에 피드백 (Floating Text 등)
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

            Debug.Log($"[SkillManager] Executing {skill.SkillName}...");
            
            // 1. 모듈형 스킬 실행 (확장성)
            if (skill.ActionModules != null && skill.ActionModules.Count > 0)
            {
                foreach (var module in skill.ActionModules)
                {
                    if (module != null)
                    {
                        // Note: Assuming Module.Execute takes (source, target, dice)
                        // If modules need GameObject target, cast 'target' accordingly
                        GameObject targetObj = GetTargetGameObject(target);
                        if(targetObj != null) 
                             module.Execute(source, targetObj, diceValue);
                    }
                }
                // 모듈이 있으면 기본 로직을 건너뛸지, 같이 실행할지 결정. 
                // 여기서는 모듈이 주 로직이면 리턴하도록 구현 가능하나, 
                // 기존 데이터 호환성을 위해 기본 데미지 로직도 실행
            }

            // 2. 기본 데미지/힐 계산
            int baseDamage = skill.CalculateDamage(source.Stats.Attack, diceValue);
            int finalDamage = baseDamage;
            
            var monsterTarget = ResolveMonsterTarget(target);
            var characterTarget = ResolveCharacterTarget(target); // 아군 타겟
            
            // 3. 패시브: 공격 전 (OnBeforeAttack)
            if (monsterTarget != null && source.Passives != null)
            {
                source.Passives.OnBeforeAttack(monsterTarget, ref finalDamage);
            }
            
            // 4. 효과 적용 (데미지 / 힐)
            switch (skill.TargetType)
            {
                case SkillTargetType.SingleEnemy:
                    if (monsterTarget != null)
                    {
                        combatManager.AttackMonster(monsterTarget, finalDamage, skill.IgnoreDefense);
                        Debug.Log($"-> Dealt {finalDamage} damage to {monsterTarget.Stats.MonsterName}");
                    }
                    break;
                    
                case SkillTargetType.AllEnemies:
                    combatManager.AttackAllMonsters(finalDamage, skill.IgnoreDefense);
                    Debug.Log($"-> Dealt {finalDamage} damage to ALL enemies");
                    break;
                    
                case SkillTargetType.Self:
                    source.Stats.Heal(finalDamage);
                    Debug.Log($"-> Healed self for {finalDamage}");
                    break;
                    
                case SkillTargetType.Ally:
                    if (characterTarget != null)
                    {
                        characterTarget.Stats.Heal(finalDamage);
                        Debug.Log($"-> Healed {characterTarget.Stats.CharacterName} for {finalDamage}");
                    }
                    break;
            }
            
            // 5. 상태 이상 부여 (Status Effects)
            ApplyStatusEffects(source, target, skill);
            
            // 6. 패시브: 공격 후 (OnAfterAttack)
            if (monsterTarget != null && source.Passives != null)
            {
                source.Passives.OnAfterAttack(monsterTarget);
            }
            
            // 7. 자원 소모 / 쿨타임 처리 (필요 시)
        }
        
        /// <summary>
        /// 상태 이상 적용 로직
        /// </summary>
        private void ApplyStatusEffects(Character source, object target, SkillData skill)
        {
            // SkillData에 Effects 리스트가 있다고 가정 (기존 코드 참조)
            if (skill.Effects == null || skill.Effects.Count == 0) return;
            
            var monsterTarget = ResolveMonsterTarget(target);
            var characterTarget = ResolveCharacterTarget(target);
            
            foreach (var effectData in skill.Effects)
            {
                // 몬스터에게 적용
                // TODO: 몬스터에게도 StatusEffectManager가 추가되어야 함. 
                // 현재는 Character에만 StatusEffectManager가 있는 것으로 보임.
                // 몬스터 시스템 확장 시 이곳 구현 필요.
                
                // 아군/자신에게 적용 (버프 등)
                if (characterTarget != null && characterTarget.StatusEffects != null)
                {
                    // 예시: effectData를 파싱하여 적용
                    // characterTarget.StatusEffects.AddEffect(effectData.Type, effectData.Value, effectData.Duration);
                }
            }
        }

        private GameObject GetTargetGameObject(object target)
        {
             if (target is MonoBehaviour mb) return mb.gameObject;
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
