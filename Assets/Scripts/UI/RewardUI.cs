using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Core;
using DiceOrbit.Data.Modifiers;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 웨이브 클리어 보상 화면 (코드 자동 생성 — 임시).
    /// 골드 표시 + 포션 슬롯(예정) + [캐릭터 업그레이드] 버튼.
    /// 업그레이드: 캐릭터 1명 선택 → 모디파이어 선택 → 그 캐릭터에 장착.
    /// </summary>
    public class RewardUI : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private int goldPerReward = 50;
        [SerializeField] private int modifierChoiceCount = 3;
        [SerializeField] private int potionSlotCount = 3;

        private bool _built;
        private TMP_FontAsset _font;

        private TextMeshProUGUI _goldText;
        private GameObject _mainPanel;
        private GameObject _upgradePanel;
        private TextMeshProUGUI _upgradeHeader;
        private Transform _choiceRow;
        private Button _upgradeButton;
        private TextMeshProUGUI _upgradeButtonLabel;

        private Character _pickedCharacter;
        private bool _upgradeUsed;

        private static readonly Color PanelColor = new Color(0.12f, 0.13f, 0.18f, 0.98f);
        private static readonly Color ButtonColor = new Color(0.25f, 0.45f, 0.8f, 1f);
        private static readonly Color SlotColor = new Color(0.2f, 0.22f, 0.28f, 1f);
        private static readonly Color GoldColor = new Color(1f, 0.85f, 0.35f, 1f);

        public void Show()
        {
            gameObject.SetActive(true);
            EnsureBuilt();

            GoldManager.EnsureInstance().AddGold(goldPerReward);
            RefreshGold();

            _upgradeUsed = false;
            _pickedCharacter = null;
            if (_upgradeButton != null) _upgradeButton.interactable = true;
            if (_upgradeButtonLabel != null) _upgradeButtonLabel.text = "캐릭터 업그레이드";
            ShowUpgradePanel(false);
        }

        public void Hide() => gameObject.SetActive(false);

        private void RefreshGold()
        {
            if (_goldText != null)
                _goldText.text = $"골드  {GoldManager.Instance?.Gold ?? 0}   (+{goldPerReward})";
        }

        // ─────────────────────────────────────────────
        // 업그레이드 흐름
        // ─────────────────────────────────────────────
        private void OnUpgradeClicked()
        {
            // 1단계: 캐릭터 선택
            _pickedCharacter = null;
            _upgradeHeader.text = "강화할 캐릭터 선택";
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
            _upgradeHeader.text = $"[{name}] 모디파이어 선택";
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
            if (_upgradeButton != null) _upgradeButton.interactable = false;
            if (_upgradeButtonLabel != null) _upgradeButtonLabel.text = "강화 완료 ✓";
            ShowUpgradePanel(false);
        }

        private void OnNextClicked()
        {
            GameFlowManager.Instance?.OnRewardComplete();
        }

        private void ShowUpgradePanel(bool visible)
        {
            if (_upgradePanel != null) _upgradePanel.SetActive(visible);
        }

        private void ClearRow()
        {
            if (_choiceRow == null) return;
            for (int i = _choiceRow.childCount - 1; i >= 0; i--)
                Destroy(_choiceRow.GetChild(i).gameObject);
        }

        // ─────────────────────────────────────────────
        // UI 자동 생성
        // ─────────────────────────────────────────────
        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

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
            _mainPanel = CreatePanel(canvasGO.transform, "MainPanel", new Vector2(900, 620), new Vector2(0, 0));
            var title = CreateText(_mainPanel.transform, "보상 획득", 64, FontStyles.Bold);
            Place(title, new Vector2(0, 250), new Vector2(800, 90));

            _goldText = CreateText(_mainPanel.transform, "골드 0", 44, FontStyles.Bold);
            _goldText.color = GoldColor;
            Place(_goldText, new Vector2(0, 160), new Vector2(800, 70));

            // 포션 슬롯 (예정 placeholder)
            BuildPotionSlots(_mainPanel.transform, new Vector2(0, 50));

            // 업그레이드 버튼
            _upgradeButton = CreateButton(_mainPanel.transform, "캐릭터 업그레이드", new Vector2(0, -90),
                new Vector2(440, 100), OnUpgradeClicked, out _upgradeButtonLabel);

            // 다음 버튼
            CreateButton(_mainPanel.transform, "다음", new Vector2(0, -230),
                new Vector2(300, 90), OnNextClicked, out _);

            // 업그레이드 패널 (모달, 초기 숨김)
            _upgradePanel = CreatePanel(canvasGO.transform, "UpgradePanel", new Vector2(1100, 520), new Vector2(0, 0));
            _upgradeHeader = CreateText(_upgradePanel.transform, "강화할 캐릭터 선택", 44, FontStyles.Bold);
            Place(_upgradeHeader, new Vector2(0, 200), new Vector2(1000, 80));

            var rowGO = CreateChild(_upgradePanel.transform, "ChoiceRow");
            Place(rowGO, new Vector2(0, -10), new Vector2(1020, 240));
            var hlg = rowGO.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            _choiceRow = rowGO;

            CreateButton(_upgradePanel.transform, "취소", new Vector2(0, -200),
                new Vector2(240, 80), () => ShowUpgradePanel(false), out _);

            _upgradePanel.SetActive(false);
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

        private void AddChoiceButton(string label, System.Action onClick)
        {
            var go = CreateChild(_choiceRow, "Choice");
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

        // ── 빌드 헬퍼 ──────────────────────────────────────
        private GameObject CreatePanel(Transform parent, string name, Vector2 size, Vector2 pos)
        {
            var go = CreateChild(parent, name);
            Place(go, pos, size);
            go.gameObject.AddComponent<Image>().color = PanelColor;
            return go.gameObject;
        }

        private Button CreateButton(Transform parent, string label, Vector2 pos, Vector2 size,
            System.Action onClick, out TextMeshProUGUI labelText)
        {
            var go = CreateChild(parent, "Button_" + label);
            Place(go, pos, size);
            var img = go.gameObject.AddComponent<Image>();
            img.color = ButtonColor;
            var btn = go.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());

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
