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
    /// 웨이브 클리어 보상 화면 — StS식 수령 리스트.
    /// 가운데 세로로 보상 행(골드/캐릭터 강화/유물/포션)이 쌓이고,
    /// 하나 클릭 → 수령 → 행이 사라지며 나머지가 위로 당겨진다. [계속]으로 맵 복귀.
    ///
    /// "캐릭터 강화" 행을 누르면 강화 창(캐릭터 선택 → 모디파이어 3택1)이 뜬다.
    ///
    /// ── 에디터 소유 레이아웃 ─────────────────────────────────────
    /// 컴포넌트 우클릭 → [기본 레이아웃 생성] → 씬에서 자유롭게 스타일링.
    /// 슬롯이 비어 있으면 런타임 폴백 생성. 행 모양은 rewardRowPrefab으로 교체 가능.
    /// </summary>
    public class RewardUI : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private int goldPerReward = 50;
        [SerializeField] private int modifierChoiceCount = 3;
        [Range(0f, 1f)]
        [SerializeField] private float potionDropChance = 0.2f;   // 전투 보상 저확률 포션 드랍 (스펙 §6)

        [Header("슬롯 (씬에서 배치 — [기본 레이아웃 생성]으로 자동 배선)")]
        [SerializeField] private TextMeshProUGUI goldText;          // 현재 보유 골드 표시
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private RectTransform rewardListRoot;      // 보상 행이 세로로 쌓이는 곳
        [SerializeField] private GameObject upgradePanel;
        [SerializeField] private TextMeshProUGUI upgradeHeader;
        [SerializeField] private RectTransform choiceRow;           // 강화 창의 선택 버튼 행
        [SerializeField] private Button nextButton;
        [SerializeField] private Button cancelUpgradeButton;

        [Header("프리팹 (선택 — 비우면 기본 스타일 생성)")]
        [SerializeField] private Button choiceButtonPrefab;         // 강화 창 선택 카드
        [SerializeField] private Button rewardRowPrefab;            // 보상 행 (TMP 자식 필요)

        private Character _pickedCharacter;
        private GameObject _upgradeRow;                             // 강화 행 (강화 완료 시 제거)
        private bool _listenersWired;
        private TMP_FontAsset _font;

        // ── 팔레트: "보드게임의 밤" — 어두운 테이블 위 카드/코인 ──
        private static readonly Color Felt     = new Color(0.043f, 0.051f, 0.078f, 0.85f);
        private static readonly Color Card     = new Color(0.118f, 0.133f, 0.200f);
        private static readonly Color CardEdge = new Color(0.239f, 0.271f, 0.400f);
        private static readonly Color CardWell = new Color(0.082f, 0.094f, 0.153f);
        private static readonly Color Ink      = new Color(0.910f, 0.894f, 0.847f);
        private static readonly Color InkMuted = new Color(0.910f, 0.894f, 0.847f, 0.45f);
        private static readonly Color Gold     = new Color(0.878f, 0.702f, 0.341f);
        private static readonly Color GoldInk  = new Color(0.140f, 0.110f, 0.055f);
        private static readonly Color Slate    = new Color(0.200f, 0.255f, 0.368f);

        private const int PanelRadius = 26;
        private const int ButtonRadius = 18;

        private void Awake()
        {
            WireButtons();
        }

        public void Show()
        {
            gameObject.SetActive(true);

            if (goldText == null)
            {
                Debug.LogWarning("[RewardUI] 슬롯이 비어 있어 기본 레이아웃을 런타임 생성합니다. " +
                                 "컴포넌트 우클릭 → [기본 레이아웃 생성]으로 씬에 고정하는 것을 권장합니다.");
                BuildDefaultLayout();
            }
            WireButtons();

            BattleInfoPanelUI.SetVisible(false);   // 보상/모집 화면 동안 정보 패널 숨김

            _pickedCharacter = null;
            _upgradeRow = null;
            ShowUpgradePanel(false);
            RefreshGold();
            BuildRewardRows();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            BattleInfoPanelUI.SetVisible(true);    // 전투 복귀 시 정보 패널 복원
        }

        private void RefreshGold()
        {
            if (goldText != null)
                goldText.text = $"골드  {GoldManager.Instance?.Gold ?? 0}";
        }

        /// <summary>씬 배선/폴백 생성 어느 쪽이든 리스너는 코드에서 1회만 연결.</summary>
        private void WireButtons()
        {
            if (_listenersWired) return;
            if (nextButton == null && cancelUpgradeButton == null) return;

            nextButton?.onClick.AddListener(OnNextClicked);
            cancelUpgradeButton?.onClick.AddListener(() => ShowUpgradePanel(false));
            _listenersWired = true;
        }

        // ─────────────────────────────────────────────
        // 보상 리스트 (StS식 — 클릭 수령, 행 제거)
        // ─────────────────────────────────────────────

        private void BuildRewardRows()
        {
            if (rewardListRoot == null) return;

            for (int i = rewardListRoot.childCount - 1; i >= 0; i--)
                Destroy(rewardListRoot.GetChild(i).gameObject);

            // ① 골드 (+유물 보너스, 예: 황금 주사위)
            int goldAmount = goldPerReward + (RelicManager.Instance?.BattleGoldBonus ?? 0);
            AddRewardRow($"<color=#{ColorUtility.ToHtmlStringRGB(Gold)}>●</color>  골드 +{goldAmount}", row =>
            {
                GoldManager.EnsureInstance().AddGold(goldAmount);
                RefreshGold();
                Destroy(row);
            });

            // ② 캐릭터 강화 (모디파이어 3택1)
            _upgradeRow = AddRewardRow("⬆  캐릭터 강화  <size=60%>모디파이어 3택1</size>", row =>
            {
                BeginUpgradeFlow();   // 수령은 모디파이어 선택 완료 시점 (취소하면 행 유지)
            });

            // ③ 유물 (엘리트 클리어 시 — 스펙 §2)
            var runNode = RunManager.Instance?.CurrentNode;
            if (runNode != null && runNode.Type == MapNodeType.Elite)
            {
                var relic = RelicManager.EnsureInstance().GetShopOfferings(1).FirstOrDefault();
                if (relic != null)
                {
                    AddRewardRow($"🏺  유물 — {relic.RelicName}", row =>
                    {
                        RelicManager.Instance.Grant(relic);
                        Destroy(row);
                    });
                }
            }

            // ④ 포션 (저확률 드랍)
            if (Random.value < potionDropChance)
            {
                var potion = PotionManager.EnsureInstance().GetShopOfferings(1).FirstOrDefault();
                if (potion != null)
                {
                    AddRewardRow($"🧪  포션 — {potion.PotionName}", row =>
                    {
                        if (PotionManager.Instance.TryAdd(potion))
                            Destroy(row);
                        else
                            Debug.Log("[Reward] 포션 슬롯이 가득 — HUD에서 버린 뒤 수령하세요.");
                    });
                }
            }
        }

        /// <summary>보상 행 추가 (StS식 가로 바). 클릭 시 onClaim(자기 자신)을 호출.</summary>
        private GameObject AddRewardRow(string label, System.Action<GameObject> onClaim)
        {
            if (rewardListRoot == null) return null;

            if (rewardRowPrefab != null)
            {
                var instance = Instantiate(rewardRowPrefab, rewardListRoot);
                var tmp = instance.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null) tmp.text = label;
                var go2 = instance.gameObject;
                instance.onClick.AddListener(() => onClaim(go2));
                return go2;
            }

            var go = CreateChild(rewardListRoot, "RewardRow");
            var le = go.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 74;
            le.flexibleWidth = 1f;

            var img = go.gameObject.AddComponent<Image>();
            img.sprite = UiRoundedSprite.Get(ButtonRadius);
            img.type = Image.Type.Sliced;
            img.color = Color.white;

            var shadow = go.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
            shadow.effectDistance = new Vector2(0f, -4f);

            var btn = go.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.colors = MakeColors(new Color(0.145f, 0.169f, 0.259f));
            var captured = go.gameObject;
            btn.onClick.AddListener(() => onClaim(captured));

            var txt = CreateText(go, label, 27, FontStyles.Bold);
            txt.alignment = TextAlignmentOptions.MidlineLeft;
            txt.margin = new Vector4(26, 8, 18, 8);
            Stretch(txt);

            return go.gameObject;
        }

        // ─────────────────────────────────────────────
        // 캐릭터 강화 흐름 (강화 행 클릭 → 창)
        // ─────────────────────────────────────────────
        private void BeginUpgradeFlow()
        {
            _pickedCharacter = null;
            if (upgradeHeader != null) upgradeHeader.text = "강화할 캐릭터 선택";
            ClearRow();

            var party = PartyManager.Instance?.Party;
            if (party != null)
            {
                foreach (var ch in party)
                {
                    if (ch == null || !ch.IsAlive) continue;
                    var captured = ch;
                    string name = ch.Stats != null ? ch.Stats.CharacterName : ch.name;
                    AddChoiceButton(name, () => OnCharacterPicked(captured));
                }
            }
            ShowUpgradePanel(true);
        }

        private void OnCharacterPicked(Character ch)
        {
            _pickedCharacter = ch;

            string name = ch.Stats != null ? ch.Stats.CharacterName : ch.name;
            if (upgradeHeader != null) upgradeHeader.text = $"[{name}] 모디파이어 선택";
            ClearRow();

            // 이 캐릭터에게 적용 가능한 모디파이어만 제시
            var choices = ModifierRegistry.GetRandomChoicesFor(ch, modifierChoiceCount);
            foreach (var mod in choices)
            {
                var captured = mod;
                AddChoiceButton($"{mod.ModifierName}\n<size=60%>{mod.Description}</size>", () => OnModifierPicked(captured));
            }
        }

        private void OnModifierPicked(CharacterModifier mod)
        {
            if (_pickedCharacter != null && mod != null)
            {
                _pickedCharacter.Stats?.Modifiers?.Add(mod);
                Debug.Log($"[Reward] {_pickedCharacter.Stats?.CharacterName} ← '{mod.ModifierName}' 장착");
            }

            // 강화 행 수령 완료 → 제거
            if (_upgradeRow != null) { Destroy(_upgradeRow); _upgradeRow = null; }
            ShowUpgradePanel(false);
        }

        private void OnNextClicked()
        {
            GameFlowManager.Instance?.OnRewardComplete();
        }

        private void ShowUpgradePanel(bool visible)
        {
            if (upgradePanel != null) upgradePanel.SetActive(visible);
        }

        private void ClearRow()
        {
            if (choiceRow == null) return;
            for (int i = choiceRow.childCount - 1; i >= 0; i--)
                Destroy(choiceRow.GetChild(i).gameObject);
        }

        /// <summary>강화 창 선택 버튼 1개 추가. 프리팹이 있으면 그걸로, 없으면 기본 스타일로 생성.</summary>
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

            // 손패의 카드 한 장 — 호버 시 밝아짐 (집어 드는 느낌)
            var go = CreateChild(choiceRow, "Choice");
            var le = go.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 250; le.preferredHeight = 220;

            var img = go.gameObject.AddComponent<Image>();
            img.sprite = UiRoundedSprite.Get(ButtonRadius);
            img.type = Image.Type.Sliced;
            img.color = Color.white;

            var shadow = go.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(0f, -6f);

            var btn = go.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.colors = MakeColors(new Color(0.145f, 0.169f, 0.259f));   // 카드보다 살짝 밝은 남색
            btn.onClick.AddListener(() => onClick());

            var txt = CreateText(go, label, 25, FontStyles.Bold);
            Stretch(txt);
            txt.margin = new Vector4(14, 14, 14, 14);
        }

        // ─────────────────────────────────────────────
        // [에디터] 기본 레이아웃 생성 — 1회 실행 후 씬에서 자유롭게 스타일링
        // (런타임 폴백으로도 동일 메서드 사용)
        // ─────────────────────────────────────────────
        [ContextMenu("기본 레이아웃 생성")]
        private void BuildDefaultLayout()
        {
            if (transform.Find("_RewardCanvas") != null)
            {
                Debug.LogWarning("[RewardUI] _RewardCanvas가 이미 있습니다. 다시 생성하려면 기존 것을 삭제하세요.");
                return;
            }

            _font = FindAnyObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include)?.font;

            var canvasGO = new GameObject("_RewardCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1500;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            // 어두운 배경 (테이블의 어둠)
            var dim = CreateChild(canvasGO.transform, "Dim");
            Stretch(dim);
            dim.gameObject.AddComponent<Image>().color = Felt;

            // 메인 패널 — 카드 스톡
            mainPanel = CreatePanel(canvasGO.transform, "MainPanel", new Vector2(820, 720), new Vector2(0, 0));

            var eyebrow = CreateText(mainPanel.transform, "웨이브 클리어", 22, FontStyles.Normal);
            eyebrow.color = InkMuted;
            Place(eyebrow, new Vector2(0, 306), new Vector2(700, 34));

            var title = CreateText(mainPanel.transform, "보상 획득", 56, FontStyles.Bold);
            Place(title, new Vector2(0, 254), new Vector2(700, 72));

            // 시그니처: 주사위 5눈 핍 디바이더
            BuildPipDivider(mainPanel.transform, new Vector2(0, 204));

            // 골드 — 코인 필(pill) 토큰 (현재 보유량)
            var pill = CreateChild(mainPanel.transform, "GoldPill");
            Place(pill, new Vector2(0, 152), new Vector2(360, 52));
            var pillImg = pill.gameObject.AddComponent<Image>();
            pillImg.sprite = UiRoundedSprite.Get(26);
            pillImg.type = Image.Type.Sliced;
            pillImg.color = CardWell;
            goldText = CreateText(pill, "골드 0", 30, FontStyles.Bold);
            goldText.color = Gold;
            Stretch(goldText);

            // 보상 행 리스트 (세로 스택 — 수령하면 사라지고 나머지가 올라옴)
            var listGO = CreateChild(mainPanel.transform, "RewardList");
            Place(listGO, new Vector2(0, -60), new Vector2(620, 340));
            rewardListRoot = listGO;
            var vlg = listGO.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 14;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // 계속 버튼 (주 행동 — Gold)
            nextButton = CreateButton(mainPanel.transform, "계속", new Vector2(0, -300),
                new Vector2(300, 80), out _, primary: true);

            // 강화 패널 (모달, 초기 숨김)
            upgradePanel = CreatePanel(canvasGO.transform, "UpgradePanel", new Vector2(1120, 560), new Vector2(0, 0));
            upgradeHeader = CreateText(upgradePanel.transform, "강화할 캐릭터 선택", 42, FontStyles.Bold);
            Place(upgradeHeader, new Vector2(0, 212), new Vector2(1000, 76));

            var rowGO = CreateChild(upgradePanel.transform, "ChoiceRow");
            Place(rowGO, new Vector2(0, -6), new Vector2(1040, 260));
            var hlg = rowGO.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 24; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            choiceRow = rowGO;

            cancelUpgradeButton = CreateButton(upgradePanel.transform, "취소", new Vector2(0, -218),
                new Vector2(220, 72), out _, primary: false);

            upgradePanel.SetActive(false);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
            Debug.Log("[RewardUI] 기본 레이아웃 생성 완료 — 계층을 자유롭게 스타일링한 뒤 씬을 저장하세요. " +
                      "(버튼 동작은 코드가 참조로 연결하므로 OnClick을 따로 배선할 필요 없음)");
        }

        /// <summary>시그니처 요소: 주사위 5눈(⚄) 모양의 점 디바이더.</summary>
        private void BuildPipDivider(Transform parent, Vector2 anchoredPos)
        {
            var row = CreateChild(parent, "PipDivider");
            Place(row, anchoredPos, new Vector2(200, 14));

            const int pipCount = 5;
            const float pipSize = 10f;
            const float spacing = 24f;
            float startX = -(pipCount - 1) * spacing * 0.5f;

            for (int i = 0; i < pipCount; i++)
            {
                var pip = CreateChild(row, $"Pip{i}");
                Place(pip, new Vector2(startX + i * spacing, 0), new Vector2(pipSize, pipSize));
                var img = pip.gameObject.AddComponent<Image>();
                img.sprite = UiRoundedSprite.Get((int)(pipSize * 0.5f));   // 반지름 = 크기/2 → 원
                img.color = Gold;
                img.raycastTarget = false;
            }
        }

        // ── 빌드 헬퍼 ──────────────────────────────────────

        /// <summary>카드 스타일 패널: 모서리(테두리) + 인셋 배경 2겹 라운드 + 그림자.</summary>
        private GameObject CreatePanel(Transform parent, string name, Vector2 size, Vector2 pos)
        {
            var go = CreateChild(parent, name);
            Place(go, pos, size);

            var edge = go.gameObject.AddComponent<Image>();
            edge.sprite = UiRoundedSprite.Get(PanelRadius);
            edge.type = Image.Type.Sliced;
            edge.color = CardEdge;

            var shadow = go.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(0f, -10f);

            var bg = CreateChild(go, "BG");
            Stretch(bg);
            bg.offsetMin = new Vector2(3f, 3f);
            bg.offsetMax = new Vector2(-3f, -3f);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.sprite = UiRoundedSprite.Get(PanelRadius - 3);
            bgImg.type = Image.Type.Sliced;
            bgImg.color = Card;

            return go.gameObject;
        }

        /// <summary>
        /// 라운드 버튼: 호버/눌림은 ColorBlock으로 (밝아짐/어두워짐).
        /// primary = Gold(주 행동, 어두운 글자) / 아니면 Slate(보조, 크림 글자).
        /// </summary>
        private Button CreateButton(Transform parent, string label, Vector2 pos, Vector2 size,
            out TextMeshProUGUI labelText, bool primary)
        {
            Color fill = primary ? Gold : Slate;
            Color textColor = primary ? GoldInk : Ink;

            var go = CreateChild(parent, "Button_" + label);
            Place(go, pos, size);

            var img = go.gameObject.AddComponent<Image>();
            img.sprite = UiRoundedSprite.Get(ButtonRadius);
            img.type = Image.Type.Sliced;
            img.color = Color.white;   // 실제 색은 ColorBlock이 곱함 (호버 시 밝아짐)

            var shadow = go.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
            shadow.effectDistance = new Vector2(0f, -4f);

            var btn = go.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.colors = MakeColors(fill);

            labelText = CreateText(go, label, 32, FontStyles.Bold);
            labelText.color = textColor;
            Stretch(labelText);
            return btn;
        }

        private static ColorBlock MakeColors(Color fill)
        {
            var cb = ColorBlock.defaultColorBlock;
            cb.normalColor      = fill;
            cb.highlightedColor = Color.Lerp(fill, Color.white, 0.15f);
            cb.pressedColor     = Color.Lerp(fill, Color.black, 0.25f);
            cb.selectedColor    = fill;
            cb.disabledColor    = new Color(fill.r, fill.g, fill.b, 0.35f);
            cb.fadeDuration     = 0.08f;
            return cb;
        }

        private TextMeshProUGUI CreateText(Transform parent, string text, float size, FontStyles style)
        {
            var go = CreateChild(parent, "Text");
            var tmp = go.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Ink;
            tmp.raycastTarget = false;
            if (_font != null) tmp.font = _font;
            return tmp;
        }

        private static RectTransform CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Place(RectTransform rt, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        // TMP/Image 등 Graphic을 직접 넘길 수 있게 오버로드 (rectTransform 위임)
        private static void Place(Graphic g, Vector2 anchoredPos, Vector2 size) => Place(g.rectTransform, anchoredPos, size);

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Stretch(Graphic g) => Stretch(g.rectTransform);
    }
}
