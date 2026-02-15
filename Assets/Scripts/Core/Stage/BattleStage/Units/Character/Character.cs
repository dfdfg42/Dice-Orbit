using UnityEngine;
using DiceOrbit.Data;
using System.Collections.Generic;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 게임 캐릭터 (플레이어)
    /// </summary>
    public class Character : Unit<CharacterStats>
    {
        // 타일 위 유닛 위치 offset
        protected static readonly Vector3 TILE_OFFSET = new Vector3(0, 1.5f, 1.0f);

        [Header("Movement")]
        [SerializeField] private TileData currentTile;
        [SerializeField] private int startTileIndex = 0;
        private bool stopMovementRequested = false;  

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
                    // CharacterSpriteVisual 컴포넌트가 있으면 사용, 없으면 직접 Transform 조정
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

            // 스킬 재초기화
            InitializeSkills();

            ApplyStartingPassives();

            Debug.Log($"Character initialized: {stat.CharacterName} (HP: {stat.MaxHP}, ATK: {stat.Attack})");
        }

        /// <summary>
        /// 스킬 초기화
        /// </summary>
        private void InitializeSkills()
        {
            if (stat == null) return;

            // Preset에서 초기 스킬을 가져오지 못한 경우 (예: 구 버전 데이터)
            if (stat.RuntimeActiveSkills.Count == 0)
            {
               Debug.LogWarning("No active skills initialized.");
            }
        }

        private void ApplyStartingPassives()
        {
            if (passives == null) return;

            var preset = stat.SourcePreset;
            if (preset == null) return;

            foreach (var passive in preset.GetStartingPassives())
            {
                passives.AddPassive(passive);
            }
        }
        
        protected override void Awake()
        {
            // 자식 오브젝트에서 SpriteRenderer 찾기 (Visual 분리 지원)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

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
                
                while (elapsed < stepDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / stepDuration;
                    transform.position = Vector3.Lerp(startPos, endPos, t);
                    yield return null;
                }
                
                transform.position = endPos;
                currentTile = tile;
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
                Debug.Log($"{stat?.CharacterName} arrived at tile {arrivalTile.TileIndex}");
                arrivalTile.OnArrive(this);
            }

            // Notify pipeline about movement distance
            if (Pipeline.CombatPipeline.Instance != null)
            {
                var moveAction = new Pipeline.CombatAction("Move", Pipeline.ActionType.Utility, stepsTraveled);
                moveAction.AddTag("Move");
                var moveContext = new Pipeline.CombatContext(this, this, moveAction);
                Pipeline.CombatPipeline.Instance.Process(moveContext);
            }

            // 색상 복원
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
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
            Debug.Log($"[Character] {stat?.CharacterName} Start Turn");
            base.OnStartTurn();
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

        /// <summary>
        /// 데미지 처리 (파이프라인 외부 호출 대비)
        /// </summary>
        public override void TakeDamage(int damage)
        {
            base.TakeDamage(damage);
        }
        
        /// <summary>
        /// 캐릭터 선택됨 (CharacterSelector에서 호출)
        /// </summary>
        public void OnSelected()
        {
            Debug.Log($"{stat?.CharacterName} OnSelected called!");

            // ActionPanel 표시 - 주사위 드롭 대기 상태 (비활성화된 것도 찾기)
            var actionPanel = Object.FindFirstObjectByType<UI.ActionPanel>(FindObjectsInactive.Include);

            if (actionPanel != null)
            {
                Debug.Log("ActionPanel found! Showing panel...");
                actionPanel.ShowPanelForCharacter(this);
                Debug.Log($"{stat?.CharacterName} selected! Waiting for dice...");
            }
            else
            {
                Debug.LogError("ActionPanel NOT FOUND! Make sure ActionPanel component exists in scene.");
            }
        }
    }
}
