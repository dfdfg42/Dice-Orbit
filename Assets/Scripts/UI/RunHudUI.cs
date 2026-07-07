using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DiceOrbit.Core;
using DiceOrbit.Core.Run;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 런 상단 HUD (StS 스타일): 골드 · 유물 아이콘 행 · 포션 슬롯 3칸.
    /// 전투/맵/상점/이벤트/보상 동안 상시 표시, 메뉴/모집에서 숨김.
    ///
    /// 유물: 호버 → 커서 옆 툴팁으로 이름+설명.
    /// 포션: 좌클릭 = 사용, 우클릭 = 버리기, 호버 = 툴팁.
    ///
    /// ── 에디터 소유 레이아웃 ─────────────────────────────────────
    /// 컴포넌트 우클릭 → [기본 레이아웃 생성] → 씬에서 자유롭게 스타일링.
    /// 슬롯이 비어 있으면 런타임 폴백 생성.
    /// </summary>
    public class RunHudUI : MonoBehaviour
    {
        public static RunHudUI Instance { get; private set; }

        [Header("슬롯 (씬에서 배치 — [기본 레이아웃 생성]으로 자동 배선)")]
        [SerializeField] private GameObject rootCanvas;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private RectTransform relicRow;
        [SerializeField] private RectTransform potionRow;

        [Header("배치")]
        [SerializeField] private float chipSize = 52f;

        // 보드게임의 밤 팔레트
        private static readonly Color BarBg    = new Color(0.043f, 0.051f, 0.078f, 0.82f);
        private static readonly Color CardWell = new Color(0.082f, 0.094f, 0.153f);
        private static readonly Color CardEdge = new Color(0.239f, 0.271f, 0.400f);
        private static readonly Color Ink      = new Color(0.910f, 0.894f, 0.847f);
        private static readonly Color Gold     = new Color(0.878f, 0.702f, 0.341f);
        private static readonly Color RelicTint  = new Color(0.68f, 0.55f, 0.30f);
        private static readonly Color PotionTint = new Color(0.36f, 0.55f, 0.42f);

        private TMP_FontAsset _font;
        private static readonly HashSet<GameState> VisibleStates = new HashSet<GameState>
        {
            GameState.Combat, GameState.Map, GameState.Shop, GameState.Event, GameState.Reward,
        };

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (rootCanvas == null) BuildDefaultLayout();

            // 데이터 변경 구독
            GoldManager.EnsureInstance().OnGoldChanged += _ => RefreshGold();
            RelicManager.EnsureInstance().OnRelicsChanged += RebuildRelics;
            PotionManager.EnsureInstance().OnChanged += RebuildPotions;
            if (GameFlowManager.Instance != null)
                GameFlowManager.Instance.OnStateChanged += OnGameStateChanged;

            OnGameStateChanged(GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentState : GameState.MainMenu);
            RefreshAll();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (GameFlowManager.Instance != null)
                GameFlowManager.Instance.OnStateChanged -= OnGameStateChanged;
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            Instance = FindFirstObjectByType<RunHudUI>(FindObjectsInactive.Include);
            if (Instance == null)
            {
                var go = new GameObject("RunHudUI");
                Instance = go.AddComponent<RunHudUI>();
            }
        }

        private void OnGameStateChanged(GameState state)
        {
            if (rootCanvas != null) rootCanvas.SetActive(VisibleStates.Contains(state));
            if (VisibleStates.Contains(state)) RefreshAll();
        }

        // ── 갱신 ──────────────────────────────────────────────

        private void RefreshAll()
        {
            RefreshGold();
            RebuildRelics();
            RebuildPotions();
        }

        private void RefreshGold()
        {
            if (goldText != null)
                goldText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(Gold)}>●</color> {GoldManager.Instance?.Gold ?? 0}";
        }

        private void RebuildRelics()
        {
            if (relicRow == null) return;
            Clear(relicRow);

            var owned = RelicManager.Instance?.Owned;
            if (owned == null) return;

            foreach (var relic in owned)
            {
                if (relic == null) continue;
                var chip = CreateChip(relicRow, relic.Icon, relic.RelicName, RelicTint);
                AddHoverTooltip(chip, $"<b>[{relic.RelicName}]</b>\n{relic.Description}");
            }
        }

        private void RebuildPotions()
        {
            if (potionRow == null) return;
            Clear(potionRow);

            var pm = PotionManager.Instance;
            int slotCount = pm != null ? pm.SlotCount : 3;

            for (int i = 0; i < slotCount; i++)
            {
                bool filled = pm != null && i < pm.Slots.Count;
                var potion = filled ? pm.Slots[i] : null;

                var chip = CreateChip(potionRow, potion != null ? potion.Icon : null,
                    potion != null ? potion.PotionName : null, PotionTint, empty: !filled);

                if (potion == null) continue;

                int index = i;
                string usage = potion.CombatOnly ? "\n<size=80%>전투 중에만 사용 가능</size>" : "";
                AddHoverTooltip(chip, $"<b>[{potion.PotionName}]</b>\n{potion.Description}{usage}\n<size=80%><color=#8B8B8B>좌클릭 사용 · 우클릭 버리기</color></size>");

                var clickable = chip.AddComponent<PotionSlotWidget>();
                clickable.Setup(
                    onUse: () => PotionManager.Instance?.TryUse(index),
                    onDiscard: () => PotionManager.Instance?.Discard(index));
            }
        }

        // ── 위젯 생성 ─────────────────────────────────────────

        /// <summary>아이콘 칩. 아이콘 없으면 이름 첫 글자, empty면 빈 홈.</summary>
        private GameObject CreateChip(RectTransform parent, Sprite icon, string fallbackName, Color tint, bool empty = false)
        {
            var go = new GameObject("Chip", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = chipSize; le.preferredHeight = chipSize;

            var bg = go.AddComponent<Image>();
            bg.sprite = UiRoundedSprite.Get(12);
            bg.type = Image.Type.Sliced;
            bg.color = empty ? CardWell : Color.Lerp(CardWell, tint, 0.35f);

            if (empty) return go;

            if (icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform));
                iconGo.transform.SetParent(go.transform, false);
                var iconRect = (RectTransform)iconGo.transform;
                iconRect.anchorMin = Vector2.zero; iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = new Vector2(6, 6); iconRect.offsetMax = new Vector2(-6, -6);
                var img = iconGo.AddComponent<Image>();
                img.sprite = icon;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }
            else
            {
                var label = new GameObject("Label", typeof(RectTransform));
                label.transform.SetParent(go.transform, false);
                var tmp = label.AddComponent<TextMeshProUGUI>();
                tmp.text = string.IsNullOrEmpty(fallbackName) ? "?" : fallbackName.Substring(0, 1);
                tmp.fontSize = 24f;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Ink;
                tmp.raycastTarget = false;
                if (_font != null) tmp.font = _font;
                var r = tmp.rectTransform;
                r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
                r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            }
            return go;
        }

        private static void AddHoverTooltip(GameObject go, string text)
        {
            var hover = go.AddComponent<HudTooltipWidget>();
            hover.TooltipText = text;
        }

        private static void Clear(RectTransform row)
        {
            for (int i = row.childCount - 1; i >= 0; i--)
                Destroy(row.GetChild(i).gameObject);
        }

        // ─────────────────────────────────────────────
        // [에디터] 기본 레이아웃 생성 (런타임 폴백 겸용)
        // ─────────────────────────────────────────────
        [ContextMenu("기본 레이아웃 생성")]
        private void BuildDefaultLayout()
        {
            if (transform.Find("_RunHudCanvas") != null)
            {
                Debug.LogWarning("[RunHudUI] _RunHudCanvas가 이미 있습니다. 다시 생성하려면 기존 것을 삭제하세요.");
                return;
            }

            _font = FindAnyObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include)?.font;

            var canvasGo = new GameObject("_RunHudCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;   // 일반 UI 위, 맵(900)/상점(1450) 아래
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();
            rootCanvas = canvasGo;

            // 상단 바 (왼쪽 절반 — 오른쪽은 정보 패널 영역)
            var bar = new GameObject("TopBar", typeof(RectTransform));
            bar.transform.SetParent(canvasGo.transform, false);
            var barRect = (RectTransform)bar.transform;
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(0f, 1f);
            barRect.pivot = new Vector2(0f, 1f);
            barRect.anchoredPosition = new Vector2(12f, -10f);
            barRect.sizeDelta = new Vector2(880f, 66f);
            var barImg = bar.AddComponent<Image>();
            barImg.sprite = UiRoundedSprite.Get(16);
            barImg.type = Image.Type.Sliced;
            barImg.color = BarBg;

            var layout = bar.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 7, 7);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            // 골드
            var goldGo = new GameObject("GoldText", typeof(RectTransform));
            goldGo.transform.SetParent(bar.transform, false);
            goldText = goldGo.AddComponent<TextMeshProUGUI>();
            goldText.fontSize = 28f;
            goldText.fontStyle = FontStyles.Bold;
            goldText.alignment = TextAlignmentOptions.MidlineLeft;
            goldText.color = Ink;
            goldText.raycastTarget = false;
            if (_font != null) goldText.font = _font;
            var goldLe = goldGo.AddComponent<LayoutElement>();
            goldLe.preferredWidth = 120f; goldLe.preferredHeight = chipSize;

            // 유물 행
            relicRow = CreateRow(bar.transform, "RelicRow", 380f);

            // 구분선
            var divider = new GameObject("Divider", typeof(RectTransform));
            divider.transform.SetParent(bar.transform, false);
            var divImg = divider.AddComponent<Image>();
            divImg.color = new Color(1f, 1f, 1f, 0.12f);
            divImg.raycastTarget = false;
            var divLe = divider.AddComponent<LayoutElement>();
            divLe.preferredWidth = 2f; divLe.preferredHeight = chipSize * 0.8f;

            // 포션 행
            potionRow = CreateRow(bar.transform, "PotionRow", 190f);

            rootCanvas.SetActive(false);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
            Debug.Log("[RunHudUI] 기본 레이아웃 생성 완료 — 계층을 자유롭게 스타일링한 뒤 씬을 저장하세요.");
        }

        private RectTransform CreateRow(Transform parent, string name, float width)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = chipSize;
            return rect;
        }
    }

    /// <summary>HUD 칩 호버 툴팁 (커서 옆 경량 툴팁 재사용).</summary>
    public class HudTooltipWidget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string TooltipText;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(TooltipText)) return;
            HoverTooltipUI.EnsureInstance();
            HoverTooltipUI.Instance?.ShowPinned(TooltipText);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HoverTooltipUI.Instance?.HidePinned();
        }

        private void OnDisable()
        {
            HoverTooltipUI.Instance?.HidePinned();
        }
    }

    /// <summary>포션 슬롯 클릭: 좌클릭 사용, 우클릭 버리기.</summary>
    public class PotionSlotWidget : MonoBehaviour, IPointerClickHandler
    {
        private System.Action _onUse;
        private System.Action _onDiscard;

        public void Setup(System.Action onUse, System.Action onDiscard)
        {
            _onUse = onUse;
            _onDiscard = onDiscard;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) _onUse?.Invoke();
            else if (eventData.button == PointerEventData.InputButton.Right) _onDiscard?.Invoke();
        }
    }
}
