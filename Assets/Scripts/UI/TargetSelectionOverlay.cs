using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 타겟 지정 모드 진입 시 반투명 오버레이 제어
    /// </summary>
    public class TargetSelectionOverlay : MonoBehaviour
    {
        [SerializeField] private Image overlayImage;
        [SerializeField] private float fadeDuration = 0.2f;
        [SerializeField] private float targetAlpha = 0.6f;

        public event Action OnOverlayCancelled;

        private Coroutine fadeCoroutine;

        private void Awake()
        {
            if (overlayImage != null)
            {
                var c = overlayImage.color;
                c.a = 0f;
                overlayImage.color = c;
                overlayImage.gameObject.SetActive(false);
            }
        }

        public void Show()
        {
            if (overlayImage == null) return;
            overlayImage.gameObject.SetActive(true);
            StopFade();
            fadeCoroutine = StartCoroutine(FadeTo(targetAlpha));
        }

        public void Hide()
        {
            if (overlayImage == null) return;
            StopFade();
            fadeCoroutine = StartCoroutine(FadeTo(0f, () => overlayImage.gameObject.SetActive(false)));
        }

        // 오버레이 클릭 시 취소 (Button 컴포넌트에서 호출)
        public void OnOverlayClicked()
        {
            OnOverlayCancelled?.Invoke();
        }

        private void StopFade()
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }
        }

        private IEnumerator FadeTo(float targetA, Action onComplete = null)
        {
            float startA = overlayImage.color.a;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                t = t * t * (3f - 2f * t); // SmoothStep

                var c = overlayImage.color;
                c.a = Mathf.Lerp(startA, targetA, t);
                overlayImage.color = c;
                yield return null;
            }

            var final = overlayImage.color;
            final.a = targetA;
            overlayImage.color = final;

            onComplete?.Invoke();
            fadeCoroutine = null;
        }
    }
}
