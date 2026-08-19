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

        /// <summary>전체 캐릭터 풀 (상점 교체 후보 등 외부 조회용 — ShopUI).</summary>
        public IReadOnlyList<Core.CharacterPreset> AllCharacters => allCharacters;

        /// <summary>튜토리얼 step 13 — 아직 골라야 할 캐릭터 수 (0이면 선택 완료).</summary>
        public int RemainingToSelect => Mathf.Max(0, sessionTargetCount - selectedCount);
        /// <summary>튜토리얼 step 13 하이라이트 대상 — 카드 컨테이너.</summary>
        public RectTransform CardContainerRect => cardContainer as RectTransform;

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
        [Tooltip("LeftDescriptionPanel이 일자에서 위아래로 펼쳐지는 시간(초)")]
        [SerializeField] private float panelUnfoldDuration = 0.3f;
        [Header("Shade (패널 전체 어둡게)")]
        [Tooltip("반투명 검정 알파 (0~1). 190/255 ≈ 0.745")]
        [SerializeField, Range(0f, 1f)] private float topShadeStrength = 0.745f;

        private List<Core.CharacterPreset> currentChoices = new List<Core.CharacterPreset>();
        private List<CharacterCard> currentCards = new List<CharacterCard>();
        private CharacterCard activeDetailCard;
        private Core.CharacterPreset activeDetailPreset;
        private bool isTransitioning;
        private Coroutine activeTransitionRoutine;
        private Coroutine _unfoldCo;
        private RectTransform _leftPanel;
        private Vector3 _leftPanelFullScale = Vector3.one;
        private Image _topShade;

        private int selectedCount;
        private int sessionTargetCount = 1;
        private readonly List<Core.CharacterPreset> pickedPresets = new List<Core.CharacterPreset>();

        private void Start()
        {
            if (selectionCanvas == null)
            {
                selectionCanvas = GetComponentInParent<Canvas>();
            }

            if (detailRoot != null)
            {
                _leftPanel = detailRoot.transform.Find("LeftDescriptionPanel") as RectTransform;
                if (_leftPanel != null) { _leftPanelFullScale = _leftPanel.localScale; EnsureTopShade(); }
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

            // LeftDescriptionPanel을 가로선(일자)에서 위아래로 펼치는 연출 (center pivot → 위아래 양방향).
            if (_leftPanel != null)
            {
                _leftPanel.localScale = new Vector3(_leftPanelFullScale.x, 0f, _leftPanelFullScale.z);
                if (_unfoldCo != null) StopCoroutine(_unfoldCo);
                _unfoldCo = StartCoroutine(UnfoldPanel(_leftPanel, _leftPanelFullScale, panelUnfoldDuration));
            }
        }

        /// <summary>패널을 세로 스케일 0 → full 로 펼침 (EaseOutBack 살짝 튕김).</summary>
        private IEnumerator UnfoldPanel(RectTransform panel, Vector3 full, float duration)
        {
            if (panel == null) yield break;
            if (duration <= 0f) { panel.localScale = full; _unfoldCo = null; yield break; }
            float e = 0f;
            while (e < duration)
            {
                e += Time.unscaledDeltaTime;
                float k = EaseOutBack(Mathf.Clamp01(e / duration));
                panel.localScale = new Vector3(full.x, k * full.y, full.z);
                yield return null;
            }
            panel.localScale = full;
            _unfoldCo = null;
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = 1.70158f + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        /// <summary>LeftDescriptionPanel 전체를 균일하게 어둡게 하는 반투명 검정 오버레이를 배경 위·내용 아래에 1회 생성.
        /// 패널 자식이라 언폴드 때 함께 나타난다.</summary>
        private void EnsureTopShade()
        {
            if (_leftPanel == null || _topShade != null) return;

            var go = new GameObject("Shade", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_leftPanel, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.SetAsFirstSibling();   // 배경 위 · 내용(텍스트/일러스트) 아래

            _topShade = go.AddComponent<Image>();
            _topShade.color = new Color(0f, 0f, 0f, topShadeStrength);   // 그라데이션 없이 균일 반투명
            _topShade.raycastTarget = false;
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

                // 발동 조건 표기 — 제한이 없으면 FormatDiceCondition이 "주사위"만 돌려주므로 생략
                string cond = active.FormatDiceCondition();
                bool hasCond = !string.IsNullOrWhiteSpace(cond) && cond != "주사위";

                if (sb.Length > 0) sb.Append('\n').Append('\n');
                sb.Append("<b>[").Append(name).Append("]</b>");
                if (hasCond) sb.Append(" <alpha=#B4>(조건: ").Append(cond).Append(")</alpha>");
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
