using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DiceOrbit.Data;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Skills;
using DiceOrbit.Data.Skills.Effects;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 페르소나 스타일 캐릭터 액션 UI
    /// 기존 ActionPanel.cs를 대체합니다.
    /// </summary>
    public class CharacterActionUI : MonoBehaviour
    {
        public static CharacterActionUI Instance { get; private set; }

        [Header("패널 루트 (슬라이드 대상)")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Vector2 hiddenPosition = new Vector2(600f, -200f);  // 화면 오른쪽 바깥
        [SerializeField] private Vector2 shownPosition  = new Vector2(-20f,  -20f);  // 오른쪽 하단
        [SerializeField] private float slideInDuration  = 0.25f;

        [Header("초상화")]
        [SerializeField] private Image portraitImage;

        [Header("메인 버튼")]
        [SerializeField] private Button moveButton;
        [SerializeField] private Button skillButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private float buttonStaggerDelay = 0.07f;

        [Header("스킬 선택 패널")]
        [SerializeField] private GameObject skillSelectPanel;
        [SerializeField] private Transform skillButtonContainer;
        [SerializeField] private GameObject skillSelectButtonPrefab;

        [Header("오버레이")]
        [SerializeField] private TargetSelectionOverlay overlay;

        // 런타임 상태
        private Character    currentCharacter;
        private DiceData     currentDice;
        private bool         waitingForDice = false;
        private OrbitManager orbitManager;

        private List<RectTransform> actionButtons = new List<RectTransform>();
        private Coroutine slideCoroutine;

        // ─────────────────────────────────────────────
        // 초기화
        // ─────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // 버튼 이벤트
            moveButton?.onClick.AddListener(OnMoveClicked);
            skillButton?.onClick.AddListener(OnSkillClicked);
            cancelButton?.onClick.AddListener(OnCancelClicked);

            // 오버레이 취소 이벤트
            if (overlay != null)
                overlay.OnOverlayCancelled += OnCancelClicked;

            // 버튼 목록 (스태거용)
            if (moveButton  != null) actionButtons.Add(moveButton.GetComponent<RectTransform>());
            if (skillButton != null) actionButtons.Add(skillButton.GetComponent<RectTransform>());

            // 초기 상태: 숨김
            if (panelRoot != null) panelRoot.anchoredPosition = hiddenPosition;
            if (skillSelectPanel != null) skillSelectPanel.SetActive(false);
            SetButtonsInteractable(false);
        }

        private void Start()
        {
            orbitManager = FindAnyObjectByType<OrbitManager>();
        }

        // ─────────────────────────────────────────────
        // 공개 API
        // ─────────────────────────────────────────────

        /// <summary>캐릭터 선택 시 패널 표시</summary>
        public void Show(Character character)
        {
            currentCharacter = character;
            currentDice      = null;
            waitingForDice   = true;

            // 초상화 설정
            if (portraitImage != null && character.Stats?.CharacterSprite != null)
                portraitImage.sprite = character.Stats.CharacterSprite;

            SetButtonsInteractable(false);
            if (skillSelectPanel != null) skillSelectPanel.SetActive(false);

            // 슬라이드 인
            StopSlide();
            slideCoroutine = StartCoroutine(SlideIn());
        }

        /// <summary>패널 숨기기</summary>
        public void Hide()
        {
            overlay?.Hide();
            HoverTooltipUI.Instance?.HidePinned();
            if (skillSelectPanel != null) skillSelectPanel.SetActive(false);

            StopSlide();
            slideCoroutine = StartCoroutine(SlideOut());

            currentCharacter = null;
            currentDice      = null;
            waitingForDice   = false;
        }

        /// <summary>주사위 드롭 처리 (DiceElement에서 호출)</summary>
        public void OnDiceDropped(DiceData dice)
        {
            if (!waitingForDice || currentCharacter == null) return;

            currentDice    = dice;
            waitingForDice = false;

            SetButtonsInteractable(true);
        }

        // ─────────────────────────────────────────────
        // 버튼 핸들러
        // ─────────────────────────────────────────────

        private void OnMoveClicked()
        {
            if (currentDice == null || currentCharacter == null) return;

            var diceManager = DiceManager.Instance;
            if (diceManager != null)
            {
                bool success = diceManager.AssignDice(currentDice, currentCharacter, ActionType.Move);
                if (success)
                {
                    orbitManager?.Move(currentCharacter, currentDice.Value);
                    MarkDiceUsed();
                    ReturnDiceElement();
                }
            }
            Hide();
        }

        private void OnSkillClicked()
        {
            if (currentDice == null || currentCharacter == null) return;

            PopulateSkillList(currentCharacter);
            if (skillSelectPanel != null) skillSelectPanel.SetActive(true);
            overlay?.Show();
        }

        private void OnCancelClicked()
        {
            ReturnDiceElement();
            Hide();
        }

        private void OnSpecificSkillClicked(int index)
        {
            ExecuteSkill(index);
        }

        // ─────────────────────────────────────────────
        // 내부 로직
        // ─────────────────────────────────────────────

        private void PopulateSkillList(Character character)
        {
            if (skillButtonContainer == null || skillSelectButtonPrefab == null) return;

            foreach (Transform child in skillButtonContainer) Destroy(child.gameObject);

            var skills = character.Stats.RuntimeActiveSkills;
            for (int i = 0; i < skills.Count; i++)
            {
                int index = i;
                var runtimeSkill = skills[i];
                var go    = Instantiate(skillSelectButtonPrefab, skillButtonContainer);

                var txt = go.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null)
                {
                    var skillName = runtimeSkill.BaseSkill != null ? runtimeSkill.BaseSkill.SkillName : "Unknown Skill";
                    txt.text = $"{skillName} (Lv.{runtimeSkill.CurrentLevel})";
                }

                var imgs = go.GetComponentsInChildren<Image>();
                if (imgs.Length > 1 && runtimeSkill.BaseSkill != null && runtimeSkill.BaseSkill.Icon != null) imgs[1].sprite = runtimeSkill.BaseSkill.Icon;

                var hoverPreview = go.GetComponent<SkillPreviewHoverUI>();
                if (hoverPreview == null) hoverPreview = go.AddComponent<SkillPreviewHoverUI>();
                hoverPreview.SetPreview(BuildDamagePreview(runtimeSkill));

                var btn = go.GetComponent<Button>();
                btn?.onClick.AddListener(() => OnSpecificSkillClicked(index));
            }
        }

        private string BuildDamagePreview(RuntimeSkill runtimeSkill)
        {
            if (runtimeSkill == null || runtimeSkill.BaseSkill == null || currentCharacter == null || currentDice == null)
                return "예상: -";

            var skillData = runtimeSkill.CurrentSkillData;
            if (skillData == null || skillData.Effects == null || skillData.Effects.Count == 0)
                return "예상: -";

            int dice = currentDice.Value;
            var lines = new List<string>();

            foreach (var effect in skillData.Effects)
            {
                if (effect == null) continue;

                if (effect is DiceMultiplierDamageEffect diceEffect)
                {
                    int baseDamage = dice * diceEffect.multiplier;
                    lines.Add($"예상 피해: ({dice} x {diceEffect.multiplier}) = {baseDamage}");
                    lines.Add("실피해: max(1, 예상 피해 - 대상 DEF)");
                }
                else if (effect is MageStackDamageEffect mageEffect)
                {
                    int focusStacks = currentCharacter.StatusEffects != null
                        ? currentCharacter.StatusEffects.GetEffectValue(EffectType.Focus)
                        : 0;

                    int baseDamage = dice * mageEffect.baseMultiplier;
                    float bonusMultiplier = 1.0f + (focusStacks * mageEffect.bonusDamageRatioPerStack);
                    int finalDamage = Mathf.RoundToInt(baseDamage * bonusMultiplier);
                    float bonusPercent = focusStacks * mageEffect.bonusDamageRatioPerStack * 100f;

                    lines.Add($"예상 피해: ({dice} x {mageEffect.baseMultiplier}) x (1 + {focusStacks} x {mageEffect.bonusDamageRatioPerStack:0.##})");
                    lines.Add($"= {baseDamage} x {bonusMultiplier:0.##} = {finalDamage} (집중 +{bonusPercent:0.#}%)");
                    lines.Add("실피해: max(1, 예상 피해 - 대상 DEF)");
                }
            }

            return lines.Count > 0 ? string.Join("\n", lines) : "예상: -";
        }

        private void ExecuteSkill(int index)
        {
            if (currentDice == null || currentCharacter == null) return;

            var diceManager = DiceManager.Instance;
            if (diceManager != null)
            {
                bool success = diceManager.AssignDice(currentDice, currentCharacter, ActionType.Skill);
                if (success)
                {
                    currentCharacter.UseSkillByIndex(index, currentDice.Value);
                    MarkDiceUsed();
                    ReturnDiceElement();
                }
            }
            Hide();
        }

        private void MarkDiceUsed()
        {
            var diceUI = FindFirstObjectByType<DiceUI>();
            diceUI?.MarkDiceAsUsed(currentDice);
        }

        private void ReturnDiceElement()
        {
            if (currentDice == null) return;
            var elements = FindObjectsByType<DiceElement>(FindObjectsSortMode.None);
            foreach (var e in elements)
            {
                if (e.Data == currentDice) { e.ReturnToOriginalPosition(); break; }
            }
        }

        private void SetButtonsInteractable(bool value)
        {
            if (moveButton  != null) moveButton.interactable  = value;
            if (skillButton != null) skillButton.interactable = value;
        }

        // ─────────────────────────────────────────────
        // 애니메이션
        // ─────────────────────────────────────────────

        private void StopSlide()
        {
            if (slideCoroutine != null) { StopCoroutine(slideCoroutine); slideCoroutine = null; }
        }

        private IEnumerator SlideIn()
        {
            // 버튼 스케일 초기화
            foreach (var btn in actionButtons) btn.localScale = Vector3.zero;

            float elapsed = 0f;
            Vector2 start = panelRoot.anchoredPosition;

            while (elapsed < slideInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / slideInDuration);
                t = t * t * (3f - 2f * t); // SmoothStep
                panelRoot.anchoredPosition = Vector2.Lerp(start, shownPosition, t);
                yield return null;
            }
            panelRoot.anchoredPosition = shownPosition;

            // 버튼 스태거 팝업
            for (int i = 0; i < actionButtons.Count; i++)
            {
                StartCoroutine(PopButton(actionButtons[i], i * buttonStaggerDelay));
            }
            slideCoroutine = null;
        }

        private IEnumerator SlideOut()
        {
            float elapsed = 0f;
            Vector2 start = panelRoot.anchoredPosition;

            while (elapsed < slideInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / slideInDuration);
                t = t * t * (3f - 2f * t);
                panelRoot.anchoredPosition = Vector2.Lerp(start, hiddenPosition, t);
                yield return null;
            }
            panelRoot.anchoredPosition = hiddenPosition;
            slideCoroutine = null;
        }

        private IEnumerator PopButton(RectTransform btn, float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            float duration = 0.12f;
            float elapsed  = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Overshoot (elastic feel)
                float scale = Mathf.Sin(t * Mathf.PI * 0.5f) * 1.1f;
                if (t > 0.7f) scale = Mathf.Lerp(1.1f, 1f, (t - 0.7f) / 0.3f);
                btn.localScale = Vector3.one * scale;
                yield return null;
            }
            btn.localScale = Vector3.one;
        }
    }
}
