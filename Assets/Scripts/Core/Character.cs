using UnityEngine;
using DiceOrbit.Data;
using System.Collections.Generic;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 게임 캐릭터 (플레이어)
    /// TestCharacter 기능 + 스탯 시스템 통합
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Character : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField] private CharacterStats stats = new CharacterStats();
        
        [Header("Movement")]
        [SerializeField] private TileData currentTile;
        [SerializeField] private int startTileIndex = 0;
        
        [Header("Visual")]
        [SerializeField] private Color highlightColor = Color.yellow;
        private SpriteRenderer spriteRenderer;
        private Color originalColor;
        private Camera mainCamera;
        
        [Header("System")]
        public Systems.Effects.StatusEffectManager StatusEffects;
        public Systems.Passives.PassiveManager Passives;

        // 타일 위 캐릭터 위치 offset
        private static readonly Vector3 TILE_OFFSET = new Vector3(0, 1.5f, 1.0f);

        
        // Properties
        public CharacterStats Stats => stats;
        public TileData CurrentTile => currentTile;
        public bool IsAlive => stats.IsAlive;
        
        /// <summary>
        /// Stats 초기화 (캐릭터 선택 후)
        /// </summary>
        public void InitializeStats(CharacterStats newStats)
        {
            stats = newStats;
            
            // 스프라이트 업데이트
            if (spriteRenderer != null)
            {
                if (stats.CharacterSprite != null)
                {
                    spriteRenderer.sprite = stats.CharacterSprite;
                }
                spriteRenderer.color = stats.SpriteColor;
                originalColor = spriteRenderer.color;
            }
            
            // 스킬 재초기화
            InitializeSkills();

            // Status & Passive 초기화 (컴포넌트가 없을 수 있으므로 확인/추가)
            if (StatusEffects == null) StatusEffects = gameObject.GetComponent<Systems.Effects.StatusEffectManager>() ?? gameObject.AddComponent<Systems.Effects.StatusEffectManager>();
            StatusEffects.Initialize(this);
            
            if (Passives == null) Passives = gameObject.GetComponent<Systems.Passives.PassiveManager>() ?? gameObject.AddComponent<Systems.Passives.PassiveManager>();
            Passives.Initialize(this);

            // Preset에서 Innate Passive 로드
            if (stats.SourcePreset != null)
            {
                foreach(var passive in stats.SourcePreset.NativePassives)
                {
                    Passives.AddPassive(passive);
                }
            }
            
            Debug.Log($"Character initialized: {stats.CharacterName} (HP: {stats.MaxHP}, ATK: {stats.Attack})");
        }
        
        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            
            if (spriteRenderer != null)
            {
                // 스탯에서 스프라이트 설정
                if (stats.CharacterSprite != null)
                {
                    spriteRenderer.sprite = stats.CharacterSprite;
                }
                
                spriteRenderer.color = stats.SpriteColor;
                originalColor = spriteRenderer.color;
            }
            else
            {
                Debug.LogWarning("SpriteRenderer not found! Add SpriteRenderer component.");
            }
            
            mainCamera = Camera.main;
            
            // 스킬 초기화
            InitializeSkills();

            // Status Effect System 초기화
            StatusEffects = GetComponent<Systems.Effects.StatusEffectManager>();
            if (StatusEffects == null) StatusEffects = gameObject.AddComponent<Systems.Effects.StatusEffectManager>();
            StatusEffects.Initialize(this);

            // Passive System 초기화
            Passives = GetComponent<Systems.Passives.PassiveManager>();
            if (Passives == null) Passives = gameObject.AddComponent<Systems.Passives.PassiveManager>();
            Passives.Initialize(this);
        }

        public void OnStartTurn()
        {
            if (StatusEffects != null)
            {
                StatusEffects.OnTurnStart();
            }
            
            if (Passives != null)
            {
                Passives.OnTurnStart();
            }
        }
        
        /// <summary>
        /// 스킬 초기화
        /// </summary>
        private void InitializeSkills()
        {
            // Preset에서 초기 스킬을 가져오지 못한 경우 (예: 구 버전 데이터)
            // 우선은 비어있으면 경고만. 실제로 DraftSystem으로 채워질 것임.
            if (stats.RuntimeActiveSkills.Count == 0)
            {
               Debug.LogWarning("No active skills initialized.");
            }
            
            // Passive 스킬 자동 적용
            ApplyPassiveSkills();
        }
        
        /// <summary>
        /// 패시브 스킬 자동 적용
        /// </summary>
        /// <summary>
        /// 패시브 스킬 자동 적용
        /// </summary>
        private void ApplyPassiveSkills()
        {
            // Refactor 2.0: Fixed Loadout
            if (stats.StartingPassive != null && Passives != null)
            {
                Passives.AddPassive(stats.StartingPassive);
                Debug.Log($"{stats.CharacterName}: Registered fixed passive '{stats.StartingPassive.PassiveName}'");
            }

            // Legacy support (optional, can be removed)
            foreach (var runtimePassive in stats.RuntimePassiveSkills)
            {
                // ...
            }
        }
        
        private void LateUpdate()
        {
            // Billboard: 항상 카메라를 향하도록
            if (mainCamera != null)
            {
                transform.rotation = mainCamera.transform.rotation;
            }
        }
        
        private void Start()
        {
            // 한 프레임 기다려서 OrbitManager가 타일 생성하도록 함
            StartCoroutine(InitializeAfterDelay());
        }
        
        private System.Collections.IEnumerator InitializeAfterDelay()
        {
            yield return null;
            
            Debug.Log($"Character {stats.CharacterName} initializing - Current Tile: {(currentTile != null ? "Assigned" : "NULL")}");
            
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
                    Debug.Log($"{stats.CharacterName} assigned to tile {currentTile.TileIndex} at position {currentTile.Position}");
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
                
                // Trigger Passive OnMove (distance 1 per tile)
                if(Passives != null) Passives.OnMove(1);
            }
            
            // 최종 도착
            var finalTile = path[path.Count - 1];
            Debug.Log($"{stats.CharacterName} arrived at tile {finalTile.TileIndex}");
            finalTile.OnArrive(this);

            // 색상 복원
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
            
            // Check LevelUp Tile
            if (finalTile.Type == TileType.LevelUp)
            {
                stats.LevelUp();
                // UI Removed in Refactor 2.0
                Debug.Log($"[Character] {stats.CharacterName} leveled up! (No UI yet)");
            }
        }

        public void TakeDamage(int rawDamage)
        {
            // 1. Evasion Check
            float finalEvasion = stats.Evasion;
            if (StatusEffects != null && StatusEffects.HasEffect(StatusEffectType.EvasionUp))
            {
                // Assuming EvasionUp Value is percentage (e.g. 15 for 15%)
                // Or flat value 0.15? Let's assume Integer 15 = 15%.
                // Wait, CharacterStats.Evasion is float (0.0~1.0). StatusEffect.Value is int.
                // Converting: 15 -> 0.15f
                finalEvasion += (StatusEffects.GetEffectValue(StatusEffectType.EvasionUp) / 100f);
            }
            
            if (Random.value < finalEvasion)
            {
                Debug.Log($"** MISS! ** {stats.CharacterName} evaded the attack!");
                return;
            }

            // 2. Defense Calculation
            float finalDefense = stats.Defense;
            
            if (StatusEffects != null)
            {
                if (StatusEffects.HasEffect(StatusEffectType.DefenseUp))
                    finalDefense += StatusEffects.GetEffectValue(StatusEffectType.DefenseUp);
                
                if (StatusEffects.HasEffect(StatusEffectType.DefenseDown))
                    finalDefense -= StatusEffects.GetEffectValue(StatusEffectType.DefenseDown);
            }
            
            int actualDamage = Mathf.Max(1, rawDamage - (int)finalDefense);
            stats.TakeDamage(actualDamage); // Assuming CharacterStats has basic logic
            // stats.CurrentHP -= actualDamage; // If direct modification needed
            
            if (!IsAlive)
            {
                gameObject.SetActive(false);
                Debug.Log($"{stats.CharacterName} died.");
            }
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
        /// /// </summary>
        public void UseSkillByIndex(int skillIndex, int diceValue)
        {
            if (stats.RuntimeActiveSkills.Count == 0)
            {
                Debug.LogWarning($"{stats.CharacterName}: No active skills available!");
                return;
            }
            
            if (skillIndex < 0 || skillIndex >= stats.RuntimeActiveSkills.Count)
            {
                Debug.LogWarning($"{stats.CharacterName}: Invalid skill index {skillIndex}!");
                return;
            }
            
            var runtimeSkill = stats.RuntimeActiveSkills[skillIndex];
            var skillData = runtimeSkill.ToSkillData();

            if (skillData == null) return;
            
            if (!skillData.CanUse(diceValue))
            {
                Debug.LogWarning($"{stats.CharacterName}: Cannot use {skillData.SkillName} with dice value {diceValue}. {skillData.Requirement.GetDescription()}");
                return;
            }
            
            Debug.Log($"{stats.CharacterName} preparing {skillData.SkillName} with dice {diceValue}!");
            
            // CombatManager 확인
            var combatManager = CombatManager.Instance;
            if (combatManager == null || !combatManager.InCombat)
            {
                Debug.LogWarning("Not in combat or CombatManager not found!");
                return;
            }
            
            // Effect 기반 스킬이면 Effect 시스템 사용
            if (skillData.Effects.Count > 0)
            {
                ExecuteEffectBasedSkill(skillData, diceValue);
            }
            else
            {
                // 레거시 스킬 (하위 호환)
                ExecuteLegacySkill(skillData, diceValue);
            }
        }
        
        /// <summary>
        /// Effect 기반 스킬 실행
        /// </summary>
        private void ExecuteEffectBasedSkill(SkillData skill, int diceValue)
        {
            // 타겟 선택 모드 시작
            var targetSelector = SkillTargetSelector.Instance;
            if (targetSelector != null)
            {
                // 임시: 스킬과 주사위 값 저장
                targetSelector.StartTargetSelection(this, skill, diceValue);
            }
            else
            {
                Debug.LogError("SkillTargetSelector not found! Add to scene.");
            }
        }
        
        /// <summary>
        /// 레거시 스킬 실행 (하위 호환)
        /// </summary>
        private void ExecuteLegacySkill(SkillData skill, int diceValue)
        {
            // 타겟 선택 모드 시작
            var targetSelector = SkillTargetSelector.Instance;
            if (targetSelector != null)
            {
                targetSelector.StartTargetSelection(this, skill, diceValue);
            }
            else
            {
                Debug.LogError("SkillTargetSelector not found! Add to scene.");
            }
        }
        
        /// <summary>
        /// 시작 타일 찾기 (재시도용)
        /// </summary>
        private void FindStartTile()
        {
            var orbitManager = FindFirstObjectByType<OrbitManager>();
            if (orbitManager != null)
            {
                Debug.Log($"OrbitManager found! Tile count: {orbitManager.TileCount}");
                currentTile = orbitManager.GetTile(startTileIndex);
                
                if (currentTile != null)
                {
                    Debug.Log($"Assigned to tile {currentTile.TileIndex} at position {currentTile.Position}");
                    transform.position = currentTile.Position + TILE_OFFSET;
                }
            }
        }
        
        /// <summary>
        /// 캐릭터 선택됨 (CharacterSelector에서 호출)
        /// </summary>
        public void OnSelected()
        {
            Debug.Log($"{stats.CharacterName} OnSelected called!");
            
            // ActionPanel 표시 - 주사위 드롭 대기 상태 (비활성화된 것도 찾기)
            var actionPanel = Object.FindFirstObjectByType<UI.ActionPanel>(FindObjectsInactive.Include);
            
            if (actionPanel != null)
            {
                Debug.Log("ActionPanel found! Showing panel...");
                actionPanel.ShowPanelForCharacter(this);
                Debug.Log($"{stats.CharacterName} selected! Waiting for dice...");
            }
            else
            {
                Debug.LogError("ActionPanel NOT FOUND! Make sure ActionPanel component exists in scene.");
            }
        }
        
        /// <summary>
        /// 마우스 호버 시 하이라이트
        /// </summary>
        private void OnMouseEnter()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = highlightColor;
            }
        }
        
        /// <summary>
        /// 마우스 나갈 때 원래 색상
        /// </summary>
        private void OnMouseExit()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
        }
    }
}
