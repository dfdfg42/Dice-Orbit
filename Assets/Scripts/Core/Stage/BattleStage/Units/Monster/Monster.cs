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
        private AttackIntent nextIntent; // 다음 턴에 사용할 AttackIntent
        public AttackIntent CurrentIntent => nextIntent; // AttackIntent 타입으로 반환

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

            // AttackIndicator에서 Intent 제거
            UI.AttackIndicator.Instance?.RemoveAttackIntent(this);
        }
        
        /// <summary>
        /// 다음 턴 행동 결정
        /// </summary>
        public void SelectNextIntent()
        {
            if (runtimeAiPattern != null)
            {
                nextSkill = runtimeAiPattern.GetNextSkill();

                if (nextSkill != null)
                {
                    // MonsterSkill이 타겟 선정 및 Intent 생성
                    nextIntent = nextSkill.GenerateIntent(this);

                    if (nextIntent != null)
                    {
                        Debug.Log($"[Monster] Next Intent Selected: {nextIntent}");

                        // AttackIndicator에 Intent 등록 (시각화는 Battle 시스템에서 Show() 호출)
                        UI.AttackIndicator.Instance?.AddAttackIntent(this, nextIntent);
                    }
                    else
                    {
                        Debug.LogWarning($"[Monster] Failed to generate intent.");
                    }
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

            if (nextIntent != null && nextSkill != null)
            {
                // AttackIntent의 유효성 확인 (죽은 타겟 제거)
                nextIntent.RefreshTargets();

                // 스킬 실행 (Intent에 저장된 타겟 사용)
                ExecuteSkillWithIntent(nextSkill.skillData, nextIntent);
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
        
        private void ExecuteSkillWithIntent(SkillData skill, AttackIntent intent)
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
                        var tiles = intent.TargetTiles;
                        tileModule.Execute(this, 0, tiles);
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

            // Intent에서 타겟 가져오기
            var targets = intent.Targets;
            if (targets == null || targets.Count == 0)
            {
                Debug.LogWarning("[Monster] No valid targets in intent.");
                return;
            }

            // 각 타겟에게 스킬 적용
            int damage = skill.CalculateDamage(stat.Attack, 0);
            var actionType = Pipeline.ActionType.Attack;

            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive) continue;

                var context = new Pipeline.CombatContext(
                    this, 
                    target, 
                    new Pipeline.CombatAction(skill.SkillName, actionType, damage)
                );
                Pipeline.CombatPipeline.Instance.Process(context);
            }
        }

        // === Damage & Death ===

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

            // AttackIndicator에서 Intent 제거
            UI.AttackIndicator.Instance?.RemoveAttackIntent(this);

            var combatManager = CombatManager.Instance;
            if (combatManager != null) combatManager.OnMonsterDefeated(this);
            Destroy(gameObject);
        }

        // === Tooltip ===
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
                sb.AppendLine("--- Intent ---");
                sb.AppendLine(CurrentIntent.ToString()); // AttackIntent의 ToString 사용

                var targets = CurrentIntent.Targets;
                if (targets != null && targets.Count > 0)
                {
                    sb.AppendLine($"Targets: {string.Join(", ", targets.Select(t => t.Stats.CharacterName))}");
                }

                // 스킬 설명 (nextSkill에서 가져오기)
                if (nextSkill != null && nextSkill.skillData != null)
                {
                    if (!string.IsNullOrWhiteSpace(nextSkill.skillData.Description))
                    {
                        sb.AppendLine(nextSkill.skillData.Description.Trim());
                    }

                    // ActionModules 정보
                    if (nextSkill.skillData.ActionModules != null)
                    {
                        foreach (var module in nextSkill.skillData.ActionModules)
                        {
                            if (module == null) continue;
                            var moduleText = module.GetTooltipDescription();
                            if (!string.IsNullOrWhiteSpace(moduleText))
                            {
                                sb.AppendLine($"- {moduleText.Trim()}");
                            }
                        }
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
