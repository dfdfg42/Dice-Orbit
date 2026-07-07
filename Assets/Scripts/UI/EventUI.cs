using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Core;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 이벤트 노드 화면 (GameState.Event) — 주사위 도박 (스펙 §1: 주사위 정체성을 맵에서 살리는 자리).
    ///
    /// 이벤트 내용은 Inspector의 gambleEvents 목록에서 편집 (주사위 수 / 목표 합 / 보상 골드 / 실패 피해).
    /// 진입 시 랜덤 1개 제시 → [도전한다] 주사위 연출 후 성공/실패 정산, [지나간다] 무사 통과.
    ///
    /// ── 에디터 소유 레이아웃 ─────────────────────────────────────
    /// 컴포넌트 우클릭 → [기본 레이아웃 생성] → 씬에서 자유롭게 스타일링.
    /// 슬롯이 비어 있으면 런타임 폴백 생성.
    /// </summary>
    public class EventUI : MonoBehaviour
    {
        public static EventUI Instance { get; private set; }

        [System.Serializable]
        public class DiceGambleEvent
        {
            public string Title = "운명의 주사위";
            [Range(1, 5)] public int DiceCount = 3;
            public int SuccessThreshold = 11;
            public int RewardGold = 60;
            [Tooltip("실패 시 파티 전원이 입는 피해 (HP 1 미만으로는 안 내려감 — 전투 밖 사망 방지)")]
            public int FailDamage = 5;
        }

        [Header("이벤트 목록 (진입 시 랜덤 1개)")]
        [SerializeField] private List<DiceGambleEvent> gambleEvents = new List<DiceGambleEvent>
        {
            new DiceGambleEvent { Title = "운명의 주사위", DiceCount = 3, SuccessThreshold = 11, RewardGold = 60, FailDamage = 5 },
            new DiceGambleEvent { Title = "악마의 흥정",   DiceCount = 2, SuccessThreshold = 8,  RewardGold = 90, FailDamage = 10 },
        };

        [Header("연출")]
        [SerializeField] private float rollDuration = 0.7f;
        [SerializeField] private float settleInterval = 0.25f;

        [Header("슬롯 (씬에서 배치 — [기본 레이아웃 생성]으로 자동 배선)")]
        [SerializeField] private GameObject rootCanvas;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descText;
        [SerializeField] private RectTransform diceRow;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private Button challengeButton;
        [SerializeField] private Button passButton;

        private DiceGambleEvent _current;
        private bool _resolved;
        private bool _listenersWired;
        private TMP_FontAsset _font;
        private readonly List<TextMeshProUGUI> _diceLabels = new List<TextMeshProUGUI>();

        // ── 보드게임의 밤 팔레트 ──
        private static readonly Color Felt     = new Color(0.043f, 0.051f, 0.078f, 0.85f);
        private static readonly Color Card     = new Color(0.118f, 0.133f, 0.200f);
        private static readonly Color CardEdge = new Color(0.239f, 0.271f, 0.400f);
        private static readonly Color Ink      = new Color(0.910f, 0.894f, 0.847f);
        private static readonly Color Gold     = new Color(0.878f, 0.702f, 0.341f);
        private static readonly Color GoldInk  = new Color(0.140f, 0.110f, 0.055f);
        private static readonly Color Slate    = new Color(0.200f, 0.255f, 0.368f);
        private static readonly Color Danger   = new Color(0.75f, 0.30f, 0.28f);

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
            Instance = FindFirstObjectByType<EventUI>(FindObjectsInactive.Include);
            if (Instance == null)
            {
                var go = new GameObject("EventUI");
                Instance = go.AddComponent<EventUI>();
            }
        }

        // ── 공개 API ──────────────────────────────────────────

        public void Show()
        {
            gameObject.SetActive(true);

            if (rootCanvas == null)
            {
                Debug.LogWarning("[EventUI] 슬롯이 비어 있어 기본 레이아웃을 런타임 생성합니다. " +
                                 "컴포넌트 우클릭 → [기본 레이아웃 생성]으로 씬에 고정하는 것을 권장합니다.");
                BuildDefaultLayout();
            }
            WireButtons();

            rootCanvas.SetActive(true);
            BattleInfoPanelUI.SetVisible(false);

            _resolved = false;
            _current = gambleEvents.Count > 0 ? gambleEvents[Random.Range(0, gambleEvents.Count)] : new DiceGambleEvent();

            if (titleText != null) titleText.text = _current.Title;
            if (descText != null)
                descText.text = $"주사위 {_current.DiceCount}개를 굴려 합이 <color=#{ColorUtility.ToHtmlStringRGB(Gold)}>{_current.SuccessThreshold} 이상</color>이면 " +
                                $"<color=#{ColorUtility.ToHtmlStringRGB(Gold)}>골드 +{_current.RewardGold}</color>.\n" +
                                $"실패하면 파티 전원이 <color=#C05048>{_current.FailDamage} 피해</color>를 입는다.";
            if (resultText != null) resultText.text = "";

            SetupDiceLabels(_current.DiceCount);
            SetButtons(challenge: true, pass: true, passLabel: "지나간다");
        }

        public void Hide()
        {
            if (rootCanvas != null) rootCanvas.SetActive(false);
            BattleInfoPanelUI.SetVisible(true);
        }

        // ── 흐름 ──────────────────────────────────────────────

        private void WireButtons()
        {
            if (_listenersWired) return;
            if (challengeButton == null && passButton == null) return;

            challengeButton?.onClick.AddListener(() => { if (!_resolved) StartCoroutine(RollRoutine()); });
            passButton?.onClick.AddListener(() => GameFlowManager.Instance?.OnEventComplete());
            _listenersWired = true;
        }

        private IEnumerator RollRoutine()
        {
            _resolved = true;
            SetButtons(challenge: false, pass: false, passLabel: "지나간다");

            // 굴림 연출: 숫자 순환 → 하나씩 확정
            var results = new int[_diceLabels.Count];
            float elapsed = 0f;
            while (elapsed < rollDuration)
            {
                foreach (var label in _diceLabels)
                    label.text = Random.Range(1, 7).ToString();
                elapsed += 0.06f;
                yield return new WaitForSeconds(0.06f);
            }

            int sum = 0;
            for (int i = 0; i < _diceLabels.Count; i++)
            {
                results[i] = Random.Range(1, 7);
                sum += results[i];
                _diceLabels[i].text = results[i].ToString();
                _diceLabels[i].color = Gold;
                yield return new WaitForSeconds(settleInterval);
            }

            bool success = sum >= _current.SuccessThreshold;
            if (success)
            {
                GoldManager.EnsureInstance().AddGold(_current.RewardGold);
                if (resultText != null)
                {
                    resultText.color = Gold;
                    resultText.text = $"합 {sum} — 성공!  골드 +{_current.RewardGold}";
                }
            }
            else
            {
                ApplyFailDamage(_current.FailDamage);
                if (resultText != null)
                {
                    resultText.color = Danger;
                    resultText.text = $"합 {sum} — 실패...  파티 전원 {_current.FailDamage} 피해";
                }
            }

            SetButtons(challenge: false, pass: true, passLabel: "확인");
        }

        /// <summary>실패 피해: 전투 밖이므로 HP 1 미만으로 내려가지 않는다 (사망/부활 로직 미개입).</summary>
        private static void ApplyFailDamage(int damage)
        {
            var party = PartyManager.Instance?.Party;
            if (party == null) return;

            foreach (var ch in party)
            {
                if (ch == null || ch.Stats == null || !ch.IsAlive) continue;
                ch.Stats.CurrentHP = Mathf.Max(1, ch.Stats.CurrentHP - damage);
            }
        }

        private void SetButtons(bool challenge, bool pass, string passLabel)
        {
            if (challengeButton != null) challengeButton.gameObject.SetActive(challenge);
            if (passButton != null)
            {
                passButton.gameObject.SetActive(pass);
                var tmp = passButton.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null) tmp.text = passLabel;
            }
        }

        /// <summary>주사위 카드 라벨을 개수에 맞게 재생성.</summary>
        private void SetupDiceLabels(int count)
        {
            if (diceRow == null) return;

            for (int i = diceRow.childCount - 1; i >= 0; i--)
                Destroy(diceRow.GetChild(i).gameObject);
            _diceLabels.Clear();

            for (int i = 0; i < count; i++)
            {
                var die = new GameObject("Die", typeof(RectTransform));
                die.transform.SetParent(diceRow, false);
                var le = die.AddComponent<LayoutElement>();
                le.preferredWidth = 110; le.preferredHeight = 110;

                var img = die.AddComponent<Image>();
                img.sprite = UiRoundedSprite.Get(22);
                img.type = Image.Type.Sliced;
                img.color = new Color(0.082f, 0.094f, 0.153f);
                img.raycastTarget = false;

                var label = CreateText(die, "?", 52, FontStyles.Bold);
                Stretch(label);
                _diceLabels.Add(label);
            }
        }

        // ─────────────────────────────────────────────
        // [에디터] 기본 레이아웃 생성 (런타임 폴백 겸용)
        // ─────────────────────────────────────────────
        [ContextMenu("기본 레이아웃 생성")]
        private void BuildDefaultLayout()
        {
            if (transform.Find("_EventCanvas") != null)
            {
                Debug.LogWarning("[EventUI] _EventCanvas가 이미 있습니다. 다시 생성하려면 기존 것을 삭제하세요.");
                return;
            }

            _font = FindAnyObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include)?.font;

            var canvasGo = new GameObject("_EventCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1450;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();
            rootCanvas = canvasGo;

            // 딤
            var dim = new GameObject("Dim", typeof(RectTransform));
            dim.transform.SetParent(canvasGo.transform, false);
            var dimRect = (RectTransform)dim.transform;
            dimRect.anchorMin = Vector2.zero; dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero; dimRect.offsetMax = Vector2.zero;
            dim.AddComponent<Image>().color = Felt;

            // 패널
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(canvasGo.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(880, 620);
            var edge = panel.AddComponent<Image>();
            edge.sprite = UiRoundedSprite.Get(26);
            edge.type = Image.Type.Sliced;
            edge.color = CardEdge;
            var shadow = panel.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(0f, -10f);

            var bg = new GameObject("BG", typeof(RectTransform));
            bg.transform.SetParent(panel.transform, false);
            var bgRect = (RectTransform)bg.transform;
            bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = new Vector2(3, 3); bgRect.offsetMax = new Vector2(-3, -3);
            var bgImg = bg.AddComponent<Image>();
            bgImg.sprite = UiRoundedSprite.Get(23);
            bgImg.type = Image.Type.Sliced;
            bgImg.color = Card;
            bgImg.raycastTarget = false;

            // 텍스트들
            titleText = CreateText(panel, "이벤트", 42, FontStyles.Bold);
            PlaceTop(titleText, 34, 56);

            descText = CreateText(panel, "", 26, FontStyles.Normal);
            PlaceTop(descText, 110, 110);

            // 주사위 행
            var rowGo = new GameObject("DiceRow", typeof(RectTransform));
            rowGo.transform.SetParent(panel.transform, false);
            diceRow = (RectTransform)rowGo.transform;
            diceRow.anchorMin = diceRow.anchorMax = new Vector2(0.5f, 0.5f);
            diceRow.anchoredPosition = new Vector2(0, 10);
            diceRow.sizeDelta = new Vector2(700, 130);
            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 22; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            resultText = CreateText(panel, "", 30, FontStyles.Bold);
            PlaceAt(resultText, new Vector2(0, -390), new Vector2(760, 50));

            // 버튼
            challengeButton = CreateButton(panel, "ChallengeButton", "도전한다", new Vector2(-170, -510), true);
            passButton = CreateButton(panel, "PassButton", "지나간다", new Vector2(170, -510), false);

            rootCanvas.SetActive(false);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
            Debug.Log("[EventUI] 기본 레이아웃 생성 완료 — 계층을 자유롭게 스타일링한 뒤 씬을 저장하세요.");
        }

        private Button CreateButton(GameObject parent, string name, string label, Vector2 pos, bool primary)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(280, 72);

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

            var txt = CreateText(go, label, 28, FontStyles.Bold);
            txt.color = primary ? GoldInk : Ink;
            Stretch(txt);
            return btn;
        }

        private TextMeshProUGUI CreateText(GameObject parent, string text, float size, FontStyles style)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
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
