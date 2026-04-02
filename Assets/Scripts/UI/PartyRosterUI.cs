using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 우측 상단 파티 목록 UI (초상화/이름/레벨/체력)
    /// 런타임 자동 생성 + 파티 상태 동기화
    /// </summary>
    public class PartyRosterUI : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static PartyRosterUI Instance { get; private set; }

        [Header("Layout")]
        [SerializeField] private Vector2 anchorOffset = new Vector2(-24f, -24f);
        [SerializeField] private float entrySpacing = 10f;
        [SerializeField] private Vector2 entrySize = new Vector2(320f, 84f);
        [SerializeField] private Color entryBackgroundColor = new Color(0f, 0f, 0f, 0.62f);

        [Header("Portrait")]
        [SerializeField] private Vector2 portraitSize = new Vector2(56f, 56f);
        [SerializeField] private Sprite portraitMaskSprite;

        [Header("HP")]
        [SerializeField] private Color hpFillColor = new Color(0.2f, 0.95f, 0.4f, 1f);
        [SerializeField] private Color hpBackgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.95f);

        private Canvas canvas;
        private RectTransform rootRect;
        private RectTransform listRect;
        private TMP_FontAsset resolvedFont;

        private readonly Dictionary<Core.Character, EntryWidgets> entryByCharacter = new Dictionary<Core.Character, EntryWidgets>();
        private readonly List<Core.Character> orderedCharacters = new List<Core.Character>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildUI();
        }

        private void Update()
        {
            SyncEntries();
            RefreshEntries();
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            var go = new GameObject("PartyRosterUI");
            go.AddComponent<PartyRosterUI>();
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6000;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            rootRect = canvasGo.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var panelGo = new GameObject("PartyPanel", typeof(RectTransform));
            panelGo.transform.SetParent(canvasGo.transform, false);
            listRect = panelGo.GetComponent<RectTransform>();
            listRect.anchorMin = new Vector2(1f, 1f);
            listRect.anchorMax = new Vector2(1f, 1f);
            listRect.pivot = new Vector2(1f, 1f);
            listRect.anchoredPosition = anchorOffset;

            var layout = panelGo.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperRight;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.spacing = entrySpacing;

            var fitter = panelGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            resolvedFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (portraitMaskSprite == null)
            {
                portraitMaskSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
            }
        }

        private void SyncEntries()
        {
            var partyManager = Core.PartyManager.Instance;
            if (partyManager == null)
            {
                ClearAllEntries();
                return;
            }

            orderedCharacters.Clear();
            orderedCharacters.AddRange(partyManager.Party);

            // Remove stale
            var toRemove = new List<Core.Character>();
            foreach (var kv in entryByCharacter)
            {
                if (kv.Key == null || !orderedCharacters.Contains(kv.Key))
                {
                    toRemove.Add(kv.Key);
                }
            }

            foreach (var character in toRemove)
            {
                RemoveEntry(character);
            }

            // Ensure existing/new
            for (int i = 0; i < orderedCharacters.Count; i++)
            {
                var character = orderedCharacters[i];
                if (character == null) continue;

                if (!entryByCharacter.TryGetValue(character, out var entry))
                {
                    entry = CreateEntry(character);
                    entryByCharacter[character] = entry;
                }

                entry.Root.SetSiblingIndex(i);
            }
        }

        private void RefreshEntries()
        {
            foreach (var kv in entryByCharacter)
            {
                var character = kv.Key;
                var entry = kv.Value;
                if (character == null || entry == null) continue;

                var stats = character.Stats;
                if (stats == null) continue;

                string displayName = string.IsNullOrWhiteSpace(stats.CharacterName) ? character.name : stats.CharacterName;
                entry.NameLevelText.text = $"{displayName}  Lv.{stats.Level}";

                float maxHp = Mathf.Max(1, stats.MaxHP);
                float currentHp = Mathf.Clamp(stats.CurrentHP, 0, stats.MaxHP);
                entry.HpSlider.maxValue = maxHp;
                entry.HpSlider.value = currentHp;
                entry.HpText.text = $"{stats.CurrentHP}/{stats.MaxHP}";

                var portrait = stats.SourcePreset != null && stats.SourcePreset.Portrait != null
                    ? stats.SourcePreset.Portrait
                    : stats.CharacterSprite;
                entry.PortraitImage.sprite = portrait;

                float alpha = character.IsAlive ? 1f : 0.45f;
                entry.BackgroundImage.color = new Color(entryBackgroundColor.r, entryBackgroundColor.g, entryBackgroundColor.b, entryBackgroundColor.a * alpha);
                entry.NameLevelText.alpha = alpha;
                entry.HpText.alpha = alpha;
                entry.PortraitImage.color = new Color(1f, 1f, 1f, alpha);
            }
        }

        private EntryWidgets CreateEntry(Core.Character character)
        {
            var entryGo = new GameObject($"Entry_{character.name}", typeof(RectTransform), typeof(Image));
            entryGo.transform.SetParent(listRect, false);

            var entryRect = entryGo.GetComponent<RectTransform>();
            entryRect.sizeDelta = entrySize;

            var background = entryGo.GetComponent<Image>();
            background.color = entryBackgroundColor;

            var layout = entryGo.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            // Portrait circle container
            var portraitMaskGo = new GameObject("PortraitMask", typeof(RectTransform), typeof(Image), typeof(Mask));
            portraitMaskGo.transform.SetParent(entryGo.transform, false);
            var portraitMaskRect = portraitMaskGo.GetComponent<RectTransform>();
            portraitMaskRect.sizeDelta = portraitSize;

            var maskImage = portraitMaskGo.GetComponent<Image>();
            maskImage.sprite = portraitMaskSprite;
            maskImage.type = Image.Type.Sliced;
            maskImage.color = Color.white;

            var mask = portraitMaskGo.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(portraitMaskGo.transform, false);
            var portraitRect = portraitGo.GetComponent<RectTransform>();
            portraitRect.anchorMin = Vector2.zero;
            portraitRect.anchorMax = Vector2.one;
            portraitRect.offsetMin = Vector2.zero;
            portraitRect.offsetMax = Vector2.zero;

            var portraitImage = portraitGo.GetComponent<Image>();
            portraitImage.preserveAspect = true;

            // Right info column
            var infoGo = new GameObject("Info", typeof(RectTransform), typeof(VerticalLayoutGroup));
            infoGo.transform.SetParent(entryGo.transform, false);
            var infoRect = infoGo.GetComponent<RectTransform>();
            infoRect.sizeDelta = new Vector2(entrySize.x - portraitSize.x - 46f, entrySize.y - 20f);

            var infoLayout = infoGo.GetComponent<VerticalLayoutGroup>();
            infoLayout.childAlignment = TextAnchor.UpperLeft;
            infoLayout.childControlWidth = true;
            infoLayout.childControlHeight = false;
            infoLayout.childForceExpandWidth = true;
            infoLayout.childForceExpandHeight = false;
            infoLayout.spacing = 4f;

            var nameLevel = CreateText("NameLevel", infoGo.transform, 22f, FontStyles.Bold);
            nameLevel.alignment = TextAlignmentOptions.Left;

            // HP row
            var hpRow = new GameObject("HPRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            hpRow.transform.SetParent(infoGo.transform, false);
            var hpRowRect = hpRow.GetComponent<RectTransform>();
            hpRowRect.sizeDelta = new Vector2(infoRect.sizeDelta.x, 26f);

            var hpRowLayout = hpRow.GetComponent<HorizontalLayoutGroup>();
            hpRowLayout.spacing = 8f;
            hpRowLayout.childAlignment = TextAnchor.MiddleLeft;
            hpRowLayout.childControlWidth = false;
            hpRowLayout.childControlHeight = false;
            hpRowLayout.childForceExpandWidth = false;
            hpRowLayout.childForceExpandHeight = false;

            var hpSliderGo = new GameObject("HpSlider", typeof(RectTransform), typeof(Slider));
            hpSliderGo.transform.SetParent(hpRow.transform, false);
            var hpSliderRect = hpSliderGo.GetComponent<RectTransform>();
            hpSliderRect.sizeDelta = new Vector2(165f, 18f);

            var hpBg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            hpBg.transform.SetParent(hpSliderGo.transform, false);
            var hpBgRect = hpBg.GetComponent<RectTransform>();
            hpBgRect.anchorMin = Vector2.zero;
            hpBgRect.anchorMax = Vector2.one;
            hpBgRect.offsetMin = Vector2.zero;
            hpBgRect.offsetMax = Vector2.zero;
            var hpBgImage = hpBg.GetComponent<Image>();
            hpBgImage.color = hpBackgroundColor;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(hpSliderGo.transform, false);
            var fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = new Vector2(2f, 2f);
            fillAreaRect.offsetMax = new Vector2(-2f, -2f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var fillImage = fill.GetComponent<Image>();
            fillImage.color = hpFillColor;

            var hpSlider = hpSliderGo.GetComponent<Slider>();
            hpSlider.fillRect = fillRect;
            hpSlider.targetGraphic = fillImage;
            hpSlider.direction = Slider.Direction.LeftToRight;
            hpSlider.interactable = false;

            var hpText = CreateText("HpText", hpRow.transform, 18f, FontStyles.Normal);
            hpText.alignment = TextAlignmentOptions.MidlineLeft;

            var hoverProxy = entryGo.AddComponent<PartyRosterEntryHoverProxy>();
            hoverProxy.Bind(character);

            return new EntryWidgets
            {
                Character = character,
                Root = entryRect,
                BackgroundImage = background,
                PortraitImage = portraitImage,
                NameLevelText = nameLevel,
                HpSlider = hpSlider,
                HpText = hpText,
                HoverProxy = hoverProxy
            };
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            if (resolvedFont != null)
            {
                text.font = resolvedFont;
            }
            return text;
        }

        private void RemoveEntry(Core.Character character)
        {
            if (!entryByCharacter.TryGetValue(character, out var entry)) return;

            if (entry.Root != null)
            {
                Destroy(entry.Root.gameObject);
            }

            entryByCharacter.Remove(character);
        }

        private void ClearAllEntries()
        {
            foreach (var kv in entryByCharacter)
            {
                if (kv.Value?.Root != null)
                {
                    Destroy(kv.Value.Root.gameObject);
                }
            }
            entryByCharacter.Clear();
        }

        private sealed class EntryWidgets
        {
            public Core.Character Character;
            public RectTransform Root;
            public Image BackgroundImage;
            public Image PortraitImage;
            public TextMeshProUGUI NameLevelText;
            public Slider HpSlider;
            public TextMeshProUGUI HpText;
            public PartyRosterEntryHoverProxy HoverProxy;
        }
    }

    public class PartyRosterEntryHoverProxy : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private Core.Character character;

        public void Bind(Core.Character target)
        {
            character = target;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (character == null) return;

            HoverTooltipUI.EnsureInstance();
            HoverTooltipUI.Instance?.ShowPinned(character.GetHoverTooltipText());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HoverTooltipUI.Instance?.HidePinned();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (character == null) return;
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;

            // 월드 클릭 선택과 동일 흐름(재클릭 토글 포함)
            character.OnSelected();
        }

        private void OnDisable()
        {
            HoverTooltipUI.Instance?.HidePinned();
        }
    }
}
