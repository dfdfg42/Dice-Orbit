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
