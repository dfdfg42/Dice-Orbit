using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
        
        [Header("Spawner")]
        [SerializeField] private Core.CharacterSpawner characterSpawner;

        [Header("Settings")]
        [SerializeField] private int numberOfChoices = 4;
        
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
        
        private void OnCharacterSelected(Core.CharacterPreset preset)
        {
            selectedCharacter = preset;
            characterSpawner?.Spawn(preset);

            if (selectionCanvas != null)
                selectionCanvas.gameObject.SetActive(false);
            else
                transform.parent.gameObject.SetActive(false);

            DiceUI.Instance?.SetPanelVisible(true);
            Core.GameFlowManager.Instance?.OnCharacterSelected();
        }
        
    }
}
