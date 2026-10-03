using System.Collections;
using TMPro;
using UnityEngine;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 코루틴 미니 모션 — 트윈 라이브러리 없이 팝인·페이드·카운트업·도장 (보상 리워크 2026-10-03).
    /// 전부 unscaled time (일시정지·타임스케일과 무관). 끝나면 항상 최종값에 정확히 맞춘다.
    /// </summary>
    public static class UiMotion
    {
        /// <summary>살짝 작은 크기에서 커지며 나타난다.</summary>
        public static IEnumerator PopIn(RectTransform target, CanvasGroup group, float duration = 0.18f, float fromScale = 0.94f)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = EaseOutCubic(Mathf.Clamp01(t / duration));
                target.localScale = Vector3.one * Mathf.LerpUnclamped(fromScale, 1f, k);
                if (group != null) group.alpha = k;
                yield return null;
            }
            target.localScale = Vector3.one;
            if (group != null) group.alpha = 1f;
        }

        public static IEnumerator Fade(CanvasGroup group, float to, float duration)
        {
            float from = group.alpha;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            group.alpha = to;
        }

        /// <summary>숫자가 from에서 to로 올라간다 (예: "+0" → "+50").</summary>
        public static IEnumerator CountUp(TMP_Text label, int from, int to, float duration, string prefix = "+")
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = EaseOutCubic(Mathf.Clamp01(t / duration));
                label.text = prefix + Mathf.RoundToInt(Mathf.Lerp(from, to, k));
                yield return null;
            }
            label.text = prefix + to;
        }

        /// <summary>도장: 크게 나타나 쿵 찍히고, 잠깐 머문 뒤 사라진다.</summary>
        public static IEnumerator Stamp(RectTransform target, CanvasGroup group, float hold = 0.45f)
        {
            const float press = 0.14f, release = 0.12f;
            float t = 0f;
            while (t < press)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / press);
                target.localScale = Vector3.one * Mathf.LerpUnclamped(1.5f, 1f, EaseOutBack(k));
                group.alpha = k;
                yield return null;
            }
            target.localScale = Vector3.one;
            group.alpha = 1f;

            t = 0f;
            while (t < hold) { t += Time.unscaledDeltaTime; yield return null; }

            yield return Fade(group, 0f, release);
        }

        private static float EaseOutCubic(float k) => 1f - Mathf.Pow(1f - k, 3f);

        private static float EaseOutBack(float k)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
        }
    }
}
