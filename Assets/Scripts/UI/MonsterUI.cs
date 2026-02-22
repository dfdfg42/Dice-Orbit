using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DiceOrbit.Data;

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
        [SerializeField] private Image intentIcon;
        [SerializeField] private TextMeshProUGUI intentText;
        
        [Header("Intent Colors")]
        [SerializeField] private Color attackColor = Color.red;
        [SerializeField] private Color defendColor = Color.blue;
        [SerializeField] private Color buffColor = Color.yellow;
        [SerializeField] private Color specialColor = Color.magenta;
        
        [Header("Settings")]
        [SerializeField] private Vector3 uiOffset = new Vector3(0, 2f, 0);
        [SerializeField] private bool autoFindMonster = true;
        
        private Camera mainCamera;
        
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
            if (monster == null || monster.CurrentIntent == null) return;

            AttackIntent intent = monster.CurrentIntent; // AttackIntent 타입으로 변경

            // AttackIntent가 이미 IntentType을 가지고 있음
            IntentType type = intent.Type;

            // 의도 아이콘 색상
            if (intentIcon != null)
            {
                intentIcon.color = GetIntentColor(type);
            }
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
