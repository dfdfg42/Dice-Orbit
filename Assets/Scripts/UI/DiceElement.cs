using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DiceOrbit.Data;
using TMPro;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 개별 주사위 UI 요소 (클릭 선택)
    /// </summary>
    public class DiceElement : MonoBehaviour, IPointerClickHandler
    {
        [Header("References")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI valueText;
        
        [Header("Visual Settings")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        [SerializeField] private Color usedColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        
        // Data
        private DiceData diceData;
        private bool isSelected;
        private DiceUI parentDiceUI;
        
        // Properties
        public DiceData Data => diceData;
        
        private void Awake()
        {
            // backgroundImage 자동 찾기
            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
            }
            
            // valueText 자동 찾기
            if (valueText == null)
            {
                valueText = GetComponentInChildren<TextMeshProUGUI>();
            }

            parentDiceUI = GetComponentInParent<DiceUI>();
        }
        
        /// <summary>
        /// 주사위 데이터 설정
        /// </summary>
        public void SetDiceData(DiceData data)
        {
            diceData = data;
            UpdateVisual();
        }
        
        /// <summary>
        /// 비주얼 업데이트
        /// </summary>
        private void UpdateVisual()
        {
            if (diceData == null) return;
            
            // 값 표시
            if (valueText != null)
            {
                valueText.text = diceData.Value.ToString();
                Debug.Log($"Dice UI value set to: {diceData.Value}");
            }
            else
            {
                Debug.LogWarning("ValueText is null! Cannot display dice value.");
            }
            
            // 배경 색상
            if (backgroundImage != null)
            {
                backgroundImage.color = ResolveCurrentColor();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (diceData == null || diceData.IsUsed) return;

            parentDiceUI = parentDiceUI != null ? parentDiceUI : GetComponentInParent<DiceUI>();
            parentDiceUI?.HandleDiceElementClicked(this);
        }
        
        public void SetSelected(bool selected)
        {
            isSelected = selected && diceData != null && !diceData.IsUsed;
            UpdateVisual();
        }

        /// <summary>
        /// 연출용 임시 숫자 표시 (데이터 값은 변경하지 않음)
        /// </summary>
        public void SetDisplayValue(int value)
        {
            if (valueText != null)
            {
                valueText.text = value.ToString();
            }
        }

        /// <summary>
        /// 표시 숫자를 실제 주사위 데이터 값으로 동기화
        /// </summary>
        public void RefreshDisplayFromData()
        {
            if (diceData == null || valueText == null) return;
            valueText.text = diceData.Value.ToString();
        }

        /// <summary>
        /// 기존 호출 호환: 선택 해제/원상 복귀 용도
        /// </summary>
        public void ReturnToOriginalPosition()
        {
            SetSelected(false);
        }

        public bool IsSelected => isSelected;
        
        /// <summary>
        /// 사용됨 표시
        /// </summary>
        public void MarkAsUsed()
        {
            if (diceData != null)
            {
                isSelected = false;
                UpdateVisual();
            }
        }

        private Color ResolveCurrentColor()
        {
            if (diceData == null) return normalColor;
            if (diceData.IsUsed) return usedColor;
            return isSelected ? selectedColor : normalColor;
        }
    }
    
    /// <summary>
    /// 드롭 존 인터페이스
    /// </summary>
    public interface IDropZone
    {
        object GetDropTarget();
    }
}
