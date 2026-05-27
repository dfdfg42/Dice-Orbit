using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 캐릭터 선택 UI (유리병 3개 → 선택 시 detail 화면)
    /// </summary>
    public class CharacterSelectionUI : MonoBehaviour
    {
        [Header("Character Presets")]
        [SerializeField] private List<Core.CharacterPreset> allCharacters = new List<Core.CharacterPreset>();

        [Header("Card UI References")]
        [SerializeField] private Transform cardContainer;
        [SerializeField] private GameObject characterCardPrefab;
        [SerializeField] private Canvas selectionCanvas;

        [Header("Detail UI References")]
        [SerializeField] private GameObject detailRoot;
        [SerializeField] private RectTransform selectedBottleAnchor;
        [SerializeField] private TextMeshProUGUI detailNameText;
        [SerializeField] private TextMeshProUGUI detailDescriptionText;
        [SerializeField] private TextMeshProUGUI detailStatsText;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Image ldIllustrationImage;
        [SerializeField] private Button ldConfirmButton;

        [Header("Spawner")]
        [SerializeField] private Core.CharacterSpawner characterSpawner;

        [Header("Settings")]
        [SerializeField] private int numberOfChoices = 3;

        private List<Core.CharacterPreset> currentChoices = new List<Core.CharacterPreset>();
        private List<CharacterCard> currentCards = new List<CharacterCard>();
        private CharacterCard activeDetailCard;
        private Core.CharacterPreset activeDetailPreset;
        private bool isTransitioning;
        private Coroutine activeTransitionRoutine;

        private void Start()
        {
            if (selectionCanvas == null)
            {
                selectionCanvas = GetComponentInParent<Canvas>();
            }

            HideDetail();
            WireDetailButtons();
            GenerateRandomChoices();
        }

        private void WireDetailButtons()
        {
            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(OnCancelClicked);
            }

            if (ldConfirmButton != null)
            {
                ldConfirmButton.onClick.RemoveAllListeners();
                ldConfirmButton.onClick.AddListener(OnConfirmClicked);
            }
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
            HideDetail();
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

        private void GenerateRandomChoices()
        {
            foreach (Transform child in cardContainer)
            {
                Destroy(child.gameObject);
            }

            currentChoices.Clear();
            currentCards.Clear();
            activeDetailCard = null;
            activeDetailPreset = null;
            isTransitioning = false;

            if (allCharacters.Count >= numberOfChoices)
            {
                var shuffled = allCharacters.OrderBy(x => Random.value).ToList();
                currentChoices = shuffled.Take(numberOfChoices).ToList();
            }
            else
            {
                currentChoices = new List<Core.CharacterPreset>(allCharacters);
            }

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
                if (card == null) continue;

                card.CaptureIntroTargetPosition();
                card.PlayIntro(i * staggerDelay);
            }
        }

        private void OnCharacterSelectedRequested(CharacterCard selectedCard, Core.CharacterPreset character)
        {
            if (isTransitioning) return;
            if (selectedCard == null || character == null) return;

            if (activeTransitionRoutine != null) StopCoroutine(activeTransitionRoutine);
            activeTransitionRoutine = StartCoroutine(EnterDetailRoutine(selectedCard, character));
        }

        private IEnumerator EnterDetailRoutine(CharacterCard selectedCard, Core.CharacterPreset character)
        {
            isTransitioning = true;
            activeDetailCard = selectedCard;
            activeDetailPreset = character;

            foreach (var card in currentCards)
            {
                if (card == null) continue;
                card.SetSelectionLocked(true);
                if (card != selectedCard)
                {
                    card.SetDimmed(true);
                }
            }

            var anchorPos = selectedBottleAnchor != null
                ? selectedBottleAnchor.anchoredPosition
                : selectedCard.GetComponent<RectTransform>().anchoredPosition;

            yield return StartCoroutine(selectedCard.PlayDetailEntryRoutine(anchorPos));

            ShowDetail(character);
            isTransitioning = false;
            activeTransitionRoutine = null;
        }

        private void ShowDetail(Core.CharacterPreset character)
        {
            if (detailRoot != null) detailRoot.SetActive(true);

            if (detailNameText != null) detailNameText.text = character.CharacterName;
            if (detailDescriptionText != null) detailDescriptionText.text = character.Description;
            if (detailStatsText != null) detailStatsText.text = $"HP: {character.MaxHP}";

            if (ldIllustrationImage != null)
            {
                var sprite = character.CharacterWindowSprite != null ? character.CharacterWindowSprite : character.Portrait;
                ldIllustrationImage.sprite = sprite;
                ldIllustrationImage.enabled = sprite != null;
            }

            if (cancelButton != null) cancelButton.interactable = true;
            if (ldConfirmButton != null) ldConfirmButton.interactable = true;
        }

        private void HideDetail()
        {
            if (detailRoot != null) detailRoot.SetActive(false);
        }

        private void OnCancelClicked()
        {
            if (isTransitioning) return;
            if (activeDetailCard == null) return;

            if (activeTransitionRoutine != null) StopCoroutine(activeTransitionRoutine);
            activeTransitionRoutine = StartCoroutine(CancelDetailRoutine());
        }

        private IEnumerator CancelDetailRoutine()
        {
            isTransitioning = true;
            HideDetail();

            var returningCard = activeDetailCard;
            activeDetailCard = null;
            activeDetailPreset = null;

            yield return StartCoroutine(returningCard.PlayDetailReturnRoutine());

            foreach (var card in currentCards)
            {
                if (card == null) continue;
                card.SetDimmed(false);
                card.SetSelectionLocked(false);
            }

            isTransitioning = false;
            activeTransitionRoutine = null;
        }

        private void OnConfirmClicked()
        {
            if (isTransitioning) return;
            if (activeDetailCard == null || activeDetailPreset == null) return;

            if (activeTransitionRoutine != null) StopCoroutine(activeTransitionRoutine);
            activeTransitionRoutine = StartCoroutine(ConfirmDetailRoutine());
        }

        private IEnumerator ConfirmDetailRoutine()
        {
            isTransitioning = true;

            if (cancelButton != null) cancelButton.interactable = false;
            if (ldConfirmButton != null) ldConfirmButton.interactable = false;

            var preset = activeDetailPreset;
            var card = activeDetailCard;

            HideDetail();

            yield return StartCoroutine(card.PlayConfirmFallRoutine());

            FinalizeSelection(preset);
        }

        private void FinalizeSelection(Core.CharacterPreset preset)
        {
            characterSpawner?.Spawn(preset);

            if (selectionCanvas != null)
                selectionCanvas.gameObject.SetActive(false);
            else
                transform.parent.gameObject.SetActive(false);

            DiceUI.Instance?.SetPanelVisible(true);
            // 첫 영입이면 Combat으로, 웨이브 클리어 후 영입이면 Reward로 라우팅.
            Core.GameFlowManager.Instance?.OnRecruitComplete();
        }
    }
}
