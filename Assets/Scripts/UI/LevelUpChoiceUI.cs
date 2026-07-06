using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Core;
using DiceOrbit.Data.Modifiers;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 레벨업 타일 모디파이어 선택 모달: 밟은 캐릭터에게 모디파이어 3택1.
    /// (구 스킬/패시브 자동 레벨업을 대체 — 성장은 전부 모디파이어로, 기획 REV05)
    ///
    /// 보상 화면과 같은 "보드게임의 밤" 팔레트/카드 스타일.
    /// 에디터 소유 레이아웃: [기본 레이아웃 생성] 후 씬에서 스타일링.
    /// 슬롯이 비어 있으면 런타임 폴백 생성.
    /// </summary>
    public class LevelUpChoiceUI : MonoBehaviour
    {
        public static LevelUpChoiceUI Instance { get; private set; }

        [Header("Tuning")]
        [SerializeField] private int choiceCount = 3;

        [Header("슬롯 (씬에서 배치 — [기본 레이아웃 생성]으로 자동 배선)")]
        [SerializeField] private GameObject rootCanvas;
        [SerializeField] private TextMeshProUGUI header;
        [SerializeField] private RectTransform choiceRow;

        [Header("선택 카드 프리팹 (선택 — 비우면 기본 카드 생성)")]
        [SerializeField] private Button choiceButtonPrefab;

        private System.Action _onDone;
        private TMP_FontAsset _font;

        // 보상 화면 팔레트 계승
        private static readonly Color Felt     = new Color(0.043f, 0.051f, 0.078f, 0.85f);
        private static readonly Color Card     = new Color(0.118f, 0.133f, 0.200f);
        private static readonly Color CardEdge = new Color(0.239f, 0.271f, 0.400f);
        private static readonly Color Ink      = new Color(0.910f, 0.894f, 0.847f);
        private static readonly Color Gold     = new Color(0.878f, 0.702f, 0.341f);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            Instance = FindFirstObjectByType<LevelUpChoiceUI>(FindObjectsInactive.Include);
            if (Instance == null)
            {
                var go = new GameObject("LevelUpChoiceUI");
                Instance = go.AddComponent<LevelUpChoiceUI>();
            }
        }

        /// <summary>캐릭터에게 모디파이어 3택1 제시. 선택(또는 후보 없음) 시 onDone 호출.</summary>
        public void Show(Character character, System.Action onDone)
        {
            if (character == null) { onDone?.Invoke(); return; }

            if (rootCanvas == null) BuildDefaultLayout();
            _onDone = onDone;

            gameObject.SetActive(true);
            rootCanvas.SetActive(true);
            BattleInfoPanelUI.SetVisible(false);

            string name = character.Stats != null ? character.Stats.CharacterName : character.name;
            if (header != null)
                header.text = $"레벨업!  <color=#E0B357>{name}</color>\n<size=55%>모디파이어를 하나 선택하세요</size>";

            ClearRow();
            var choices = ModifierRegistry.GetRandomChoicesFor(character, choiceCount);
            if (choices == null || choices.Count == 0)
            {
                // 제시할 게 없으면 그냥 통과
                Close();
                return;
            }

            foreach (var mod in choices)
            {
                var captured = mod;
                AddChoiceButton($"{mod.ModifierName}\n<size=60%>{mod.Description}</size>", () =>
                {
                    character.Stats?.Modifiers?.Add(captured);
                    Debug.Log($"[LevelUp] {name} ← '{captured.ModifierName}' 장착");
                    Close();
                });
            }
        }

        private void Close()
        {
            if (rootCanvas != null) rootCanvas.SetActive(false);
            BattleInfoPanelUI.SetVisible(true);

            var done = _onDone;
            _onDone = null;
            done?.Invoke();
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
            le.preferredWidth = 250; le.preferredHeight = 220;

            var img = go.AddComponent<Image>();
            img.sprite = UiRoundedSprite.Get(18);
            img.type = Image.Type.Sliced;
            img.color = Color.white;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(0f, -6f);

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

            var txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            var txt = txtGo.AddComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.fontSize = 25;
            txt.fontStyle = FontStyles.Bold;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = Ink;
            txt.raycastTarget = false;
            txt.margin = new Vector4(14, 14, 14, 14);
            if (_font != null) txt.font = _font;
            var r = txt.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }

        // ─────────────────────────────────────────────
        // [에디터] 기본 레이아웃 생성 (런타임 폴백 겸용)
        // ─────────────────────────────────────────────
        [ContextMenu("기본 레이아웃 생성")]
        private void BuildDefaultLayout()
        {
            if (transform.Find("_LevelUpCanvas") != null)
            {
                Debug.LogWarning("[LevelUpChoiceUI] _LevelUpCanvas가 이미 있습니다. 다시 생성하려면 기존 것을 삭제하세요.");
                return;
            }

            _font = FindAnyObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include)?.font;

            var canvasGO = new GameObject("_LevelUpCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1400;   // 보상(1500) 아래, 일반 UI 위
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();
            rootCanvas = canvasGO;

            var dim = new GameObject("Dim", typeof(RectTransform));
            dim.transform.SetParent(canvasGO.transform, false);
            var dimRect = (RectTransform)dim.transform;
            dimRect.anchorMin = Vector2.zero; dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero; dimRect.offsetMax = Vector2.zero;
            dim.AddComponent<Image>().color = Felt;

            // 카드 패널
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(canvasGO.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1000, 480);
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

            // 헤더
            var headerGo = new GameObject("Header", typeof(RectTransform));
            headerGo.transform.SetParent(panel.transform, false);
            var headerRect = (RectTransform)headerGo.transform;
            headerRect.anchorMin = headerRect.anchorMax = new Vector2(0.5f, 0.5f);
            headerRect.anchoredPosition = new Vector2(0, 168);
            headerRect.sizeDelta = new Vector2(900, 110);
            header = headerGo.AddComponent<TextMeshProUGUI>();
            header.fontSize = 40;
            header.fontStyle = FontStyles.Bold;
            header.alignment = TextAlignmentOptions.Center;
            header.color = Ink;
            header.raycastTarget = false;
            if (_font != null) header.font = _font;

            // 선택 카드 행
            var rowGo = new GameObject("ChoiceRow", typeof(RectTransform));
            rowGo.transform.SetParent(panel.transform, false);
            choiceRow = (RectTransform)rowGo.transform;
            choiceRow.anchorMin = choiceRow.anchorMax = new Vector2(0.5f, 0.5f);
            choiceRow.anchoredPosition = new Vector2(0, -50);
            choiceRow.sizeDelta = new Vector2(920, 250);
            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 24; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            rootCanvas.SetActive(false);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
            Debug.Log("[LevelUpChoiceUI] 기본 레이아웃 생성 완료 — 계층을 자유롭게 스타일링한 뒤 씬을 저장하세요.");
        }
    }
}
