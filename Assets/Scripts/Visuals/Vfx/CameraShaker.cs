using System.Collections;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 카메라 흔들림 — 전용 피벗(이 컴포넌트가 붙은 트랜스폼)을 감쇠 랜덤 오프셋으로 흔든다.
    /// 카메라를 이 피벗의 자식으로 두면 유닛 빌보드(카메라 참조)와 충돌 없이 흔들 수 있다.
    /// </summary>
    public class CameraShaker : MonoBehaviour
    {
        public static CameraShaker Instance { get; private set; }

        private Vector3 baseLocalPos;
        private Coroutine running;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            baseLocalPos = transform.localPosition;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Shake(float amplitude, float duration)
        {
            if (amplitude <= 0f || duration <= 0f) return;
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(ShakeRoutine(amplitude, duration));
        }

        private IEnumerator ShakeRoutine(float amplitude, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float falloff = 1f - (elapsed / duration);
                Vector2 rand = Random.insideUnitCircle * amplitude * falloff;
                transform.localPosition = baseLocalPos + new Vector3(rand.x, rand.y, 0f);
                elapsed += Time.unscaledDeltaTime;   // 히트스탑(timeScale=0) 중에도 흔들리도록 unscaled
                yield return null;
            }
            transform.localPosition = baseLocalPos;
            running = null;
        }
    }
}
