using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 마우스 오버 시 버튼 크기를 조절하는 간단한 스크립트
    /// </summary>
    public class UIButtonScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Settings")]
        [SerializeField] private float hoverScale = 1.1f;
        [SerializeField] private float animationDuration = 0.1f;

        private Vector3 originalScale; // 초기 크기
        private Coroutine scaleCoroutine;

        private void Awake()
        {
            originalScale = transform.localScale;
        }

        private void OnEnable()
        {
            // 비활성화되었다가 다시 켜질 때 크기 초기화
            transform.localScale = originalScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            StopActiveCoroutine();
            scaleCoroutine = StartCoroutine(AnimateScale(originalScale * hoverScale));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            StopActiveCoroutine();
            scaleCoroutine = StartCoroutine(AnimateScale(originalScale));
        }

        private void StopActiveCoroutine()
        {
            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
                scaleCoroutine = null;
            }
        }

        private IEnumerator AnimateScale(Vector3 targetScale)
        {
            float elapsedTime = 0f;
            Vector3 startScale = transform.localScale;

            while (elapsedTime < animationDuration)
            {
                elapsedTime += Time.unscaledDeltaTime; // UI 시간 스케일 영향 방지
                float t = elapsedTime / animationDuration;
                
                // Smooth Step (부드러운 보간)
                t = t * t * (3f - 2f * t);

                transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }

            transform.localScale = targetScale;
            scaleCoroutine = null;
        }
    }
}
