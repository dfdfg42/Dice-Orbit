using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI.Ambience
{
    /// <summary>
    /// 가끔 한 번씩 반짝인다 — 쉬는 동안은 보이지 않고, 반짝일 때 커졌다 작아지며 조금 돈다 (노드맵 배경, 2026-10-04).
    /// 가장 밝을 때의 알파는 씬에 적힌 Graphic의 알파다. 첫 반짝임은 물체마다 어긋나게 시작한다.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public class AmbientTwinkle : MonoBehaviour
    {
        [Tooltip("다음 반짝임까지 쉬는 시간 (초, 최소~최대)")]
        [SerializeField] private Vector2 interval = new Vector2(5f, 12f);
        [Tooltip("한 번 반짝이는 시간 (초)")]
        [SerializeField] private float duration = 0.75f;
        [Tooltip("한 번 반짝이는 동안 도는 각도")]
        [SerializeField] private float spin = 35f;

        private Graphic _graphic;
        private Vector3 _baseScale;
        private float _peakAlpha;
        private float _wait;
        private float _elapsed = -1f;   // 음수 = 쉬는 중

        /// <summary>씬 계층을 만드는 에디터 도구용.</summary>
        public void Configure(Vector2 interval, float duration, float spin)
        {
            this.interval = interval;
            this.duration = duration;
            this.spin = spin;
        }

        private void OnEnable()
        {
            _graphic = GetComponent<Graphic>();
            _baseScale = transform.localScale;
            _peakAlpha = _graphic.color.a;

            _elapsed = -1f;
            _wait = Random.Range(0f, interval.y);
            Apply(0f, 0f);
        }

        private void OnDisable()
        {
            transform.localRotation = Quaternion.identity;
            transform.localScale = _baseScale;
            SetAlpha(_peakAlpha);
        }

        private void Update()
        {
            float delta = Time.unscaledDeltaTime;

            if (_elapsed < 0f)
            {
                _wait -= delta;
                if (_wait > 0f) return;
                _elapsed = 0f;
            }

            _elapsed += delta;
            float progress = duration > 0f ? _elapsed / duration : 1f;
            if (progress >= 1f)
            {
                Apply(0f, 0f);
                _elapsed = -1f;
                _wait = Random.Range(interval.x, interval.y);
                return;
            }

            Apply(Mathf.Sin(progress * Mathf.PI), progress);
        }

        /// <summary>pulse 0 = 안 보임, 1 = 가장 크고 밝음.</summary>
        private void Apply(float pulse, float progress)
        {
            transform.localScale = _baseScale * pulse;
            transform.localRotation = Quaternion.Euler(0f, 0f, spin * progress);
            SetAlpha(_peakAlpha * pulse);
        }

        private void SetAlpha(float value)
        {
            var color = _graphic.color;
            color.a = value;
            _graphic.color = color;
        }
    }
}
