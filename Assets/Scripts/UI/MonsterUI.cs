using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DiceOrbit.Data;
using System.Collections;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 몬스터 UI (HP 바, 이름, 공격 의도)
    /// World Space Canvas로 몬스터 위에 표시
    /// </summary>
    public class MonsterUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Core.Monster monster;
        
        [Header("UI Elements")]
        [SerializeField] private Canvas worldCanvas;
        [SerializeField] private Slider hpSlider;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI hpText;
        [Header("의도 말풍선 (프리팹 MonsterCanvas/IntentBubble — UiSkinImage IntentBubble 9-slice, 아이콘만·라벨 없음, 2026-09-25)")]
        [SerializeField] private RectTransform intentBubbleRoot;   // 말풍선 루트 (스프라이트는 UiSkinImage가 꽂는다)
        [SerializeField] private Image intentIcon;                 // 스킬 아이콘 — AttackIntent.Icon

        [Header("Armor UI")]
        [SerializeField] private RectTransform armorRoot;
        [SerializeField] private Image armorIcon;
        [SerializeField] private TextMeshProUGUI armorText;
        [SerializeField] private Sprite armorIconSprite;
        [SerializeField] private bool hideArmorWhenZero = true;
        
        [Header("Settings")]
        [SerializeField] private Vector3 uiOffset = new Vector3(0, 2f, 0);
        [SerializeField] private bool autoFindMonster = true;
        [Tooltip("체력바 캔버스 정렬 순서. 스프라이트보다 높아야 항상 앞에 보인다.")]
        [SerializeField] private int canvasSortingOrder = 500;
        [Tooltip("체력바 캔버스 크기(월드 스케일). 키우려면 값을 올린다.")]
        [SerializeField] private float canvasScale = 0.1f;
        [SerializeField] private bool hideBubbleWhenNoIntent = true;
        
        [Header("Animation")]
        [SerializeField] private bool animateOnIntentChange = true;
        [SerializeField] private float popScale = 1.12f;
        [SerializeField] private float popDuration = 0.12f;
        
        private Camera mainCamera;
        private int lastIntentVisualKey = int.MinValue;
        private Coroutine popRoutine;
        
        private void Awake()
        {
            // 몬스터 자동 찾기
            if (autoFindMonster && monster == null)
            {
                monster = GetComponentInParent<Core.Monster>();
                
                if (monster == null)
                {
                    Debug.LogWarning("MonsterUI: Monster not found!");
                }
            }
            
            mainCamera = Camera.main;
            RequireIntentRefs();
            AutoResolveArmorRefs();
            EnsureArmorUIExists();
        }
        
        private void Start()
        {
            SetupCanvas();
            UpdateUI();
        }
        
        private void Update()
        {
            UpdateUI();
            // Canvas 회전은 부모 Monster의 Billboard가 처리함
        }
        
        /// <summary>
        /// World Space Canvas 설정
        /// </summary>
        private void SetupCanvas()
        {
            if (worldCanvas == null)
            {
                Debug.LogWarning("MonsterUI: WorldCanvas not assigned!");
                return;
            }
            
            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldCanvas.worldCamera = mainCamera;

            // 항상 스프라이트보다 앞에 표시
            worldCanvas.overrideSorting = true;
            worldCanvas.sortingOrder = canvasSortingOrder;

            // 크기 조정
            RectTransform rectTransform = worldCanvas.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = new Vector2(3, 1f);
                rectTransform.localScale = Vector3.one * canvasScale;
            }

            ConfigureNonBlockingRaycasts();

            // 상태이상 시각화: 체력바 아래 아이콘 줄 + 유닛 위 오버레이 (스펙 2026-07-29)
            // HPBar(중심 x=2.95, 실폭 117) 좌측 끝 x=-56, 바 하단(27.8) 바로 아래 y=26
            StatusIconRow.Attach(worldCanvas, monster, mainCamera, new Vector2(-56f, 26f));
            Visuals.StatusOverlayStack.Attach(monster);
        }

        private void ConfigureNonBlockingRaycasts()
        {
            if (worldCanvas == null) return;

            var raycaster = worldCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.enabled = false;
            }

            var graphics = worldCanvas.GetComponentsInChildren<Graphic>(true);
            foreach (var graphic in graphics)
            {
                if (graphic == null) continue;
                graphic.raycastTarget = false;
            }

            var canvasGroups = worldCanvas.GetComponentsInChildren<CanvasGroup>(true);
            foreach (var group in canvasGroups)
            {
                if (group == null) continue;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
        }
        
        /// <summary>말풍선 슬롯은 프리팹 배선 필수 — 비면 에러로 알리고 말풍선을 끈다 (형제 오브젝트를 뒤져 채우는 폴백 없음).</summary>
        private void RequireIntentRefs()
        {
            if (intentBubbleRoot == null || intentIcon == null)
                Debug.LogError("[MonsterUI] intentBubbleRoot/intentIcon 슬롯이 비어 있습니다 — TestMonster.prefab MonsterCanvas/IntentBubble/Icon을 배선하세요.", this);
        }
        
        /// <summary>
        /// UI 업데이트
        /// </summary>
        private void UpdateUI()
        {
            if (monster == null) return;
            
            MonsterStats stats = monster.Stats;
            
            // HP 슬라이더
            if (hpSlider != null)
            {
                hpSlider.maxValue = stats.MaxHP;
                hpSlider.value = stats.CurrentHP;
            }
            
            // 이름
            if (nameText != null)
            {
                nameText.text = stats.MonsterName;
            }
            
            // HP 텍스트
            if (hpText != null)
            {
                hpText.text = $"{stats.CurrentHP}/{stats.MaxHP}";
            }
            
            // 공격 의도
            UpdateIntent();
            UpdateArmor();
        }
        
        /// <summary>공격 의도 말풍선 갱신 — 아이콘만 (라벨·타입 색 없음). 아이콘 없는 의도는 데이터 누락으로 보고 에러 1회.</summary>
        private void UpdateIntent()
        {
            if (monster == null || intentBubbleRoot == null) { SetIntentVisible(false); return; }

            AttackIntent intent = monster.CurrentIntent;
            if (intent == null)
            {
                if (hideBubbleWhenNoIntent) SetIntentVisible(false);
                return;
            }

            SetIntentVisible(true);

            int visualKey = BuildIntentVisualKey(intent);
            bool changed = visualKey != lastIntentVisualKey;
            lastIntentVisualKey = visualKey;

            if (intentIcon != null)
            {
                if (intent.Icon != null)
                {
                    intentIcon.sprite = intent.Icon;
                    intentIcon.enabled = true;
                }
                else
                {
                    intentIcon.enabled = false;
                    if (changed) Debug.LogError($"[MonsterUI] {monster.name}의 의도 '{intent.Type}'에 아이콘이 없습니다 — 몬스터 스킬 데이터에 Icon을 지정하세요.", monster);
                }
            }

            if (animateOnIntentChange && changed) PlayIntentPop();
        }

        private void SetIntentVisible(bool visible)
        {
            if (intentBubbleRoot != null && intentBubbleRoot.gameObject.activeSelf != visible)
                intentBubbleRoot.gameObject.SetActive(visible);
        }

        private void UpdateArmor()
        {
            if (monster == null) return;
            int armor = Mathf.Max(0, monster.Stats.TempArmor);

            if (armorRoot != null)
            {
                armorRoot.gameObject.SetActive(!hideArmorWhenZero || armor > 0);
            }

            if (armorText != null)
            {
                armorText.text = armor.ToString();
            }
        }

        private void AutoResolveArmorRefs()
        {
            if (worldCanvas == null) return;

            if (armorRoot == null)
            {
                var rects = worldCanvas.GetComponentsInChildren<RectTransform>(true);
                foreach (var rect in rects)
                {
                    if (rect == null) continue;
                    var n = rect.name.ToLowerInvariant();
                    if (n.Contains("armor"))
                    {
                        armorRoot = rect;
                        break;
                    }
                }
            }

            if (armorIcon == null && armorRoot != null)
            {
                armorIcon = armorRoot.GetComponent<Image>();
                if (armorIcon == null)
                {
                    armorIcon = armorRoot.GetComponentInChildren<Image>(true);
                }
            }

            if (armorText == null && armorRoot != null)
            {
                armorText = armorRoot.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        private void EnsureArmorUIExists()
        {
            if (worldCanvas == null || armorRoot != null) return;

            GameObject rootGo = new GameObject("ArmorBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rootGo.transform.SetParent(worldCanvas.transform, false);
            armorRoot = rootGo.GetComponent<RectTransform>();
            armorRoot.anchorMin = new Vector2(0.5f, 0.5f);
            armorRoot.anchorMax = new Vector2(0.5f, 0.5f);
            armorRoot.pivot = new Vector2(0.5f, 0.5f);
            armorRoot.anchoredPosition = new Vector2(120f, 24f);
            armorRoot.sizeDelta = new Vector2(40f, 40f);
            armorRoot.localScale = Vector3.one;

            armorIcon = rootGo.GetComponent<Image>();
            armorIcon.raycastTarget = false;
            armorIcon.color = Color.white;
            if (armorIconSprite != null)
            {
                armorIcon.sprite = armorIconSprite;
                armorIcon.type = Image.Type.Simple;
                armorIcon.preserveAspect = true;
            }
            else
            {
                // 아이콘 스프라이트가 없으면 배지 배경색으로 대체
                armorIcon.sprite = null;
                armorIcon.color = new Color(0.2f, 0.45f, 1f, 0.9f);
            }

            GameObject textGo = new GameObject("ArmorText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(armorRoot, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            armorText = textGo.GetComponent<TextMeshProUGUI>();
            armorText.alignment = TextAlignmentOptions.Center;
            armorText.fontSize = 18f;
            armorText.color = Color.white;
            armorText.raycastTarget = false;
            armorText.text = "0";
        }
        
        private int BuildIntentVisualKey(AttackIntent intent)
        {
            unchecked
            {
                int key = 17;
                key = key * 31 + (int)intent.Type;
                key = key * 31 + (int)intent.TargetType;
                key = key * 31 + intent.AreaRadius;
                key = key * 31 + (intent.Icon != null ? intent.Icon.GetInstanceID() : 0);
                key = key * 31 + (intent.Targets != null ? intent.Targets.Count : 0);
                return key;
            }
        }
        
        private void PlayIntentPop()
        {
            if (intentBubbleRoot == null) return;
            if (popRoutine != null) StopCoroutine(popRoutine);
            popRoutine = StartCoroutine(CoPlayIntentPop());
        }
        
        private IEnumerator CoPlayIntentPop()
        {
            Vector3 baseScale = Vector3.one;
            float half = Mathf.Max(0.01f, popDuration * 0.5f);
            float t = 0f;
            
            while (t < half)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / half);
                intentBubbleRoot.localScale = Vector3.Lerp(baseScale, baseScale * popScale, k);
                yield return null;
            }

            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / half);
                intentBubbleRoot.localScale = Vector3.Lerp(baseScale * popScale, baseScale, k);
                yield return null;
            }
            
            intentBubbleRoot.localScale = baseScale;
            popRoutine = null;
        }
        
        /// <summary>
        /// 몬스터 참조 설정
        /// </summary>
        public void SetMonster(Core.Monster newMonster)
        {
            monster = newMonster;
            UpdateUI();
        }

        // ── 호버 툴팁 연동: 의도 버블 히트테스트용 노출 ──

        /// <summary>의도 버블 RectTransform (호버 히트테스트용). 없으면 null.</summary>
        public RectTransform IntentBubbleRect => intentBubbleRoot;

        /// <summary>의도 버블이 현재 표시 중이고 유효한 의도가 있는지.</summary>
        public bool IsIntentBubbleActive =>
            intentBubbleRoot != null && intentBubbleRoot.gameObject.activeInHierarchy
            && monster != null && monster.CurrentIntent != null;

        /// <summary>의도 버블(월드 캔버스)을 렌더하는 카메라 (RectangleContainsScreenPoint용).</summary>
        public Camera IntentWorldCamera =>
            worldCanvas != null && worldCanvas.worldCamera != null ? worldCanvas.worldCamera : mainCamera;
    }
}
