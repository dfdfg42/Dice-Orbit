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
            [Tooltip("이벤트 전체 배경 (비우면 기본 펠트 배경)")]
            public Sprite Background;
            [TextArea(2, 4)]
            [Tooltip("연출용 상황 설명 (비우면 규칙 요약만 표시)")]
            public string FlavorText = "";
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
        [SerializeField] private Image backgroundImage;             // 전체 배경 (이벤트별 스프라이트)
        [SerializeField] private TextMeshProUGUI titleText;         // 우측 상단 제목
        [SerializeField] private TextMeshProUGUI descText;          // 제목 아래 설명
        [SerializeField] private RectTransform choiceColumn;        // 설명 아래 선택지 세로 스택
        [SerializeField] private RectTransform diceRow;             // 좌중앙 주사위 연출
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

            // 배경: 이벤트별 스프라이트, 없으면 펠트
            if (backgroundImage != null)
            {
                backgroundImage.sprite = _current.Background;
                backgroundImage.color = _current.Background != null ? Color.white : Felt;
            }

            if (titleText != null) titleText.text = _current.Title;
            if (descText != null)
            {
                string rules = $"주사위 {_current.DiceCount}개를 굴려 합이 <color=#{ColorUtility.ToHtmlStringRGB(Gold)}>{_current.SuccessThreshold} 이상</color>이면 " +
                               $"<color=#{ColorUtility.ToHtmlStringRGB(Gold)}>골드 +{_current.RewardGold}</color>.\n" +
                               $"실패하면 파티 전원이 <color=#C05048>{_current.FailDamage} 피해</color>를 입는다.";
                descText.text = string.IsNullOrWhiteSpace(_current.FlavorText) ? rules : $"{_current.FlavorText}\n\n{rules}";
            }
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

            // 전체 배경 (이벤트별 스프라이트 — Show에서 교체, 없으면 펠트색)
            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgRect = (RectTransform)bgGo.transform;
            bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;
            backgroundImage = bgGo.AddComponent<Image>();
            backgroundImage.color = Felt;
            backgroundImage.preserveAspect = false;

            // 우측 텍스트 칼럼 (반투명 패널 — 배경 아트 위에서 글이 읽히게)
            var colGo = new GameObject("RightColumn", typeof(RectTransform));
            colGo.transform.SetParent(canvasGo.transform, false);
            var colRect = (RectTransform)colGo.transform;
            colRect.anchorMin = new Vector2(0.60f, 0.08f);
            colRect.anchorMax = new Vector2(0.97f, 0.92f);
            colRect.offsetMin = Vector2.zero; colRect.offsetMax = Vector2.zero;
            var colImg = colGo.AddComponent<Image>();
            colImg.sprite = UiRoundedSprite.Get(22);
            colImg.type = Image.Type.Sliced;
            colImg.color = new Color(Card.r, Card.g, Card.b, 0.88f);
            var colShadow = colGo.AddComponent<Shadow>();
            colShadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            colShadow.effectDistance = new Vector2(0f, -8f);

            // 제목 (칼럼 상단)
            titleText = CreateText(colGo, "이벤트", 40, FontStyles.Bold);
            var titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f); titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -30f);
            titleRect.sizeDelta = new Vector2(-60f, 54f);
            titleText.alignment = TextAlignmentOptions.TopLeft;

            // 설명 (제목 아래)
            descText = CreateText(colGo, "", 24, FontStyles.Normal);
            var descRect = descText.rectTransform;
            descRect.anchorMin = new Vector2(0f, 0.42f); descRect.anchorMax = new Vector2(1f, 1f);
            descRect.offsetMin = new Vector2(30f, 0f);
            descRect.offsetMax = new Vector2(-30f, -100f);
            descText.alignment = TextAlignmentOptions.TopLeft;

            // 선택지 세로 스택 (설명 아래)
            var choiceGo = new GameObject("ChoiceColumn", typeof(RectTransform));
            choiceGo.transform.SetParent(colGo.transform, false);
            choiceColumn = (RectTransform)choiceGo.transform;
            choiceColumn.anchorMin = new Vector2(0f, 0f);
            choiceColumn.anchorMax = new Vector2(1f, 0.42f);
            choiceColumn.offsetMin = new Vector2(30f, 26f);
            choiceColumn.offsetMax = new Vector2(-30f, -8f);
            var vlg = choiceGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 14f;
            vlg.childAlignment = TextAnchor.LowerCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            challengeButton = CreateChoiceBar("도전한다", true);
            passButton = CreateChoiceBar("지나간다", false);

            // 좌중앙: 주사위 연출 + 결과
            var rowGo = new GameObject("DiceRow", typeof(RectTransform));
            rowGo.transform.SetParent(canvasGo.transform, false);
            diceRow = (RectTransform)rowGo.transform;
            diceRow.anchorMin = diceRow.anchorMax = new Vector2(0.30f, 0.52f);
            diceRow.anchoredPosition = Vector2.zero;
            diceRow.sizeDelta = new Vector2(700, 130);
            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 22; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            resultText = CreateText(canvasGo, "", 32, FontStyles.Bold);
            var resultRect = resultText.rectTransform;
            resultRect.anchorMin = resultRect.anchorMax = new Vector2(0.30f, 0.36f);
            resultRect.anchoredPosition = Vector2.zero;
            resultRect.sizeDelta = new Vector2(760, 50);

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

        /// <summary>선택지 바 (StS식 — 칼럼 폭 전체, 세로 스택).</summary>
        private Button CreateChoiceBar(string label, bool primary)
        {
            var go = new GameObject($"Choice_{label}", typeof(RectTransform));
            go.transform.SetParent(choiceColumn, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 66f;
            le.flexibleWidth = 1f;

            var img = go.AddComponent<Image>();
            img.sprite = UiRoundedSprite.Get(16);
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

            var txt = CreateText(go, label, 27, FontStyles.Bold);
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
