using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DiceOrbit.Data;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Skills;
using DiceOrbit.Data.Skills;
using DiceOrbit.Data.Passives;

namespace DiceOrbit.UI
{
    /// <summary>
    /// ?�르?�나 ?��???캐릭???�션 UI
    /// 기존 ActionPanel.cs�??�체합?�다.
    /// </summary>
    public class CharacterActionUI : MonoBehaviour
    {
        public static CharacterActionUI Instance { get; private set; }

        [Header("?�널 루트 (?�라?�드 ?�??")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Vector2 hiddenPosition = new Vector2(600f, -200f);  // ?�면 ?�른�?바깥
        [SerializeField] private Vector2 shownPosition  = new Vector2(-20f,  -20f);  // ?�른�??�단
        [SerializeField] private float slideInDuration  = 0.25f;

        [Header("초상화")]
        [SerializeField] private Image portraitImage;

        [Header("메인 버튼")]
        [SerializeField] private Button moveButton;
        [SerializeField] private Button skillButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private float buttonStaggerDelay = 0.07f;

        [Header("?�킬 ?�택 ?�널")]
        [SerializeField] private GameObject skillSelectPanel;
        [SerializeField] private Transform skillButtonContainer;
        [SerializeField] private GameObject skillSelectButtonPrefab;

        [Header("?�버?�이")]
        [SerializeField] private TargetSelectionOverlay overlay;

        // ?��????�태
        private Character    currentCharacter;
        private DiceData     currentDice;
        private bool         waitingForDice = false;
        private bool         isPanelVisible = false;
        private OrbitManager orbitManager;
        private SpriteRenderer currentPortraitSource;

        private List<RectTransform> actionButtons = new List<RectTransform>();
        private Coroutine slideCoroutine;

        // ?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�
        // 초기??
        // ?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // 버튼 ?�벤??
            moveButton?.onClick.AddListener(OnMoveClicked);
            skillButton?.onClick.AddListener(OnSkillClicked);
            cancelButton?.onClick.AddListener(OnCancelClicked);

            // ?�버?�이 취소 ?�벤??
            if (overlay != null)
                overlay.OnOverlayCancelled += OnCancelClicked;

            // 버튼 목록 (?�태거용)
            if (moveButton  != null) actionButtons.Add(moveButton.GetComponent<RectTransform>());
            if (skillButton != null) actionButtons.Add(skillButton.GetComponent<RectTransform>());

            // 초상???�롯??Rect 비율�??�프?�이??비율???�라???�려 보이지 ?�게 비율??고정?�니??
            if (portraitImage != null)
            {
                portraitImage.preserveAspect = true;
            }

            // 초기 ?�태: ?��?
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
            // ?�널???�려 ?�는 ?�안 ?�재 캐릭?�의 ?�니메이???�레?�을 초상?�에 미러링합?�다.
            if (!isPanelVisible || currentCharacter == null || portraitImage == null)
                return;

            RefreshPortraitImage();
        }

        // ?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�
        // 공개 API
        // ?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�

        /// <summary>캐릭???�택 ???�널 ?�시</summary>
        public void Show(Character character)
        {
            currentCharacter = character;
            currentDice      = null;
            waitingForDice   = true;
            isPanelVisible   = true;

            if (character?.Passives != null)
            {
                foreach (var passive in character.Passives.ActivePassives)
                    passive?.OnOwnerSelected(character);
            }

            // Animator 기반 캐릭?�는 ?�시�?SpriteRenderer ?�레?�을 ?�선 ?�용?�니??
            currentPortraitSource = character != null ? character.GetComponentInChildren<SpriteRenderer>() : null;
            RefreshPortraitImage();

            RefreshActionButtonsState();

            // 주사?��? 먼�? ?�택????캐릭?��? ?�택??경우�?지?�합?�다.
            TryApplyPreselectedDice();
            if (skillSelectPanel != null) skillSelectPanel.SetActive(false);
            RefreshSkillButtonPreview();

            // ?�라?�드 ??
            StopSlide();
            slideCoroutine = StartCoroutine(SlideIn());
        }

