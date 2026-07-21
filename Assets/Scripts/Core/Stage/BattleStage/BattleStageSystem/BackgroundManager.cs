using UnityEngine;
using DiceOrbit.Data.Waves;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 배틀 스테이지 배경 관리자. 카메라 정면에 배경 스프라이트를 깔아준다.
    /// </summary>
    public class BackgroundManager : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private SpriteRenderer backgroundRenderer;
        [SerializeField] private Camera targetCamera;

        [Header("Settings")]
        [SerializeField] private float distanceFromCamera = 50f;
        [SerializeField] private int sortingOrder = -100;

        [Tooltip("화면 높이 대비 비율. (0,0.1)이면 위로 화면의 10% 만큼 올라감.")]
        [SerializeField] private Vector2 viewportOffset = Vector2.zero;

        private void Start()
        {
            if (backgroundRenderer == null)
                backgroundRenderer = GetComponentInChildren<SpriteRenderer>();

            if (targetCamera == null)
                targetCamera = Camera.main;

            if (backgroundRenderer != null)
                backgroundRenderer.sortingOrder = sortingOrder;

            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.OnCombatStart += OnCombatStart;

                if (CombatManager.Instance.InCombat)
                    OnCombatStart();
            }
        }

        private void OnDestroy()
        {
            if (CombatManager.Instance != null)
                CombatManager.Instance.OnCombatStart -= OnCombatStart;
        }

        private void LateUpdate()
        {
            if (backgroundRenderer == null || targetCamera == null) return;

            float viewHeight = GetViewHeight();
            float viewWidth = viewHeight * targetCamera.aspect;

            var camTransform = targetCamera.transform;
            Vector3 center = camTransform.position + camTransform.forward * distanceFromCamera;
            Vector3 offset = camTransform.right * (viewWidth * viewportOffset.x)
                           + camTransform.up    * (viewHeight * viewportOffset.y);

            backgroundRenderer.transform.SetPositionAndRotation(center + offset, camTransform.rotation);

            FitToCameraView(viewWidth, viewHeight);
        }

        private float GetViewHeight()
        {
            return targetCamera.orthographic
                ? targetCamera.orthographicSize * 2f
                : 2f * distanceFromCamera * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        private void FitToCameraView(float viewWidth, float viewHeight)
        {
            var sprite = backgroundRenderer.sprite;
            if (sprite == null) return;

            var spriteSize = sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;

            float scale = Mathf.Max(viewWidth / spriteSize.x, viewHeight / spriteSize.y);
            backgroundRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>배경 결정: 몹 세트 오버라이드 → 막 기본 (스펙 2026-07-21 §4).</summary>
        private void OnCombatStart()
        {
            var encounter = CombatManager.Instance != null ? CombatManager.Instance.CurrentEncounter : null;
            Sprite sprite = encounter != null ? encounter.BackgroundSprite : null;
            if (sprite == null)
                sprite = Run.RunManager.Instance?.CurrentAct?.DefaultBackground;
            if (sprite != null)
                SetBackground(sprite);
        }

        public void SetBackground(Sprite sprite)
        {
            if (backgroundRenderer == null) return;
            backgroundRenderer.sprite = sprite;
        }
    }
}
