using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 메인 화면에서 캐릭터 4명이 각 모서리에서 날아 들어오는 연출
    /// Inspector에서 Image 4개와 Sprite 4개를 직접 할당해주세요.
    /// </summary>
    public class MainMenuCharacterDisplay : MonoBehaviour
    {
        [Header("Character Images (Canvas UI)")]
        [SerializeField] private Image topLeftImage;
        [SerializeField] private Image topRightImage;
        [SerializeField] private Image bottomLeftImage;
        [SerializeField] private Image bottomRightImage;
        // ✔ Sprite는 각 Image 컴포넌트의 Source Image 슬롯에 직접 할당하세요

        [Header("Animation Settings")]
        [SerializeField] private float animDuration = 0.6f;
        [SerializeField] private float delayBetween = 0.08f; // 각 캐릭터 등장 간격

        [Header("Positions (앵커 기준 RectTransform anchoredPosition)")]
        [SerializeField] private Vector2 topLeftStart     = new Vector2(-1300,  750);
        [SerializeField] private Vector2 topLeftLand      = new Vector2( -734,  255);

        [SerializeField] private Vector2 topRightStart    = new Vector2( 1300,  750);
        [SerializeField] private Vector2 topRightLand     = new Vector2(  734,  255);

        [SerializeField] private Vector2 bottomLeftStart  = new Vector2(-1300, -750);
        [SerializeField] private Vector2 bottomLeftLand   = new Vector2( -734, -255);

        [SerializeField] private Vector2 bottomRightStart = new Vector2( 1300, -750);
        [SerializeField] private Vector2 bottomRightLand  = new Vector2(  734, -255);

        private void Start()
        {
            PlayEntranceAnimation();
        }

        /// <summary>
        /// 4캐릭터 등장 애니메이션 시작
        /// </summary>
        public void PlayEntranceAnimation()
        {
            StartCoroutine(RunEntranceSequence());
        }

        private IEnumerator RunEntranceSequence()
        {
            // 모두 시작 위치로 초기화
            SetPosition(topLeftImage,     topLeftStart);
            SetPosition(topRightImage,    topRightStart);
            SetPosition(bottomLeftImage,  bottomLeftStart);
            SetPosition(bottomRightImage, bottomRightStart);

            // 순서대로 날아오기 (약간씩 딜레이로 자연스럽게)
            StartCoroutine(Animate(topLeftImage,     topLeftStart,     topLeftLand,     0f));
            StartCoroutine(Animate(topRightImage,    topRightStart,    topRightLand,    delayBetween));
            StartCoroutine(Animate(bottomLeftImage,  bottomLeftStart,  bottomLeftLand,  delayBetween * 2));
            StartCoroutine(Animate(bottomRightImage, bottomRightStart, bottomRightLand, delayBetween * 3));

            yield return null;
        }

        private void SetPosition(Image img, Vector2 pos)
        {
            if (img == null) return;
            img.rectTransform.anchoredPosition = pos;
        }

        /// <summary>
        /// 단일 캐릭터 이미지를 from → to 로 애니메이션
        /// EaseOutBack: 목표를 살짝 넘었다가 돌아오는 통통 튀는 효과
        /// </summary>
        private IEnumerator Animate(Image img, Vector2 from, Vector2 to, float delay)
        {
            if (img == null) yield break;

            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            float elapsed = 0f;
            img.rectTransform.anchoredPosition = from;

            while (elapsed < animDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / animDuration);
                float easedT = EaseOutBack(t);
                img.rectTransform.anchoredPosition = Vector2.LerpUnclamped(from, to, easedT);
                yield return null;
            }

            img.rectTransform.anchoredPosition = to;
        }

        /// <summary>
        /// EaseOutBack: 목표를 살짝 넘은 뒤 제자리로 돌아오는 Easing
        /// </summary>
        private float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