        /// <summary>?�널 ?�기�?/summary>
        public void Hide()
        {
            if (currentCharacter?.Passives != null)
            {
                foreach (var passive in currentCharacter.Passives.ActivePassives)
                    passive?.OnOwnerDeselected();
            }

            overlay?.Hide();
            HoverTooltipUI.Instance?.HidePinned();
            if (skillSelectPanel != null) skillSelectPanel.SetActive(false);

            StopSlide();
            slideCoroutine = StartCoroutine(SlideOut());

            // ?�겟팅 중이 ?�닐 ?�만 초기??
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

        /// <summary>주사???�롭 처리 (DiceElement?�서 ?�출)</summary>
        public void OnDiceDropped(DiceData dice)
        {
            // ?�널???�린 ?�태?�서??주사?��? ?�시 ?�택?�도 즉시 교체 반영?�어???�니??
            if (currentCharacter == null || dice == null || dice.State == DiceState.Used) return;

            currentDice    = dice;
            waitingForDice = false;

            // 버튼 ?�성?�는 ???�산/?�태�??�께 고려??갱신?�니??
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

        // ?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�
        // 버튼 ?�들??
        // ?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�

        private void OnMoveClicked()
        {
            if (currentDice == null || currentCharacter == null) return;

            var combatManager = CombatManager.Instance;
            if (combatManager == null || !combatManager.PlayerTurnActive || !combatManager.CanSpendMove(currentCharacter))
            {
                Debug.LogWarning("[CharacterActionUI] ?�동 가???�수가 ?�거???�레?�어 ?�이 ?�닙?�다.");
                ReturnDiceElement();
                return;
            }

            if (!currentCharacter.Stats.canMove())
            {
                Debug.LogWarning("[CharacterActionUI] ?�동 불�? ?�태(?�박 ???�니??");
                ReturnDiceElement();
                return;
            }

            var diceManager = DiceManager.Instance;
            if (diceManager != null)
            {
                bool success = diceManager.AssignDice(currentDice, currentCharacter, ActionType.Move);
                if (success)
                {
                    // ?�제 ?�동 ?�행 직전???�동 ?�산 1?��? ?�정 ?�비?�니??
                    if (!combatManager.TrySpendMove(currentCharacter))
                    {
                        diceManager.UnassignDice(currentDice);
                        ReturnDiceElement();
                        return;
                    }

                    // ?�동 코루?�을 ?�션 ?�에 ?�록?�니??
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

            // ?�재 ?�티�??�킬??1개인 구조?��?�?버튼 ?�릭 ??즉시 ?�용
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
                hoverPreview.SetPreview("?�티�??�킬 ?�보 ?�음");

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
        /// (SkillTargetSelector?�서 ?�출) ?��??�택???�정?�었????최종 ?�행
        /// </summary>
        public void ConfirmSkillTarget(Unit target, Character character, RuntimeAbility ability, DiceData dice)
        {

            var combatManager = CombatManager.Instance;
            var diceManager = DiceManager.Instance;
            var runtimeAbility = ability; // ?�겟팅???�작?�던 ?�킬

            if (dice == null || character == null || runtimeAbility == null || combatManager == null || diceManager == null)
            {
                ReturnDiceElement();
                return;
            }

            // 최종?�으�??�동 ?�산???�모?�고 주사?��? 배정
            if (!combatManager.TrySpendAction(character))
            {
                diceManager.UnassignDice(dice);
                ReturnDiceElement();
                return;
            }

            // ?�제 ?�킬 ?�행 로직???��? 코루?�을 ?�에 ?�록
            ActionQueueManager.Instance.EnqueueAction(
                FinalSkillExecutionRoutine(character, runtimeAbility, target, dice)
            );

            MarkDiceUsed(dice);
            ReturnDiceElement();
            // Hide()???�겟팅 ?�작 ???��? ?�출?�었?��?�??�기?�는 ?�출?��? ?�음
        }

        /// <summary>
        /// (SkillTargetSelector?�서 ?�출) ?�???��??�택???�정?�었????최종 ?�행
        /// </summary>
        public void ConfirmTileSkillTarget(List<TileData> tiles, Character character, RuntimeAbility ability, DiceData dice)
        {
            var combatManager = CombatManager.Instance;
            var diceManager   = DiceManager.Instance;

            if (dice == null || character == null || ability == null || combatManager == null || diceManager == null)
            {
                ReturnDiceElement();
                return;
            }

            if (!combatManager.TrySpendAction(character))
            {
                diceManager.UnassignDice(dice);
                ReturnDiceElement();
                return;
            }

            ActionQueueManager.Instance.EnqueueAction(
                FinalTileSkillExecutionRoutine(character, ability, tiles, dice)
            );

            MarkDiceUsed(dice);
            ReturnDiceElement();
        }

        // ?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�
        // ?��? 로직
        // ?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�

        private void PopulateSkillList(Character character)
        {
            if (skillButtonContainer == null || skillSelectButtonPrefab == null) return;

            foreach (Transform child in skillButtonContainer) Destroy(child.gameObject);

            var skills = new List<RuntimeAbility>(character.Stats.ActiveAbilities);
            for (int i = 0; i < skills.Count; i++)
            {
                int index = i;
                // UI?�는 ?�티�??�력�??�시?�고, ?�시브는 리액??체인?�서 ?�동 처리?�니??
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
                return "����: -";

            var activeTemplate = runtimeAbility.BaseSkill.ActiveTemplate;
            if (activeTemplate != null)
            {
                string coupledPreview = activeTemplate.BuildPreview(currentCharacter, runtimeAbility, currentDice.Value);
                if (!string.IsNullOrWhiteSpace(coupledPreview))
                {
                    return coupledPreview;
                }
            }

            return "����: -";
        }

                private string BuildSkillHoverText(RuntimeAbility runtimeAbility)
        {
            if (runtimeAbility == null || runtimeAbility.BaseSkill == null)
                return "스킬 정보: -";

            var baseSkill = runtimeAbility.BaseSkill;
            var lines = new System.Collections.Generic.List<string>
            {
                $"{baseSkill.SkillName} (Lv.{runtimeAbility.CurrentLevel})"
            };

            string description = baseSkill.GetDescription(runtimeAbility.CurrentLevel);
            if (!string.IsNullOrWhiteSpace(description))
            {
                lines.Add(description.Trim());
            }

            lines.Add($"대상: {GetTargetTypeLabel(baseSkill.TargetType)}");
            var reqText = BuildRequirementText(runtimeAbility.GetRequirement());
            lines.Add($"조건: {reqText}");

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
                case CharacterSkillTargetType.OneEnemy: return "단일 적";
                case CharacterSkillTargetType.None:     return "타겟 없음";
                case CharacterSkillTargetType.OneTile:  return "타일 하나";
                case CharacterSkillTargetType.AllTiles: return "모든 타일";
                default: return targetType.ToString();
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
                    parts.Add($"{requirement.MinDiceValue} ?�상");
                }

                if (requirement.MaxDiceValue.HasValue)
                {
                    parts.Add($"{requirement.MaxDiceValue.Value} ?�하");
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
                return "?�한 ?�음";
            }

            return string.Join(", ", parts);
        }

        private void ExecuteSkill(int index)
        {
            if (currentDice == null || currentCharacter == null) return;

            var combatManager = CombatManager.Instance;
            if (combatManager == null || !combatManager.PlayerTurnActive || !combatManager.CanSpendAction(currentCharacter))
            {
                Debug.LogWarning("[CharacterActionUI] ?�동 가???�수가 ?�거???�레?�어 ?�이 ?�닙?�다.");
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

            // `RuntimeAbility.TargetType`??기�??�로 분기?�니??
            if (runtimeAbility.TargetType == CharacterSkillTargetType.OneEnemy
             || runtimeAbility.TargetType == CharacterSkillTargetType.OneTile
             || runtimeAbility.TargetType == CharacterSkillTargetType.AllTiles)
            {
                // ?��??�택???�요??경우: ?��??�택 ?�스?�을 ?�작?�니??
                currentDice.State = DiceState.Reserved;
                DiceUI.Instance?.RefreshDiceVisual(currentDice);
                SkillTargetSelector.Instance.StartTargetSelection(currentCharacter, runtimeAbility, currentDice);
                Hide(); // CharacterActionUI???�깁?�다.
            }
            else if (runtimeAbility.TargetType == CharacterSkillTargetType.None)
            {
                // ?��??�택???�요 ?�는 경우 (None, AllEnemies, Self ??: 즉시 ?�에 ?�록?�니??
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
                Debug.LogError("?�규 ?�겟팅 방법???�른 ?�정 ?�요");
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

            // ?�제 ?�킬 로직 ?�행
            currentCharacter.UseSkillByIndex(skillIndex, dice);

            // ?�출???�한 ?�시 ?�레??
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>
        /// (ActionQueue?�서 ?�행?? 최종 ?�킬 ?�행 코루??
        /// </summary>
        private System.Collections.IEnumerator FinalSkillExecutionRoutine(Character character, RuntimeAbility ability, Unit target, DiceData dice)
        {
            // ??코루?�이 ?�행???? CharacterActionUI??currentCharacter???�른 값일 ???�으므�?
            // ?�자�?받�? character�??�용?�야 ?�니??
            if (character != null && character.IsAlive)
            {
                SkillManager.Instance.OnTargetSelected(character, target, ability, dice.Value);
            }

            // ?�출 ?��?(?�시)
            yield return new WaitForSeconds(0.5f);
        }

        private System.Collections.IEnumerator FinalTileSkillExecutionRoutine(
            Character character, RuntimeAbility ability, List<TileData> tiles, DiceData dice)
        {
            if (character != null && character.IsAlive)
            {
                var activeTemplate = ability.BaseSkill?.ActiveTemplate;
                if (activeTemplate != null)
                    activeTemplate.Execute(character, ability, new List<Unit>(), tiles, dice.Value);
            }

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
            // 버튼 ?�태??"주사???�택 + ?�레?�어 ??+ 캐릭?�별 ?�여 ?�산"???�시??만족?�야 ?�성?�됩?�다.
            bool hasDice = !waitingForDice && currentDice != null && currentCharacter != null;
            var combatManager = CombatManager.Instance;
            bool playerTurn = combatManager != null && combatManager.PlayerTurnActive;
            bool canUseSelectedDiceForSkill = false;

            var primaryAbility = GetPrimaryActiveAbility();
            if (hasDice && primaryAbility?.BaseSkill != null)
            {
                // ?�재 ?�택??주사???�금???�킬 조건??만족???�만 ?�킬 버튼???�성?�합?�다.
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

        // ?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�
        // ?�니메이??
        // ?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�

        private void StopSlide()
        {
            if (slideCoroutine != null) { StopCoroutine(slideCoroutine); slideCoroutine = null; }
        }

        private IEnumerator SlideIn()
        {
            // 버튼 ?��???초기??
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

            // 버튼 ?�태�??�업
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



