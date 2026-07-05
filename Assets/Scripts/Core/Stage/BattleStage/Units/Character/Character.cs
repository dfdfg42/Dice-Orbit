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
    public class Character : Unit<CharacterStats>, UI.IBattleInfoProvider
    {
        public static readonly Vector3 TILE_OFFSET = new Vector3(0, 1.5f, 1.0f);

        [Header("Movement")]
        [SerializeField] private TileData currentTile;
        [SerializeField] private int startTileIndex = 0;

        /// <summary>스폰 시 시작 타일 인덱스를 지정 (Start의 지연 초기화 전에 호출).</summary>
        public void SetStartTileIndex(int index) => startTileIndex = index;
        [SerializeField] private float stepHopHeight = 0.6f;       // 한 칸 점프 높이 (클수록 높이 뜀)
        [SerializeField] private float stepIdlePause = 0.15f;      // 한 칸 착지 후 잠깐 멈추는 시간(초)
        [SerializeField] private float stepDuration = 0.4f;        // 한 칸 건너가는 시간(초) — 길수록 천천히, 이동 애니메이션이 보일 시간 확보
        private bool stopMovementRequested = false;

        // 스프라이트 비주얼
        private CharacterSpriteVisual spriteVisual;

        // CharacterStats 타입으로 반환 (기존 코드 호환성 유지)
        public new CharacterStats Stats => stat;

        public TileData CurrentTile => currentTile;
        public Core.CharacterPreset SourcePreset => Stats?.SourcePreset;

        [Header("Hover")]
        [SerializeField] private int hoverSortingBoost = 100; // 마우스 올렸을 때 sortingOrder 가산량(같은 타일에서 앞으로)
        private int baseSortingOrder;
        private bool sortingCaptured;

        // 호버 강조: 같은 타일에서 겹칠 때 마우스 올린 캐릭터를 앞으로(sortingOrder 가산).
        // 새 Input System 전용 프로젝트라 레거시 OnMouseEnter/Exit가 안 불리므로 CharacterSelector가 매 프레임 호출한다.
        public void SetHoverHighlight(bool hovered)
        {
            if (spriteRenderer == null) return;
            if (hovered)
            {
                if (!sortingCaptured) { baseSortingOrder = spriteRenderer.sortingOrder; sortingCaptured = true; }
                spriteRenderer.sortingOrder = baseSortingOrder + hoverSortingBoost;
            }
            else if (sortingCaptured)
            {
                spriteRenderer.sortingOrder = baseSortingOrder;
            }
        }

        /// <summary>
        /// Stats 초기화 (캐릭터 선택 후)
        /// </summary>
        public void InitializeStats(CharacterStats newStats)
        {
            stat = newStats;
            var preset = stat.SourcePreset;

            if (spriteRenderer != null && preset != null)
            {
                if (preset.CharacterSprite != null)
                    spriteRenderer.sprite = preset.CharacterSprite;

                var visual = spriteRenderer.GetComponent<DiceOrbit.Visuals.CharacterSpriteVisual>();
                if (visual != null)
                    visual.SetScale(preset.VisualScale);
                else
                    spriteRenderer.transform.localScale = new Vector3(preset.VisualScale, preset.VisualScale, 1f);
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

            InitializeModifierManager();

            // 스킬 재초기화
            InitializeSkills();
            InitializePassives();

            Debug.Log($"Character initialized: {stat.CharacterName} (HP: {stat.MaxHP})");
        }

        private void InitializeModifierManager()
        {
            if (stat == null) return;

            // 모디파이어 컨테이너 생성 (컨텍스트는 스킬별로 그때그때 생성하므로 타입 지정 불필요)
            if (stat.Modifiers == null)
            {
                stat.Modifiers = new Data.Modifiers.ModifierManager();
                stat.Modifiers.Initialize(this);
            }

            // 각 액티브 슬롯에 소유자 주입 → 슬롯이 유효 타게팅(모디파이어 반영)을 계산할 수 있게 함
            if (stat.ActiveAbilities != null)
            {
                foreach (var slot in stat.ActiveAbilities)
                    if (slot != null) slot.Owner = this;
            }
        }

        private void InitializeSkills()
        {
            if (stat == null) return;
            if (stat.ActiveAbilityCount == 0)
                Debug.LogWarning($"[Character] {stat.CharacterName}: no active skills.");
        }

        private void InitializePassives()
        {
            if (passives == null || stat == null) return;

            foreach (var passive in stat.PassiveInstances)
            {
                if (passive == null) continue;
                passive.Initialize(this);
                passives.AddPassive(passive);
            }
        }

        public void SyncPassiveLevelsFromRuntime()
        {
            if (stat == null) return;

            foreach (var passive in stat.PassiveInstances)
                passive?.SetLevel(passive.CurrentLevel);
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
            spriteVisual = GetComponentInChildren<CharacterSpriteVisual>();

            if (spriteRenderer == null)
                Debug.LogWarning("SpriteRenderer not found in children! Add SpriteRenderer component to a child object.");

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
                    passives?.BroadcastOwnerMoved(currentTile);
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
            int stepsTraveled = 0;
            TileData arrivalTile = null;

            foreach (var tile in path)
            {
                Vector3 startPos = transform.position;
                Vector3 endPos = tile.Position + TILE_OFFSET;
                float elapsed = 0f;
                UpdateFacingByMoveDirection(endPos - startPos);

                // 한 칸 이동 시작 → 이동 애니메이션을 처음부터 재생 (한 칸 건너가는 시간 = stepDuration)
                spriteVisual?.PlayMoveStep();

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

            // 이번 턴 이동 거리 누적 ("이동 안 함" 판정용)
            if (stat != null) stat.MoveOnThisTurn += stepsTraveled;

            if (arrivalTile != null)
            {
                RefreshTileFormation(arrivalTile);
                passives?.BroadcastOwnerMoved(arrivalTile);
                Debug.Log($"{stat?.CharacterName} arrived at tile {arrivalTile.TileIndex}");
                arrivalTile.OnArrive(this);
            }

            // Notify pipeline about movement distance
            if (Pipeline.CombatPipeline.Instance != null)
            {
                var moveContext = new Pipeline.MoveContext(this, this, stepsTraveled);
                Pipeline.CombatPipeline.Instance.Process(moveContext);
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
            // 이번 턴 이동 거리 초기화 (서리토템 동상 등 "이동 안 함" 판정용)
            if (stat != null) stat.MoveOnThisTurn = 0;
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
        //public void UseSkill(int diceValue)
        //{
        //    UseSkillByIndex(0, diceValue);
        //}

        /// <summary>
        /// 특정 인덱스의 스킬 사용
        /// </summary>
        public void UseSkillByIndex(int skillIndex, DiceData dice)
        {
            // SkillManager에게 위임
            if (SkillManager.Instance != null)
            {
                SkillManager.Instance.PrepareSkill(this, skillIndex, dice);
            }
            else
            {
                Debug.LogError("[Character] SkillManager not found!");
            }
        }

        public void OnSkillResolved()
        {
            spriteVisual?.SetAiming(false);
            spriteVisual?.PlayIdle();
        }

        public void OnSkillTargetingStarted()
        {
            spriteVisual?.SetAiming(true);
        }

        public void OnSkillTargetingEnded()
        {
            spriteVisual?.SetAiming(false);
        }

        public void OnSkillExecutionStarted()
        {
            spriteVisual?.PlaySkill();
        }

        /// <summary>
        /// 데미지 처리 (파이프라인 외부 호출 대비)
        /// </summary>
        public override int TakeDamage(int damage)
        {
            int actual = base.TakeDamage(damage);

            if (!IsAlive)
            {
                HandleDeath();
            }
            else if (actual > 0)
            {
                spriteVisual?.PlayDamage();
            }

            return actual;
        }

        [SerializeField] private float deathDespawnDelay = 0.7f;
        private bool isDying = false;

        protected override void HandleDeath()
        {
            if (isDying) return;
            isDying = true;

            base.HandleDeath();
            spriteVisual?.PlayDeath();

            // 전멸/전투 종료 판정 (HP 기준이라 파괴 전에 호출해도 안전)
            var combatManager = CombatManager.Instance;
            if (combatManager != null) combatManager.OnCharacterDefeated(this);

            // 사망 연출 후 파티에서 제거하고 오브젝트 파괴
            StartCoroutine(DespawnAfterDeath());
        }

        private System.Collections.IEnumerator DespawnAfterDeath()
        {
            yield return new WaitForSeconds(Mathf.Max(0f, deathDespawnDelay));

            PartyManager.Instance?.RemoveCharacter(this);
            Destroy(gameObject);
        }

        /// <summary>
        /// 캐릭터 선택됨 (CharacterSelector에서 호출)
        /// </summary>
        public void OnSelected()
        {
            Debug.Log($"{stat?.CharacterName} OnSelected called!");

            var combatManager = CombatManager.Instance;
            // 플레이어 턴이 아니면 액션 패널을 열지 않습니다.
            if (combatManager != null && (!combatManager.InCombat || !combatManager.PlayerTurnActive))
            {
                Debug.Log($"[Character] {stat?.CharacterName} 선택 무시: 플레이어 턴이 아닙니다.");
                return;
            }

            // 이동/행동이 모두 소진된 캐릭터는 선택만으로 패널을 열지 않습니다.
            if (combatManager != null && !combatManager.HasAnyTurnActionRemaining(this))
            {
                Debug.Log($"[Character] {stat?.CharacterName} 선택 무시: 이번 턴 행동권이 없습니다.");
                return;
            }

            // CharacterActionUI 표시
            var actionUI = UI.CharacterActionUI.Instance;
            var partyManager = PartyManager.Instance;

            if (partyManager != null && partyManager.SelectedCharacter == this && actionUI != null && actionUI.IsShowingCharacter(this))
            {
                actionUI.CancelSelection();
                partyManager.DeselectCharacter();
                Debug.Log($"{stat?.CharacterName} selection cancelled by re-click.");
                return;
            }

            partyManager?.SelectCharacter(this);

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

        /// <summary>정보 패널용 구조화 데이터 (빌드 로직은 UnitInfoBuilder로 단일화).</summary>
        public UI.UnitInfoData GetBattleInfo() => UI.UnitInfoBuilder.Build(this);

        public override void CollectReactors(List<Pipeline.ICombatReactor> list)
        {
            base.CollectReactors(list);
            stat?.Modifiers?.CollectReactors(list);
        }
    }
}
