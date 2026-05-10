using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
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

        [Header("Portrait Sprite")]
        [SerializeField] private Sprite defaultPortraitOverride;
        [SerializeField] private List<PortraitSpriteOption> portraitSprites = new List<PortraitSpriteOption>();

        [Header("Hover Settings")]
        [SerializeField] private float normalAlpha = 0.5f;
        [SerializeField] private float hoverAlpha = 1f;
        [SerializeField] private List<HoverPortraitSpriteOption> hoverPortraitSprites = new List<HoverPortraitSpriteOption>();

        [Header("Select Button Sprite")]
        [SerializeField] private Image selectButtonImage;
        [SerializeField] private Sprite defaultSelectButtonSprite;
        [SerializeField] private List<SelectButtonSpriteOption> selectButtonSprites = new List<SelectButtonSpriteOption>();

        [Header("Intro Animation")]
        [SerializeField] private float introOffsetY = 750f; //350
        [SerializeField] private float introDuration = 0.35f;
        [SerializeField] private AnimationCurve introCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        private Core.CharacterPreset character;
        private System.Action<Core.CharacterPreset> onSelected;
        private Sprite defaultPortraitSprite;
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Vector2 introTargetPosition;
        private bool hasCapturedIntroTarget;

        [System.Serializable]
        private class SelectButtonSpriteOption
        {
            public string characterName;
            public Sprite sprite;
        }

        [System.Serializable]
        private class PortraitSpriteOption
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
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (selectButton != null)
            {
                selectButton.onClick.AddListener(OnSelectClicked);
            }

            // Ensure initial alpha and register pointer events so hover works
            ApplyAlphaToUI(normalAlpha);
            RegisterPointerEvents();
        }

        public void CaptureIntroTargetPosition()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            if (rectTransform == null)
            {
                return;
            }

            introTargetPosition = rectTransform.anchoredPosition;
            hasCapturedIntroTarget = true;
        }

        public Coroutine PlayIntro(float startDelay)
        {
            return StartCoroutine(PlayIntroRoutine(startDelay));
        }

        private IEnumerator PlayIntroRoutine(float startDelay)
        {
            if (!hasCapturedIntroTarget)
            {
                CaptureIntroTargetPosition();
            }

            if (rectTransform == null)
            {
                yield break;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            yield return new WaitForSecondsRealtime(startDelay);

            var startPosition = introTargetPosition + new Vector2(0f, -introOffsetY);
            rectTransform.anchoredPosition = startPosition;

            var elapsed = 0f;
            while (elapsed < introDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / introDuration);
                var easedT = introCurve != null ? introCurve.Evaluate(t) : t;
                rectTransform.anchoredPosition = Vector2.LerpUnclamped(startPosition, introTargetPosition, easedT);

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = easedT;
                }

                yield return null;
            }

            rectTransform.anchoredPosition = introTargetPosition;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
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
            var resolvedPortrait = ResolvePortraitSprite(preset);
            if (portraitImage != null && resolvedPortrait != null)
            {
                portraitImage.sprite = resolvedPortrait;
                defaultPortraitSprite = resolvedPortrait;
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

        private Sprite ResolvePortraitSprite(Core.CharacterPreset preset)
        {
            if (preset == null)
            {
                return defaultPortraitOverride;
            }

            var presetName = preset.CharacterName;
            for (int i = 0; i < portraitSprites.Count; i++)
            {
                var option = portraitSprites[i];
                if (option != null && !string.IsNullOrEmpty(option.characterName) && option.characterName == presetName)
                {
                    return option.sprite;
                }
            }

            if (defaultPortraitOverride != null)
            {
                return defaultPortraitOverride;
            }

            return preset.Portrait;
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
