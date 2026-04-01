using UnityEngine;
using DiceOrbit.Data;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Skills;
using DiceOrbit.Visuals;
using System.Collections.Generic;
using System.Text;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 게임 캐릭터 (플레이어)
    /// </summary>
    public class Character : Unit<CharacterStats>, UI.IHoverTooltipProvider
    {
        // 타일 위 유닛 위치 offset
        protected static readonly Vector3 TILE_OFFSET = new Vector3(0, 1.5f, 1.0f);

        [Header("Movement")]
        [SerializeField] private TileData currentTile;
        [SerializeField] private int startTileIndex = 0;
        [SerializeField] private float stepHopHeight = 0.35f;
        [SerializeField] private float stepIdlePause = 0.05f;
        private bool stopMovementRequested = false;

        // 스프라이트 비주얼
        private CharacterSpriteVisual spriteVisual;

        // CharacterStats 타입으로 반환 (기존 코드 호환성 유지)
        public new CharacterStats Stats => stat;

        public TileData CurrentTile => currentTile;
        public Core.CharacterPreset SourcePreset => Stats?.SourcePreset;
        
        /// <summary>
        /// Stats 초기화 (캐릭터 선택 후)
        /// </summary>
        public void InitializeStats(CharacterStats newStats)
        {
            stat = newStats;

            // 스프라이트 업데이트
            if (spriteRenderer != null)
            {
                if (stat.CharacterSprite != null)
                {
                    spriteRenderer.sprite = stat.CharacterSprite;
                }
                spriteRenderer.color = stat.SpriteColor;
                originalColor = spriteRenderer.color;

                // Visual Scale 적용
                if (stat.SourcePreset != null)
                {
                    var visual = spriteRenderer.GetComponent<DiceOrbit.Visuals.CharacterSpriteVisual>();
                    if (visual != null)
                    {
                        visual.SetScale(stat.SourcePreset.VisualScale);
                    }
                    else
                    {
                        spriteRenderer.transform.localScale = new Vector3(stat.SourcePreset.VisualScale, stat.SourcePreset.VisualScale, 1f);
                    }
                }
            }

            // Preset의 애니메이션 스프라이트를 spriteVisual에 적용
            if (spriteVisual != null && stat.SourcePreset != null)
            {
                spriteVisual.SetAnimationSprites(
                    stat.SourcePreset.IdleSprite,
                    stat.SourcePreset.MoveSprite,
                    stat.SourcePreset.DamageSprite,
                    stat.SourcePreset.SkillSprite
                );
                spriteVisual.PlayIdle();
            }

            // 스킬 재초기화
            InitializeSkills();

            // 패시브는 템플릿 에셋이므로 캐릭터 인스턴스별로 복제/바인딩합니다.
            ApplyRuntimePassiveSkills();
            // 복제된 패시브 인스턴스 레벨을 런타임 능력 레벨과 동기화합니다.
            SyncPassiveLevelsFromRuntime();

            Debug.Log($"Character initialized: {stat.CharacterName} (HP: {stat.MaxHP})");
        }

        /// <summary>
        /// 스킬 초기화
        /// </summary>
        private void InitializeSkills()
        {
            if (stat == null) return;
            stat.NormalizeRuntimeAbilities();

            // Preset에서 초기 스킬을 가져오지 못한 경우 (예: 구 버전 데이터)
            if (stat.ActiveAbilityCount == 0)
            {
               Debug.LogWarning("No active skills initialized.");
            }
        }

        private void ApplyRuntimePassiveSkills()
        {
            if (passives == null || stat == null) return;

            foreach (var ability in stat.PassiveAbilities)
            {
                if (ability?.BaseSkill == null) continue;
                if (ability.BaseSkill.Type != CharacterSkillType.Passive) continue;

                var template = ability.BaseSkill.PassiveTemplate;
                if (template == null)
                {
                    Debug.LogWarning($"[Character] Passive skill '{ability.BaseSkill.SkillName}' has no PassiveTemplate.");
                    continue;
                }

                var clonedPassive = template.Clone();
                clonedPassive.Initialize(this);
                clonedPassive.SetLevel(ability.CurrentLevel);
                ability.RuntimePassiveInstance = clonedPassive;
                // 복제된 패시브를 유닛 리액터 체인에 등록합니다.
                passives.AddPassive(clonedPassive);
            }
        }

        public void SyncPassiveLevelsFromRuntime()
        {
            if (passives == null || stat == null) return;

            // 기본 패시브는 캐릭터 레벨 동기화
            foreach (var passive in passives.ActivePassives)
            {
                if (passive == null) continue;
                passive.SetLevel(stat.Level);
            }

            // Runtime passive abilities are synced by their own levels.
            foreach (var ability in stat.PassiveAbilities)
            {
                if (ability == null) continue;

                if (ability.RuntimePassiveInstance != null)
                {
                    ability.RuntimePassiveInstance.SetLevel(ability.CurrentLevel);
                }
            }
        }

        public void LevelUpCharacter()
        {
            if (stat == null) return;

            stat.LevelUp();
            CharacterProgressionService.ApplyLevelUp(this);

            Debug.Log($"[Character] {stat.CharacterName} leveled up -> Lv.{stat.Level}");
        }
        
        protected override void Awake()
        {
            // 자식 오브젝트에서 SpriteRenderer 찾기 (Visual 분리 지원)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            spriteVisual   = GetComponentInChildren<CharacterSpriteVisual>();

            if (spriteRenderer != null)
            {
                // 스탯에서 스프라이트 설정
                if (stat != null && stat.CharacterSprite != null)
                {
                    spriteRenderer.sprite = stat.CharacterSprite;
                }

                if (stat != null)
                {
                    spriteRenderer.color = stat.SpriteColor;
                    originalColor = spriteRenderer.color;
                }
            }
            else
            {
                Debug.LogWarning("SpriteRenderer not found in children! Add SpriteRenderer component to a child object.");
            }

            mainCamera = Camera.main;

            // Systems 초기화 (기존 컴포넌트 유지를 위해 GetComponent 유지, 필요시 Children으로 변경 고려)
            passives = GetComponent<Systems.Passives.PassiveManager>();
            if (passives == null) passives = gameObject.AddComponent<Systems.Passives.PassiveManager>();
            passives.Initialize(this);

            statusEffects = GetComponent<Systems.Effects.StatusEffectManager>();
            if (statusEffects == null) statusEffects = gameObject.AddComponent<Systems.Effects.StatusEffectManager>();
            statusEffects.Initialize(this);
        }
        
        private void Start()
        {
            // 한 프레임 기다려서 OrbitManager가 타일 생성하도록 함
            StartCoroutine(InitializeAfterDelay());
        }
        
        private System.Collections.IEnumerator InitializeAfterDelay()
        {
            yield return null;

            Debug.Log($"Character {stat?.CharacterName} initializing - Current Tile: {(currentTile != null ? "Assigned" : "NULL")}");

            // 이미 Inspector에서 타일이 할당되었으면 사용
            if (currentTile != null)
            {
                Debug.Log($"Using manually assigned tile: {currentTile.TileIndex}");
                transform.position = currentTile.Position + TILE_OFFSET;
                RefreshTileFormation(currentTile);
                yield break;
            }

            // OrbitManager에서 시작 타일 가져오기
            var orbitManager = Object.FindAnyObjectByType<OrbitManager>();
            if (orbitManager != null)
            {
                Debug.Log($"OrbitManager found! Tile count: {orbitManager.TileCount}");

                currentTile = orbitManager.GetTile(startTileIndex);

                if (currentTile != null)
                {
                    Debug.Log($"{stat?.CharacterName} assigned to tile {currentTile.TileIndex} at position {currentTile.Position}");
                    transform.position = currentTile.Position + TILE_OFFSET;
                    RefreshTileFormation(currentTile);
                }
                else
                {
                    Debug.LogError($"Could not get tile at index {startTileIndex}! OrbitManager may not have generated tiles yet.");
                }
            }
            else
            {
                Debug.LogError("OrbitManager not found! Make sure OrbitSystem exists in scene.");
            }
        }
        

        /// <summary>
        /// 타일을 하나씩 거쳐서 이동
        /// </summary>
        public System.Collections.IEnumerator MoveStepByStep(List<TileData> path)
        {
            float stepDuration = 0.2f;
            int stepsTraveled = 0;
            TileData arrivalTile = null;
            
            foreach (var tile in path)
            {
                Vector3 startPos = transform.position;
                Vector3 endPos = tile.Position + TILE_OFFSET;
                float elapsed = 0f;
                UpdateFacingByMoveDirection(endPos - startPos);

                // 한 칸 이동 시작 → Move 스프라이트
                spriteVisual?.PlayMove();

                while (elapsed < stepDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / stepDuration;
                    var pos = Vector3.Lerp(startPos, endPos, t);
                    pos.y += Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)) * stepHopHeight;
                    transform.position = pos;
                    yield return null;
                }

                transform.position = endPos;
                currentTile = tile;

                // 한 칸 도착 → Idle 스프라이트
                spriteVisual?.PlayIdle();
                if (stepIdlePause > 0f)
                {
                    yield return new WaitForSeconds(stepIdlePause);
                }

                tile.OnTraverse(this);

                stepsTraveled++;

                if (stopMovementRequested)
                {
                    stopMovementRequested = false;
                    arrivalTile = tile;
                    break;
                }
            }
            
            // 최종 도착
            if (arrivalTile == null && path.Count > 0)
            {
                arrivalTile = path[path.Count - 1];
            }

            if (arrivalTile != null)
            {
                RefreshTileFormation(arrivalTile);
                Debug.Log($"{stat?.CharacterName} arrived at tile {arrivalTile.TileIndex}");
                arrivalTile.OnArrive(this);
            }

            // Notify pipeline about movement distance
            if (Pipeline.CombatPipeline.Instance != null)
            {
                var moveAction = new Pipeline.CombatAction("Move", Pipeline.ActionType.Move, stepsTraveled);
                moveAction.AddTag("Move");
                var moveContext = new Pipeline.CombatContext(this, this, moveAction);
                Pipeline.CombatPipeline.Instance.Process(moveContext);
            }

            // 색상 복원
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }

            spriteVisual?.PlayIdle();
        }

        private void RefreshTileFormation(TileData tile)
        {
            if (tile == null) return;

            var orbitManager = Object.FindAnyObjectByType<OrbitManager>();
            orbitManager?.RefreshCharactersOnTile(tile);
        }

        private void UpdateFacingByMoveDirection(Vector3 moveDirection)
        {
            if (spriteRenderer == null) return;
            if (moveDirection.sqrMagnitude <= 0.000001f) return;

            Vector3 rightAxis = mainCamera != null ? mainCamera.transform.right : Vector3.right;
            float lateral = Vector3.Dot(moveDirection.normalized, rightAxis.normalized);

            // Base sprites are left-facing. Flip only when moving to screen-right.
            if (Mathf.Abs(lateral) > 0.01f)
            {
                spriteRenderer.flipX = lateral > 0f;
            }
        }

        /// <summary>
        /// 타일 이동 중지 요청 (트랩 등)
        /// </summary>
        public void RequestStopMovement()
        {
            stopMovementRequested = true;
        }
        
        /// <summary>
        /// 턴 시작 처리 (Pipeline)
        /// </summary>
        public override void OnStartTurn()
        {
            base.OnStartTurn();
        }

        /// <summary>
        /// 턴 종료 처리
        /// </summary>
        public override void OnEndTurn()
        {
            base.OnEndTurn();
            currentTile.OnEndTurn(this);
        }

        /// <summary>
        /// 스킬 사용 (타겟 선택 시작) - 첫 번째 Active 스킬 사용
        /// </summary>
        public void UseSkill(int diceValue)
        {
            UseSkillByIndex(0, diceValue);
        }
        
        /// <summary>
        /// 특정 인덱스의 스킬 사용
        /// </summary>
        public void UseSkillByIndex(int skillIndex, int diceValue)
        {
            spriteVisual?.PlaySkill();

            // SkillManager에게 위임
            if (SkillManager.Instance != null)
            {
                SkillManager.Instance.PrepareSkill(this, skillIndex, diceValue);
            }
            else
            {
                Debug.LogError("[Character] SkillManager not found!");
            }
        }

        public void OnSkillResolved()
        {
            spriteVisual?.PlayIdle();
        }

        /// <summary>
        /// 데미지 처리 (파이프라인 외부 호출 대비)
        /// </summary>
        public override int TakeDamage(int damage)
        {
            spriteVisual?.PlayDamage();
            return base.TakeDamage(damage);
        }
        
        /// <summary>
        /// 캐릭터 선택됨 (CharacterSelector에서 호출)
        /// </summary>
        public void OnSelected()
        {
            Debug.Log($"{stat?.CharacterName} OnSelected called!");

            // CharacterActionUI 표시
            var actionUI = UI.CharacterActionUI.Instance;
            if (actionUI != null)
            {
                actionUI.Show(this);
                Debug.Log($"{stat?.CharacterName} selected! Waiting for dice...");
            }
            else
            {
                Debug.LogError("CharacterActionUI NOT FOUND! Make sure CharacterActionUI component exists in scene.");
            }
        }

        public string GetHoverTooltipText()
        {
            return BuildCharacterTooltipText();
        }

        private string BuildCharacterTooltipText()
        {
            var sb = new StringBuilder();
            string characterName = stat != null && !string.IsNullOrWhiteSpace(stat.CharacterName)
                ? stat.CharacterName
                : name;

            sb.AppendLine(characterName);

            string profileDescription = stat?.SourcePreset != null
                ? stat.SourcePreset.Description
                : string.Empty;
            if (!string.IsNullOrWhiteSpace(profileDescription))
            {
                sb.AppendLine(profileDescription.Trim());
            }

            if (passives != null && passives.ActivePassives.Count > 0)
            {
                sb.AppendLine("--- Passive ---");
                foreach (var passive in passives.ActivePassives)
                {
                    if (passive == null) continue;
                    string passiveName = string.IsNullOrWhiteSpace(passive.PassiveName) ? "Unknown Passive" : passive.PassiveName;
                    sb.Append($"• {passiveName}");
                    if (passive.CurrentLevel > 0)
                    {
                        sb.Append($" (Lv.{passive.CurrentLevel})");
                    }

                    string passiveCoefficientLine = BuildPassiveCoefficientLine(passive);
                    if (!string.IsNullOrWhiteSpace(passiveCoefficientLine))
                    {
                        sb.AppendLine($": {passiveCoefficientLine}");
                    }
                    else
                    {
                        sb.AppendLine();
                    }
                }
            }

            if (statusEffects != null)
            {
                var effects = statusEffects.GetActiveEffects();
                if (effects != null && effects.Count > 0)
                {
                    sb.AppendLine("--- Status ---");
                    foreach (var effect in effects)
                    {
                        if (effect == null) continue;
                        string durationText = effect.Duration < 0 ? "∞" : effect.Duration.ToString();
                        sb.AppendLine($"• {effect.Type}: {effect.Value} ({durationText}T)");
                    }
                }
            }

            return UI.TooltipKeywordFormatter.AppendKeywordSection(sb.ToString().TrimEnd());
        }

        private static string BuildPassiveCoefficientLine(PassiveAbility passive)
        {
            if (passive == null) return string.Empty;

            switch (passive)
            {
                case BattleCryPassive battleCry:
                    return $"피해 +{(battleCry.CurrentDamageMultiplier - 1f) * 100f:0.#}%";

                case StableReactionPassive stableReaction:
                    return $"체력 {(stableReaction.healthThresholdRatio * 100f):0.#}% 이상일 때 피해 +{(stableReaction.CurrentDamageMultiplier - 1f) * 100f:0.#}%";

                case PositioningPassive positioning:
                    return $"이동 {positioning.CurrentThresholdDistance}칸 이상 시 다음 공격 피해 +{(positioning.CurrentDamageMultiplier - 1f) * 100f:0.#}%";

                case FocusPassive focus:
                    return $"집중 스택당 추가 피해 +{focus.BonusDamageRatioPerStack * 100f:0.#}%";

                default:
                    return string.Empty;
            }
        }
    }
}
