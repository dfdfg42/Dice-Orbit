using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Linq;
using System.Collections.Generic;
using System.Text;

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
    private KeywordGlossaryPanelUI keywordGlossaryPanel;
    private StatusGlossaryPanelUI statusGlossaryPanel;
    private RectTransform keywordDetailRect;
    private TextMeshProUGUI keywordDetailText;

        private Vector2 padding = new Vector2(14f, 10f);
        private Vector2 offset = new Vector2(16f, -16f);
    private Vector2 detailPadding = new Vector2(12f, 8f);
    private Vector2 detailOffset = new Vector2(14f, 0f);
        private bool visible;
        private IHoverTooltipProvider currentProvider;
        private bool pinnedByUI;
        private Camera cachedCamera;
        private TMP_FontAsset resolvedFont;
    private bool keywordDetailPinned;
    private string pinnedKeywordKey;

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
            UpdateKeywordDetailInteraction();
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
            string mainText = TooltipKeywordFormatter.FormatMainTooltipText(message);
            var statuses = TooltipKeywordFormatter.ExtractStatuses(mainText, out string strippedMainText);
            tooltipText.text = mainText;
            if (!string.IsNullOrWhiteSpace(strippedMainText))
            {
                tooltipText.text = strippedMainText;
            }
            UpdateSize();
            visible = true;
            panelRect.gameObject.SetActive(true);
            FollowMouse();

            keywordGlossaryPanel?.SetFont(resolvedFont);
            statusGlossaryPanel?.SetFont(resolvedFont);

            var matchedKeywords = TooltipKeywordFormatter.ExtractMatches(tooltipText.text);
            keywordGlossaryPanel?.Show(matchedKeywords, panelRect.position, panelRect.sizeDelta);
            statusGlossaryPanel?.Show(statuses, panelRect.position, panelRect.sizeDelta);

            if (!keywordDetailPinned)
            {
                HideKeywordDetail();
            }
        }

        public void Hide()
        {
            visible = false;
            if (panelRect != null)
            {
                panelRect.gameObject.SetActive(false);
            }

            keywordDetailPinned = false;
            pinnedKeywordKey = null;
            HideKeywordDetail();
            keywordGlossaryPanel?.Hide();
            statusGlossaryPanel?.Hide();
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

            var detailGO = new GameObject("KeywordDetailPanel");
            detailGO.transform.SetParent(canvasGO.transform, false);
            keywordDetailRect = detailGO.AddComponent<RectTransform>();
            var detailImage = detailGO.AddComponent<Image>();
            detailImage.color = new Color(0f, 0f, 0f, 0.88f);
            detailImage.raycastTarget = false;

            var detailTextGO = new GameObject("KeywordDetailText");
            detailTextGO.transform.SetParent(detailGO.transform, false);
            var detailTextRect = detailTextGO.AddComponent<RectTransform>();
            keywordDetailText = detailTextGO.AddComponent<TextMeshProUGUI>();
            keywordDetailText.fontSize = 18f;
            keywordDetailText.color = new Color(1f, 0.93f, 0.66f, 1f);
            keywordDetailText.alignment = TextAlignmentOptions.TopLeft;
            keywordDetailText.textWrappingMode = TextWrappingModes.Normal;
            keywordDetailText.raycastTarget = false;
            TryApplyPreferredFont();

            detailTextRect.anchorMin = Vector2.zero;
            detailTextRect.anchorMax = Vector2.one;
            detailTextRect.offsetMin = new Vector2(detailPadding.x, detailPadding.y);
            detailTextRect.offsetMax = new Vector2(-detailPadding.x, -detailPadding.y);

            var glossaryGo = new GameObject("KeywordGlossaryPanel");
            keywordGlossaryPanel = glossaryGo.AddComponent<KeywordGlossaryPanelUI>();
            keywordGlossaryPanel.Initialize(canvasGO.transform, resolvedFont);

            var statusPanelGo = new GameObject("StatusGlossaryPanel");
            statusGlossaryPanel = statusPanelGo.AddComponent<StatusGlossaryPanelUI>();
            statusGlossaryPanel.Initialize(canvasGO.transform, resolvedFont);

            HideKeywordDetail();
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
            keywordGlossaryPanel?.Reposition(panelRect.position, panelRect.sizeDelta);
            statusGlossaryPanel?.Reposition(panelRect.position, panelRect.sizeDelta);
            RepositionKeywordDetailPanel();
        }

        private void RepositionKeywordDetailPanel()
        {
            if (keywordDetailRect == null || !keywordDetailRect.gameObject.activeSelf || panelRect == null) return;

            Vector2 basePos = panelRect.position;
            float x = basePos.x + panelRect.sizeDelta.x + detailOffset.x;
            float y = basePos.y;

            var detailSize = keywordDetailRect.sizeDelta;
            if (x + detailSize.x > Screen.width)
            {
                x = basePos.x - detailSize.x - detailOffset.x;
            }

            x = Mathf.Clamp(x, 0f, Mathf.Max(0f, Screen.width - detailSize.x));
            y = Mathf.Clamp(y, detailSize.y, Screen.height);

            keywordDetailRect.position = new Vector2(x, y);
        }

        private void UpdateKeywordDetailInteraction()
        {
            if (tooltipText == null || keywordDetailText == null || !visible)
            {
                return;
            }

            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                keywordDetailPinned = false;
                pinnedKeywordKey = null;
            }

            int linkIndex = TMP_TextUtilities.FindIntersectingLink(tooltipText, GetMousePosition(), null);
            if (linkIndex < 0)
            {
                if (!keywordDetailPinned)
                {
                    HideKeywordDetail();
                }
                return;
            }

            TMP_LinkInfo linkInfo = tooltipText.textInfo.linkInfo[linkIndex];
            string linkId = linkInfo.GetLinkID();
            if (!TooltipKeywordFormatter.TryGetDescriptionByLinkId(linkId, out string keyword, out string description))
            {
                if (!keywordDetailPinned)
                {
                    HideKeywordDetail();
                }
                return;
            }

            ShowKeywordDetail(keyword, description);

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (keywordDetailPinned && string.Equals(pinnedKeywordKey, keyword, System.StringComparison.OrdinalIgnoreCase))
                {
                    keywordDetailPinned = false;
                    pinnedKeywordKey = null;
                    HideKeywordDetail();
                }
                else
                {
                    keywordDetailPinned = true;
                    pinnedKeywordKey = keyword;
                    ShowKeywordDetail(keyword, description);
                }
            }
        }

        private void ShowKeywordDetail(string keyword, string description)
        {
            if (keywordDetailRect == null || keywordDetailText == null) return;

            if (TooltipKeywordFormatter.TryGetVisuals(keyword, out Color keywordColor, out _))
            {
                keywordDetailText.color = keywordColor;
            }
            else
            {
                keywordDetailText.color = new Color(1f, 0.93f, 0.66f, 1f);
            }

            keywordDetailText.text = $"[{keyword}]\n{description}\n\n(좌클릭: 고정 / 우클릭: 해제)";
            Vector2 preferred = keywordDetailText.GetPreferredValues(keywordDetailText.text, 360f, 0f);
            keywordDetailRect.sizeDelta = new Vector2(Mathf.Clamp(preferred.x + detailPadding.x * 2f, 200f, 380f), preferred.y + detailPadding.y * 2f);
            keywordDetailRect.gameObject.SetActive(true);
            RepositionKeywordDetailPanel();
        }

        private void HideKeywordDetail()
        {
            if (keywordDetailRect != null)
            {
                keywordDetailRect.gameObject.SetActive(false);
            }
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

namespace DiceOrbit.UI
{
    public static class TooltipKeywordFormatter
    {
        public readonly struct KeywordDisplayData
        {
            public readonly string Key;
            public readonly string Description;
            public readonly Color Color;
            public readonly Sprite Icon;

            public KeywordDisplayData(string key, string description, Color color, Sprite icon)
            {
                Key = key;
                Description = description;
                Color = color;
                Icon = icon;
            }
        }

        public readonly struct StatusDisplayData
        {
            public readonly string Name;
            public readonly string StackText;
            public readonly string DurationText;
            public readonly string Description;
            public readonly Color Color;

            public StatusDisplayData(string name, string stackText, string durationText, string description, Color color)
            {
                Name = name;
                StackText = stackText;
                DurationText = durationText;
                Description = description;
                Color = color;
            }
        }

        private const string DatabaseResourcePath = "UI/TooltipKeywordDatabase";

        private readonly struct KeywordEntry
        {
            public readonly string Key;
            public readonly string Description;
            public readonly Color Color;
            public readonly Sprite Icon;

            public KeywordEntry(string key, string description, Color color, Sprite icon = null)
            {
                Key = key;
                Description = description;
                Color = color;
                Icon = icon;
            }
        }

        private static readonly KeywordEntry[] FallbackEntries =
        {
            new KeywordEntry("집중", "액티브 발동 시 소모되며, 스택당 추가 피해를 제공합니다.", new Color(1f, 0.83f, 0.42f, 1f)),
            new KeywordEntry("진창눈", "이동 디버프 상태입니다. 이동 가능 칸이 감소합니다.", new Color(0.74f, 0.88f, 1f, 1f)),
            new KeywordEntry("꿀", "꿀 디버프 상태입니다. 이동력이 감소합니다.", new Color(1f, 0.84f, 0.35f, 1f)),
            new KeywordEntry("Honey", "꿀 디버프 상태입니다. 이동력이 감소합니다.", new Color(1f, 0.84f, 0.35f, 1f)),
            new KeywordEntry("방어도", "피해를 먼저 흡수하는 임시 수치입니다.", new Color(0.64f, 0.86f, 1f, 1f)),
            new KeywordEntry("중독", "턴마다 피해를 입는 상태 이상입니다.", new Color(0.73f, 1f, 0.62f, 1f)),
            new KeywordEntry("약화", "공격 피해량이 감소하는 상태 이상입니다.", new Color(1f, 0.85f, 0.62f, 1f)),
            new KeywordEntry("취약", "받는 피해가 증가하는 상태 이상입니다.", new Color(1f, 0.66f, 0.66f, 1f)),
            new KeywordEntry("기절", "행동이 제한되는 상태 이상입니다.", new Color(1f, 0.78f, 0.62f, 1f)),
            new KeywordEntry("침묵", "스킬 사용이 제한되는 상태 이상입니다.", new Color(0.9f, 0.76f, 1f, 1f)),
            new KeywordEntry("이동", "턴에 이동 가능한 칸 수를 의미합니다.", new Color(0.75f, 0.95f, 0.75f, 1f)),
        };

        private static readonly Dictionary<string, string> StatusNameAliases = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "Honey", "꿀" },
            { "SlushSnow", "진창눈" },
            { "Focus", "집중" },
            { "Poison", "중독" },
            { "Weak", "약화" },
            { "Vulnerable", "취약" },
            { "Stun", "기절" },
            { "Silence", "침묵" },
        };

        private static TooltipKeywordDatabase cachedDatabase;
        private static int cachedDatabaseInstanceId;
        private static KeywordEntry[] cachedEntries;

        private static KeywordEntry[] GetEntries()
        {
            var db = LoadDatabase();
            if (db == null || db.entries == null || db.entries.Count == 0)
            {
                cachedEntries = FallbackEntries;
                return cachedEntries;
            }

            int id = db.GetInstanceID();
            if (cachedEntries != null && cachedDatabaseInstanceId == id)
            {
                return cachedEntries;
            }

            var list = new List<KeywordEntry>();
            foreach (var entry in db.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key)) continue;
                if (string.IsNullOrWhiteSpace(entry.description)) continue;
                list.Add(new KeywordEntry(entry.key.Trim(), entry.description.Trim(), entry.color, entry.icon));
            }

            if (list.Count == 0)
            {
                cachedEntries = FallbackEntries;
            }
            else
            {
                cachedEntries = list.ToArray();
                cachedDatabaseInstanceId = id;
            }

            return cachedEntries;
        }

        private static TooltipKeywordDatabase LoadDatabase()
        {
            if (cachedDatabase != null) return cachedDatabase;
            cachedDatabase = Resources.Load<TooltipKeywordDatabase>(DatabaseResourcePath);
            return cachedDatabase;
        }

        private static string ToColorHex(Color color)
        {
            return ColorUtility.ToHtmlStringRGB(color);
        }

        public static bool TryGetVisuals(string keyword, out Color color, out Sprite icon)
        {
            color = new Color(1f, 0.83f, 0.42f, 1f);
            icon = null;

            foreach (var entry in GetEntries())
            {
                if (!string.Equals(entry.Key, keyword, System.StringComparison.OrdinalIgnoreCase)) continue;
                color = entry.Color;
                icon = entry.Icon;
                return true;
            }

            return false;
        }

        public static bool TryGetDescription(string keyword, out string description)
        {
            description = null;
            if (string.IsNullOrWhiteSpace(keyword)) return false;

            foreach (var entry in GetEntries())
            {
                if (!string.Equals(entry.Key, keyword, System.StringComparison.OrdinalIgnoreCase)) continue;
                description = entry.Description;
                return true;
            }

            return false;
        }

        public static bool TryGetDescriptionByLinkId(string linkId, out string keyword, out string description)
        {
            keyword = null;
            description = null;

            if (string.IsNullOrWhiteSpace(linkId)) return false;
            const string prefix = "kw:";
            if (!linkId.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)) return false;

            keyword = linkId.Substring(prefix.Length);
            if (string.IsNullOrWhiteSpace(keyword)) return false;

            return TryGetDescription(keyword, out description);
        }

        public static string FormatMainTooltipText(string rawText)
        {
            return string.IsNullOrWhiteSpace(rawText) ? rawText : rawText.TrimEnd();
        }

        public static List<StatusDisplayData> ExtractStatuses(string rawText, out string strippedText)
        {
            strippedText = FormatMainTooltipText(rawText) ?? string.Empty;
            var statuses = new List<StatusDisplayData>();

            if (string.IsNullOrWhiteSpace(rawText))
            {
                return statuses;
            }

            var lines = rawText.Replace("\r\n", "\n").Split('\n');
            bool inStatusSection = false;
            var remaining = new List<string>();

            foreach (var rawLine in lines)
            {
                string line = rawLine?.TrimEnd() ?? string.Empty;

                if (line.StartsWith("--- Status", System.StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("--- 상태", System.StringComparison.OrdinalIgnoreCase))
                {
                    inStatusSection = true;
                    continue;
                }

                if (inStatusSection)
                {
                    if (line.StartsWith("--- ", System.StringComparison.Ordinal))
                    {
                        inStatusSection = false;
                        remaining.Add(line);
                        continue;
                    }

                    if (TryParseStatusLine(line, out var statusData))
                    {
                        statuses.Add(statusData);
                    }

                    continue;
                }

                remaining.Add(line);
            }

            strippedText = string.Join("\n", remaining).TrimEnd();
            return statuses;
        }

        private static bool TryParseStatusLine(string line, out StatusDisplayData statusData)
        {
            statusData = default;
            if (string.IsNullOrWhiteSpace(line)) return false;
            if (!line.TrimStart().StartsWith("•")) return false;

            string normalized = line.Trim().TrimStart('•').Trim();
            if (string.IsNullOrWhiteSpace(normalized)) return false;

            string[] parts = normalized.Split(new[] { "  " }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;

            string rawName = parts[0].Trim();
            string displayName = ResolveStatusDisplayName(rawName);

            string stackText = string.Empty;
            string durationText = string.Empty;

            for (int i = 1; i < parts.Length; i++)
            {
                string token = parts[i].Trim();
                if (token.StartsWith("x", System.StringComparison.OrdinalIgnoreCase))
                {
                    stackText = token;
                }
                else if (token.StartsWith("(") && token.EndsWith(")"))
                {
                    durationText = token;
                }
            }

            if (!TryGetDescription(displayName, out string description))
            {
                TryGetDescription(rawName, out description);
            }

            if (!TryGetVisuals(displayName, out Color color, out _) && !TryGetVisuals(rawName, out color, out _))
            {
                color = new Color(0.85f, 0.93f, 1f, 1f);
            }

            statusData = new StatusDisplayData(displayName, stackText, durationText, description, color);
            return true;
        }

        private static string ResolveStatusDisplayName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return rawName;
            if (StatusNameAliases.TryGetValue(rawName, out string mapped) && !string.IsNullOrWhiteSpace(mapped))
            {
                return mapped;
            }

            return rawName;
        }

        public static List<KeywordDisplayData> ExtractMatches(string rawText)
        {
            var result = new List<KeywordDisplayData>();
            if (string.IsNullOrWhiteSpace(rawText)) return result;

            foreach (var entry in GetEntries())
            {
                if (string.IsNullOrWhiteSpace(entry.Key)) continue;
                if (rawText.IndexOf(entry.Key, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (result.Any(r => string.Equals(r.Key, entry.Key, System.StringComparison.OrdinalIgnoreCase))) continue;

                result.Add(new KeywordDisplayData(entry.Key, entry.Description, entry.Color, entry.Icon));
            }

            return result;
        }

        public static string AppendKeywordSection(string rawText)
        {
            // Backward-compatible API: separate glossary panel now handles keyword explanations.
            return FormatMainTooltipText(rawText);
        }
    }
}


