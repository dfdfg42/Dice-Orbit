using UnityEngine;
using TMPro;

namespace DiceOrbit.UI
{
    public class TileAttributeBubbleUI : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.3f, 0f);
        [SerializeField] private float iconScale = 0.3f;
        [SerializeField] private float bgScale = 0.4f;
        [SerializeField] private int sortingOrder = 150;
        [SerializeField] private bool showLabel = true;

        private Transform target;
        private Camera mainCamera;
        private SpriteRenderer bgRenderer;
        private SpriteRenderer iconRenderer;
        private TextMeshPro labelText;

        private void Awake()
        {
            EnsureVisuals();
            mainCamera = Camera.main;
        }

        public void Setup(Transform followTarget, Sprite bubbleSprite, Sprite iconSprite, Color iconTint, string label)
        {
            target = followTarget;
            EnsureVisuals();

            bgRenderer.sprite = bubbleSprite;
            bgRenderer.sortingOrder = sortingOrder;
            bgRenderer.transform.localScale = Vector3.one * bgScale;

            iconRenderer.sprite = iconSprite;
            iconRenderer.color = iconTint;
            iconRenderer.sortingOrder = sortingOrder + 1;
            iconRenderer.transform.localScale = Vector3.one * iconScale;

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
            if (bgRenderer == null)
            {
                var bg = new GameObject("BG");
                bg.transform.SetParent(transform, false);
                bgRenderer = bg.AddComponent<SpriteRenderer>();
            }

            if (iconRenderer == null)
            {
                var icon = new GameObject("Icon");
                icon.transform.SetParent(transform, false);
                icon.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                iconRenderer = icon.AddComponent<SpriteRenderer>();
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
    }
}

