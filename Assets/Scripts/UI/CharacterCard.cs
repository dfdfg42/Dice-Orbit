using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 캐릭터 선택 카드
    /// </summary>
    public class CharacterCard : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private Button selectButton;
        
        private Core.CharacterPreset character;
        private System.Action<Core.CharacterPreset> onSelected;
        
        private void Awake()
        {
            if (selectButton != null)
            {
                selectButton.onClick.AddListener(OnSelectClicked);
            }
        }
        
        /// <summary>
        /// 카드 설정
        /// </summary>
        public void Setup(Core.CharacterPreset preset, System.Action<Core.CharacterPreset> callback)
        {
            character = preset;
            onSelected = callback;
            
            // UI 업데이트
            if (portraitImage != null && preset.Portrait != null)
            {
                portraitImage.sprite = preset.Portrait;
            }
            
            if (nameText != null)
            {
                nameText.text = preset.CharacterName;
            }
            
            if (descriptionText != null)
            {
                descriptionText.text = preset.Description;
            }
            
            if (statsText != null)
            {
                statsText.text = $"HP: {preset.MaxHP}\nATK: {preset.Attack}\nDEF: {preset.Defense}";
            }
        }
        
        /// <summary>
        /// 선택 버튼 클릭
        /// </summary>
        private void OnSelectClicked()
        {
            Debug.Log($"[CharacterCard] Select button clicked for {character?.CharacterName}");
            onSelected?.Invoke(character);
        }
    }
}
