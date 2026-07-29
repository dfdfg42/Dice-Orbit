using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Core;
using DiceOrbit.Core.Run;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 이벤트 노드 화면 (GameState.Event) — 범용 다중 선택지 이벤트 (StS 레이아웃).
    /// 전체 배경 아트 + 우측 칼럼(제목/설명/선택지 세로 스택), 주사위 판정 연출은 좌중앙.
    ///
    /// 이벤트는 EventDefinition 에셋 (Create > DiceOrbit > Event Definition) —
    /// 선택지마다 즉시/주사위 판정 + 성공/실패 결과 묶음. eventPool이 비면 기본 3종 런타임 생성.
    ///
    /// 레이아웃 슬롯은 씬에서 배치한다.
    /// </summary>
    public class EventUI : MonoBehaviour
    {
        public static EventUI Instance { get; private set; }

        [Header("이벤트 풀 (진입 시 랜덤 1개 — 비우면 기본 3종 런타임 생성)")]
        [SerializeField] private List<EventDefinition> eventPool = new List<EventDefinition>();

        [Header("연출")]
        [SerializeField] private float rollDuration = 0.7f;
        [SerializeField] private float settleInterval = 0.25f;

        [Header("슬롯 (씬에서 배치)")]
        [SerializeField] private GameObject rootCanvas;
        [SerializeField] private Image backgroundImage;             // 전체 배경 (이벤트별 스프라이트)
        [SerializeField] private TextMeshProUGUI titleText;         // 우측 상단 제목
        [SerializeField] private TextMeshProUGUI descText;          // 제목 아래 설명
        [SerializeField] private RectTransform choiceColumn;        // 설명 아래 선택지 세로 스택
        [SerializeField] private RectTransform diceRow;             // 좌중앙 주사위 연출
        [SerializeField] private TextMeshProUGUI resultText;

        private EventDefinition _current;
        private bool _resolving;
        private TMP_FontAsset _font;
        private readonly List<TextMeshProUGUI> _diceLabels = new List<TextMeshProUGUI>();

        // ── 보드게임의 밤 팔레트 ──
        private static readonly Color Felt     = new Color(0.043f, 0.051f, 0.078f, 0.85f);
        private static readonly Color Card     = new Color(0.118f, 0.133f, 0.200f);
        private static readonly Color Ink      = new Color(0.910f, 0.894f, 0.847f);
        private static readonly Color Gold     = new Color(0.878f, 0.702f, 0.341f);
        private static readonly Color GoldInk  = new Color(0.140f, 0.110f, 0.055f);
        private static readonly Color Slate    = new Color(0.200f, 0.255f, 0.368f);
        private static readonly Color Danger   = new Color(0.75f, 0.30f, 0.28f);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            EnsureDefaultPool();
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

            rootCanvas.SetActive(true);
            BattleInfoPanelUI.SetVisible(false);

            _resolving = false;
            var pool = eventPool.Where(e => e != null && e.Choices.Count > 0).ToList();
            _current = pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
            if (_current == null)
            {
                Debug.LogWarning("[EventUI] 이벤트 풀이 비어 있어 통과 처리합니다.");
                GameFlowManager.Instance?.OnEventComplete();
                return;
            }

            if (backgroundImage != null)
            {
                backgroundImage.sprite = _current.Background;
                backgroundImage.color = _current.Background != null ? Color.white : Felt;
            }
            if (titleText != null) titleText.text = _current.Title;
            if (descText != null) descText.text = _current.FlavorText;
            if (resultText != null) resultText.text = "";

            ClearDice();
            ShowChoices();
        }

        public void Hide()
        {
            if (rootCanvas != null) rootCanvas.SetActive(false);
            BattleInfoPanelUI.SetVisible(true);
        }

        // ── 선택지 ────────────────────────────────────────────

        private void ShowChoices()
        {
            ClearChoices();
            foreach (var choice in _current.Choices)
            {
                var captured = choice;
                bool isGamble = choice.Resolution == EventResolution.DiceCheck;
                CreateChoiceBar(BuildChoiceLabel(choice), primary: isGamble, () => OnChoicePicked(captured));
            }
        }

        /// <summary>선택지 문구: 판정 정보/결과 미리보기를 작은 글씨로 병기.</summary>
        private string BuildChoiceLabel(EventChoice choice)
        {
            if (choice.Resolution == EventResolution.DiceCheck)
                return $"{choice.Label}  <size=65%>[주사위 {choice.DiceCount}개 · 합 {choice.SuccessThreshold}+]</size>";

            string preview = EventOutcomes.Preview(choice.SuccessOutcomes);
            return string.IsNullOrEmpty(preview) ? choice.Label : $"{choice.Label}  <size=65%>[{preview}]</size>";
        }

        private void OnChoicePicked(EventChoice choice)
        {
            if (_resolving) return;
            _resolving = true;
            ClearChoices();

            if (choice.Resolution == EventResolution.DiceCheck)
                StartCoroutine(RollRoutine(choice));
            else
                Resolve(choice, success: true, sum: -1);
        }

        private IEnumerator RollRoutine(EventChoice choice)
        {
            SetupDiceLabels(choice.DiceCount);

            float elapsed = 0f;
            while (elapsed < rollDuration)
            {
                foreach (var label in _diceLabels)
                    label.text = Random.Range(1, 7).ToString();
                elapsed += 0.06f;
                yield return new WaitForSeconds(0.06f);
            }

            int sum = 0;
            foreach (var label in _diceLabels)
            {
                int value = Random.Range(1, 7);
                sum += value;
                label.text = value.ToString();
                label.color = Gold;
                yield return new WaitForSeconds(settleInterval);
            }

            Resolve(choice, sum >= choice.SuccessThreshold, sum);
        }

        private void Resolve(EventChoice choice, bool success, int sum)
        {
            var outcomes = success ? choice.SuccessOutcomes : choice.FailOutcomes;
            string summary = EventOutcomes.Apply(outcomes);
            string flavor = success ? choice.SuccessText : choice.FailText;

            if (resultText != null)
            {
                var sb = new System.Text.StringBuilder();
                if (sum >= 0) sb.Append(success ? $"합 {sum} — 성공!  " : $"합 {sum} — 실패...  ");
                if (!string.IsNullOrWhiteSpace(flavor)) sb.Append(flavor).Append("  ");
                sb.Append(summary);
                resultText.color = success ? Gold : Danger;
                resultText.text = sb.ToString();
            }

            // [확인] 하나만 남긴다
            ClearChoices();
            CreateChoiceBar("확인", primary: true, () => GameFlowManager.Instance?.OnEventComplete());
        }

        private void ClearChoices()
        {
            if (choiceColumn == null) return;
            for (int i = choiceColumn.childCount - 1; i >= 0; i--)
                Destroy(choiceColumn.GetChild(i).gameObject);
        }

        // ── 주사위 연출 ───────────────────────────────────────

        private void ClearDice()
        {
            if (diceRow == null) return;
            for (int i = diceRow.childCount - 1; i >= 0; i--)
                Destroy(diceRow.GetChild(i).gameObject);
            _diceLabels.Clear();
        }

        private void SetupDiceLabels(int count)
        {
            ClearDice();
            if (diceRow == null) return;

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

        // ── 기본 이벤트 (에셋 미지정 폴백) ─────────────────────

        private void EnsureDefaultPool()
        {
            if (eventPool.Count > 0) return;

            eventPool.Add(CreateGamble("운명의 주사위",
                "낡은 탁자 위에 주사위 세 개가 놓여 있다. 던져볼 텐가?",
                3, 11, gold: 60, damage: 5));
            eventPool.Add(CreateGamble("악마의 흥정",
                "그림자가 속삭인다. \"둘이면 충분하지. 대가는... 실패했을 때 치르면 돼.\"",
                2, 8, gold: 90, damage: 10));
            eventPool.Add(CreateWreck());
            Debug.Log("[EventUI] 이벤트 풀이 비어 있어 기본 3종을 런타임 생성했습니다 (에셋으로 교체 권장).");
        }

        private static EventDefinition CreateGamble(string title, string flavor, int dice, int threshold, int gold, int damage)
        {
            var def = ScriptableObject.CreateInstance<EventDefinition>();
            def.name = title;
            def.Title = title;
            def.FlavorText = flavor;
            def.Choices.Add(new EventChoice
            {
                Label = "도전한다",
                Resolution = EventResolution.DiceCheck,
                DiceCount = dice,
                SuccessThreshold = threshold,
                SuccessOutcomes = { new GainGold { amount = gold } },
                FailOutcomes = { new DamageParty { amount = damage } },
            });
            def.Choices.Add(new EventChoice { Label = "지나간다" });
            return def;
        }

        private static EventDefinition CreateWreck()
        {
            var def = ScriptableObject.CreateInstance<EventDefinition>();
            def.name = "부서진 마차";
            def.Title = "부서진 마차";
            def.FlavorText = "길가에 마차가 부서져 있다. 잔해 사이로 뭔가 반짝인다... 안쪽에서 신음 소리도 들리는 것 같다.";
            def.Choices.Add(new EventChoice
            {
                Label = "잔해를 뒤진다",
                SuccessOutcomes = { new GainGold { amount = 30 } },
                SuccessText = "동전 주머니를 찾았다.",
            });
            def.Choices.Add(new EventChoice
            {
                Label = "생존자를 찾는다",
                Resolution = EventResolution.DiceCheck,
                DiceCount = 2,
                SuccessThreshold = 7,
                SuccessOutcomes = { new GainRandomRelic() },
                SuccessText = "구조된 상인이 보답으로 가보를 건넨다.",
                FailOutcomes = { new DamageParty { amount = 8 } },
                FailText = "잔해가 무너져 내렸다!",
            });
            def.Choices.Add(new EventChoice { Label = "지나친다" });
            return def;
        }

        /// <summary>선택지 바 (StS식 — 칼럼 폭 전체, 세로 스택).</summary>
        private Button CreateChoiceBar(string label, bool primary, System.Action onClick)
        {
            var go = new GameObject("Choice", typeof(RectTransform));
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
            btn.onClick.AddListener(() => onClick());

            var txt = CreateText(go, label, 26, FontStyles.Bold);
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
    }
}
