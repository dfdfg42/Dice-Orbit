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
    /// 레이아웃은 씬에서 배치하고 인스펙터 슬롯에 배선한다.
    /// </summary>
    public class ShopUI : MonoBehaviour
    {
        public static ShopUI Instance { get; private set; }

        [Header("Tuning")]
        [SerializeField] private int swapBaseCost = 60;
        [SerializeField] private int swapCostPerModifier = 30;
        [SerializeField] private int candidateCount = 3;
        [SerializeField] private int modifierChoiceCount = 3;
        [SerializeField] private int potionOfferCount = 2;
        [SerializeField] private int relicOfferCount = 2;

        [Header("슬롯 (씬에서 배치 — 인스펙터에 배선)")]
        [SerializeField] private GameObject rootCanvas;
        [SerializeField] private Image backgroundImage;             // 상점 배경 (전체)
        [SerializeField] private Image merchantImage;               // 우측 상인
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private Button swapButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private GameObject stepPanel;              // 교체 단계 진행 패널 (모달)
        [SerializeField] private TextMeshProUGUI stepHeader;
        [SerializeField] private RectTransform choiceRow;
        [SerializeField] private Button cancelStepButton;
        [SerializeField] private RectTransform potionShelfRow;      // 물약 선반 (아이템이 올라가는 곳)
        [SerializeField] private RectTransform relicShelfRow;       // 유물 선반

        [Header("스킨 (선택 — 비우면 기본색)")]
        [SerializeField] private Sprite backgroundSprite;           // 상점 내부 아트
        [SerializeField] private Sprite merchantSprite;             // 상인 일러스트

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

            WireButtons();

            rootCanvas.SetActive(true);
            BattleInfoPanelUI.SetVisible(false);

            // 스킨 적용 (스프라이트 없으면 기본색/숨김)
            if (backgroundImage != null)
            {
                backgroundImage.sprite = backgroundSprite;
                backgroundImage.color = backgroundSprite != null ? Color.white : Felt;
            }
            if (merchantImage != null)
            {
                merchantImage.sprite = merchantSprite;
                merchantImage.enabled = merchantSprite != null;
            }

            _outgoing = null;
            _incoming = null;
            _repickRemaining = 0;
            ShowStepPanel(false);

            BuildStockOffers();
            RenderStock();
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
            return ApplyDiscount(swapBaseCost + swapCostPerModifier * modCount);
        }

        /// <summary>유물 할인 적용 (예: 단골 도장 -20%).</summary>
        private static int ApplyDiscount(int price)
        {
            float discount = ArtifactManager.Instance?.ShopDiscount01 ?? 0f;
            return Mathf.Max(1, Mathf.RoundToInt(price * (1f - discount)));
        }

        // ─────────────────────────────────────────────
        // 진열대: 포션 + 유물 (스펙 §2 — 골드의 소비처)
        // ─────────────────────────────────────────────

        private readonly List<Potion> _potionOffers = new List<Potion>();
        private readonly List<ArtifactData> _relicOffers = new List<ArtifactData>();
        private readonly HashSet<Object> _soldOut = new HashSet<Object>();

        private void BuildStockOffers()
        {
            _potionOffers.Clear();
            _relicOffers.Clear();
            _soldOut.Clear();

            _potionOffers.AddRange(PotionManager.EnsureInstance().GetShopOfferings(potionOfferCount));
            _relicOffers.AddRange(ArtifactManager.EnsureInstance().GetShopOfferings(relicOfferCount));
        }

        private void RenderStock()
        {
            ClearShelf(potionShelfRow);
            ClearShelf(relicShelfRow);

            int gold = GoldManager.Instance?.Gold ?? 0;

            foreach (var potion in _potionOffers)
            {
                var captured = potion;
                int price = ApplyDiscount(potion.ShopPrice);
                AddStockCard(potionShelfRow, potion.PotionName, potion.Description, price,
                    sold: _soldOut.Contains(potion),
                    affordable: gold >= price && (PotionManager.Instance?.HasFreeSlot ?? false),
                    onBuy: () =>
                    {
                        if (!(PotionManager.Instance?.HasFreeSlot ?? false)) return;
                        if (!GoldManager.Instance.TrySpend(ApplyDiscount(captured.ShopPrice))) return;
                        PotionManager.Instance.TryAdd(captured);
                        _soldOut.Add(captured);
                        RefreshGold();
                        RenderStock();
                    });
            }

            foreach (var artifact in _relicOffers)
            {
                var captured = artifact;
                int price = ApplyDiscount(artifact.shopPrice);
                AddStockCard(relicShelfRow, artifact.artifactName, artifact.artifactTooltip, price,
                    sold: _soldOut.Contains(artifact),
                    affordable: gold >= price,
                    onBuy: () =>
                    {
                        if (!GoldManager.Instance.TrySpend(ApplyDiscount(captured.shopPrice))) return;
                        ArtifactManager.Instance.Grant(captured);
                        _soldOut.Add(captured);
                        RefreshGold();
                        RenderStock();
                    });
            }
        }

        private static void ClearShelf(RectTransform shelf)
        {
            if (shelf == null) return;
            for (int i = shelf.childCount - 1; i >= 0; i--)
                Destroy(shelf.GetChild(i).gameObject);
        }

        /// <summary>선반 위 상품 카드: 이름(위) + 설명 + 가격표(아래).</summary>
        private void AddStockCard(RectTransform shelf, string title, string desc, int price, bool sold, bool affordable, System.Action onBuy)
        {
            if (shelf == null) return;

            string label = sold
                ? $"<color=#777777>{title}</color>\n\n<size=60%><color=#666666>품절</color></size>"
                : $"{title}\n<size=50%>{desc}</size>\n<size=70%><color=#{ColorUtility.ToHtmlStringRGB(Gold)}>{price}G</color>" +
                  (affordable ? "" : " <color=#B05050>✕</color>") + "</size>";

            var go = new GameObject("Goods", typeof(RectTransform));
            go.transform.SetParent(shelf, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 158; le.preferredHeight = 168;

            var img = go.AddComponent<Image>();
            img.sprite = UiRoundedSprite.Get(14);
            img.type = Image.Type.Sliced;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            shadow.effectDistance = new Vector2(0f, -5f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.interactable = !sold && affordable;
            var fill = new Color(0.145f, 0.169f, 0.259f, 0.94f);
            var cb = ColorBlock.defaultColorBlock;
            cb.normalColor = fill;
            cb.highlightedColor = Color.Lerp(fill, Color.white, 0.15f);
            cb.pressedColor = Color.Lerp(fill, Color.black, 0.25f);
            cb.selectedColor = fill;
            cb.disabledColor = new Color(fill.r, fill.g, fill.b, 0.5f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            btn.onClick.AddListener(() => onBuy());

            var txt = CreateText(go, label, 18, FontStyles.Bold);
            txt.margin = new Vector4(8, 10, 8, 10);
            Stretch(txt);
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

        // ── 생성 헬퍼 (런타임 상품/버튼 UI) ───────────────────

        private GameObject CreateChild(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go;
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
    }
}
