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
        public void UpdateVisual()
        {
            if (diceData == null) return;

            // 값 표시
            if (valueText != null)
            {
                valueText.text = diceData.Value.ToString();
            }

            // 배경 색상 및 상호작용
            if (backgroundImage != null)
            {
                backgroundImage.color = ResolveCurrentColor();
            }

            var button = GetComponent<Button>();
            if (button != null)
            {
                button.interactable = diceData.State == DiceState.Available;
            }

            RefreshBadge();
        }

        // 캐릭터 선택 중, 이 값으로는 그 캐릭터의 어떤 스킬 조건도 못 맞출 때 — 몸통 색은 그대로 두고
        // 모서리에 "스킬 아이콘+빗금" 배지만 붙인다 (붉은 몸통 = 완전 잠금과 시각 언어 분리).
        private bool skillUnusableHint;
        private Sprite skillHintIcon;        // 배지에 넣을 스킬 아이콘 (선택된 캐릭터의 것)
        private string skillHintCondition;   // 호버 툴팁용 조건 문구 (예: "주사위 4 이상")

        // 튜토리얼 잠금 등 "이 주사위 자체를 못 씀" — 이때만 몸통을 붉게 틴트
        private bool lockedTint;

        private GameObject badgeRoot;
        private Image badgeIconImage;

        private static readonly Color BadgeRingColor = new Color(0.90f, 0.28f, 0.30f, 1f);
        private static readonly Color BadgeBackColor = new Color(0.15f, 0.14f, 0.18f, 0.95f);

        public bool   SkillUnusableHint  => skillUnusableHint;
        public string SkillHintCondition => skillHintCondition;

        /// <summary>스킬 사용 불가 배지 토글 (CharacterActionUI 열림/닫힘에 맞춰 DiceUI가 호출).
        /// 이동에는 쓸 수 있으므로 몸통 색은 바꾸지 않는다.</summary>
        public void SetSkillUnusableHint(bool unusable, Sprite skillIcon = null, string conditionText = null)
        {
            skillHintIcon      = unusable ? skillIcon : null;
            skillHintCondition = unusable ? conditionText : null;
            skillUnusableHint  = unusable;
            UpdateVisual();
        }

        /// <summary>완전 사용 불가 틴트 (튜토리얼 잠금 등). 몸통 전체를 붉게.</summary>
        public void SetLockedTint(bool locked)
        {
            if (lockedTint == locked) return;
            lockedTint = locked;
            UpdateVisual();
        }

        private Color ResolveCurrentColor()
        {
            if (diceData == null) return normalColor;

            switch (diceData.State)
            {
                case DiceState.Available:
                    if (isSelected) return selectedColor;
                    // 완전 잠금(튜토리얼 등)일 때만 몸통을 붉게 (선택/예약/사용 상태색이 항상 우선)
                    return lockedTint ? Color.Lerp(normalColor, new Color(1f, 0.25f, 0.25f, normalColor.a), 0.4f) : normalColor;
                case DiceState.Reserved:
                    return reservedColor;
                case DiceState.Used:
                    return usedColor;
                default:
                    return normalColor;
            }
        }

        private void RefreshBadge()
        {
            bool show = skillUnusableHint && diceData != null && diceData.State == DiceState.Available;
            if (!show)
            {
                if (badgeRoot != null) badgeRoot.SetActive(false);
                return;
            }

            if (badgeRoot == null) BuildBadge();
            badgeRoot.SetActive(true);
            if (badgeIconImage != null)
            {
                badgeIconImage.sprite  = skillHintIcon;
                badgeIconImage.enabled = skillHintIcon != null;
            }
        }

        // 배지 = 붉은 링 원 + 어두운 속 + 스킬 아이콘 + 빗금. 아트 에셋 없이 코드 생성.
        private void BuildBadge()
        {
            var dieRect = (RectTransform)transform;
            float size = Mathf.Clamp(dieRect.rect.height * 0.42f, 20f, 40f);

            badgeRoot = new GameObject("SkillUnusableBadge", typeof(RectTransform));
            var root = (RectTransform)badgeRoot.transform;
            root.SetParent(transform, false);
            root.anchorMin = root.anchorMax = Vector2.one;   // 주사위 우상단 모서리
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;            // 모서리에 반쯤 걸치게
            root.sizeDelta = new Vector2(size, size);

            int circleRadius = Mathf.CeilToInt(size * 0.5f);

            Image MakeCircle(string name, float diameter, Color color)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(root, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(diameter, diameter);
                var img = go.GetComponent<Image>();
                img.sprite = UiRoundedSprite.Get(circleRadius);
                img.type = Image.Type.Sliced;
                img.color = color;
                img.raycastTarget = false;
                return img;
            }

            MakeCircle("Ring", size, BadgeRingColor);
            MakeCircle("Back", size - 3f, BadgeBackColor);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var iconRt = (RectTransform)iconGo.transform;
            iconRt.SetParent(root, false);
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(size * 0.62f, size * 0.62f);
            badgeIconImage = iconGo.GetComponent<Image>();
            badgeIconImage.preserveAspect = true;
            badgeIconImage.raycastTarget = false;

            var slashGo = new GameObject("Slash", typeof(RectTransform), typeof(Image));
            var slashRt = (RectTransform)slashGo.transform;
            slashRt.SetParent(root, false);
            slashRt.anchorMin = slashRt.anchorMax = new Vector2(0.5f, 0.5f);
            slashRt.sizeDelta = new Vector2(size * 0.92f, Mathf.Max(2.5f, size * 0.11f));
            slashRt.localEulerAngles = new Vector3(0f, 0f, -45f);   // ↘ 빗금
            var slashImg = slashGo.GetComponent<Image>();
            slashImg.color = BadgeRingColor;
            slashImg.raycastTarget = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (diceData == null || diceData.State != DiceState.Available) return;

            parentDiceUI = parentDiceUI != null ? parentDiceUI : GetComponentInParent<DiceUI>();
            parentDiceUI?.HandleDiceElementClicked(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (diceData == null) return;
            // 특수 주사위(Source 있음)거나 스킬 불가 배지가 떠 있으면 툴팁 표시
            if (diceData.Source == null && !skillUnusableHint) return;
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
