using UnityEngine;
using UnityEngine.EventSystems;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 보상 화면 요소(전리품 칩·파티 초상) 위에 커서를 올리면 HoverTooltipUI로 설명을 띄우는 경량 프록시.
    /// Description이 비어 있으면 아무것도 띄우지 않는다.
    /// </summary>
    public class RewardHoverInfo : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string Description;

        private bool _showing;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(Description)) return;
            HoverTooltipUI.EnsureInstance();
            HoverTooltipUI.Instance?.ShowPinned(Description);
            _showing = true;
        }

        public void OnPointerExit(PointerEventData eventData) => HideIfShowing();

        // 호버 중 요소가 파괴·비활성되면(박자 전환) Exit가 오지 않으므로 직접 닫는다.
        private void OnDisable() => HideIfShowing();

        private void HideIfShowing()
        {
            if (!_showing) return;
            _showing = false;
            HoverTooltipUI.Instance?.HidePinned();
        }
    }
}
