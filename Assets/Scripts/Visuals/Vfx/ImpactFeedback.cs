using System.Collections;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 카메라 쉐이크 + 히트스톱 진입점. VfxService(큐 설정)와 HitDirector(타격 등급)가 호출한다.
    /// 히트스톱은 겹치면 긴 쪽 하나만 — 같은 프레임의 여러 타격이 멈춤을 쌓거나 서로 끊지 않는다 (타격감 리워크 2026-10-03).
    /// </summary>
    public class ImpactFeedback : MonoBehaviour
    {
        public static ImpactFeedback Instance { get; private set; }

        private Coroutine _hitStopRoutine;
        private float _stopEndRealtime;
        private float _resumeTimeScale = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            if (_hitStopRoutine != null) Time.timeScale = _resumeTimeScale;   // 멈춘 채 파괴되면 시간이 멈춘 채로 남는다
            Instance = null;
        }

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

        /// <summary>seconds(realtime) 동안 게임 시간을 멈춘다. 이미 멈춰 있으면 더 늦게 끝나는 쪽으로만 늘어난다.</summary>
        public static void HitStop(float seconds)
        {
            if (seconds <= 0f) return;
            EnsureInstance();
            if (Instance == null) return;
            Instance.RequestStop(seconds);
        }

        private void RequestStop(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            if (_hitStopRoutine != null)
            {
                if (end > _stopEndRealtime) _stopEndRealtime = end;
                return;
            }

            _stopEndRealtime = end;
            _hitStopRoutine = StartCoroutine(HitStopRoutine());
        }

        private IEnumerator HitStopRoutine()
        {
            _resumeTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            while (Time.realtimeSinceStartup < _stopEndRealtime) yield return null;
            Time.timeScale = _resumeTimeScale;
            _hitStopRoutine = null;
        }
    }
}
