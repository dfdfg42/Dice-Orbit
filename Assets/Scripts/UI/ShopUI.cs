using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Core;
using DiceOrbit.Core.Run;
using DiceOrbit.Data.Modifiers;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 상점 노드 화면 (GameState.Shop). 스펙 §2: 골드 소비처 — 캐릭터 교체 (+포션/유물은 시스템 구현 후).
    ///
    /// 캐릭터 교체 (스펙 §3):
    ///   비용 = 기본 + 모디파이어 개수 비례 / 내보낸 캐릭터는 런에서 소멸 /
    ///   새 멤버는 기존 모디파이어 개수만큼 3택1 재선택 / 풀피 + 부활 스톡 만땅 입장.
    ///
    /// ── 에디터 소유 레이아웃 ─────────────────────────────────────
    /// 컴포넌트 우클릭 → [기본 레이아웃 생성] → 씬에서 자유롭게 스타일링.
    /// 슬롯이 비어 있으면 런타임 폴백 생성 (셋업 전에도 동작).
    /// </summary>
    public class ShopUI : MonoBehaviour
    {
        public static ShopUI Instance { get; private set; }

        [Header("Tuning")]
        [SerializeField] private int swapBaseCost = 60;
        [SerializeField] private int swapCostPerModifier = 30;
        [SerializeField] private int candidateCount = 3;
        [SerializeField] private int modifierChoiceCount = 3;

        [Header("슬롯 (씬에서 배치 — [기본 레이아웃 생성]으로 자동 배선)")]
        [SerializeField] private GameObject rootCanvas;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private Button swapButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private GameObject stepPanel;              // 교체 단계 진행 패널
        [SerializeField] private TextMeshProUGUI stepHeader;
        [SerializeField] private RectTransform choiceRow;
        [SerializeField] private Button cancelStepButton;

        [Header("선택 (비우면 기본 생성/탐색)")]
        [SerializeField] private Button choiceButtonPrefab;
        [Tooltip("교체 후보 풀. 비우면 CharacterSelectionUI의 목록을 사용")]
        [SerializeField] private List<CharacterPreset> characterPool = new List<CharacterPreset>();

        private bool _listenersWired;
        private TMP_FontAsset _font;

        // 교체 진행 상태
        private Character _outgoing;
        private int _repickRemaining;
        private Character _incoming;

        // ── 보드게임의 밤 팔레트 (RewardUI 계승) ──
        private static readonly Color Felt     = new Color(0.043f, 0.051f, 0.078f, 0.85f);
        private static readonly Color Card     = new Color(0.118f, 0.133f, 0.200f);
        private static readonly Color CardEdge = new Color(0.239f, 0.271f, 0.400f);
        private static readonly Color Ink      = new Color(0.910f, 0.894f, 0.847f);
        private static readonly Color InkMuted = new Color(0.910f, 0.894f, 0.847f, 0.45f);
        private static readonly Color Gold     = new Color(0.878f, 0.702f, 0.341f);
        private static readonly Color GoldInk  = new Color(0.140f, 0.110f, 0.055f);
        private static readonly Color Slate    = new Color(0.200f, 0.255f, 0.368f);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            WireButtons();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            Instance = FindFirstObjectByType<ShopUI>(FindObjectsInactive.Include);
            if (Instance == null)
            {
                var go = new GameObject("ShopUI");
                Instance = go.AddComponent<ShopUI>();
            }
        }

        // ── 공개 API ──────────────────────────────────────────

        public void Show()
        {
            gameObject.SetActive(true);

            if (rootCanvas == null)
            {
                Debug.LogWarning("[ShopUI] 슬롯이 비어 있어 기본 레이아웃을 런타임 생성합니다. " +
                                 "컴포넌트 우클릭 → [기본 레이아웃 생성]으로 씬에 고정하는 것을 권장합니다.");
                BuildDefaultLayout();
            }
            WireButtons();

            rootCanvas.SetActive(true);
            BattleInfoPanelUI.SetVisible(false);

            _outgoing = null;
            _incoming = null;
            _repickRemaining = 0;
            ShowStepPanel(false);
            RefreshGold();
        }

        public void Hide()
        {
            if (rootCanvas != null) rootCanvas.SetActive(false);
            BattleInfoPanelUI.SetVisible(true);
        }

        // ── 버튼 배선 ─────────────────────────────────────────

        private void WireButtons()
        {
            if (_listenersWired) return;
            if (swapButton == null && leaveButton == null) return;

            swapButton?.onClick.AddListener(BeginSwapFlow);
            leaveButton?.onClick.AddListener(() => GameFlowManager.Instance?.OnShopComplete());
            cancelStepButton?.onClick.AddListener(CancelStep);
            _listenersWired = true;
        }

        private void RefreshGold()
        {
            if (goldText != null)
                goldText.text = $"골드  {GoldManager.Instance?.Gold ?? 0}";
        }

        // ─────────────────────────────────────────────
        // 캐릭터 교체 흐름 (3단계)
        // ─────────────────────────────────────────────

        /// <summary>1단계: 내보낼 파티원 선택 (비용 = 기본 + 모디파이어 수 비례).</summary>
        private void BeginSwapFlow()
        {
            _outgoing = null;
            _incoming = null;
            if (stepHeader != null) stepHeader.text = "내보낼 파티원 선택";
            ClearRow();

            var party = PartyManager.Instance?.Party;
            if (party == null || party.Count == 0) return;

            int gold = GoldManager.Instance?.Gold ?? 0;
            foreach (var ch in party)
            {
                if (ch == null || ch.Stats == null) continue;
                var captured = ch;
                int cost = GetSwapCost(ch);
                bool affordable = gold >= cost;

                string label = $"{ch.Stats.CharacterName}\n<size=60%><color=#{ColorUtility.ToHtmlStringRGB(Gold)}>비용 {cost}G</color>" +
                               (affordable ? "" : "  <color=#B05050>(골드 부족)</color>") + "</size>";
                AddChoiceButton(label, () => { if (affordable) OnOutgoingPicked(captured); });
            }
            ShowStepPanel(true);
        }

        private int GetSwapCost(Character ch)
        {
            int modCount = ch?.Stats?.Modifiers?.Modifiers?.Count ?? 0;
            return swapBaseCost + swapCostPerModifier * modCount;
        }

        /// <summary>2단계: 새 캐릭터 후보 선택 (파티/소멸 제외 풀에서 랜덤).</summary>
        private void OnOutgoingPicked(Character outgoing)
        {
            _outgoing = outgoing;
            if (stepHeader != null) stepHeader.text = $"[{outgoing.Stats.CharacterName}] 대신 영입할 캐릭터";
            ClearRow();

            var candidates = BuildCandidates();
            if (candidates.Count == 0)
            {
                if (stepHeader != null) stepHeader.text = "영입 가능한 캐릭터가 없습니다";
                return;
            }

            foreach (var preset in candidates)
            {
                var captured = preset;
                AddChoiceButton($"{preset.CharacterName}\n<size=60%>HP {preset.MaxHP}</size>", () => OnIncomingPicked(captured));
            }
        }

        private List<CharacterPreset> BuildCandidates()
        {
            var pool = characterPool.Count > 0
                ? characterPool
                : FindFirstObjectByType<CharacterSelectionUI>(FindObjectsInactive.Include)?.AllCharacters as IEnumerable<CharacterPreset>;
            if (pool == null) return new List<CharacterPreset>();

            var partyPresets = new HashSet<CharacterPreset>(
                (PartyManager.Instance?.Party ?? new List<Character>())
                    .Where(c => c != null && c.Stats != null && c.Stats.SourcePreset != null)
                    .Select(c => c.Stats.SourcePreset));

            var run = RunManager.Instance;
            return pool
                .Where(p => p != null && !partyPresets.Contains(p) && (run == null || !run.IsBanished(p)))
                .OrderBy(_ => Random.value)
                .Take(candidateCount)
                .ToList();
        }

        /// <summary>3단계: 결제 + 교체 실행 + 모디파이어 재선택 시작.</summary>
        private void OnIncomingPicked(CharacterPreset incomingPreset)
        {
            if (_outgoing == null || incomingPreset == null) return;

            int cost = GetSwapCost(_outgoing);
            if (GoldManager.Instance == null || !GoldManager.Instance.TrySpend(cost))
            {
                Debug.LogWarning("[ShopUI] 골드 부족 — 교체 취소");
                CancelStep();
                return;
            }

            int repickCount = _outgoing.Stats?.Modifiers?.Modifiers?.Count ?? 0;
            var outgoingPreset = _outgoing.Stats?.SourcePreset;
            string outgoingName = _outgoing.Stats?.CharacterName;

            // 내보내기: 파티 제거 + 오브젝트 파괴 + 런에서 소멸 (재영입 불가 — 리롤 세탁 방지)
            PartyManager.Instance?.RemoveCharacter(_outgoing);
            Destroy(_outgoing.gameObject);
            if (outgoingPreset != null) RunManager.Instance?.RegisterBanished(outgoingPreset);
            _outgoing = null;

            // 새 멤버 영입 (풀피 + 부활 스톡 만땅 = CreateStats 기본값)
            var spawner = FindFirstObjectByType<CharacterSpawner>();
            int slotCount = PartyManager.Instance != null ? PartyManager.Instance.MaxPartySize : 4;
            int slotIndex = PartyManager.Instance != null ? PartyManager.Instance.PartySize : 0;
            _incoming = spawner != null ? spawner.Spawn(incomingPreset, slotIndex, slotCount) : null;

            Debug.Log($"[ShopUI] 교체: {outgoingName} → {incomingPreset.CharacterName} ({cost}G, 재선택 {repickCount}회)");
            RefreshGold();

            // 모디파이어 재선택 (내보낸 캐릭터의 개수만큼 3택1)
            _repickRemaining = repickCount;
            if (_incoming == null || _repickRemaining <= 0)
            {
                ShowStepPanel(false);
                return;
            }
            ShowNextRepick(repickCount);
        }

        private void ShowNextRepick(int total)
        {
            int round = total - _repickRemaining + 1;
            string name = _incoming.Stats != null ? _incoming.Stats.CharacterName : _incoming.name;
            if (stepHeader != null) stepHeader.text = $"[{name}] 모디파이어 재선택 ({round}/{total})";
            ClearRow();

            var choices = ModifierRegistry.GetRandomChoicesFor(_incoming, modifierChoiceCount);
            if (choices == null || choices.Count == 0)
            {
                ShowStepPanel(false);
                return;
            }

            foreach (var mod in choices)
            {
                var captured = mod;
                AddChoiceButton($"{mod.ModifierName}\n<size=60%>{mod.Description}</size>", () =>
                {
                    _incoming.Stats?.Modifiers?.Add(captured);
                    _repickRemaining--;
                    if (_repickRemaining > 0) ShowNextRepick(total);
                    else ShowStepPanel(false);
                });
            }
        }

        private void CancelStep()
        {
            // 재선택 도중엔 취소 불가 (결제/교체가 이미 실행됨)
            if (_repickRemaining > 0) return;
            _outgoing = null;
            ShowStepPanel(false);
        }

        private void ShowStepPanel(bool visible)
        {
            if (stepPanel != null) stepPanel.SetActive(visible);
            if (cancelStepButton != null) cancelStepButton.gameObject.SetActive(visible && _repickRemaining <= 0);
        }

        private void ClearRow()
        {
            if (choiceRow == null) return;
            for (int i = choiceRow.childCount - 1; i >= 0; i--)
                Destroy(choiceRow.GetChild(i).gameObject);
        }

        private void AddChoiceButton(string label, System.Action onClick)
        {
            if (choiceRow == null) return;

            if (choiceButtonPrefab != null)
            {
                var btnInstance = Instantiate(choiceButtonPrefab, choiceRow);
                var tmp = btnInstance.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null) tmp.text = label;
                btnInstance.onClick.AddListener(() => onClick());
                return;
            }

            var go = new GameObject("Choice", typeof(RectTransform));
            go.transform.SetParent(choiceRow, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 240; le.preferredHeight = 200;

            var img = go.AddComponent<Image>();
            img.sprite = UiRoundedSprite.Get(18);
            img.type = Image.Type.Sliced;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(0f, -5f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = ColorBlock.defaultColorBlock;
            var fill = new Color(0.145f, 0.169f, 0.259f);
            cb.normalColor = fill;
            cb.highlightedColor = Color.Lerp(fill, Color.white, 0.15f);
            cb.pressedColor = Color.Lerp(fill, Color.black, 0.25f);
            cb.selectedColor = fill;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            btn.onClick.AddListener(() => onClick());

            var txt = CreateText(go, label, 22, FontStyles.Bold);
            txt.margin = new Vector4(12, 12, 12, 12);
            Stretch(txt);
        }

        // ─────────────────────────────────────────────
        // [에디터] 기본 레이아웃 생성 (런타임 폴백 겸용)
        // ─────────────────────────────────────────────
        [ContextMenu("기본 레이아웃 생성")]
        private void BuildDefaultLayout()
        {
            if (transform.Find("_ShopCanvas") != null)
            {
                Debug.LogWarning("[ShopUI] _ShopCanvas가 이미 있습니다. 다시 생성하려면 기존 것을 삭제하세요.");
                return;
            }

            _font = FindAnyObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include)?.font;

            var canvasGo = new GameObject("_ShopCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1450;   // 보상(1500) 바로 아래
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();
            rootCanvas = canvasGo;

            // 딤
            var dim = CreateChild(canvasGo, "Dim");
            StretchRect(dim);
            dim.AddComponent<Image>().color = Felt;

            // 메인 패널
            mainPanel = CreatePanel(canvasGo, "MainPanel", new Vector2(760, 560));

            var title = CreateText(mainPanel, "상  점", 44, FontStyles.Bold);
            PlaceTop(title, 30, 60);

            // 골드 pill
            var pill = CreateChild(mainPanel, "GoldPill");
            var pillRect = (RectTransform)pill.transform;
            pillRect.anchorMin = pillRect.anchorMax = new Vector2(0.5f, 1f);
            pillRect.anchoredPosition = new Vector2(0, -120);
            pillRect.sizeDelta = new Vector2(300, 54);
            var pillImg = pill.AddComponent<Image>();
            pillImg.sprite = UiRoundedSprite.Get(27);
            pillImg.type = Image.Type.Sliced;
            pillImg.color = new Color(0.082f, 0.094f, 0.153f);
            goldText = CreateText(pill, "골드 0", 30, FontStyles.Bold);
            goldText.color = Gold;
            Stretch(goldText);

            // 상품: 캐릭터 교체 버튼 + 준비 중 슬롯 2개
            swapButton = CreateButton(mainPanel, "SwapButton", "캐릭터 교체", new Vector2(0, -230), new Vector2(420, 76), false);

            var potionSoon = CreateText(mainPanel, "포션 — 준비 중", 24, FontStyles.Normal);
            potionSoon.color = InkMuted;
            PlaceAt(potionSoon, new Vector2(0, -320), new Vector2(420, 40));

            var relicSoon = CreateText(mainPanel, "유물 — 준비 중", 24, FontStyles.Normal);
            relicSoon.color = InkMuted;
            PlaceAt(relicSoon, new Vector2(0, -370), new Vector2(420, 40));

            // 떠나기 (주 행동)
            leaveButton = CreateButton(mainPanel, "LeaveButton", "떠나기", new Vector2(0, -470), new Vector2(300, 70), true);

            // ── 단계 패널 (교체 흐름) ──
            stepPanel = CreatePanel(canvasGo, "StepPanel", new Vector2(1100, 420));
            var stepRect = (RectTransform)stepPanel.transform;
            stepRect.anchoredPosition = new Vector2(0, -80);

            stepHeader = CreateText(stepPanel, "선택", 32, FontStyles.Bold);
            PlaceTop(stepHeader, 24, 50);

            var rowGo = CreateChild(stepPanel, "ChoiceRow");
            choiceRow = (RectTransform)rowGo.transform;
            choiceRow.anchorMin = choiceRow.anchorMax = new Vector2(0.5f, 0.5f);
            choiceRow.anchoredPosition = new Vector2(0, -20);
            choiceRow.sizeDelta = new Vector2(1020, 240);
            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            cancelStepButton = CreateButton(stepPanel, "CancelButton", "취소", new Vector2(0, -350), new Vector2(200, 56), false);
            var cancelRect = (RectTransform)cancelStepButton.transform;
            cancelRect.anchorMin = cancelRect.anchorMax = new Vector2(0.5f, 0f);
            cancelRect.anchoredPosition = new Vector2(0, 40);

            stepPanel.SetActive(false);
            rootCanvas.SetActive(false);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
            Debug.Log("[ShopUI] 기본 레이아웃 생성 완료 — 계층을 자유롭게 스타일링한 뒤 씬을 저장하세요.");
        }

        // ── 생성 헬퍼 ─────────────────────────────────────────

        private GameObject CreateChild(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private GameObject CreatePanel(GameObject parent, string name, Vector2 size)
        {
            var panel = CreateChild(parent, name);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;

            var edge = panel.AddComponent<Image>();
            edge.sprite = UiRoundedSprite.Get(26);
            edge.type = Image.Type.Sliced;
            edge.color = CardEdge;

            var shadow = panel.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(0f, -10f);

            var bg = CreateChild(panel, "BG");
            var bgRect = (RectTransform)bg.transform;
            bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = new Vector2(3, 3); bgRect.offsetMax = new Vector2(-3, -3);
            var bgImg = bg.AddComponent<Image>();
            bgImg.sprite = UiRoundedSprite.Get(23);
            bgImg.type = Image.Type.Sliced;
            bgImg.color = Card;
            bgImg.raycastTarget = false;

            return panel;
        }

        private Button CreateButton(GameObject parent, string name, string label, Vector2 pos, Vector2 size, bool primary)
        {
            var go = CreateChild(parent, name);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.sprite = UiRoundedSprite.Get(18);
            img.type = Image.Type.Sliced;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            Color fill = primary ? Gold : Slate;
            var cb = ColorBlock.defaultColorBlock;
            cb.normalColor = fill;
            cb.highlightedColor = Color.Lerp(fill, Color.white, 0.12f);
            cb.pressedColor = Color.Lerp(fill, Color.black, 0.2f);
            cb.selectedColor = fill;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            var txt = CreateText(go, label, 26, FontStyles.Bold);
            txt.color = primary ? GoldInk : Ink;
            Stretch(txt);
            return btn;
        }

        private TextMeshProUGUI CreateText(GameObject parent, string text, float size, FontStyles style)
        {
            var go = CreateChild(parent, "Text");
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Ink;
            tmp.raycastTarget = false;
            if (_font != null) tmp.font = _font;
            return tmp;
        }

        private static void Stretch(TextMeshProUGUI tmp)
        {
            var r = tmp.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }

        private static void StretchRect(GameObject go)
        {
            var r = (RectTransform)go.transform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }

        private static void PlaceTop(TextMeshProUGUI tmp, float topOffset, float height)
        {
            var r = tmp.rectTransform;
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -topOffset);
            r.sizeDelta = new Vector2(0f, height);
        }

        private static void PlaceAt(TextMeshProUGUI tmp, Vector2 pos, Vector2 size)
        {
            var r = tmp.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }
    }
}
