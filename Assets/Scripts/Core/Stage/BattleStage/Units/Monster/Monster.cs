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
        [Header("Animation")]
        [SerializeField] private float attackSpriteDuration = 0.25f;
        [SerializeField] private float damageSpriteDuration = 0.35f;

        [Header("Preset")]
        [SerializeField] private Data.Monsters.MonsterPreset preset;

        [Header("AI")]
        [SerializeField] private Data.MonsterAI.MonsterAI aiPattern; // Inspector 설정 전용 (원본 참조)
        private Data.MonsterAI.MonsterAI runtimeAiPattern; // 실제 실행되는 런타임 인스턴스
        private MonsterSkill nextSkill;
        private AttackIntent nextIntent; // 다음 턴에 사용할 AttackIntent
        private Coroutine visualResetCoroutine;
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
            EnsureSpriteScalingIsolation();
            
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
            //if (preset != null)
            //{
            //    InitializeFromPreset(preset);
            //}
            
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

            if (spriteRenderer != null)
            {
                if (stat.MonsterSprite != null)
                {
                    spriteRenderer.sprite = stat.MonsterSprite;
                }
                spriteRenderer.color = stat.SpriteColor;

                // Visual Scale 적용
                if (preset != null)
                {
                    spriteRenderer.transform.localScale = new Vector3(preset.VisualScale, preset.VisualScale, 1f);
                }
            }
        }

        /// <summary>
        /// </summary>
        private void InitializeAI()
        {
            // Preset에서 AI 패턴 가져오기
            if (preset != null && preset.AIPattern != null)
            {
                runtimeAiPattern = preset.AIPattern;
            }
            else
            {
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
            foreach (var passive in preset.GetStartingPassives())
            {
                if (passive == null) continue;

                // 패시브 초기화
                passive.Initialize(this);

                // 매니저에 등록
                passives.AddPassive(passive);
            }
        }

        private void OnDestroy()
        {
            // AttackIndicator에서 Intent 제거
            UI.MonsterAttackIntentManager.Instance?.RemoveAttackIntent(this);
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
                        UI.MonsterAttackIntentManager.Instance?.AddAttackIntent(this, nextIntent);
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
            PlayAttackVisual();

            // SkillData의 Execute 메서드에 위임
            skill.ExecuteSkillWithIntent(this, intent);
            QueueReturnToIdle(attackSpriteDuration);
        }

        // === Damage & Death ===

        /// <summary>
        /// 데미지 처리
        /// </summary>
        public override int TakeDamage(int damage)
        {
            if (!IsAlive) return 0;
            PlayDamageVisual();
            QueueReturnToIdle(damageSpriteDuration);
            int result=base.TakeDamage(damage);
            if (!IsAlive) OnDeath();
            return result;
        }
        
        private void OnDeath()
        {
            Debug.Log($"[Monster] {stat?.MonsterName} Died.");

            // AttackIndicator에서 Intent 제거
            UI.MonsterAttackIntentManager.Instance?.RemoveAttackIntent(this);

    
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
            sb.AppendLine($"Armor: {Stats.TempArmor}");
            sb.AppendLine($"ATK: {Stats.Attack}  DEF: {Stats.Defense}");

            if (CurrentIntent != null)
            {

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
                }
            }

            if (passives != null && passives.ActivePassives.Count > 0)
            {
                sb.AppendLine("--- Passive ---");
                foreach (var passive in passives.ActivePassives)
                {
                    if (passive == null) continue;
                    var passiveName = string.IsNullOrWhiteSpace(passive.PassiveName) ? "Unknown Passive" : passive.PassiveName;
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

        private void PlayAttackVisual()
        {
            if (spriteRenderer == null || stat == null) return;
            if (stat.AttackSprite != null)
            {
                spriteRenderer.sprite = stat.AttackSprite;
            }
        }

        private void PlayDamageVisual()
        {
            if (spriteRenderer == null || stat == null) return;
            if (stat.DamageSprite != null)
            {
                spriteRenderer.sprite = stat.DamageSprite;
            }
        }

        private void ReturnToIdleVisual()
        {
            if (spriteRenderer == null || stat == null) return;
            if (stat.MonsterSprite != null)
            {
                spriteRenderer.sprite = stat.MonsterSprite;
            }
        }

        private void QueueReturnToIdle(float delay)
        {
            if (!isActiveAndEnabled) return;
            if (visualResetCoroutine != null)
            {
                StopCoroutine(visualResetCoroutine);
            }
            visualResetCoroutine = StartCoroutine(CoReturnToIdle(delay));
        }

        private System.Collections.IEnumerator CoReturnToIdle(float delay)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, delay));
            ReturnToIdleVisual();
            visualResetCoroutine = null;
        }

        private void EnsureSpriteScalingIsolation()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
                if (spriteRenderer == null) return;
            }

            // If SpriteRenderer is on root, scaling it also scales child UI.
            // Clone renderer to a child visual root so only sprite scales.
            if (spriteRenderer.transform != transform) return;

            var original = spriteRenderer;
            var visualRoot = new GameObject("MonsterVisualRoot").transform;
            visualRoot.SetParent(transform, false);
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one;

            var cloned = visualRoot.gameObject.AddComponent<SpriteRenderer>();
            cloned.sprite = original.sprite;
            cloned.color = original.color;
            cloned.material = original.sharedMaterial;
            cloned.sortingLayerID = original.sortingLayerID;
            cloned.sortingOrder = original.sortingOrder;
            cloned.flipX = original.flipX;
            cloned.flipY = original.flipY;

            original.enabled = false;
            spriteRenderer = cloned;
        }
    }
}
