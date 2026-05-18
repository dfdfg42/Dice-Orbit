using UnityEngine;
using UnityEngine.UI;
using System.Collections;
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
        [SerializeField] private Image characterWindowImage; // 왼쪽에 표시할 창 이미지
        [SerializeField] private Button confirmButton; // 선택 확인 버튼
        
        [Header("Spawner")]
        [SerializeField] private Core.CharacterSpawner characterSpawner;

        [Header("Settings")]
        [SerializeField] private int numberOfChoices = 4;
        
        private List<Core.CharacterPreset> currentChoices = new List<Core.CharacterPreset>();
        private List<CharacterCard> currentCards = new List<CharacterCard>();
        private Core.CharacterPreset selectedCharacter;
        private bool isSelectionSequencePlaying;
        
        private void Start()
        {
            // Canvas 자동 찾기
            if (selectionCanvas == null)
            {
                selectionCanvas = GetComponentInParent<Canvas>();
            }

            if (characterWindowImage != null)
            {
                characterWindowImage.gameObject.SetActive(false);
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
            currentCards.Clear();
            isSelectionSequencePlaying = false;
            
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
            var spawnedCards = new List<CharacterCard>();
            foreach (var character in currentChoices)
            {
                var card = CreateCharacterCard(character);
                if (card != null)
                {
                    spawnedCards.Add(card);
                    currentCards.Add(card);
                }
            }

            if (spawnedCards.Count > 0)
            {
                StartCoroutine(PlayCharacterCardIntro(spawnedCards));
            }
        }
        
        /// <summary>
        /// 캐릭터 카드 생성
        /// </summary>
        private CharacterCard CreateCharacterCard(Core.CharacterPreset character)
        {
            if (characterCardPrefab == null)
            {
                Debug.LogError("Character Card Prefab not assigned!");
                return null;
            }
            
            var cardObj = Instantiate(characterCardPrefab, cardContainer);
            var card = cardObj.GetComponent<CharacterCard>();
            
            if (card != null)
            {
                card.Setup(character, OnCharacterSelectedRequested);
            }

            return card;
        }

        private IEnumerator PlayCharacterCardIntro(List<CharacterCard> cards)
        {
            yield return null;

            const float staggerDelay = 0.08f;

            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card == null)
                {
                    continue;
                }

                card.CaptureIntroTargetPosition();
                card.PlayIntro(i * staggerDelay);
            }
        }
        
        private void OnCharacterSelectedRequested(CharacterCard selectedCard, Core.CharacterPreset character)
        {
            if (isSelectionSequencePlaying) return;
            StartCoroutine(PlayCharacterSelectionSequence(selectedCard, character));
        }

        private IEnumerator PlayCharacterSelectionSequence(CharacterCard selectedCard, Core.CharacterPreset character)
        {
            isSelectionSequencePlaying = true;

            for (int i = 0; i < currentCards.Count; i++)
            {
                var card = currentCards[i];
                if (card != null) card.SetSelectionLocked(true);
            }

            var selectedIndex = currentCards.IndexOf(selectedCard);
            if (selectedIndex < 0) selectedIndex = 0;

            var animationOrder = BuildSelectionOrder(selectedIndex);
            for (int i = 0; i < animationOrder.Count; i++)
            {
                var cardIndex = animationOrder[i];
                if (cardIndex < 0 || cardIndex >= currentCards.Count) continue;
                var card = currentCards[cardIndex];
                if (card == null) continue;

                StartCoroutine(card.PlaySelectionExitRoutine(cardIndex == selectedIndex));
                yield return new WaitForSeconds(0.2f);
            }

            yield return new WaitForSeconds(0.5f);
            DisplayCharacterWindow(character);
        }

        private List<int> BuildSelectionOrder(int selectedIndex)
        {
            var order = new List<int>();
            var count = currentCards.Count;
            if (count <= 0) return order;

            selectedIndex = Mathf.Clamp(selectedIndex, 0, count - 1);
            order.Add(selectedIndex);
            for (int i = selectedIndex - 1; i >= 0; i--) order.Add(i);
            for (int i = selectedIndex + 1; i < count; i++) order.Add(i);
            return order;
        }

        private void DisplayCharacterWindow(Core.CharacterPreset character)
        {
            if (character == null) return;

            if (characterWindowImage != null)
            {
                var windowSprite = character.CharacterWindowSprite;
                if (windowSprite != null)
                {
                    characterWindowImage.sprite = windowSprite;
                    characterWindowImage.enabled = true;
                    characterWindowImage.gameObject.SetActive(true);
                    characterWindowImage.transform.SetAsLastSibling();
                }
                else
                {
                    characterWindowImage.gameObject.SetActive(false);
                }
            }

            if (confirmButton != null)
            {
                confirmButton.gameObject.SetActive(true);
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(() => OnCharacterSelected(character));
            }
            else
            {
                OnCharacterSelected(character);
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
