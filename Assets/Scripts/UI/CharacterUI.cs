using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DiceOrbit.Data;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 개별 캐릭터 UI (HP 바, 이름)
    /// World Space Canvas로 캐릭터 위에 표시
    /// </summary>
    public class CharacterUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Core.Character character;
        
        [Header("UI Elements")]
        [SerializeField] private Canvas worldCanvas;
        [SerializeField] private Slider hpSlider;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private TextMeshProUGUI levelText;
        
        [Header("Settings")]
        [SerializeField] private Vector3 uiOffset = new Vector3(0, 1.2f, 0);
        [SerializeField] private bool autoFindCharacter = true;
        
        private Camera mainCamera;
        
        private void Awake()
        {
            // 캐릭터 자동 찾기
            if (autoFindCharacter && character == null)
            {
                character = GetComponentInParent<Core.Character>();
                
                if (character == null)
                {
                    Debug.LogWarning("CharacterUI: Character not found! Assign manually or place as child of Character.");
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
            // Canvas 회전은 부모 Character의 Billboard가 처리함
        }
        
        /// <summary>
        /// World Space Canvas 설정
        /// </summary>
        private void SetupCanvas()
        {
            if (worldCanvas == null)
            {
                Debug.LogWarning("CharacterUI: WorldCanvas not assigned!");
                return;
            }
            
            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldCanvas.worldCamera = mainCamera;
            
            // 위치 설정
            worldCanvas.transform.position = transform.position + uiOffset;
            
            // 크기 조정
            RectTransform rectTransform = worldCanvas.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = new Vector2(2, 0.5f);
                rectTransform.localScale = Vector3.one * 0.07f; // 작은 크기로
            }
        }
        
        /// <summary>
        /// UI 업데이트
        /// </summary>
        private void UpdateUI()
        {
            if (character == null) return;
            
            CharacterStats stats = character.Stats;
            
            // HP 슬라이더
            if (hpSlider != null)
            {
                hpSlider.maxValue = stats.MaxHP;
                hpSlider.value = stats.CurrentHP;
            }
            
            // 이름
            if (nameText != null)
            {
                nameText.text = stats.CharacterName;
            }
            
            // HP 텍스트
            if (hpText != null)
            {
                hpText.text = $"{stats.CurrentHP}/{stats.MaxHP}";
            }
            
            // 레벨
            if (levelText != null)
            {
                levelText.text = $"Lv.{stats.Level}";
            }
        }
        
        /// <summary>
        /// 캐릭터 참조 설정
        /// </summary>
        public void SetCharacter(Core.Character newCharacter)
        {
            character = newCharacter;
            UpdateUI();
        }
    }
}
