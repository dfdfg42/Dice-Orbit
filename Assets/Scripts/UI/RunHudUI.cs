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
    /// 슬롯은 씬에서 배치·배선한다.
    /// </summary>
    public class RunHudUI : MonoBehaviour
    {
        public static RunHudUI Instance { get; private set; }

        [Header("슬롯 (씬에서 배치·배선)")]
        [SerializeField] private GameObject rootCanvas;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private RectTransform relicRow;   // 화면 하단 중앙 — 유물 아이콘 한 줄
        [SerializeField] private RectTransform potionRow;

        [Header("배치")]
        [SerializeField] private float chipSize = 52f;
        [Tooltip("맵(900)/상점(1450)/보상(1500) 위, 환경설정(2000) 아래 — 상단 바는 모든 게임 화면 위에")]
        [SerializeField] private int hudSortingOrder = 1600;

        [Header("칩 프리팹 (선택 — 비우면 기본 칩 생성)")]
        [Tooltip("루트에 Image(배경), 자식 이름 Icon(Image)/Label(TMP)을 자동 탐색해 채운다")]
        [SerializeField] private GameObject chipPrefab;

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
            // 씬에 저장된 캔버스에도 최신 정렬값 강제 (구버전 100으로 생성된 레이아웃 교정)
            var canvas = rootCanvas != null ? rootCanvas.GetComponent<Canvas>() : null;
            if (canvas != null) canvas.sortingOrder = hudSortingOrder;

            // (설정 버튼 제거 — 환경설정은 ESC 토글로만 진입)

            // 데이터 변경 구독
            GoldManager.EnsureInstance().OnGoldChanged += _ => RefreshGold();
            ArtifactManager.EnsureInstance().OnArtifactsChanged += RebuildRelics;
            PotionManager.EnsureInstance().OnChanged += RebuildPotions;
            if (GameFlowManager.Instance != null)
                GameFlowManager.Instance.OnStateChanged += OnGameStateChanged;

            OnGameStateChanged(GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentState : GameState.MainMenu);
            RefreshAll();
        }

        private void Update()
        {
            // ESC = 환경설정 토글 (HUD가 떠 있는 게임플레이 상태에서만)
            // 단, 포션 조준 중이면 ESC는 조준 취소가 우선 (PotionTargetSelector가 처리)
            if (rootCanvas != null && rootCanvas.activeSelf &&
                (PotionTargetSelector.Instance == null || !PotionTargetSelector.Instance.IsSelecting) &&
                UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SettingsUI.EnsureInstance();
                SettingsUI.Instance?.Toggle();
            }
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
            if (goldText == null) return;
            EnsureCoinIcon();
            goldText.text = _coinIcon != null
                ? $"{GoldManager.Instance?.Gold ?? 0}"
                : $"<color=#{ColorUtility.ToHtmlStringRGB(Gold)}>●</color> {GoldManager.Instance?.Gold ?? 0}";   // 스프라이트 폴백
        }

        private Image _coinIcon;

        /// <summary>골드 텍스트 왼쪽에 코인 스프라이트 아이콘 부착 (구 노란 ● 대체). 텍스트는 마진으로 밀어낸다.</summary>
        private void EnsureCoinIcon()
        {
            if (_coinIcon != null) return;

            var coin = Resources.Load<Sprite>("UI/코인");
            if (coin == null) return;   // 스프라이트 없으면 ● 폴백 유지

            float size = goldText.fontSize + 6f;   // 글자보다 살짝 크게

            var go = new GameObject("CoinIcon", typeof(RectTransform));
            go.transform.SetParent(goldText.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(size, size);

            _coinIcon = go.AddComponent<Image>();
            _coinIcon.sprite = coin;
            _coinIcon.preserveAspect = true;
            _coinIcon.raycastTarget = false;

            goldText.margin = new Vector4(size + 4f, 0f, 0f, 0f);   // 아이콘 폭 + 간격만큼 텍스트 들여쓰기
        }

        private void RebuildRelics()
        {
            if (relicRow == null) return;
            Clear(relicRow);

            var owned = ArtifactManager.Instance?.Artifacts;
            if (owned == null) return;

            foreach (var artifact in owned)
            {
                if (artifact == null) continue;
                var data = artifact.data;
                string title = data != null ? data.artifactName : artifact.GetType().Name;
                var chip = CreateChip(relicRow, data != null ? data.artifactIcon : null, title, RelicTint, bare: true);
                AddHoverTooltip(chip, $"<b>[{title}]</b>\n{(data != null ? data.artifactTooltip : "")}");
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
                var chipGo = chip;
                string usage = potion.CombatOnly ? "\n<size=80%>전투 중에만 사용 가능</size>" : "";
                AddHoverTooltip(chip, $"<b>[{potion.PotionName}]</b>\n{potion.Description}{usage}\n<size=80%><color=#8B8B8B>좌클릭 사용 · 우클릭 버리기</color></size>");

                var clickable = chip.AddComponent<PotionSlotWidget>();
                clickable.Setup(
                    onUse: () =>
                    {
                        var pm = PotionManager.Instance;
                        if (pm == null || index >= pm.Slots.Count) return;

                        // 대상 지정형은 조준 모드로 (칩 위치에서 캐릭터로 아크)
                        if (PotionManager.RequiresTarget(pm.Slots[index]))
                        {
                            PotionTargetSelector.EnsureInstance();
                            PotionTargetSelector.Instance?.Begin(index, chipGo.transform.position);
                        }
                        else
                        {
                            pm.TryUse(index);
                        }
                    },
                    onDiscard: () => PotionManager.Instance?.Discard(index));
            }
        }

        // ── 위젯 생성 ─────────────────────────────────────────

        /// <summary>아이콘 칩. 아이콘 없으면 이름 첫 글자, empty면 빈 홈, bare면 배경판 없이 아이콘만.
        /// 프리팹이 있으면 그걸로 찍는다 (bare는 기본 생성 경로에만 적용).</summary>
        private GameObject CreateChip(RectTransform parent, Sprite icon, string fallbackName, Color tint, bool empty = false, bool bare = false)
        {
            if (chipPrefab != null)
                return CreateChipFromPrefab(parent, icon, fallbackName, tint, empty);

            var go = new GameObject("Chip", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = chipSize; le.preferredHeight = chipSize;

            bool bareIcon = bare && icon != null;   // 아이콘 없는 유물은 글자 칩으로 폴백 (배경 유지)

            var bg = go.AddComponent<Image>();
            bg.sprite = UiRoundedSprite.Get(12);
            bg.type = Image.Type.Sliced;
            // bare 칩의 배경은 투명 — 시각적으론 아이콘만, 호버 히트 영역으로만 기능
            bg.color = bareIcon ? Color.clear : empty ? CardWell : Color.Lerp(CardWell, tint, 0.35f);

            if (empty) return go;

            if (icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform));
                iconGo.transform.SetParent(go.transform, false);
                var iconRect = (RectTransform)iconGo.transform;
                iconRect.anchorMin = Vector2.zero; iconRect.anchorMax = Vector2.one;
                float inset = bareIcon ? 0f : 6f;
                iconRect.offsetMin = new Vector2(inset, inset); iconRect.offsetMax = new Vector2(-inset, -inset);
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

        /// <summary>프리팹 기반 칩: 루트 Image = 배경, 자식 Icon(Image)/Label(TMP) 이름 탐색.</summary>
        private GameObject CreateChipFromPrefab(RectTransform parent, Sprite icon, string fallbackName, Color tint, bool empty)
        {
            var go = Instantiate(chipPrefab, parent);
            go.name = "Chip";

            var bg = go.GetComponent<Image>();
            if (bg != null)
                bg.color = empty ? CardWell : Color.Lerp(CardWell, tint, 0.35f);

            var iconImg = go.transform.Find("Icon")?.GetComponent<Image>();
            var label = go.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();

            if (empty)
            {
                if (iconImg != null) iconImg.gameObject.SetActive(false);
                if (label != null) label.gameObject.SetActive(false);
                return go;
            }

            bool hasIcon = icon != null;
            if (iconImg != null)
            {
                iconImg.gameObject.SetActive(hasIcon);
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
            }
            if (label != null)
            {
                label.gameObject.SetActive(!hasIcon);
                label.text = string.IsNullOrEmpty(fallbackName) ? "?" : fallbackName.Substring(0, 1);
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
