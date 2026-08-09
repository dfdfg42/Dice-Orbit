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
    public class DiceElement : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("References")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI valueText;
        
        [Header("Visual Settings")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        [SerializeField] private Color usedColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        [SerializeField] private Color reservedColor = new Color(0.8f, 0.3f, 0.3f, 0.8f);

        // Data
        private DiceData diceData;
        private bool isSelected;
        private DiceUI parentDiceUI;

        // 3D 주사위 (Dice3DService에서 빌림 — 실패 시 null = 기존 2D 숫자 표시)
        private Dice3DView view3d;
        private RawImage visual3d;

        // Properties
        public DiceData Data => diceData;
        public Dice3DView View3D => view3d;

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
            TryAttach3D();
            UpdateVisual();
        }

        // 3D 뷰를 빌려 RawImage로 표시. 서비스가 실패하면 조용히 2D(숫자 텍스트) 유지.
        private void TryAttach3D()
        {
            if (view3d != null || diceData == null) return;
            view3d = Dice3DService.Acquire(diceData);
            if (view3d == null) return;

            if (visual3d == null)
            {
                var go = new GameObject("Visual3D", typeof(RectTransform), typeof(RawImage));
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                visual3d = go.GetComponent<RawImage>();
                visual3d.raycastTarget = false;
            }
            visual3d.texture = view3d.Texture;
            visual3d.gameObject.SetActive(true);

            if (valueText != null) valueText.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            Dice3DService.Release(view3d);
            view3d = null;
        }

        /// <summary>
        /// 비주얼 업데이트
        /// </summary>
        public void UpdateVisual()
        {
            if (diceData == null) return;

            // 값 표시
            if (valueText != null)
            {
                valueText.text = diceData.Value.ToString();
            }

            // 배경 색상 및 상호작용 (3D 렌더도 같은 상태색으로 틴트 — 선택/잠금/사용됨이 동일하게 보이게)
            var stateColor = ResolveCurrentColor();
            if (backgroundImage != null)
            {
                backgroundImage.color = stateColor;
            }
            if (visual3d != null)
            {
                visual3d.color = stateColor;
            }

            var button = GetComponent<Button>();
            if (button != null)
            {
                button.interactable = diceData.State == DiceState.Available;
            }
        }

        // 캐릭터 선택 중, 이 값으로는 그 캐릭터의 어떤 스킬 조건도 못 맞출 때 살짝 붉게
        private bool skillUnusableHint;

        /// <summary>스킬 사용 불가 힌트 표시 토글 (CharacterActionUI 열림/닫힘에 맞춰 DiceUI가 호출).</summary>
        public void SetSkillUnusableHint(bool unusable)
        {
            if (skillUnusableHint == unusable) return;
            skillUnusableHint = unusable;
            UpdateVisual();
        }

        private Color ResolveCurrentColor()
        {
            if (diceData == null) return normalColor;

            switch (diceData.State)
            {
                case DiceState.Available:
                    if (isSelected) return selectedColor;
                    // 사용 불가 힌트: 평상색에서 붉은 쪽으로 약간만 (선택/예약/사용 상태색이 항상 우선)
                    return skillUnusableHint ? Color.Lerp(normalColor, new Color(1f, 0.25f, 0.25f, normalColor.a), 0.4f) : normalColor;
                case DiceState.Reserved:
                    return reservedColor;
                case DiceState.Used:
                    return usedColor;
                default:
                    return normalColor;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (diceData == null || diceData.State != DiceState.Available) return;

            parentDiceUI = parentDiceUI != null ? parentDiceUI : GetComponentInParent<DiceUI>();
            parentDiceUI?.HandleDiceElementClicked(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (diceData?.Source == null) return;
            DiceHoverTooltipUI.EnsureInstance()?.Show(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            DiceHoverTooltipUI.Instance?.Hide();
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected && diceData != null && diceData.State == DiceState.Available;
            UpdateVisual();
        }

        /// <summary>
        /// 연출용 임시 숫자 표시 (데이터 값은 변경하지 않음)
        /// </summary>
        public void SetDisplayValue(int value)
        {
            if (view3d != null) return;   // 3D 모드는 숫자 셔플 대신 큐브가 직접 구른다
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
            if (diceData == null) return;
            if (view3d != null)
            {
                view3d.SnapToValue(diceData.Value);
                return;
            }
            if (valueText == null) return;
            valueText.text = diceData.Value.ToString();
        }

        public bool IsSelected => isSelected;
    }

    /// <summary>
    /// 드롭 존 인터페이스
    /// </summary>
    public interface IDropZone
    {
        object GetDropTarget();
    }
}
