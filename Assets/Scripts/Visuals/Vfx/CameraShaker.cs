using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 카메라 흔들림 — 트라우마 모델 (타격감 리워크 2026-10-03).
    /// 타격이 트라우마(0~1)를 더하고, 흔들림 = 최대폭 × 트라우마² × 펄린 노이즈. 트라우마는 선형으로 식는다.
    /// 완전 랜덤 오프셋(구 방식)보다 부드럽고, 연속 타격이 자연스럽게 쌓이며, 약한 타격은 거의 안 흔들리고 강한 타격만 크게 흔들린다.
    ///
    /// 전용 피벗(이 컴포넌트가 붙은 트랜스폼)을 흔든다 — 카메라를 이 피벗의 자식으로 두면 유닛 빌보드(카메라 참조)와 충돌하지 않는다.
    /// 시간은 unscaled — 히트스톱(timeScale 0) 중에도 흔들린다.
    /// </summary>
    public class CameraShaker : MonoBehaviour
    {
        public static CameraShaker Instance { get; private set; }

        [Tooltip("트라우마 1일 때의 최대 이동(피벗 로컬 유닛)")]
        [SerializeField] private float maxOffset = 0.55f;
        [Tooltip("트라우마 1일 때의 최대 롤(도)")]
        [SerializeField] private float maxRollDegrees = 1.2f;
        [Tooltip("초당 식는 트라우마")]
        [SerializeField] private float decayPerSecond = 1.9f;
        [Tooltip("노이즈 진동수 — 클수록 잘게 떤다")]
        [SerializeField] private float frequency = 26f;

        private Vector3 _baseLocalPos;
        private Quaternion _baseLocalRot;
        private float _trauma;
        private float _seed;

        public float Trauma => _trauma;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _baseLocalPos = transform.localPosition;
            _baseLocalRot = transform.localRotation;
            _seed = Random.value * 100f;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>트라우마를 더한다 (누적, 최대 1).</summary>
        public void AddTrauma(float amount)
        {
            if (amount <= 0f) return;
            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        /// <summary>
        /// 큐(VfxCue.shake)용 구 API — amplitude(피벗 유닛)만큼 흔들리도록 트라우마로 환산해 더한다.
        /// duration은 쓰지 않는다 (식는 속도는 decayPerSecond가 정한다).
        /// </summary>
        public void Shake(float amplitude, float duration)
        {
            if (amplitude <= 0f) return;
            AddTrauma(TraumaForAmplitude(amplitude, maxOffset));
        }

        private void LateUpdate()
        {
            if (_trauma <= 0f) return;

            float shake = _trauma * _trauma;
            float t = Time.unscaledTime * frequency;
            float x = (Mathf.PerlinNoise(_seed, t) * 2f - 1f) * maxOffset * shake;
            float y = (Mathf.PerlinNoise(_seed + 17.3f, t) * 2f - 1f) * maxOffset * shake;
            float roll = (Mathf.PerlinNoise(_seed + 41.7f, t) * 2f - 1f) * maxRollDegrees * shake;

            transform.localPosition = _baseLocalPos + new Vector3(x, y, 0f);
            transform.localRotation = _baseLocalRot * Quaternion.Euler(0f, 0f, roll);

            _trauma = Decay(_trauma, decayPerSecond, Time.unscaledDeltaTime);
            if (_trauma <= 0f)
            {
                transform.localPosition = _baseLocalPos;
                transform.localRotation = _baseLocalRot;
            }
        }

        /// <summary>선형 감쇠 (순수 — 자가 테스트).</summary>
        public static float Decay(float trauma, float decayPerSecond, float deltaTime)
            => Mathf.Max(0f, trauma - decayPerSecond * deltaTime);

        /// <summary>흔들림 폭 amplitude를 내는 트라우마 = √(amplitude / 최대폭) (순수 — 자가 테스트).</summary>
        public static float TraumaForAmplitude(float amplitude, float maxOffset)
            => maxOffset <= 0f ? 0f : Mathf.Clamp01(Mathf.Sqrt(Mathf.Max(0f, amplitude) / maxOffset));
    }
}
