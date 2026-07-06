using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Core;
using DiceOrbit.Data.Modifiers;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 웨이브 클리어 보상 화면.
    /// 골드 표시 + 포션 슬롯(예정) + [캐릭터 업그레이드] (캐릭터 선택 → 모디파이어 선택 → 장착).
    ///
    /// ── 에디터 소유 레이아웃 ─────────────────────────────────────
    /// 패널/버튼/텍스트는 씬의 슬롯 참조로 분리 — 코드는 내용 채우기와 흐름만 담당.
    /// 최초 셋업: 컴포넌트 우클릭 → [기본 레이아웃 생성] → 생성된 계층을 자유롭게 스타일링.
    /// 슬롯이 비어 있으면 런타임에 기본 레이아웃을 폴백 생성한다 (셋업 전에도 게임 동작).
    ///
    /// 선택 카드 모양 커스텀: choiceButtonPrefab에 Button+TMP 프리팹을 꽂으면 그걸로 찍어낸다.
    /// </summary>
    public class RewardUI : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private int goldPerReward = 50;
        [SerializeField] private int modifierChoiceCount = 3;
        [SerializeField] private int potionSlotCount = 3;

        [Header("슬롯 (씬에서 배치 — [기본 레이아웃 생성]으로 자동 배선)")]
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject upgradePanel;
        [SerializeField] private TextMeshProUGUI upgradeHeader;
        [SerializeField] private RectTransform choiceRow;          // 선택 버튼(캐릭터/모디파이어)이 이 안에 생성됨
        [SerializeField] private Button upgradeButton;
        [SerializeField] private TextMeshProUGUI upgradeButtonLabel;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button cancelUpgradeButton;

        [Header("선택 카드 프리팹 (선택 — 비우면 기본 버튼 생성)")]
        [SerializeField] private Button choiceButtonPrefab;

        private Character _pickedCharacter;
        private bool _upgradeUsed;
        private bool _listenersWired;
        private TMP_FontAsset _font;

        // ── 팔레트: "보드게임의 밤" — 어두운 테이블 위 카드/코인 ──
        private static readonly Color Felt     = new Color(0.043f, 0.051f, 0.078f, 0.85f); // 딤 (테이블의 어둠)
        private static readonly Color Card     = new Color(0.118f, 0.133f, 0.200f);        // 패널 (남색 카드 스톡)
        private static readonly Color CardEdge = new Color(0.239f, 0.271f, 0.400f);        // 카드 모서리
        private static readonly Color CardWell = new Color(0.082f, 0.094f, 0.153f);        // 파인 슬롯 (인셋)
        private static readonly Color Ink      = new Color(0.910f, 0.894f, 0.847f);        // 본문 (크림 잉크)
        private static readonly Color InkMuted = new Color(0.910f, 0.894f, 0.847f, 0.45f);
        private static readonly Color Gold     = new Color(0.878f, 0.702f, 0.341f);        // 시그니처 액센트 (코인)
        private static readonly Color GoldInk  = new Color(0.140f, 0.110f, 0.055f);        // 골드 위 글자
        private static readonly Color Slate    = new Color(0.200f, 0.255f, 0.368f);        // 보조 버튼

        private const int PanelRadius = 26;
        private const int ButtonRadius = 18;

        private void Awake()
        {
            WireButtons();
        }

        public void Show()
        {
            gameObject.SetActive(true);

            // 씬에 레이아웃이 없으면 런타임 폴백 생성 (셋업 전에도 게임이 멈추지 않게)
            if (goldText == null)
            {
                Debug.LogWarning("[RewardUI] 슬롯이 비어 있어 기본 레이아웃을 런타임 생성합니다. " +
                                 "컴포넌트 우클릭 → [기본 레이아웃 생성]으로 씬에 고정하는 것을 권장합니다.");
                BuildDefaultLayout();
            }
            WireButtons();

            BattleInfoPanelUI.SetVisible(false);   // 보상/모집 화면 동안 정보 패널 숨김

            GoldManager.EnsureInstance().AddGold(goldPerReward);
            RefreshGold();

            _upgradeUsed = false;
            _pickedCharacter = null;
            if (upgradeButton != null) upgradeButton.interactable = true;
            if (upgradeButtonLabel != null) upgradeButtonLabel.text = "캐릭터 업그레이드";
            ShowUpgradePanel(false);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            BattleInfoPanelUI.SetVisible(true);    // 전투 복귀 시 정보 패널 복원
        }

        private void RefreshGold()
        {
            if (goldText != null)
                goldText.text = $"골드  {GoldManager.Instance?.Gold ?? 0}   (+{goldPerReward})";
        }

        /// <summary>씬 배선/폴백 생성 어느 쪽이든 리스너는 코드에서 1회만 연결.</summary>
        private void WireButtons()
        {
            if (_listenersWired) return;
            if (upgradeButton == null && nextButton == null && cancelUpgradeButton == null) return;

            upgradeButton?.onClick.AddListener(OnUpgradeClicked);
            nextButton?.onClick.AddListener(OnNextClicked);
            cancelUpgradeButton?.onClick.AddListener(() => ShowUpgradePanel(false));
            _listenersWired = true;
        }

        // ─────────────────────────────────────────────
        // 업그레이드 흐름
        // ─────────────────────────────────────────────
        private void OnUpgradeClicked()
        {
            // 1단계: 캐릭터 선택
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

            // 2단계: 모디파이어 선택
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

            _upgradeUsed = true;
            if (upgradeButton != null) upgradeButton.interactable = false;
            if (upgradeButtonLabel != null) upgradeButtonLabel.text = "강화 완료 ✓";
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

        /// <summary>선택 버튼 1개 추가. 프리팹이 있으면 그걸로, 없으면 기본 스타일로 생성.</summary>
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
            mainPanel = CreatePanel(canvasGO.transform, "MainPanel", new Vector2(880, 640), new Vector2(0, 0));

            var eyebrow = CreateText(mainPanel.transform, "웨이브 클리어", 22, FontStyles.Normal);
            eyebrow.color = InkMuted;
            Place(eyebrow, new Vector2(0, 262), new Vector2(700, 34));

            var title = CreateText(mainPanel.transform, "보상 획득", 60, FontStyles.Bold);
            Place(title, new Vector2(0, 210), new Vector2(700, 76));

            // 시그니처: 주사위 5눈 핍 디바이더
            BuildPipDivider(mainPanel.transform, new Vector2(0, 156));

            // 골드 — 코인 필(pill) 토큰
            var pill = CreateChild(mainPanel.transform, "GoldPill");
            Place(pill, new Vector2(0, 96), new Vector2(430, 58));
            var pillImg = pill.gameObject.AddComponent<Image>();
            pillImg.sprite = UiRoundedSprite.Get(28);
            pillImg.type = Image.Type.Sliced;
            pillImg.color = CardWell;
            goldText = CreateText(pill, "골드 0", 34, FontStyles.Bold);
            goldText.color = Gold;
            Stretch(goldText);

            // 포션 슬롯 (예정 placeholder)
            BuildPotionSlots(mainPanel.transform, new Vector2(0, -4));

            // 업그레이드 버튼 (보조 — Slate)
            upgradeButton = CreateButton(mainPanel.transform, "캐릭터 업그레이드", new Vector2(0, -136),
                new Vector2(440, 88), out upgradeButtonLabel, primary: false);

            // 다음 버튼 (주 행동 — Gold)
            nextButton = CreateButton(mainPanel.transform, "다음", new Vector2(0, -244),
                new Vector2(300, 84), out _, primary: true);

            // 업그레이드 패널 (모달, 초기 숨김)
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

        private void BuildPotionSlots(Transform parent, Vector2 anchoredPos)
        {
            var rowGO = CreateChild(parent, "PotionRow");
            Place(rowGO, anchoredPos, new Vector2(700, 120));
            var hlg = rowGO.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 18; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            for (int i = 0; i < Mathf.Max(0, potionSlotCount); i++)
            {
                // 파인 슬롯(인셋) — 아직 채워지지 않은 트레이 느낌
                var slot = CreateChild(rowGO, $"PotionSlot{i}");
                var le = slot.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 104; le.preferredHeight = 104;
                var img = slot.gameObject.AddComponent<Image>();
                img.sprite = UiRoundedSprite.Get(16);
                img.type = Image.Type.Sliced;
                img.color = CardWell;

                var t = CreateText(slot, "포션\n(예정)", 20, FontStyles.Normal);
                Stretch(t);
                t.color = InkMuted;
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
