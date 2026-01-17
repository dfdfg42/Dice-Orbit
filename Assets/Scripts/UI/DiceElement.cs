using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DiceOrbit.Data;
using TMPro;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 개별 주사위 UI 요소 (드래그 가능)
    /// </summary>
    public class DiceElement : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("References")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI valueText;
        
        [Header("Visual Settings")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color usedColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        [SerializeField] private Color dragColor = new Color(1f, 1f, 1f, 0.7f);
        
        // Data
        private DiceData diceData;
        
        // Drag state
        private Canvas canvas;
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Vector2 originalPosition;
        private Transform originalParent;
        
        // Properties
        public DiceData Data => diceData;
        
        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            
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
            
            // Canvas 찾기
            canvas = GetComponentInParent<Canvas>();
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
                backgroundImage.color = diceData.IsUsed ? usedColor : normalColor;
            }
            
            // 사용된 주사위는 드래그 불가
            canvasGroup.blocksRaycasts = !diceData.IsUsed;
        }
        
        /// <summary>
        /// 드래그 시작
        /// </summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (diceData == null || diceData.IsUsed) return;
            
            // 원래 위치 저장
            originalPosition = rectTransform.anchoredPosition;
            originalParent = transform.parent;
            
            // Canvas 최상단으로 이동 (다른 UI 위에 표시)
            transform.SetParent(canvas.transform);
            transform.SetAsLastSibling();
            
            // 반투명하게
            canvasGroup.alpha = 0.7f;
            canvasGroup.blocksRaycasts = false;
            
            if (backgroundImage != null)
            {
                backgroundImage.color = dragColor;
            }
        }
        
        /// <summary>
        /// 드래그 중
        /// </summary>
        public void OnDrag(PointerEventData eventData)
        {
            if (diceData == null || diceData.IsUsed) return;
            
            // 마우스 위치로 이동
            rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
        }
        
        /// <summary>
        /// 드래그 종료
        /// </summary>
        public void OnEndDrag(PointerEventData eventData)
        {
            if (diceData == null || diceData.IsUsed) return;
            
            // Raycast로 드롭 대상 확인
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            
            bool dropped = false;
            
            foreach (var result in results)
            {
                // ActionPanel에 드롭했는지 확인
                var actionPanel = result.gameObject.GetComponentInParent<ActionPanel>();
                if (actionPanel != null)
                {
                    // ActionPanel에 주사위 전달
                    actionPanel.OnDiceDropped(diceData);
                    dropped = true;
                    
                    // 드롭 성공 - 원위치 복귀 (사용 후 UI 업데이트)
                    ReturnToOriginalPosition();
                    break;
                }
            }
            
            // 드롭 실패 시 원위치 복귀
            if (!dropped)
            {
                ReturnToOriginalPosition();
            }
        }
        
        /// <summary>
        /// 원래 위치로 복귀
        /// </summary>
        public void ReturnToOriginalPosition()
        {
            transform.SetParent(originalParent);
            rectTransform.anchoredPosition = originalPosition;
            
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            
            if (backgroundImage != null)
            {
                backgroundImage.color = diceData.IsUsed ? usedColor : normalColor;
            }
        }
        
        /// <summary>
        /// 사용됨 표시
        /// </summary>
        public void MarkAsUsed()
        {
            if (diceData != null)
            {
                UpdateVisual();
            }
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
