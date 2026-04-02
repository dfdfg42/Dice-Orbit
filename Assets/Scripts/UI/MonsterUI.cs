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
        [SerializeField] private RectTransform intentBubbleRoot;
        [SerializeField] private Image intentBubbleBg;
        [SerializeField] private Image intentIcon;
        [SerializeField] private TextMeshProUGUI intentText;

        [Header("Armor UI")]
        [SerializeField] private RectTransform armorRoot;
        [SerializeField] private Image armorIcon;
        [SerializeField] private TextMeshProUGUI armorText;
        [SerializeField] private Sprite armorIconSprite;
        [SerializeField] private bool hideArmorWhenZero = true;
        
        [Header("Intent Colors")]
        [SerializeField] private Color attackColor = Color.red;
        [SerializeField] private Color defendColor = Color.blue;
        [SerializeField] private Color buffColor = Color.yellow;
        [SerializeField] private Color specialColor = Color.magenta;
        [SerializeField] private Color neutralBubbleColor = new Color(1f, 1f, 1f, 0.92f);
        
        [Header("Settings")]
        [SerializeField] private Vector3 uiOffset = new Vector3(0, 2f, 0);
        [SerializeField] private bool autoFindMonster = true;
        [SerializeField] private bool hideBubbleWhenNoIntent = true;
        [SerializeField] private bool tintBubbleByIntent = false;
        [SerializeField] private bool showIntentText = true;
        
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
            AutoResolveIntentRefs();
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
            
            // 크기 조정
            RectTransform rectTransform = worldCanvas.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = new Vector2(3, 1f);
                rectTransform.localScale = Vector3.one * 0.1f;
            }

            ConfigureNonBlockingRaycasts();
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
        
        private void AutoResolveIntentRefs()
        {
            if (intentBubbleRoot == null && intentIcon != null)
            {
                intentBubbleRoot = intentIcon.rectTransform;
            }
            
            if (intentBubbleBg == null && intentBubbleRoot != null)
            {
                intentBubbleBg = intentBubbleRoot.GetComponent<Image>();
            }
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
        
        /// <summary>
        /// 공격 의도 UI 업데이트
        /// </summary>
        private void UpdateIntent()
        {
            if (monster == null)
            {
                SetIntentVisible(false);
                return;
            }

            AttackIntent intent = monster.CurrentIntent;
            if (intent == null)
            {
                if (hideBubbleWhenNoIntent)
                {
                    SetIntentVisible(false);
                }
                return;
            }

            SetIntentVisible(true);

            IntentType type = intent.Type;
            int visualKey = BuildIntentVisualKey(intent);
            bool changed = visualKey != lastIntentVisualKey;
            lastIntentVisualKey = visualKey;

            if (intentBubbleBg != null)
            {
                intentBubbleBg.color = tintBubbleByIntent ? GetIntentColor(type) : neutralBubbleColor;
            }

            if (intentIcon != null)
            {
                if (intent.Icon != null)
                {
                    intentIcon.sprite = intent.Icon;
                    intentIcon.color = Color.white;
                    intentIcon.enabled = true;
                }
                else
                {
                    // 기본 스프라이트를 유지하고 색상만 의도 타입에 맞춤
                    intentIcon.color = GetIntentColor(type);
                    intentIcon.enabled = true;
                }
            }

            if (intentText != null)
            {
                if (showIntentText)
                {
                    intentText.text = GetIntentLabel(type, intent);
                    intentText.gameObject.SetActive(true);
                }
                else
                {
                    intentText.gameObject.SetActive(false);
                }
            }

            if (animateOnIntentChange && changed)
            {
                PlayIntentPop();
            }
        }
        
        private void SetIntentVisible(bool visible)
        {
            if (intentBubbleRoot != null)
            {
                intentBubbleRoot.gameObject.SetActive(visible);
            }
            else
            {
                if (intentIcon != null) intentIcon.gameObject.SetActive(visible);
                if (intentText != null) intentText.gameObject.SetActive(visible && showIntentText);
            }
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
        
        private string GetIntentLabel(IntentType type, AttackIntent intent)
        {
            switch (type)
            {
                case IntentType.Attack:
                    return "공격";
                case IntentType.Multi:
                    return intent.Targets != null && intent.Targets.Count > 1 ? $"연타 x{intent.Targets.Count}" : "연타";
                case IntentType.Defend:
                    return "방어";
                case IntentType.Buff:
                    return "강화";
                case IntentType.Special:
                    return "특수";
                default:
                    return "행동";
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
        /// 의도 타입별 색상
        /// </summary>
        private Color GetIntentColor(IntentType type)
        {
            switch (type)
            {
                case IntentType.Attack:
                case IntentType.Multi:
                    return attackColor;
                case IntentType.Defend:
                    return defendColor;
                case IntentType.Buff:
                    return buffColor;
                case IntentType.Special:
                    return specialColor;
                default:
                    return Color.white;
            }
        }
        
        /// <summary>
        /// 몬스터 참조 설정
        /// </summary>
        public void SetMonster(Core.Monster newMonster)
        {
            monster = newMonster;
            UpdateUI();
        }
    }
}
