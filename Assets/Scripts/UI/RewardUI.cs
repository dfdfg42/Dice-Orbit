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

        private static readonly Color PanelColor = new Color(0.12f, 0.13f, 0.18f, 0.98f);
        private static readonly Color ButtonColor = new Color(0.25f, 0.45f, 0.8f, 1f);
        private static readonly Color SlotColor = new Color(0.2f, 0.22f, 0.28f, 1f);
        private static readonly Color GoldColor = new Color(1f, 0.85f, 0.35f, 1f);

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

            var go = CreateChild(choiceRow, "Choice");
            var le = go.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 240; le.preferredHeight = 200;
            var img = go.gameObject.AddComponent<Image>();
            img.color = ButtonColor;
            var btn = go.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());

            var txt = CreateText(go, label, 26, FontStyles.Bold);
            Stretch(txt);
            txt.margin = new Vector4(10, 10, 10, 10);
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

            // 어두운 배경
            var dim = CreateChild(canvasGO.transform, "Dim");
            Stretch(dim);
            dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            // 메인 패널
            mainPanel = CreatePanel(canvasGO.transform, "MainPanel", new Vector2(900, 620), new Vector2(0, 0));
            var title = CreateText(mainPanel.transform, "보상 획득", 64, FontStyles.Bold);
            Place(title, new Vector2(0, 250), new Vector2(800, 90));

            goldText = CreateText(mainPanel.transform, "골드 0", 44, FontStyles.Bold);
            goldText.color = GoldColor;
            Place(goldText, new Vector2(0, 160), new Vector2(800, 70));

            // 포션 슬롯 (예정 placeholder)
            BuildPotionSlots(mainPanel.transform, new Vector2(0, 50));

            // 업그레이드 버튼
            upgradeButton = CreateButton(mainPanel.transform, "캐릭터 업그레이드", new Vector2(0, -90),
                new Vector2(440, 100), out upgradeButtonLabel);

            // 다음 버튼
            nextButton = CreateButton(mainPanel.transform, "다음", new Vector2(0, -230),
                new Vector2(300, 90), out _);

            // 업그레이드 패널 (모달, 초기 숨김)
            upgradePanel = CreatePanel(canvasGO.transform, "UpgradePanel", new Vector2(1100, 520), new Vector2(0, 0));
            upgradeHeader = CreateText(upgradePanel.transform, "강화할 캐릭터 선택", 44, FontStyles.Bold);
            Place(upgradeHeader, new Vector2(0, 200), new Vector2(1000, 80));

            var rowGO = CreateChild(upgradePanel.transform, "ChoiceRow");
            Place(rowGO, new Vector2(0, -10), new Vector2(1020, 240));
            var hlg = rowGO.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            choiceRow = rowGO;

            cancelUpgradeButton = CreateButton(upgradePanel.transform, "취소", new Vector2(0, -200),
                new Vector2(240, 80), out _);

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

        private void BuildPotionSlots(Transform parent, Vector2 anchoredPos)
        {
            var rowGO = CreateChild(parent, "PotionRow");
            Place(rowGO, anchoredPos, new Vector2(700, 120));
            var hlg = rowGO.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            for (int i = 0; i < Mathf.Max(0, potionSlotCount); i++)
            {
                var slot = CreateChild(rowGO, $"PotionSlot{i}");
                var le = slot.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 110; le.preferredHeight = 110;
                slot.gameObject.AddComponent<Image>().color = SlotColor;
                var t = CreateText(slot, "포션\n(예정)", 22, FontStyles.Normal);
                Stretch(t);
                t.color = new Color(1, 1, 1, 0.4f);
            }
        }

        // ── 빌드 헬퍼 ──────────────────────────────────────
        private GameObject CreatePanel(Transform parent, string name, Vector2 size, Vector2 pos)
        {
            var go = CreateChild(parent, name);
            Place(go, pos, size);
            go.gameObject.AddComponent<Image>().color = PanelColor;
            return go.gameObject;
        }

        private Button CreateButton(Transform parent, string label, Vector2 pos, Vector2 size,
            out TextMeshProUGUI labelText)
        {
            var go = CreateChild(parent, "Button_" + label);
            Place(go, pos, size);
            var img = go.gameObject.AddComponent<Image>();
            img.color = ButtonColor;
            var btn = go.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            labelText = CreateText(go, label, 34, FontStyles.Bold);
            Stretch(labelText);
            return btn;
        }

        private TextMeshProUGUI CreateText(Transform parent, string text, float size, FontStyles style)
        {
            var go = CreateChild(parent, "Text");
            var tmp = go.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
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
