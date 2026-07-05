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
        [SerializeField] private TextMeshProUGUI detailStatsText;
        [Tooltip("패시브 설명 박스 (스킬 데이터에서 자동 생성)")]
        [SerializeField] private TextMeshProUGUI detailPassiveText;
        [Tooltip("액티브 설명 박스 (스킬 데이터에서 자동 생성)")]
        [SerializeField] private TextMeshProUGUI detailActiveText;
        [Tooltip("(옵션) 분리 박스를 안 쓸 때만 사용하는 통합 설명 텍스트")]
        [SerializeField] private TextMeshProUGUI detailDescriptionText;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Image ldIllustrationImage;
        [SerializeField] private Button ldConfirmButton;

        [Header("Spawner")]
        [SerializeField] private Core.CharacterSpawner characterSpawner;

        [Header("Settings")]
        [SerializeField] private int numberOfChoices = 3;
        [Tooltip("시작 시 고를 캐릭터 수 (이 횟수만큼 선택을 반복)")]
        [SerializeField] private int charactersToSelect = 2;

        private List<Core.CharacterPreset> currentChoices = new List<Core.CharacterPreset>();
        private List<CharacterCard> currentCards = new List<CharacterCard>();
        private CharacterCard activeDetailCard;
        private Core.CharacterPreset activeDetailPreset;
        private bool isTransitioning;
        private Coroutine activeTransitionRoutine;

        private int selectedCount;
        private int sessionTargetCount = 1;
        private readonly List<Core.CharacterPreset> pickedPresets = new List<Core.CharacterPreset>();

        private void Start()
        {
            if (selectionCanvas == null)
            {
                selectionCanvas = GetComponentInParent<Canvas>();
            }

            HideDetail();
            WireDetailButtons();
            ResetSelectionSession();
            GenerateRandomChoices();
        }

        private void ResetSelectionSession()
        {
            selectedCount = 0;
            pickedPresets.Clear();

            // 파티가 비어있으면 첫 시작 → charactersToSelect명 선택,
            // 이미 파티원이 있으면 (웨이브 후 영입 등) → 1명만 선택.
            int partySize = Core.PartyManager.Instance != null ? Core.PartyManager.Instance.PartySize : 0;
            sessionTargetCount = (partySize == 0) ? Mathf.Max(1, charactersToSelect) : 1;
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
            BattleInfoPanelUI.SetVisible(false);   // 캐릭터 선택/모집 동안 정보 패널 숨김
            HideDetail();
            ResetSelectionSession();
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
            BattleInfoPanelUI.SetVisible(true);    // 전투 복귀 시 정보 패널 복원
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

            // 이미 고른 캐릭터는 다음 선택지에서 제외
            var pool = allCharacters.Where(c => c != null && !pickedPresets.Contains(c)).ToList();
            if (pool.Count >= numberOfChoices)
            {
                currentChoices = pool.OrderBy(x => Random.value).Take(numberOfChoices).ToList();
            }
            else
            {
                currentChoices = pool;
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
            if (detailStatsText != null) detailStatsText.text = $"HP: {character.MaxHP}";

            bool hasSplitBoxes = detailPassiveText != null || detailActiveText != null;
            if (detailPassiveText != null) detailPassiveText.text = BuildPassiveSummary(character);
            if (detailActiveText != null) detailActiveText.text = BuildActiveSummary(character);
            if (detailDescriptionText != null)
                detailDescriptionText.text = hasSplitBoxes ? string.Empty : character.Description;

            if (ldIllustrationImage != null)
            {
                var sprite = character.CharacterWindowSprite != null ? character.CharacterWindowSprite : character.Portrait;
                ldIllustrationImage.sprite = sprite;
                ldIllustrationImage.enabled = sprite != null;
            }

            if (cancelButton != null) cancelButton.interactable = true;
            if (ldConfirmButton != null) ldConfirmButton.interactable = true;
        }

        private string BuildPassiveSummary(Core.CharacterPreset character)
        {
            if (character?.StartingPassives == null) return string.Empty;

            var sb = new System.Text.StringBuilder();
            foreach (var passive in character.StartingPassives)
            {
                if (passive == null) continue;

                string name = string.IsNullOrWhiteSpace(passive.PassiveName) ? "패시브" : passive.PassiveName;
                string body = passive.GetDynamicDescription();
                if (string.IsNullOrWhiteSpace(body)) body = passive.Description;

                if (sb.Length > 0) sb.Append('\n').Append('\n');
                sb.Append("<b>[").Append(name).Append("]</b>");
                if (!string.IsNullOrWhiteSpace(body)) sb.Append('\n').Append(body);
            }
            return sb.ToString();
        }

        private string BuildActiveSummary(Core.CharacterPreset character)
        {
            if (character?.StartingActives == null) return string.Empty;

            var sb = new System.Text.StringBuilder();
            foreach (var active in character.StartingActives)
            {
                if (active == null) continue;

                string name = string.IsNullOrWhiteSpace(active.SkillName) ? "액티브" : active.SkillName;
                string body = active.GetDynamicDescription();
                if (string.IsNullOrWhiteSpace(body)) body = active.Description;

                if (sb.Length > 0) sb.Append('\n').Append('\n');
                sb.Append("<b>[").Append(name).Append("]</b>");
                if (!string.IsNullOrWhiteSpace(body)) sb.Append('\n').Append(body);
            }
            return sb.ToString();
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
            // 전체 타일을 최대 파티 인원으로 균등 분할해 배치 (현재 파티 인원 = 슬롯 인덱스)
            int slotIndex = Core.PartyManager.Instance != null ? Core.PartyManager.Instance.PartySize : selectedCount;
            int slotCount = Core.PartyManager.Instance != null ? Core.PartyManager.Instance.MaxPartySize : 4;
            characterSpawner?.Spawn(preset, slotIndex, slotCount);

            pickedPresets.Add(preset);
            selectedCount++;

            // 아직 더 골라야 하면 선택지를 새로 생성하고 선택 화면 유지
            if (selectedCount < sessionTargetCount)
            {
                HideDetail();
                GenerateRandomChoices();
                return;
            }

            // 모두 선택 완료 → 화면 닫고 진행
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
