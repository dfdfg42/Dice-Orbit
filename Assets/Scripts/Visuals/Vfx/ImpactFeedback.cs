using System.Collections;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>카메라 쉐이크 + 히트스탑 진입점. VfxService가 큐 설정에 따라 호출.</summary>
    public class ImpactFeedback : MonoBehaviour
    {
        public static ImpactFeedback Instance { get; private set; }

        private Coroutine hitStopRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            var existing = FindAnyObjectByType<ImpactFeedback>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return; }
            new GameObject("ImpactFeedback").AddComponent<ImpactFeedback>();
        }

        public static void Shake(float amplitude, float duration)
        {
            if (CameraShaker.Instance != null) CameraShaker.Instance.Shake(amplitude, duration);
        }

        public static void HitStop(float seconds)
        {
            if (seconds <= 0f) return;
            EnsureInstance();
            if (Instance == null) return;
            if (Instance.hitStopRoutine != null) Instance.StopCoroutine(Instance.hitStopRoutine);
            Instance.hitStopRoutine = Instance.StartCoroutine(Instance.HitStopRoutine(seconds));
        }

        private IEnumerator HitStopRoutine(float seconds)
        {
            float prev = Time.timeScale;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = prev == 0f ? 1f : prev;   // 중첩 대비 0 복원 방지
            hitStopRoutine = null;
        }
    }
}
