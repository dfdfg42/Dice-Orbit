using UnityEngine;
using TMPro;
using System.Collections.Generic;

namespace DiceOrbit.UI
{
    public class TileAttributeBubbleUI : MonoBehaviour
    {
        public readonly struct BubbleIconData
        {
            public readonly Sprite Icon;
            public readonly Color Tint;

            public BubbleIconData(Sprite icon, Color tint)
            {
                Icon = icon;
                Tint = tint;
            }
        }

        [Header("Settings")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.3f, 0f);
        [SerializeField] private float iconScale = 0.2f;
        [SerializeField] private float tailScale = 0.28f;
        [SerializeField] private float iconSpacing = 0.24f;
        [SerializeField] private float bodyMinWidth = 0.42f;
        [SerializeField] private float bodyHeight = 0.24f;
        [SerializeField] private float bodyPaddingX = 0.08f;
        [SerializeField] private float bodyYOffset = 0.12f;
        [SerializeField] private float iconYOffset = 0.12f;
        [SerializeField] private int sortingOrder = 150;
        [SerializeField] private bool showLabel = false;

        private Transform target;
        private Camera mainCamera;
        private SpriteRenderer tailRenderer;
        private SpriteRenderer bodyRenderer;
        private readonly List<SpriteRenderer> iconRenderers = new List<SpriteRenderer>();
        private TextMeshPro labelText;
        private static Sprite whitePixelSprite;

        private void Awake()
        {
            EnsureVisuals();
            mainCamera = Camera.main;
        }

        public void Setup(Transform followTarget, Sprite bubbleSprite, IReadOnlyList<BubbleIconData> icons, string label)
        {
            target = followTarget;
            EnsureVisuals();

            int iconCount = icons != null ? icons.Count : 0;

            tailRenderer.sprite = bubbleSprite;
            tailRenderer.sortingOrder = sortingOrder;
            tailRenderer.transform.localScale = Vector3.one * tailScale;
            tailRenderer.gameObject.SetActive(tailRenderer.sprite != null);

            float iconSpan = iconCount > 0
                ? (iconCount * iconScale) + ((iconCount - 1) * iconSpacing)
                : 0f;
            float bodyWidth = Mathf.Max(bodyMinWidth, iconSpan + (bodyPaddingX * 2f));
            bodyRenderer.sortingOrder = sortingOrder;
            bodyRenderer.transform.localPosition = new Vector3(0f, bodyYOffset, 0f);
            bodyRenderer.transform.localScale = new Vector3(bodyWidth, bodyHeight, 1f);
            bodyRenderer.gameObject.SetActive(iconCount > 0);

            EnsureIconRenderers(iconCount);
            for (int i = 0; i < iconRenderers.Count; i++)
            {
                var renderer = iconRenderers[i];
                bool active = i < iconCount;
                renderer.gameObject.SetActive(active);
                if (!active) continue;

                var iconData = icons[i];
                renderer.sprite = iconData.Icon;
                renderer.color = iconData.Tint;
                renderer.sortingOrder = sortingOrder + 1;

                float centeredIndex = i - ((iconCount - 1) * 0.5f);
                float x = centeredIndex * (iconScale + iconSpacing);
                renderer.transform.localPosition = new Vector3(x, iconYOffset, 0f);
                renderer.transform.localScale = Vector3.one * iconScale;
            }

            if (labelText != null)
            {
                labelText.text = label ?? string.Empty;
                labelText.gameObject.SetActive(showLabel && !string.IsNullOrWhiteSpace(label));
            }

            UpdateTransform();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                Destroy(gameObject);
                return;
            }

            if (mainCamera == null || !mainCamera.isActiveAndEnabled)
            {
                mainCamera = Camera.main;
            }

            UpdateTransform();
        }

        private void UpdateTransform()
        {
            transform.position = target.position + worldOffset;
            if (mainCamera != null)
            {
                transform.LookAt(
                    transform.position + mainCamera.transform.rotation * Vector3.forward,
                    mainCamera.transform.rotation * Vector3.up
                );
            }
        }

        private void EnsureVisuals()
        {
            if (tailRenderer == null)
            {
                var bg = new GameObject("Tail");
                bg.transform.SetParent(transform, false);
                tailRenderer = bg.AddComponent<SpriteRenderer>();
            }

            if (bodyRenderer == null)
            {
                var body = new GameObject("Body");
                body.transform.SetParent(transform, false);
                body.transform.localPosition = new Vector3(0f, bodyYOffset, 0f);
                bodyRenderer = body.AddComponent<SpriteRenderer>();
                bodyRenderer.sprite = GetWhitePixelSprite();
                bodyRenderer.color = new Color(0.09f, 0.1f, 0.15f, 0.9f);
            }

            if (labelText == null)
            {
                var label = new GameObject("Label");
                label.transform.SetParent(transform, false);
                label.transform.localPosition = new Vector3(0f, -0.26f, 0f);
                labelText = label.AddComponent<TextMeshPro>();
                labelText.fontSize = 2.5f;
                labelText.alignment = TextAlignmentOptions.Center;
                labelText.color = Color.white;
            }
        }

        private void EnsureIconRenderers(int required)
        {
            while (iconRenderers.Count < required)
            {
                var icon = new GameObject($"Icon_{iconRenderers.Count}");
                icon.transform.SetParent(transform, false);
                var renderer = icon.AddComponent<SpriteRenderer>();
                iconRenderers.Add(renderer);
            }
        }

        private static Sprite GetWhitePixelSprite()
        {
            if (whitePixelSprite != null) return whitePixelSprite;

            var tex = Texture2D.whiteTexture;
            whitePixelSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            return whitePixelSprite;
        }
    }
}

