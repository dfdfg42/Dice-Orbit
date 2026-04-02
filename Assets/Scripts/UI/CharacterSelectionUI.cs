using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Visuals;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 캐릭터 선택 UI
    /// </summary>
    public class CharacterSelectionUI : MonoBehaviour
    {
        [Header("Character Presets")]
        [SerializeField] private List<Core.CharacterPreset> allCharacters = new List<Core.CharacterPreset>();
        
        [Header("UI References")]
        [SerializeField] private Transform cardContainer;
        [SerializeField] private GameObject characterCardPrefab;
        [SerializeField] private Canvas selectionCanvas; // Canvas 직접 참조
        
        [Header("Prefabs")]
        [SerializeField] private GameObject characterUIPrefab; // CharacterUI 프리팹
        [SerializeField] private RuntimeAnimatorController characterAnimatorController;
        
        [Header("Settings")]
        [SerializeField] private int numberOfChoices = 4;
    [SerializeField] private Vector3 characterScale = new Vector3(0.3f, 0.3f, 1f);
    [SerializeField] private Vector2 colliderSizeMultiplier = new Vector2(1.6f, 1.6f);
    [SerializeField] private float colliderDepth = 0.2f;
        
        private List<Core.CharacterPreset> currentChoices = new List<Core.CharacterPreset>();
        private Core.CharacterPreset selectedCharacter;
        
        private void Start()
        {
            // Canvas 자동 찾기
            if (selectionCanvas == null)
            {
                selectionCanvas = GetComponentInParent<Canvas>();
            }
            
            GenerateRandomChoices();
        }

        public void Show()
        {
            if (selectionCanvas != null)
            {
                selectionCanvas.gameObject.SetActive(true);
            }
            else
            {
                gameObject.SetActive(true);
            }

            DiceUI.Instance?.SetPanelVisible(false);

            GenerateRandomChoices();
        }

        public void Hide()
        {
            if (selectionCanvas != null)
            {
                selectionCanvas.gameObject.SetActive(false);
            }
            else
            {
                gameObject.SetActive(false);
            }

            DiceUI.Instance?.SetPanelVisible(true);
        }
        
        /// <summary>
        /// 랜덤 캐릭터 4개 생성
        /// </summary>
        private void GenerateRandomChoices()
        {
            // 기존 카드 제거
            foreach (Transform child in cardContainer)
            {
                Destroy(child.gameObject);
            }
            
            currentChoices.Clear();
            
            // 랜덤 선택
            if (allCharacters.Count >= numberOfChoices)
            {
                var shuffled = allCharacters.OrderBy(x => Random.value).ToList();
                currentChoices = shuffled.Take(numberOfChoices).ToList();
            }
            else
            {
                currentChoices = new List<Core.CharacterPreset>(allCharacters);
            }
            
            // UI 카드 생성
            foreach (var character in currentChoices)
            {
                CreateCharacterCard(character);
            }
        }
        
        /// <summary>
        /// 캐릭터 카드 생성
        /// </summary>
        private void CreateCharacterCard(Core.CharacterPreset character)
        {
            if (characterCardPrefab == null)
            {
                Debug.LogError("Character Card Prefab not assigned!");
                return;
            }
            
            var cardObj = Instantiate(characterCardPrefab, cardContainer);
            var card = cardObj.GetComponent<CharacterCard>();
            
            if (card != null)
            {
                card.Setup(character, OnCharacterSelected);
            }
        }
        
        /// <summary>
        /// 캐릭터 선택 콜백
        /// </summary>
        private void OnCharacterSelected(Core.CharacterPreset character)
        {
            selectedCharacter = character;
            Debug.Log($"[CharacterSelection] Character selected: {character.CharacterName}");
            
            // 캐릭터 생성
            CreatePlayerCharacter(character);
            
            // Canvas 비활성화
            if (selectionCanvas != null)
            {
                Debug.Log("[CharacterSelection] Hiding selection canvas");
                selectionCanvas.gameObject.SetActive(false);
            }
            else
            {
                Debug.LogWarning("[CharacterSelection] Selection Canvas is null! Trying to hide parent.");
                transform.parent.gameObject.SetActive(false);
            }

            DiceUI.Instance?.SetPanelVisible(true);
            
            // GameFlowManager에 알림
            var gameFlow = Core.GameFlowManager.Instance;
            if (gameFlow != null)
            {
                gameFlow.OnCharacterSelected();
            }
            else
            {
                Debug.LogWarning("[CharacterSelection] GameFlowManager not found!");
            }
        }
        
        /// <summary>
        /// 플레이어 캐릭터 생성
        /// </summary>
        private void CreatePlayerCharacter(Core.CharacterPreset preset)
        {
            if (preset == null)
            {
                Debug.LogError("[CreatePlayer] Preset is null!");
                return;
            }
            
            Debug.Log($"[CreatePlayer] Creating character: {preset.CharacterName}");
            
            // OrbitManager 찾기
            var orbitManager = Object.FindAnyObjectByType<Core.OrbitManager>();
            if (orbitManager == null)
            {
                Debug.LogError("[CreatePlayer] OrbitManager not found! Cannot spawn character.");
                return;
            }
            
            Debug.Log("[CreatePlayer] OrbitManager found");
            
            // 시작 타일 찾기
            var startTile = orbitManager.GetTile(0);
            if (startTile == null)
            {
                Debug.LogError("[CreatePlayer] Start tile not found!");
                return;
            }
            
            Debug.Log($"[CreatePlayer] Start tile found at: {startTile.Position}");
            
            // Character GameObject 생성
            var characterObj = new GameObject($"Player_{preset.CharacterName}");
            characterObj.transform.localScale = characterScale;
            Debug.Log($"[CreatePlayer] GameObject created: {characterObj.name}");
            
            // SpriteRenderer 추가 (Character보다 먼저 추가)
            var spriteRenderer = characterObj.AddComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogError("[CreatePlayer] Failed to add SpriteRenderer!");
                return;
            }
            
            if (preset.CharacterSprite != null)
            {
                spriteRenderer.sprite = preset.CharacterSprite;
            }
            
            spriteRenderer.color = preset.SpriteColor;
            Debug.Log($"[CreatePlayer] SpriteRenderer configured with color: {preset.SpriteColor}");

            // CharacterSpriteVisual 추가 (애니메이션 스프라이트는 CharacterPreset 값만 사용)
            var spriteVisual = characterObj.AddComponent<CharacterSpriteVisual>();
            var animator = characterObj.GetComponent<Animator>();
            if (animator == null)
            {
                animator = characterObj.AddComponent<Animator>();
            }

            RuntimeAnimatorController resolvedController = preset.AnimatorController != null
                ? preset.AnimatorController
                : characterAnimatorController;

            if (resolvedController != null)
            {
                animator.runtimeAnimatorController = resolvedController;
            }
            else
            {
                Debug.LogWarning($"[CharacterSelection] Animator controller is not assigned for '{preset.CharacterName}'. Assign CharacterPreset.AnimatorController or fallback characterAnimatorController.");
            }

            if (spriteVisual != null)
            {
                spriteVisual.SetAnimationSprites(
                    preset.IdleSprite,
                    preset.MoveSprite,
                    preset.DamageSprite,
                    preset.SkillSprite
                );
                spriteVisual.PlayIdle();
            }
            
            // Character 컴포넌트 추가
            var character = characterObj.AddComponent<Core.Character>();
            if (character == null)
            {
                Debug.LogError("[CreatePlayer] Failed to add Character component!");
                Destroy(characterObj);
                return;
            }
            Debug.Log("[CreatePlayer] Character component added");
            
            // Collider 추가 (클릭 감지용)
            var collider = characterObj.AddComponent<BoxCollider>();
            FitColliderToSprite(collider, spriteRenderer, characterObj.transform);
            
            // Stats 설정
            var stats = preset.CreateStats();
            if (stats == null)
            {
                Debug.LogError("[CreatePlayer] Failed to create stats!");
                Destroy(characterObj);
                return;
            }
            
            character.InitializeStats(stats);
            Debug.Log("[CreatePlayer] Stats initialized");
            
            // 시작 타일에 배치
            var tileOffset = new Vector3(0, 1.5f, 1.0f);
            characterObj.transform.position = startTile.Position + tileOffset;
            Debug.Log($"[CreatePlayer] Character positioned at: {characterObj.transform.position}");
            
            // PartyManager에 추가
            var partyManager = Core.PartyManager.Instance;
            if (partyManager != null)
            {
                partyManager.AddCharacter(character);
                Debug.Log("[CreatePlayer] Added to PartyManager");
            }
            else
            {
                Debug.LogWarning("[CreatePlayer] PartyManager not found!");
            }
            
            // CharacterUI 생성
            CreateCharacterUI(characterObj, character);
            
            Debug.Log($"[CreatePlayer] ✓ Character spawned successfully: {preset.CharacterName}");
        }
        
        /// <summary>
        /// 캐릭터 UI 생성
        /// </summary>
        private void CreateCharacterUI(GameObject characterObj, Core.Character character)
        {
            if (characterUIPrefab == null)
            {
                Debug.LogWarning("[CreatePlayer] CharacterUI Prefab not assigned! Skipping UI creation.");
                return;
            }
            
            // 프리팹 인스턴스화
            var uiObj = Instantiate(characterUIPrefab, characterObj.transform);
            uiObj.transform.localPosition = new Vector3(0, 1.2f, 0);
            
            // CharacterUI 컴포넌트 찾기 및 설정
            var characterUI = uiObj.GetComponent<CharacterUI>();
            if (characterUI != null)
            {
                characterUI.SetCharacter(character);
                Debug.Log("[CreatePlayer] CharacterUI created from prefab");
            }
            else
            {
                Debug.LogWarning("[CreatePlayer] CharacterUI component not found on prefab!");
            }
        }

        private void FitColliderToSprite(BoxCollider collider, SpriteRenderer renderer, Transform target)
        {
            if (collider == null || renderer == null || renderer.sprite == null || target == null) return;

            var bounds = renderer.bounds;
            var localSize = new Vector3(
                bounds.size.x / target.localScale.x,
                bounds.size.y / target.localScale.y,
                colliderDepth
            );

            localSize = new Vector3(
                localSize.x * colliderSizeMultiplier.x,
                localSize.y * colliderSizeMultiplier.y,
                localSize.z
            );

            collider.size = localSize;
            collider.center = new Vector3(0f, localSize.y * 0.5f, 0f);
        }
    }
}
