using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

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

        [Header("Hover Settings")]
        [SerializeField] private float normalAlpha = 0.5f;
        [SerializeField] private float hoverAlpha = 1f;
        [SerializeField] private List<HoverPortraitSpriteOption> hoverPortraitSprites = new List<HoverPortraitSpriteOption>();

        [Header("Select Button Sprite")]
        [SerializeField] private Image selectButtonImage;
        [SerializeField] private Sprite defaultSelectButtonSprite;
        [SerializeField] private List<SelectButtonSpriteOption> selectButtonSprites = new List<SelectButtonSpriteOption>();
        
        private Core.CharacterPreset character;
        private System.Action<Core.CharacterPreset> onSelected;
        private Sprite defaultPortraitSprite;

        [System.Serializable]
        private class SelectButtonSpriteOption
        {
            public string characterName;
            public Sprite sprite;
        }

        [System.Serializable]
        private class HoverPortraitSpriteOption
        {
            public string characterName;
            public Sprite sprite;
        }
        
        private void Awake()
        {
            if (selectButton != null)
            {
                selectButton.onClick.AddListener(OnSelectClicked);
            }

            // Ensure initial alpha and register pointer events so hover works
            ApplyAlphaToUI(normalAlpha);
            RegisterPointerEvents();
        }
        
        /// <summary>
        /// 카드 설정
        /// </summary>
        public void Setup(Core.CharacterPreset preset, System.Action<Core.CharacterPreset> callback)
        {
            character = preset;
            //Debug.Log($"[CharacterCard] preset name = '{preset?.CharacterName}'");
            onSelected = callback;

            ApplySelectButtonSprite(preset);
            
            // UI 업데이트
            if (portraitImage != null && preset.Portrait != null)
            {
                portraitImage.sprite = preset.Portrait;
                defaultPortraitSprite = preset.Portrait;
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
                statsText.text = $"HP: {preset.MaxHP}";
            }
        }

        private void ApplySelectButtonSprite(Core.CharacterPreset preset)
        {
            var buttonImage = selectButtonImage != null ? selectButtonImage : selectButton != null ? selectButton.image : null;
            if (buttonImage == null || preset == null)
            {
                return;
            }

            Sprite resolvedSprite = defaultSelectButtonSprite;
            var presetName = preset.CharacterName;

            for (int i = 0; i < selectButtonSprites.Count; i++)
            {
                var option = selectButtonSprites[i];
                if (option != null && !string.IsNullOrEmpty(option.characterName) && option.characterName == presetName)
                {
                    resolvedSprite = option.sprite;
                    break;
                }
            }

            if (resolvedSprite != null)
            {
                buttonImage.sprite = resolvedSprite;
            }
        }

        private void ApplyAlphaToUI(float alpha)
        {
            if (portraitImage != null)
            {
                var c = portraitImage.color;
                portraitImage.color = new Color(c.r, c.g, c.b, alpha);
                // ensure it can receive pointer events
                portraitImage.raycastTarget = true;
            }

            var btnImage = selectButtonImage != null ? selectButtonImage : selectButton != null ? selectButton.image : null;
            if (btnImage != null)
            {
                var c2 = btnImage.color;
                btnImage.color = new Color(c2.r, c2.g, c2.b, alpha);
            }
        }

        private void SetHover(bool hover)
        {
            ApplyAlphaToUI(hover ? hoverAlpha : normalAlpha);

            if (portraitImage != null)
            {
                if (hover)
                {
                    var hoverSprite = ResolveHoverPortraitSprite();
                    if (hoverSprite != null)
                    {
                        portraitImage.sprite = hoverSprite;
                    }
                }
                else if (!hover && defaultPortraitSprite != null)
                {
                    portraitImage.sprite = defaultPortraitSprite;
                }
            }
        }

        private Sprite ResolveHoverPortraitSprite()
        {
            if (character == null)
            {
                return null;
            }

            var presetName = character.CharacterName;
            for (int i = 0; i < hoverPortraitSprites.Count; i++)
            {
                var option = hoverPortraitSprites[i];
                if (option != null && !string.IsNullOrEmpty(option.characterName) && option.characterName == presetName)
                {
                    return option.sprite;
                }
            }

            return defaultPortraitSprite;
        }

        private void RegisterPointerEvents()
        {
            // Add EventTrigger to selectButton and portrait so pointer enter/exit work
            if (selectButton != null)
            {
                AddEventTrigger(selectButton.gameObject);
            }

            if (portraitImage != null)
            {
                portraitImage.raycastTarget = true;
                AddEventTrigger(portraitImage.gameObject);
            }
        }

        private void AddEventTrigger(GameObject go)
        {
            if (go == null) return;

            var ev = go.GetComponent<EventTrigger>();
            if (ev == null) ev = go.AddComponent<EventTrigger>();

            // Pointer Enter
            var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entryEnter.callback.AddListener((data) => { SetHover(true); });
            ev.triggers.Add(entryEnter);

            // Pointer Exit
            var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            entryExit.callback.AddListener((data) => { SetHover(false); });
            ev.triggers.Add(entryExit);
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
