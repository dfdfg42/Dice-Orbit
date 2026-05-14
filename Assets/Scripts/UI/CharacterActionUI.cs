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
        private bool         isPanelVisible = false;
        private OrbitManager orbitManager;
        private SpriteRenderer currentPortraitSource;

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

            // 초상화 슬롯의 Rect 비율과 스프라이트 비율이 달라도 눌려 보이지 않게 비율을 고정합니다.
            if (portraitImage != null)
            {
                portraitImage.preserveAspect = true;
            }

            // 초기 상태: 숨김
            if (panelRoot != null) panelRoot.anchoredPosition = hiddenPosition;
            if (skillSelectPanel != null) skillSelectPanel.SetActive(false);
            SetButtonsInteractable(false);
            RefreshSkillButtonPreview();
        }

        private void Start()
        {
            orbitManager = FindAnyObjectByType<OrbitManager>();
        }

        private void LateUpdate()
        {
            // 패널이 열려 있는 동안 현재 캐릭터의 애니메이션 프레임을 초상화에 미러링합니다.
            if (!isPanelVisible || currentCharacter == null || portraitImage == null)
                return;

            RefreshPortraitImage();
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
            isPanelVisible   = true;

            // Animator 기반 캐릭터는 실시간 SpriteRenderer 프레임을 우선 사용합니다.
            currentPortraitSource = character != null ? character.GetComponentInChildren<SpriteRenderer>() : null;
            RefreshPortraitImage();

            RefreshActionButtonsState();

            // 주사위를 먼저 선택한 뒤 캐릭터를 선택한 경우를 지원합니다.
            TryApplyPreselectedDice();
            if (skillSelectPanel != null) skillSelectPanel.SetActive(false);
            RefreshSkillButtonPreview();

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

            // 타겟팅 중이 아닐 때만 초기화
            if (SkillTargetSelector.Instance != null && !SkillTargetSelector.Instance.IsSelectingTarget)
            {
                currentCharacter = null;
                currentDice      = null;
            }
            currentPortraitSource = null;
            waitingForDice   = false;
            isPanelVisible   = false;
            RefreshSkillButtonPreview();
        }

        public void CancelSelection()
        {
            OnCancelClicked();
        }

        public bool IsShowingCharacter(Character character)
        {
            return isPanelVisible && currentCharacter == character;
        }

        /// <summary>주사위 드롭 처리 (DiceElement에서 호출)</summary>
        public void OnDiceDropped(DiceData dice)
        {
            // 패널이 열린 상태에서는 주사위를 다시 선택해도 즉시 교체 반영되어야 합니다.
            if (currentCharacter == null || dice == null || dice.State == DiceState.Used) return;

            currentDice    = dice;
            waitingForDice = false;

            // 버튼 활성화는 턴 예산/상태를 함께 고려해 갱신합니다.
            RefreshActionButtonsState();
            RefreshSkillButtonPreview();
        }

        public void OnDiceDeselected(DiceData dice)
        {
            if (dice == null) return;
            if (currentDice != dice) return;

            currentDice = null;
            waitingForDice = true;
            RefreshActionButtonsState();
            if (skillSelectPanel != null) skillSelectPanel.SetActive(false);
            RefreshSkillButtonPreview();
        }

        // ─────────────────────────────────────────────
        // 버튼 핸들러
        // ─────────────────────────────────────────────

        private void OnMoveClicked()
        {
            if (currentDice == null || currentCharacter == null) return;

            var combatManager = CombatManager.Instance;
            if (combatManager == null || !combatManager.PlayerTurnActive || !combatManager.CanSpendMove(currentCharacter))
            {
                Debug.LogWarning("[CharacterActionUI] 이동 가능 횟수가 없거나 플레이어 턴이 아닙니다.");
                ReturnDiceElement();
                return;
            }

            if (!currentCharacter.Stats.canMove())
            {
                Debug.LogWarning("[CharacterActionUI] 이동 불가 상태(속박 등)입니다.");
                ReturnDiceElement();
                return;
            }

            var diceManager = DiceManager.Instance;
            if (diceManager != null)
            {
                bool success = diceManager.AssignDice(currentDice, currentCharacter, ActionType.Move);
                if (success)
                {
                    // 실제 이동 실행 직전에 이동 예산 1회를 확정 소비합니다.
                    if (!combatManager.TrySpendMove(currentCharacter))
                    {
                        diceManager.UnassignDice(currentDice);
                        ReturnDiceElement();
                        return;
                    }

                    // 이동 코루틴을 액션 큐에 등록합니다.
                    ActionQueueManager.Instance.EnqueueAction(orbitManager.MoveRoutine(currentCharacter, currentDice.Value));

                    MarkDiceUsed(currentDice);
                    ReturnDiceElement();
                    Hide();
                    return;
                }
            }

            ReturnDiceElement();
        }

        private void OnSkillClicked()
        {
            if (currentDice == null || currentCharacter == null) return;
            if (GetPrimaryActiveAbility() == null) return;

            // 현재 액티브 스킬이 1개인 구조이므로 버튼 클릭 시 즉시 사용
            ExecuteSkill(0);
            if (skillSelectPanel != null) skillSelectPanel.SetActive(false);
            overlay?.Hide();
        }

        private RuntimeAbility GetPrimaryActiveAbility()
        {
            if (currentCharacter?.Stats?.ActiveAbilities == null)
                return null;

            foreach (var ability in currentCharacter.Stats.ActiveAbilities)
            {
                if (ability != null)
                {
                    return ability;
                }
            }

            return null;
        }

        private void RefreshSkillButtonPreview()
        {
            if (skillButton == null) return;

            var runtimeAbility = GetPrimaryActiveAbility();
            var hoverPreview = skillButton.GetComponent<SkillPreviewHoverUI>();
            if (hoverPreview == null) hoverPreview = skillButton.gameObject.AddComponent<SkillPreviewHoverUI>();

            if (runtimeAbility?.BaseSkill != null)
            {
                hoverPreview.SetPreview(BuildSkillHoverText(runtimeAbility));

                var text = skillButton.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                {
                    text.text = $"{runtimeAbility.BaseSkill.SkillName} (Lv.{runtimeAbility.CurrentLevel})";
                }
            }
            else
            {
                hoverPreview.SetPreview("액티브 스킬 정보 없음");

                var text = skillButton.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                {
                    text.text = "SKILL";
                }
            }
        }

        private void OnCancelClicked()
        {
            if (SkillTargetSelector.Instance != null && SkillTargetSelector.Instance.IsSelectingTarget)
            {
                SkillTargetSelector.Instance.CancelTargetSelection();
            }
            ReturnDiceElement();
            Hide();
        }

        private void OnSpecificSkillClicked(int index)
        {
            ExecuteSkill(index);
        }

        /// <summary>
        /// (SkillTargetSelector에서 호출) 타겟 선택이 확정되었을 때 최종 실행
        /// </summary>
        public void ConfirmSkillTarget(Unit target, Character character, RuntimeAbility ability, DiceData dice)
        {

            var combatManager = CombatManager.Instance;
            var diceManager = DiceManager.Instance;
            var runtimeAbility = ability; // 타겟팅을 시작했던 스킬

            if (dice == null || character == null || runtimeAbility == null || combatManager == null || diceManager == null)
            {
                ReturnDiceElement();
                return;
            }

            // 최종적으로 행동 예산을 소모하고 주사위를 배정
            if (!combatManager.TrySpendAction(character))
            {
                diceManager.UnassignDice(dice);
                ReturnDiceElement();
                return;
            }

            // 실제 스킬 실행 로직을 담은 코루틴을 큐에 등록
            ActionQueueManager.Instance.EnqueueAction(
                FinalSkillExecutionRoutine(character, runtimeAbility, target, dice)
            );

            MarkDiceUsed(dice);
            ReturnDiceElement();
            // Hide()는 타겟팅 시작 시 이미 호출되었으므로 여기서는 호출하지 않음
        }

        // ─────────────────────────────────────────────
        // 내부 로직
        // ─────────────────────────────────────────────

        private void PopulateSkillList(Character character)
        {
            if (skillButtonContainer == null || skillSelectButtonPrefab == null) return;

            foreach (Transform child in skillButtonContainer) Destroy(child.gameObject);

            var skills = new List<RuntimeAbility>(character.Stats.ActiveAbilities);
            for (int i = 0; i < skills.Count; i++)
            {
                int index = i;
                // UI에는 액티브 능력만 표시하고, 패시브는 리액터 체인에서 자동 처리합니다.
                var runtimeAbility = skills[i];
                var go    = Instantiate(skillSelectButtonPrefab, skillButtonContainer);

                var txt = go.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null)
                {
                    var skillName = runtimeAbility.BaseSkill != null ? runtimeAbility.BaseSkill.SkillName : "Unknown Skill";
                    txt.text = $"{skillName} (Lv.{runtimeAbility.CurrentLevel})";
                }

                var imgs = go.GetComponentsInChildren<Image>();
                if (imgs.Length > 1 && runtimeAbility.BaseSkill != null && runtimeAbility.BaseSkill.Icon != null) imgs[1].sprite = runtimeAbility.BaseSkill.Icon;

                var hoverPreview = go.GetComponent<SkillPreviewHoverUI>();
                if (hoverPreview == null) hoverPreview = go.AddComponent<SkillPreviewHoverUI>();
                hoverPreview.SetPreview(BuildSkillHoverText(runtimeAbility));

                var btn = go.GetComponent<Button>();
                btn?.onClick.AddListener(() => OnSpecificSkillClicked(index));
            }
        }

        private string BuildDamagePreview(RuntimeAbility runtimeAbility)
        {
            if (runtimeAbility == null || runtimeAbility.BaseSkill == null || currentCharacter == null || currentDice == null)
                return "예상: -";

            var activeTemplate = runtimeAbility.BaseSkill.ActiveTemplate;
            if (activeTemplate != null)
            {
                string coupledPreview = activeTemplate.BuildPreview(currentCharacter, runtimeAbility, currentDice.Value);
                if (!string.IsNullOrWhiteSpace(coupledPreview))
                {
                    return coupledPreview;
                }
            }

            var skillData = runtimeAbility.CurrentSkillData;
            if (skillData == null || skillData.Effects == null || skillData.Effects.Count == 0)
                return "예상: -";

            int dice = currentDice.Value;
            var lines = new List<string>();

            foreach (var effect in skillData.Effects)
            {
                if (effect == null) continue;

                if (effect is DiceMultiplierDamageEffect diceEffect)
                {
                    int resolvedMultiplier = diceEffect.GetMultiplierForSource(currentCharacter);
                    int baseDamage = dice * resolvedMultiplier;
                    lines.Add($"예상 피해: ({dice} x {resolvedMultiplier}) = {baseDamage}");
                }
                else if (effect is MageStackDamageEffect mageEffect)
                {
                    int focusStacks = currentCharacter.StatusEffects != null
                        ? currentCharacter.StatusEffects.GetEffectValue(EffectType.Focus)
                        : 0;
                    int resolvedBaseMultiplier = mageEffect.GetBaseMultiplierForSource(currentCharacter);
                    float bonusRatio = mageEffect.GetBonusRatioForSource(currentCharacter);

                    int baseDamage = dice * resolvedBaseMultiplier;
                    float bonusMultiplier = 1.0f + (focusStacks * bonusRatio);
                    int finalDamage = Mathf.RoundToInt(baseDamage * bonusMultiplier);
                    float bonusPercent = focusStacks * bonusRatio * 100f;

                    lines.Add($"예상 피해: ({dice} x {resolvedBaseMultiplier}) x (1 + {focusStacks} x {bonusRatio:0.##})");
                    lines.Add($"= {baseDamage} x {bonusMultiplier:0.##} = {finalDamage} (집중 +{bonusPercent:0.#}%)");
                }
            }

            return lines.Count > 0 ? string.Join("\n", lines) : "예상: -";
        }

        private string BuildSkillHoverText(RuntimeAbility runtimeAbility)
        {
            if (runtimeAbility == null || runtimeAbility.BaseSkill == null)
                return "스킬 정보: -";

            var baseSkill = runtimeAbility.BaseSkill;
            var currentData = runtimeAbility.CurrentSkillData;
            var lines = new List<string>
            {
                $"{baseSkill.SkillName} (Lv.{runtimeAbility.CurrentLevel})"
            };

            string description = currentData != null && !string.IsNullOrWhiteSpace(currentData.Description)
                ? currentData.Description
                : baseSkill.Description;
            if (!string.IsNullOrWhiteSpace(description))
            {
                lines.Add(description.Trim());
            }

            lines.Add($"대상: {GetTargetTypeLabel(baseSkill.TargetType)}");

            int diceValue = currentDice != null ? currentDice.Value : -1;
            bool canUse = currentDice != null && baseSkill.CanUse(diceValue);
            string condition = BuildRequirementText(baseSkill.Requirement);
            if (diceValue > 0)
            {
                lines.Add($"조건: {condition} (현재 주사위 {diceValue}: {(canUse ? "사용 가능" : "사용 불가")})");
            }
            else
            {
                lines.Add($"조건: {condition}");
            }

            string damagePreview = BuildDamagePreview(runtimeAbility);
            if (!string.IsNullOrWhiteSpace(damagePreview) && damagePreview != "예상: -")
            {
                lines.Add("");
                lines.Add(damagePreview);
            }

            return string.Join("\n", lines);
        }

        private static string GetTargetTypeLabel(CharacterSkillTargetType targetType)
        {
            switch (targetType)
            {
                case CharacterSkillTargetType.OneEnemy:
                    return "단일 적";
                case CharacterSkillTargetType.None:
                    return "대상 없음";
                default:
                    return "CharacterActionUI.cs GetTargetTypeLabel에서 수정 필요";
            }
        }


        private static string BuildRequirementText(DiceRequirement requirement)
        {
            if (requirement == null) return "제한 없음";

            var parts = new List<string>();

            if (requirement.ExactDiceValue.HasValue)
            {
                parts.Add($"눈금 {requirement.ExactDiceValue.Value}");
            }
            else
            {
                if (requirement.MinDiceValue > 1)
                {
                    parts.Add($"{requirement.MinDiceValue} 이상");
                }

                if (requirement.MaxDiceValue.HasValue)
                {
                    parts.Add($"{requirement.MaxDiceValue.Value} 이하");
                }
            }

            switch (requirement.Pattern)
            {
                case DicePattern.Even:
                    parts.Add("짝수");
                    break;
                case DicePattern.Odd:
                    parts.Add("홀수");
                    break;
                case DicePattern.High:
                    parts.Add("고눈금(4 이상)");
                    break;
                case DicePattern.Low:
                    parts.Add("저눈금(3 이하)");
                    break;
            }

            if (parts.Count == 0)
            {
                return "제한 없음";
            }

            return string.Join(", ", parts);
        }

        private void ExecuteSkill(int index)
        {
            if (currentDice == null || currentCharacter == null) return;

            var combatManager = CombatManager.Instance;
            if (combatManager == null || !combatManager.PlayerTurnActive || !combatManager.CanSpendAction(currentCharacter))
            {
                Debug.LogWarning("[CharacterActionUI] 행동 가능 횟수가 없거나 플레이어 턴이 아닙니다.");
                ReturnDiceElement();
                return;
            }

            var selectedAbilities = new List<RuntimeAbility>(currentCharacter.Stats.ActiveAbilities);
            if (index < 0 || index >= selectedAbilities.Count)
            {
                ReturnDiceElement();
                return;
            }

            RuntimeAbility runtimeAbility = selectedAbilities[index];
            if (runtimeAbility?.BaseSkill == null || !runtimeAbility.BaseSkill.CanUse(currentDice.Value))
            {
                Debug.LogWarning("[CharacterActionUI] Selected dice does not satisfy skill requirement. Returning dice.");
                ReturnDiceElement();
                return;
            }

            // `RuntimeAbility.TargetType`을 기준으로 분기합니다.
            if (runtimeAbility.TargetType == CharacterSkillTargetType.OneEnemy)
            {
                // 타겟 선택이 필요한 경우: 타겟 선택 시스템을 시작합니다.
                currentDice.State = DiceState.Reserved;
                DiceUI.Instance?.RefreshDiceVisual(currentDice);
                SkillTargetSelector.Instance.StartTargetSelection(currentCharacter, runtimeAbility, currentDice);
                Hide(); // CharacterActionUI는 숨깁니다.
            }
            else if (runtimeAbility.TargetType == CharacterSkillTargetType.None)
            {
                // 타겟 선택이 필요 없는 경우 (None, AllEnemies, Self 등): 즉시 큐에 등록합니다.
                var diceManager = DiceManager.Instance;
                if (diceManager != null)
                {
                    bool success = diceManager.AssignDice(currentDice, currentCharacter, ActionType.Skill);
                    if (success)
                    {
                        if (!combatManager.TrySpendAction(currentCharacter))
                        {
                            diceManager.UnassignDice(currentDice);
                            ReturnDiceElement();
                            return;
                        }

                        ActionQueueManager.Instance.EnqueueAction(
                            ExecuteSkillRoutine(index, currentDice)
                        );

                        MarkDiceUsed(currentDice);
                        ReturnDiceElement();
                        Hide();
                    }
                    else
                    {
                        ReturnDiceElement();
                    }
                }
            }
            else
            {
                Debug.LogError("신규 타겟팅 방법에 따른 수정 필요");
            }
        }

        private System.Collections.IEnumerator ExecuteSkillRoutine(int skillIndex, DiceData dice)
        {
            // Pre-execution check (optional but good practice)
            if (currentCharacter == null || !currentCharacter.IsAlive)
            {
                Debug.LogWarning($"[ActionQueue] Character {currentCharacter?.name} is no longer valid. Skipping skill action.");
                yield break;
            }

            // 실제 스킬 로직 실행
            currentCharacter.UseSkillByIndex(skillIndex, dice);

            // 연출을 위한 임시 딜레이
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>
        /// (ActionQueue에서 실행될) 최종 스킬 실행 코루틴
        /// </summary>
        private System.Collections.IEnumerator FinalSkillExecutionRoutine(Character character, RuntimeAbility ability, Unit target, DiceData dice)
        {
            // 이 코루틴이 실행될 때, CharacterActionUI의 currentCharacter는 다른 값일 수 있으므로
            // 인자로 받은 character를 사용해야 합니다.
            if (character != null && character.IsAlive)
            {
                SkillManager.Instance.OnTargetSelected(character, target, ability, dice.Value);
            }

            // 연출 대기 (임시)
            yield return new WaitForSeconds(0.5f);
        }

        private void MarkDiceUsed(DiceData dice)
        {
            var diceUI = FindFirstObjectByType<DiceUI>();
            diceUI?.MarkDiceAsUsed(dice);
        }

        private void ReturnDiceElement()
        {
            DiceUI.Instance?.ClearSelectedDice();
            currentDice = null;
            waitingForDice = true;
            RefreshActionButtonsState();
        }

        private void SetButtonsInteractable(bool value)
        {
            if (moveButton  != null) moveButton.interactable  = value;
            if (skillButton != null) skillButton.interactable = value;
        }

        private void RefreshActionButtonsState()
        {
            // 버튼 상태는 "주사위 선택 + 플레이어 턴 + 캐릭터별 잔여 예산"을 동시에 만족해야 활성화됩니다.
            bool hasDice = !waitingForDice && currentDice != null && currentCharacter != null;
            var combatManager = CombatManager.Instance;
            bool playerTurn = combatManager != null && combatManager.PlayerTurnActive;
            bool canUseSelectedDiceForSkill = false;

            var primaryAbility = GetPrimaryActiveAbility();
            if (hasDice && primaryAbility?.BaseSkill != null)
            {
                // 현재 선택한 주사위 눈금이 스킬 조건을 만족할 때만 스킬 버튼을 활성화합니다.
                canUseSelectedDiceForSkill = primaryAbility.BaseSkill.CanUse(currentDice.Value);
            }

            bool canMove = hasDice && playerTurn && currentCharacter.Stats.canMove() && combatManager.CanSpendMove(currentCharacter);
            bool canSkill = hasDice && playerTurn && canUseSelectedDiceForSkill && combatManager.CanSpendAction(currentCharacter);

            if (moveButton != null) moveButton.interactable = canMove;
            if (skillButton != null) skillButton.interactable = canSkill;
        }

        private void TryApplyPreselectedDice()
        {
            var diceUI = DiceUI.Instance;
            if (diceUI == null || currentCharacter == null) return;

            var selectedDice = diceUI.GetSelectedDiceData();
            if (selectedDice == null) return;

            currentDice = selectedDice;
            waitingForDice = false;
            RefreshActionButtonsState();
        }

        private void RefreshPortraitImage()
        {
            if (portraitImage == null || currentCharacter == null)
                return;

            Sprite portrait = null;

            if (currentPortraitSource != null)
            {
                portrait = currentPortraitSource.sprite;
            }

            if (portrait == null)
            {
                var stats = currentCharacter.Stats;
                if (stats != null)
                {
                    portrait = stats.SourcePreset != null && stats.SourcePreset.Portrait != null
                        ? stats.SourcePreset.Portrait
                        : stats.CharacterSprite;
                }
            }

            if (portrait != null)
            {
                portraitImage.sprite = portrait;
            }
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
