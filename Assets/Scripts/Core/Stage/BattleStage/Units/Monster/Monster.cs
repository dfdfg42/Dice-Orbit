using UnityEngine;
using DiceOrbit.Data;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DiceOrbit.UI;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 몬스터 (중앙 구역)
    /// AI + Skills + Managers 통합 구현
    /// </summary>
    public class Monster : Unit<MonsterStats>, UI.IHoverTooltipProvider
    {
        [Header("Preset")]
        [SerializeField] private Data.Monsters.MonsterPreset preset;

        [Header("AI")]
        [SerializeField] private Data.MonsterAI.MonsterAI aiPattern; // Inspector 설정 전용 (원본 참조)
        private Data.MonsterAI.MonsterAI runtimeAiPattern; // 실제 실행되는 런타임 인스턴스
        private MonsterSkill nextSkill;
        private SkillData nextSkillData=>nextSkill.skillData; // 다음 턴에 사용할 스킬
        public SkillData CurrentIntent => nextSkillData;

        // Target Logic
        private Character targetedCharacter;
        private Data.TileData[] targetedTiles;

        // MonsterStats 타입으로 반환 (기존 코드 호환성 유지)
        public new MonsterStats Stats => stat;
        
        protected override void Awake()
        {
            if (stat == null)
            {
                stat = new MonsterStats();
            }

            base.Awake();
            EnsureHoverCollider();

            // Systems 초기화
            passives = GetComponent<Systems.Passives.PassiveManager>();
            if (passives == null) passives = gameObject.AddComponent<Systems.Passives.PassiveManager>();
            passives.Initialize(this);

            statusEffects = GetComponent<Systems.Effects.StatusEffectManager>();
            if (statusEffects == null) statusEffects = gameObject.AddComponent<Systems.Effects.StatusEffectManager>();
            statusEffects.Initialize(this);
        }
        
        private void Start()
        {
            // Preset이 Inspector에 할당되어 있다면 바로 초기화
            if (preset != null)
            {
                InitializeFromPreset(preset);
            }
            
            // 첫 턴 의도 선택
            SelectNextIntent();
        }
        
        /// <summary>
        /// 프리셋으로부터 초기화
        /// </summary>
        public void InitializeFromPreset(Data.Monsters.MonsterPreset monsterPreset)
        {
            if (monsterPreset == null)
            {
                Debug.LogError($"[Monster] InitializeFromPreset called with null preset.");
                return;
            }
            preset = monsterPreset;

            InitializeStats();
            InitializeVisuals();
            InitializeAI();
            InitializePassives();

            Debug.Log($"Monster '{stat.MonsterName}' initialized from preset.");
        }

        /// <summary>
        /// Stats 초기화
        /// </summary>
        private void InitializeStats()
        {
            if (preset == null) return;
            stat = preset.CreateStats();
        }

        /// <summary>
        /// Visual 초기화 (Sprite, Color)
        /// </summary>
        private void InitializeVisuals()
        {
            if (stat == null) return;

            if (spriteRenderer != null && stat.MonsterSprite != null)
            {
                spriteRenderer.sprite = stat.MonsterSprite;
                spriteRenderer.color = stat.SpriteColor;
            }
        }

        /// <summary>
        /// AI 패턴 초기화
        /// </summary>
        private void InitializeAI()
        {
            // 기존 런타임 인스턴스 정리
            if (runtimeAiPattern != null)
            {
                Destroy(runtimeAiPattern);
                runtimeAiPattern = null;
            }

            // Preset에서 AI 패턴 가져오기
            if (preset != null && preset.AIPattern != null)
            {
                aiPattern = preset.AIPattern; // Inspector 표시용 원본 참조
                runtimeAiPattern = ScriptableObject.Instantiate(preset.AIPattern); // 실행용 복사본 생성
                runtimeAiPattern.name = preset.AIPattern.name;
            }
            else
            {
                aiPattern = null;
                runtimeAiPattern = null;
            }

            RefreshAvailableSkills();

            // 런타임 AI 초기화
            if (runtimeAiPattern != null)
            {
                runtimeAiPattern.Initialize(this);
            }
        }

        /// <summary>
        /// Passive 초기화
        /// </summary>
        private void InitializePassives()
        {
            if (preset == null) return;

            foreach (var passiveData in preset.GetStartingPassives())
            {
                passives.AddPassive(passiveData);
            }
        }

        private void OnDestroy()
        {
            if (runtimeAiPattern != null)
            {
                Destroy(runtimeAiPattern);
                runtimeAiPattern = null;
            }
        }
        
        /// <summary>
        /// 다음 턴 행동 결정
        /// </summary>
        public void SelectNextIntent()
        {
            RefreshAvailableSkills();

            if (runtimeAiPattern != null)
            {
                nextSkill = runtimeAiPattern.GetNextSkill();

                if (nextSkillData != null)
                {
                    Debug.Log($"[Monster] Next Skill Selected: {nextSkillData.SkillName}");
                    ShowAttackPreview();
                }
                else
                {
                    Debug.LogWarning($"[Monster] No skill selected by AI.");
                }
            }
        }
        
        /// <summary>
        /// 의도 실행 (Monster Turn)
        /// </summary>
        public void ExecuteIntent()
        {
            if (!IsAlive) return;
            
            // 턴 시작 효과 처리 (Passives etc)
            OnStartTurn();
            
            if (nextSkillData != null)
            {
                ExecuteSkill(nextSkillData);
            }
            else
            {
                Debug.Log($"[Monster] Idling (No Skill)");
            }
            
            // 다음 의도 준비
            SelectNextIntent();
        }
        
        /// <summary>
        /// 턴 시작 (Pipeline TurnStart)
        /// </summary>
        public override void OnStartTurn()
        {
            Debug.Log($"[Monster] {stat?.MonsterName} Start Turn");
            base.OnStartTurn();
        }
        
        private void ExecuteSkill(SkillData skill)
        {
            Debug.Log($"[Monster] Executing Skill: {skill.SkillName}");

            // Monster-specific action modules (tile traps, custom patterns, etc.)
            if (skill.ActionModules != null && skill.ActionModules.Count > 0)
            {
                bool handledByMonsterModule = false;
                foreach (var module in skill.ActionModules)
                {
                    if (module is Data.Skills.Modules.IMonsterTileActionModule tileModule)
                    {
                        tileModule.Execute(this, 0, targetedTiles);
                        handledByMonsterModule = true;
                    }
                    else if (module is Data.Skills.Modules.IMonsterActionModule monsterModule)
                    {
                        monsterModule.Execute(this, 0);
                        handledByMonsterModule = true;
                    }
                }

                if (handledByMonsterModule)
                {
                    return;
                }
            }
            
            // 타겟 선정 (Pipeline 처리 전 결정)
            // 여기서는 단순화하여 TargetType에 따라 처리
            // 실제로는 ActionModule 내부에서 처리하거나, Context에 Target 목록을 담아야 함.
            
            var partyManager = PartyManager.Instance;
            if (partyManager == null) return;
            
            var aliveCharacters = partyManager.GetAliveCharacters();
            if (aliveCharacters.Count == 0) return;
            
            // 타겟팅 로직 (미리보기에서 저장된 타겟 사용 혹은 새로 선정)
            Character primaryTarget = targetedCharacter; 
            if (primaryTarget == null || !primaryTarget.IsAlive)
            {
                 primaryTarget = aliveCharacters[Random.Range(0, aliveCharacters.Count)];
            }

            // 스킬의 모든 모듈 실행 via Pipeline
            // 몬스터 스킬은 ActionModule을 직접 실행하기보다,
            // SkillData 자체가 ActionModule을 가지고 있으므로
            // 각 모듈을 Pipeline Action으로 변환하여 실행
            
            foreach (var module in skill.ActionModules)
            {
                 // 모듈 실행 로직 (간소화: 모듈이 직접 Context를 받아 처리하도록 설계되어야 함)
                 // 현재 구조: CombatPipeline.Process(Context) -> Context.Action
                 // SkillData -> ActionModule -> CombatAction?
                 
                 // 임시: SkillData의 데미지 팩터만 사용 (System Migration 과도기)
                 // 추후 ActionModule.Execute(Context) 형태로 고도화 필요
                 
                 // NOTE: ActionModule가 직접 처리하지 않는 경우 기본 CombatAction 경로를 사용합니다.
                 
                 int damage = skill.CalculateDamage(stat.Attack, 0); // No dice for monsters
                 var actionType = Pipeline.ActionType.Attack;
                 
                 // Create Context
                 var context = new Pipeline.CombatContext(this, primaryTarget, new Pipeline.CombatAction(skill.SkillName, actionType, damage));
                 
                 // Apply Logic based on TargetType
                 if (skill.TargetType == SkillTargetType.AllEnemies || skill.TargetType == SkillTargetType.AllAllies) // Monster perspective: Enemy = Character
                 {
                     // Area Attack
                     foreach(var chara in aliveCharacters)
                     {
                         var areaContext = new Pipeline.CombatContext(this, chara, new Pipeline.CombatAction(skill.SkillName, actionType, damage));
                         Pipeline.CombatPipeline.Instance.Process(areaContext);
                     }
                 }
                 else
                 {
                     // Single
                     Pipeline.CombatPipeline.Instance.Process(context);
                 }
            }
            
            // ActionModule이 없는 스킬은 기본 데미지 경로를 사용
            if (skill.ActionModules.Count == 0)
            {
                 int damage = skill.CalculateDamage(stat.Attack, 0);
                 var context = new Pipeline.CombatContext(this, primaryTarget, new Pipeline.CombatAction(skill.SkillName, Pipeline.ActionType.Attack, damage));
                 Pipeline.CombatPipeline.Instance.Process(context);
            }
        }

        // === Preview Logic ===
        
        public void ShowAttackPreview()
        {
            if (nextSkillData == null) return;

            var indicator = AttackIndicator.Instance;
            if (indicator == null) return;

            indicator.Hide();
            targetedTiles = null;
            
            // 타겟 선정 (미리 해둠)
            var partyManager = PartyManager.Instance;
            if (partyManager != null)
            {
                var alive = partyManager.GetAliveCharacters();
                if (alive.Count > 0)
                {
                    targetedCharacter = alive[Random.Range(0, alive.Count)];
                    
                    // Tile-based previews (monster modules)
                    if (nextSkillData.ActionModules != null)
                    {
                        foreach (var module in nextSkillData.ActionModules)
                        {
                            if (module is Data.Skills.Modules.IMonsterTileActionModule tileModule)
                            {
                                var tiles = tileModule.GetPreviewTiles(this);
                                if (tiles != null && tiles.Length > 0)
                                {
                                    targetedTiles = tiles;
                                    UI.AttackIndicator.Instance.ShowAreaAttack(tiles);
                                    return;
                                }
                            }
                        }
                    }

                    if (nextSkillData.TargetType == SkillTargetType.SingleEnemy)
                    {
                        UI.AttackIndicator.Instance.ShowTargetedAttack(transform, targetedCharacter.transform);
                    }
                    else if (nextSkillData.TargetType == SkillTargetType.AllEnemies)
                    {
                        var tiles = alive
                            .Where(c => c != null && c.CurrentTile != null)
                            .Select(c => c.CurrentTile)
                            .Distinct()
                            .ToArray();

                        if (tiles.Length > 0)
                        {
                            UI.AttackIndicator.Instance.ShowAreaAttack(tiles);
                        }
                        else
                        {
                            UI.AttackIndicator.Instance.ShowTargetedAttack(transform, targetedCharacter.transform);
                        }
                    }
                }
            }
        }
        
        public void HideAttackPreview()
        {
            var indicator = AttackIndicator.Instance;
            if (indicator != null) indicator.Hide();
        }
        
        /// <summary>
        /// 데미지 처리
        /// </summary>
        public override void TakeDamage(int damage)
        {
            if (!IsAlive) return;
            base.TakeDamage(damage);
            if (!IsAlive) OnDeath();
        }
        
        private void OnDeath()
        {
            Debug.Log($"[Monster] {stat?.MonsterName} Died.");
            var combatManager = CombatManager.Instance;
            if (combatManager != null) combatManager.OnMonsterDefeated(this);
            Destroy(gameObject);
        }

        private void RefreshAvailableSkills()
        {
            
        }

        protected override void OnMouseEnter()
        {
            base.OnMouseEnter();
            Debug.Log($"[Hover] Monster enter: {name}");
            UI.HoverTooltipUI.EnsureInstance();
            if (UI.HoverTooltipUI.Instance != null)
            {
                UI.HoverTooltipUI.Instance.Show(BuildMonsterTooltipText());
            }
        }

        protected override void OnMouseExit()
        {
            base.OnMouseExit();
            Debug.Log($"[Hover] Monster exit: {name}");
            if (UI.HoverTooltipUI.Instance != null)
            {
                UI.HoverTooltipUI.Instance.Hide();
            }
        }

        private string BuildMonsterTooltipText()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Stats.MonsterName);
            sb.AppendLine($"HP: {Stats.CurrentHP}/{Stats.MaxHP}");
            sb.AppendLine($"ATK: {Stats.Attack}  DEF: {Stats.Defense}");

            if (CurrentIntent != null)
            {
                int expectedDamage = CurrentIntent.CalculateDamage(Stats.Attack, 0);
                sb.AppendLine("--- Intent ---");
                sb.AppendLine($"{CurrentIntent.SkillName}");
                if (!string.IsNullOrWhiteSpace(CurrentIntent.Description))
                {
                    sb.AppendLine(CurrentIntent.Description.Trim());
                }
                sb.AppendLine($"Target: {CurrentIntent.TargetType}");
                sb.AppendLine($"Expected DMG: {expectedDamage}");

                if (CurrentIntent.ActionModules != null)
                {
                    foreach (var module in CurrentIntent.ActionModules)
                    {
                        if (module == null) continue;
                        var moduleText = module.GetTooltipDescription();
                        if (string.IsNullOrWhiteSpace(moduleText)) continue;
                        sb.AppendLine($"- {moduleText.Trim()}");
                    }
                }
            }

            if (passives != null && passives.ActivePassives.Count > 0)
            {
                sb.AppendLine("--- Passive ---");
                foreach (var passive in passives.ActivePassives)
                {
                    if (passive == null) continue;
                    var passiveName = string.IsNullOrWhiteSpace(passive.PassiveName) ? passive.name : passive.PassiveName;
                    sb.AppendLine(passiveName);
                    if (!string.IsNullOrWhiteSpace(passive.Description))
                    {
                        sb.AppendLine(passive.Description.Trim());
                    }
                }
            }

            return sb.ToString().TrimEnd();
        }

        public string GetHoverTooltipText()
        {
            return BuildMonsterTooltipText();
        }

        private void EnsureHoverCollider()
        {
            var existing = GetComponent<Collider>();
            if (existing != null) return;

            var box = gameObject.AddComponent<BoxCollider>();
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                var bounds = spriteRenderer.sprite.bounds;
                box.size = new Vector3(Mathf.Max(0.5f, bounds.size.x), Mathf.Max(0.5f, bounds.size.y), 1.5f);
                box.center = new Vector3(bounds.center.x, bounds.center.y, 0f);
            }
            else
            {
                box.size = new Vector3(1.2f, 1.2f, 1.5f);
            }
        }
    }
}
