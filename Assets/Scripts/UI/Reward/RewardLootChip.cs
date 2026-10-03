using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 전리품 칩 뷰 (골드·유물·포션, 그리고 "버릴 포션 고르기" 줄의 소지 포션) — 씬 템플릿에 붙어 복제된다.
    /// 그리기만 한다: 수령·교환 규칙은 RewardUI.
    /// </summary>
    public class RewardLootChip : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Button button;          // 눌러야 하는 칩(버릴 포션)에서만 켜진다
        [SerializeField] private RewardHoverInfo hover;

        public TextMeshProUGUI Label => label;
        public CanvasGroup Group => group;
        public RectTransform Rect => (RectTransform)transform;

        /// <summary>아이콘·글자·호버 설명을 채운다. onClick이 null이면 누를 수 없는 표시용 칩.</summary>
        public void Bind(Sprite iconSprite, string text, string tooltip, Action onClick = null)
        {
            icon.sprite = iconSprite;
            icon.enabled = iconSprite != null;   // 아이콘 없는 데이터는 빈 홈으로 드러난다 (다른 그림으로 채우지 않는다)
            label.text = text;
            hover.Description = tooltip;

            button.onClick.RemoveAllListeners();
            button.interactable = onClick != null;
            if (onClick != null) button.onClick.AddListener(() => onClick());
        }

        /// <summary>못 받은 전리품(가방 가득) — 흐리게.</summary>
        public void SetBlocked(bool blocked) => group.alpha = blocked ? 0.55f : 1f;
    }
}
