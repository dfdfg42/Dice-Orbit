using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DiceOrbit.Data;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Skills;
using DiceOrbit.Visuals;

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

            // 이동 버튼 hover 프리뷰
            AddPointerEvents(moveButton,
                () => ShowMovePreview(),
                () =>
                {
                    TileSkillPreviewManager.Instance?.HidePreview();
                    MovePathPreview.Instance?.Hide();
                });

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
            TileSkillPreviewManager.Instance?.HidePreview();
            MovePathPreview.Instance?.Hide();
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

        private ActiveSkillSlot GetPrimaryActiveAbility()
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

        // ─────────────────────────────────────────────
        // 내부 로직
        // ─────────────────────────────────────────────

        private void AddPointerEvents(Button btn, System.Action onEnter, System.Action onExit)
        {
            if (btn == null) return;
            var trigger = btn.GetComponent<EventTrigger>() ?? btn.gameObject.AddComponent<EventTrigger>();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => onEnter());
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => onExit());
            trigger.triggers.Add(exit);
        }

        private void ShowMovePreview()
        {
            var path = GetMovePath();
            if (path.Count == 0) return;

            // 목적지: 회전 트레일 (액션 프리뷰 언어) / 경유: 방향 체브론 (밟는 타일 + 방향 표시)
            TileSkillPreviewManager.EnsureInstance();
            TileSkillPreviewManager.Instance?.ShowPreview(new[] { path[path.Count - 1] }, TilePreviewStyle.Neutral);
            MovePathPreview.EnsureInstance();
            MovePathPreview.Instance?.Show(path);
        }

        private TileData GetMoveDestination()
        {
            var path = GetMovePath();
            return path.Count > 0 ? path[path.Count - 1] : null;
        }

        /// <summary>이동 시 통과할 타일 순서(목적지 포함). 이동 불가면 빈 리스트.</summary>
        private List<TileData> GetMovePath()
        {
            var path = new List<TileData>();
            if (currentCharacter?.CurrentTile == null || currentDice == null) return path;

            int netModifier = currentCharacter.Stats.MoveBuff - currentCharacter.Stats.MoveDebuff;
            int steps = Mathf.Max(currentDice.Value + netModifier, 0);
            var tile = currentCharacter.CurrentTile;
            for (int i = 0; i < steps; i++)
            {
                if (tile.NextTile == null) break;
                tile = tile.NextTile;
                path.Add(tile);
            }
            return path;
        }

        private void PopulateSkillList(Character character)
        {
            if (skillButtonContainer == null || skillSelectButtonPrefab == null) return;

            foreach (Transform child in skillButtonContainer) Destroy(child.gameObject);

            var skills = new List<ActiveSkillSlot>(character.Stats.ActiveAbilities);
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
                if (imgs.Length > 1 && runtimeAbility.BaseSkill != null && runtimeAbility.BaseSkill.icon != null) imgs[1].sprite = runtimeAbility.BaseSkill.icon;

                var hoverPreview = go.GetComponent<SkillPreviewHoverUI>();
                if (hoverPreview == null) hoverPreview = go.AddComponent<SkillPreviewHoverUI>();
                hoverPreview.SetPreview(BuildSkillHoverText(runtimeAbility));

                var btn = go.GetComponent<Button>();
                btn?.onClick.AddListener(() => OnSpecificSkillClicked(index));
            }
        }

        private string BuildDamagePreview(ActiveSkillSlot runtimeAbility)
        {
            if (runtimeAbility == null || runtimeAbility.BaseSkill == null || currentCharacter == null || currentDice == null)
                return "예상: -";

            string coupledPreview = runtimeAbility.BuildPreview(currentCharacter, currentDice.Value);
            if (!string.IsNullOrWhiteSpace(coupledPreview))
            {
                return coupledPreview;
            }

            return "예상: -";
        }

        private string BuildSkillHoverText(ActiveSkillSlot runtimeAbility)
        {
            if (runtimeAbility == null || runtimeAbility.BaseSkill == null)
                return "스킬 정보: -";

            var baseSkill = runtimeAbility.BaseSkill;
            var lines = new List<string>
            {
                $"{baseSkill.SkillName} (Lv.{runtimeAbility.CurrentLevel})"
            };

            string description = baseSkill.Description;
            if (!string.IsNullOrWhiteSpace(description))
            {
                lines.Add(description.Trim());
            }

            lines.Add($"대상: {GetTargetTypeLabel(runtimeAbility.TargetType)}");

            int diceValue = currentDice != null ? currentDice.Value : -1;
            var activeSkill = baseSkill as CharacterActiveSkill;
            bool canUse = currentDice != null && (activeSkill?.CanUse(diceValue) ?? false);
            string condition = BuildRequirementText(activeSkill?.requirement);
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

        private static string GetTargetTypeLabel(CharacterSkillTargetType targetType) => targetType switch
        {
            CharacterSkillTargetType.None        => "대상 없음",
            CharacterSkillTargetType.OneEnemy    => "단일 적",
            CharacterSkillTargetType.AllEnemies  => "전체 적",
            CharacterSkillTargetType.OneAlly     => "단일 아군",
            CharacterSkillTargetType.AllAllies   => "전체 아군",
            CharacterSkillTargetType.OneTile     => "단일 타일",
            CharacterSkillTargetType.AllTiles    => "전체 타일",
            CharacterSkillTargetType.MultiEnemy  => "복수 적 선택",
            CharacterSkillTargetType.MultiAlly   => "복수 아군 선택",
            CharacterSkillTargetType.MultiTile   => "복수 타일 선택",
            _                                    => targetType.ToString(),
        };


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

            var selectedAbilities = new List<ActiveSkillSlot>(currentCharacter.Stats.ActiveAbilities);
            if (index < 0 || index >= selectedAbilities.Count)
            {
                ReturnDiceElement();
                return;
            }

            ActiveSkillSlot runtimeAbility = selectedAbilities[index];
            if (runtimeAbility?.BaseSkill == null || !runtimeAbility.CanUse(currentDice.Value))
            {
                Debug.LogWarning("[CharacterActionUI] Selected dice does not satisfy skill requirement/cooldown. Returning dice.");
                ReturnDiceElement();
                return;
            }

            SkillManager.Instance.PrepareSkill(currentCharacter, index, currentDice);
        }

        private void MarkDiceUsed(DiceData dice)
        {
            var diceUI = FindFirstObjectByType<DiceUI>();
            diceUI?.MarkDiceAsUsed(dice);
        }

        public void ReturnDiceElement()
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
                canUseSelectedDiceForSkill = primaryAbility.CanUse(currentDice.Value);
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
                var preset = currentCharacter.Stats?.SourcePreset;
                if (preset != null)
                    portrait = preset.Portrait ?? preset.CharacterSprite;
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
