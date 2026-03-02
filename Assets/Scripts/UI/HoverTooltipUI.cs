using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Linq;

namespace DiceOrbit.UI
{
    public class HoverTooltipUI : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }
        public static HoverTooltipUI Instance { get; private set; }

        private Canvas canvas;
        private RectTransform panelRect;
        private TextMeshProUGUI tooltipText;

        private Vector2 padding = new Vector2(14f, 10f);
        private Vector2 offset = new Vector2(16f, -16f);
        private bool visible;
        private IHoverTooltipProvider currentProvider;
        private bool pinnedByUI;
        private Camera cachedCamera;
        private TMP_FontAsset resolvedFont;

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
            Hide();
            Debug.Log("[HoverRaycast] Tooltip system initialized");
        }

        private void Update()
        {
            UpdateHoveredTarget();
            if (!visible || canvas == null) return;
            FollowMouse();
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            var go = new GameObject("HoverTooltipUI");
            go.AddComponent<HoverTooltipUI>();
        }

        public void Show(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                Hide();
                return;
            }

            if (tooltipText == null) return;
            TryApplyPreferredFont();
            TryPopulateDynamicGlyphs(message);
            tooltipText.text = message;
            UpdateSize();
            visible = true;
            panelRect.gameObject.SetActive(true);
            FollowMouse();
        }

        public void Hide()
        {
            visible = false;
            if (panelRect != null)
            {
                panelRect.gameObject.SetActive(false);
            }
        }

        public void ShowPinned(string message)
        {
            pinnedByUI = true;
            currentProvider = null;
            Show(message);
        }

        public void HidePinned()
        {
            pinnedByUI = false;
            Hide();
        }

        private void BuildUI()
        {
            canvas = GetComponentInChildren<Canvas>();
            if (canvas != null) return;

            var canvasGO = new GameObject("Canvas");
            canvasGO.transform.SetParent(transform, false);
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            var panelGO = new GameObject("Panel");
            panelGO.transform.SetParent(canvasGO.transform, false);
            panelRect = panelGO.AddComponent<RectTransform>();
            var panelImage = panelGO.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.78f);
            panelImage.raycastTarget = false;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(panelGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            tooltipText = textGO.AddComponent<TextMeshProUGUI>();
            tooltipText.fontSize = 20f;
            tooltipText.color = Color.white;
            tooltipText.alignment = TextAlignmentOptions.Left;
            tooltipText.textWrappingMode = TextWrappingModes.NoWrap;
            tooltipText.raycastTarget = false;
            TryApplyPreferredFont();

            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(padding.x, padding.y);
            textRect.offsetMax = new Vector2(-padding.x, -padding.y);
        }

        private void UpdateSize()
        {
            Vector2 textSize = tooltipText.GetPreferredValues(tooltipText.text);
            panelRect.sizeDelta = textSize + (padding * 2f);
        }

        private void FollowMouse()
        {
            Vector2 pos = GetMousePosition() + offset;
            var size = panelRect.sizeDelta;
            float maxX = Screen.width - size.x;
            float minY = size.y;

            pos.x = Mathf.Clamp(pos.x, 0f, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, Screen.height);
            panelRect.position = pos;
        }

        private void UpdateHoveredTarget()
        {
            if (pinnedByUI) return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                ClearProvider();
                return;
            }

            var provider = GetProviderUnderMouse();
            if (!ReferenceEquals(provider, currentProvider))
            {
                currentProvider = provider;
                if (currentProvider != null)
                {
                    Debug.Log($"[HoverRaycast] Hit: {((Component)currentProvider).name}");
                    Show(currentProvider.GetHoverTooltipText());
                }
                else
                {
                    Debug.Log("[HoverRaycast] Cleared");
                    Hide();
                }
            }
        }

        private IHoverTooltipProvider GetProviderUnderMouse()
        {
            var cam = GetWorldCamera();
            if (cam == null) return null;

            Ray ray = cam.ScreenPointToRay(GetMousePosition());
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                return null;
            }

            return hit.collider.GetComponentInParent<IHoverTooltipProvider>();
        }

        private Camera GetWorldCamera()
        {
            if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
            {
                return cachedCamera;
            }

            cachedCamera = Camera.main;
            if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
            {
                return cachedCamera;
            }

            // Fallback for scenes where MainCamera tag is missing or camera is spawned dynamically.
            cachedCamera = FindFirstObjectByType<Camera>();
            return cachedCamera;
        }

        private Vector2 GetMousePosition()
        {
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }

            return Input.mousePosition;
        }

        private void ClearProvider()
        {
            if (currentProvider != null)
            {
                currentProvider = null;
                Hide();
            }
        }

        private void TryApplyPreferredFont()
        {
            if (tooltipText == null) return;

            if (resolvedFont == null)
            {
                resolvedFont = ResolvePreferredFont();
            }

            if (resolvedFont != null && tooltipText.font != resolvedFont)
            {
                tooltipText.font = resolvedFont;
            }
        }

        private TMP_FontAsset ResolvePreferredFont()
        {
            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (fonts != null && fonts.Length > 0)
            {
                var pretendard = fonts.FirstOrDefault(f =>
                    f != null &&
                    f.name.ToLowerInvariant().Contains("pretendard"));
                if (pretendard != null)
                {
                    return pretendard;
                }
            }

            return TMP_Settings.defaultFontAsset;
        }

        private void TryPopulateDynamicGlyphs(string text)
        {
            if (resolvedFont == null || string.IsNullOrEmpty(text)) return;
            if (resolvedFont.atlasPopulationMode != AtlasPopulationMode.Dynamic) return;

            resolvedFont.TryAddCharacters(text, out _);
        }
    }
}


