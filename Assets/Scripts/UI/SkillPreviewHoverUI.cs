using UnityEngine;
using UnityEngine.EventSystems;

namespace DiceOrbit.UI
{
    public class SkillPreviewHoverUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [TextArea]
        [SerializeField] private string previewText;

        public void SetPreview(string text)
        {
            previewText = text;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            HoverTooltipUI.EnsureInstance();
            HoverTooltipUI.Instance?.ShowPinned(previewText);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HoverTooltipUI.Instance?.HidePinned();
        }

        private void OnDisable()
        {
            HoverTooltipUI.Instance?.HidePinned();
        }

        private void OnDestroy()
        {
            HoverTooltipUI.Instance?.HidePinned();
        }
    }
}
