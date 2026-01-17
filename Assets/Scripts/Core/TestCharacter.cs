using UnityEngine;
using UnityEngine.EventSystems;
using DiceOrbit.Data;
using DiceOrbit.UI;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 테스트용 캐릭터 (Phase 3 전까지 임시)
    /// </summary>
    public class TestCharacter : MonoBehaviour, IDropZone
    {
        [Header("Settings")]
        [SerializeField] private string characterName = "Test Hero";
        [SerializeField] private TileData currentTile;  // Inspector에서 수동 할당 가능
        [SerializeField] private int startTileIndex = 0; // 시작 타일 인덱스 (0 = 레벨업 타일)
        
        [Header("Visual")]
        [SerializeField] private Color highlightColor = Color.yellow;
        private SpriteRenderer spriteRenderer;
        private Color originalColor;
        private Camera mainCamera;
        
        private void Awake()
        {
            // SpriteRenderer 찾기
            spriteRenderer = GetComponent<SpriteRenderer>();
            
            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
            }
            else
            {
                Debug.LogWarning("SpriteRenderer not found! Add SpriteRenderer component.");
            }
            
            mainCamera = Camera.main;
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
            // 한 프레임 대기
            yield return null;
            
            Debug.Log($"TestCharacter Start - Current Tile: {(currentTile != null ? "Assigned" : "NULL")}");
            
            // 이미 Inspector에서 타일이 할당되었으면 사용
            if (currentTile != null)
            {
                Debug.Log($"Using manually assigned tile: {currentTile.TileIndex}");
                transform.position = currentTile.Position + Vector3.up * 0.5f;
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
                    Debug.Log($"Assigned to tile {currentTile.TileIndex} at position {currentTile.Position}");
                    transform.position = currentTile.Position + Vector3.up * 0.5f;
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
        /// 주사위로 이동 (테스트)
        /// </summary>
        public void Move(int steps)
        {
            Debug.Log($"{characterName} Move called with {steps} steps. Current tile: {(currentTile != null ? currentTile.TileIndex.ToString() : "NULL")}");
            
            // 타일이 없으면 다시 찾기 시도
            if (currentTile == null)
            {
                Debug.LogWarning($"{characterName}: No current tile! Attempting to find tile...");
                FindStartTile();
            }
            
            // 여전히 없으면 에러
            if (currentTile == null)
            {
                Debug.LogError($"{characterName}: Still no current tile after retry! Cannot move.");
                return;
            }
            
            Debug.Log($"{characterName} moving {steps} steps from tile {currentTile.TileIndex}");
            
            // 타일 경로 계산
            var tilePath = new System.Collections.Generic.List<TileData>();
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
                Debug.LogWarning($"{characterName}: No valid path found");
            }
        }
        
        /// <summary>
        /// 타일을 하나씩 거쳐서 이동
        /// </summary>
        private System.Collections.IEnumerator MoveStepByStep(System.Collections.Generic.List<TileData> path)
        {
            float stepDuration = 0.2f; // 각 타일 이동에 걸리는 시간
            
            foreach (var tile in path)
            {
                Vector3 startPos = transform.position;
                Vector3 endPos = tile.Position + Vector3.up * 0.5f;
                float elapsed = 0f;
                
                // 한 타일 이동
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
            Debug.Log($"{characterName} arrived at tile {finalTile.TileIndex}");
            
            // 색상 복원
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
            
            // 레벨업 타일이면 레벨업
            if (finalTile.Type == TileType.LevelUp)
            {
                Debug.Log($"{characterName} LEVEL UP!");
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
                    transform.position = currentTile.Position + Vector3.up * 0.5f;
                }
            }
        }
        
        /// <summary>
        /// 타일로 이동하는 코루틴
        /// </summary>
        private System.Collections.IEnumerator MoveToTile(TileData tile)
        {
            Vector3 startPos = transform.position;
            Vector3 endPos = tile.Position + Vector3.up * 0.5f;
            float duration = 0.5f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                transform.position = Vector3.Lerp(startPos, endPos, t);
                yield return null;
            }
            
            transform.position = endPos;
            Debug.Log($"{characterName} arrived at tile {tile.TileIndex}");
            
            // 색상 복원
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
            
            // 레벨업 타일이면 레벨업
            if (tile.Type == TileType.LevelUp)
            {
                Debug.Log($"{characterName} LEVEL UP!");
            }
        }
        
        /// <summary>
        /// 스킬 사용 (테스트)
        /// </summary>
        public void UseSkill(int power)
        {
            Debug.Log($"{characterName} uses skill with power: {power}");
            // TODO: Phase 4에서 실제 스킬 시스템 구현
        }
        
        /// <summary>
        /// IDropZone 구현
        /// </summary>
        public object GetDropTarget()
        {
            return this;
        }
        
        /// <summary>
        /// 캐릭터 선택됨 (CharacterSelector에서 호출)
        /// </summary>
        public void OnSelected()
        {
            Debug.Log($"{characterName} OnSelected called!");
            
            // ActionPanel 표시 - 주사위 드롭 대기 상태 (비활성화된 것도 찾기)
            var actionPanel = FindObjectOfType<UI.ActionPanel>(true);  // true = 비활성화된 것도 찾기
            
            if (actionPanel != null)
            {
                Debug.Log("ActionPanel found! Showing panel...");
                actionPanel.ShowPanelForCharacter(this);
                Debug.Log($"{characterName} selected! Waiting for dice...");
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
