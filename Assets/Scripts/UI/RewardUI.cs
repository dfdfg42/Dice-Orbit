using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Core;
using DiceOrbit.Core.Run;
using DiceOrbit.Data;
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
    /// ── 레이아웃 ─────────────────────────────────────
    /// 슬롯은 씬에서 직접 배치한다. 행 모양은 rewardRowPrefab으로 교체 가능.
    /// </summary>
    public class RewardUI : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private int goldPerReward = 50;
        [SerializeField] private int modifierChoiceCount = 3;
        [Range(0f, 1f)]
        [SerializeField] private float potionDropChance = 0.2f;   // 전투 보상 저확률 포션 드랍 (스펙 §6)

        [Header("슬롯 (씬에서 배치)")]
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
        private DieDefinitionSO _pendingNewDie;                     // 주사위 보상: 교체할 새 주사위
        private GameObject _diceRewardRow;                          // 주사위 보상 행 (교체 완료 시 제거)
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
            int goldAmount = goldPerReward + (ArtifactManager.Instance?.BattleGoldBonus ?? 0);
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
                var artifact = ArtifactManager.EnsureInstance().GetShopOfferings(1).FirstOrDefault();
                if (artifact != null)
                {
                    AddRewardRow($"🏺  유물 — {artifact.artifactName}", row =>
                    {
                        ArtifactManager.Instance.Grant(artifact);
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

            // ⑤ 주사위 획득 (특수 주사위 풀에서 랜덤 → 현재 덱과 교체)
            var newDie = DiceDeckManager.EnsureInstance()?.DrawRandomSpecial();
            if (newDie != null)
            {
                _diceRewardRow = AddRewardRow($"🎲  주사위 — {newDie.Name}", row =>
                {
                    BeginDiceReplaceFlow(newDie);   // 수령 = 교체 슬롯 선택 완료 시점
                });
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

        // ─────────────────────────────────────────────
        // 주사위 교체 흐름 (주사위 보상 행 클릭 → 덱 슬롯 선택 → 교체)
        // ─────────────────────────────────────────────
        private void BeginDiceReplaceFlow(DieDefinitionSO newDie)
        {
            _pendingNewDie = newDie;
            if (upgradeHeader != null) upgradeHeader.text = $"[{newDie.Name}]로 교체할 주사위 선택";
            ClearRow();

            var deck = DiceDeckManager.Instance?.Deck;
            if (deck != null)
            {
                for (int i = 0; i < deck.Count; i++)
                {
                    int idx = i;
                    var inst = deck[i];
                    string faces = inst.Faces != null ? string.Join(" ", inst.Faces) : "";
                    string name = inst.BaseDie != null ? inst.BaseDie.Name : "주사위";
                    AddChoiceButton($"{name}\n<size=60%>[{faces}]</size>", () => OnDiceSlotPicked(idx));
                }
            }
            ShowUpgradePanel(true);
        }

        private void OnDiceSlotPicked(int index)
        {
            if (_pendingNewDie != null)
            {
                DiceDeckManager.Instance?.Replace(index, _pendingNewDie);
                Debug.Log($"[Reward] 덱 {index}번을 '{_pendingNewDie.Name}'로 교체");
            }
            _pendingNewDie = null;

            if (_diceRewardRow != null) { Destroy(_diceRewardRow); _diceRewardRow = null; }
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
            le.preferredWidth = 340; le.preferredHeight = 320;

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
