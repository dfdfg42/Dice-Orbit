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
        }
        
        /// <summary>
        /// 스킬 초기화
        /// </summary>
        private void InitializeSkills()
        {
            // Active 스킬이 없으면 기본 스킬 추가
            if (stats.ActiveSkills.Count == 0)
            {
                var defaultSkill = new SkillData
                {
                    SkillName = "Basic Attack",
                    Type = SkillType.Active,
                    TargetType = SkillTargetType.SingleEnemy,
                    DamageMultiplier = 1
                };
                stats.ActiveSkills.Add(defaultSkill);
                Debug.Log($"{stats.CharacterName}: Added default Basic Attack skill");
            }
            
            // Passive 스킬 자동 적용
            ApplyPassiveSkills();
        }
        
        /// <summary>
        /// 패시브 스킬 자동 적용
        /// </summary>
        private void ApplyPassiveSkills()
        {
            foreach (var passive in stats.PassiveSkills)
            {
                if (passive.Effects.Count > 0)
                {
                    Systems.EffectManager.ApplyEffects(passive.Effects, this);
                    Debug.Log($"{stats.CharacterName}: Applied passive skill '{passive.SkillName}'");
                }
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
            var orbitManager = FindObjectOfType<OrbitManager>();
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
        /// 주사위로 이동
        /// </summary>
        public void Move(int steps)
        {
            Debug.Log($"{stats.CharacterName} Move called with {steps} steps. Current tile: {(currentTile != null ? currentTile.TileIndex.ToString() : "NULL")}");
            
            // 타일이 없으면 다시 찾기 시도
            if (currentTile == null)
            {
                Debug.LogWarning($"{stats.CharacterName}: No current tile! Attempting to find tile...");
                FindStartTile();
            }
            
            // 여전히 없으면 에러
            if (currentTile == null)
            {
                Debug.LogError($"{stats.CharacterName}: Still no current tile after retry! Cannot move.");
                return;
            }
            
            Debug.Log($"{stats.CharacterName} moving {steps} steps from tile {currentTile.TileIndex}");
            
            // 타일 경로 계산
            var tilePath = new List<TileData>();
            TileData currentStep = currentTile;
            
            for (int i = 0; i < steps; i++)
            {
                if (currentStep.NextTile == null)
                {
                    Debug.LogError($"NextTile is null at step {i}! Tiles may not be connected properly.");
                    break;
                }
                currentStep = currentStep.NextTile;
                tilePath.Add(currentStep);
            }
            
            if (tilePath.Count > 0)
            {
                // 마지막 타일로 currentTile 업데이트
                currentTile = tilePath[tilePath.Count - 1];
                
                // 타일을 하나씩 이동
                StartCoroutine(MoveStepByStep(tilePath));
            }
            else
            {
                Debug.LogWarning($"{stats.CharacterName}: No valid path found");
            }
        }
        
        /// <summary>
        /// 타일을 하나씩 거쳐서 이동
        /// </summary>
        private System.Collections.IEnumerator MoveStepByStep(List<TileData> path)
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
            }
            
            // 최종 도착
            var finalTile = path[path.Count - 1];
            Debug.Log($"{stats.CharacterName} arrived at tile {finalTile.TileIndex}");
            
            // 색상 복원
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
            
            // 레벨업 타일이면 레벨업
            if (finalTile.Type == TileType.LevelUp)
            {
                stats.LevelUp();
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
            if (stats.ActiveSkills.Count == 0)
            {
                Debug.LogWarning($"{stats.CharacterName}: No active skills available!");
                return;
            }
            
            if (skillIndex < 0 || skillIndex >= stats.ActiveSkills.Count)
            {
                Debug.LogWarning($"{stats.CharacterName}: Invalid skill index {skillIndex}!");
                return;
            }
            
            var skill = stats.ActiveSkills[skillIndex];
            
            if (!skill.CanUse(diceValue))
            {
                Debug.LogWarning($"{stats.CharacterName}: Cannot use {skill.SkillName} with dice value {diceValue}. {skill.Requirement.GetDescription()}");
                return;
            }
            
            Debug.Log($"{stats.CharacterName} preparing {skill.SkillName} with dice {diceValue}!");
            
            // CombatManager 확인
            var combatManager = CombatManager.Instance;
            if (combatManager == null || !combatManager.InCombat)
            {
                Debug.LogWarning("Not in combat or CombatManager not found!");
                return;
            }
            
            // Effect 기반 스킬이면 Effect 시스템 사용
            if (skill.Effects.Count > 0)
            {
                ExecuteEffectBasedSkill(skill, diceValue);
            }
            else
            {
                // 레거시 스킬 (하위 호환)
                ExecuteLegacySkill(skill, diceValue);
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
            var orbitManager = FindObjectOfType<OrbitManager>();
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
            var actionPanel = FindObjectOfType<UI.ActionPanel>(true);
            
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
