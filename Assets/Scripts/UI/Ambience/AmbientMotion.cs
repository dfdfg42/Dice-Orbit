using System;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI.Ambience
{
    /// <summary>
    /// 배경 장식을 제자리에서 살짝 움직인다 — 회전·가로세로 늘이기·밝기를 느린 파형으로 (노드맵 배경, 2026-10-04).
    /// 축은 RectTransform의 pivot이다 (촛불은 심지, 깃펜은 잉크병 입구, 고양이는 바닥).
    /// 채널마다 진폭이 0이면 쉰다. 일시정지(timeScale 0) 중에도 움직이도록 unscaled 시간을 쓴다.
    ///
    /// 배경에서 떠낸 '깃털 조각'에 붙일 때는 진폭이 조각의 여유(margin)를 넘지 않게 한다 — 넘으면 밑의 원본 그림이 비친다.
    /// 조각과 여유는 Tools/make_nodemap_ambience.py가 정한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class AmbientMotion : MonoBehaviour
    {
        [Serializable]
        public struct Wave
        {
            [Tooltip("회전은 도, 배율은 비율(0.04 = ±4%), 알파는 0~1. 0이면 이 채널은 쉰다")]
            public float amplitude;
            [Tooltip("한 번 오가는 데 걸리는 시간(초)")]
            public float period;
            [Tooltip("0 = 고른 사인(숨쉬기), 1 = 불규칙(촛불)")]
            [Range(0f, 1f)] public float jitter;

            public Wave(float amplitude, float period, float jitter)
            {
                this.amplitude = amplitude;
                this.period = period;
                this.jitter = jitter;
            }

            public bool Active => amplitude != 0f && period > 0f;

            /// <summary>-amplitude ~ +amplitude. seed는 물체·채널마다 달라 서로 박자가 어긋난다.</summary>
            public float Evaluate(float time, float seed)
            {
                if (!Active) return 0f;

                float cycle = time / period;
                float sine = Mathf.Sin((cycle + seed) * 2f * Mathf.PI);
                // 펄린은 0.5 근처에 몰려 있어 넓혀서 쓴다
                float noise = Mathf.Clamp((Mathf.PerlinNoise(cycle * 2f + seed * 37f, seed * 11f) - 0.5f) * 3.2f, -1f, 1f);
                return amplitude * Mathf.Lerp(sine, noise, jitter);
            }
        }

        [SerializeField] private Wave rotation;
        [SerializeField] private Wave scaleX;
        [SerializeField] private Wave scaleY;
        [Tooltip("Graphic의 알파를 흔든다 (기준은 씬에 적힌 알파)")]
        [SerializeField] private Wave alpha;

        private Graphic _graphic;
        private float _seed;
        private float _baseAngle;
        private Vector3 _baseScale;
        private float _baseAlpha;

        /// <summary>씬 계층을 만드는 에디터 도구용.</summary>
        public void Configure(Wave rotation, Wave scaleX, Wave scaleY, Wave alpha)
        {
            this.rotation = rotation;
            this.scaleX = scaleX;
            this.scaleY = scaleY;
            this.alpha = alpha;
        }

        private void OnEnable()
        {
            _seed = UnityEngine.Random.value * 10f;
            _baseAngle = transform.localEulerAngles.z;
            _baseScale = transform.localScale;

            _graphic = GetComponent<Graphic>();
            if (alpha.Active && _graphic == null)
            {
                Debug.LogError($"[AmbientMotion] '{name}': 알파를 흔들려면 같은 오브젝트에 Graphic(Image 등)이 있어야 합니다.", this);
                enabled = false;
                return;
            }
            if (_graphic != null) _baseAlpha = _graphic.color.a;
        }

        private void OnDisable()
        {
            // 기준 자세로 되돌린다 — 다시 켜질 때 흔들린 값을 기준으로 삼지 않게
            transform.localRotation = Quaternion.Euler(0f, 0f, _baseAngle);
            transform.localScale = _baseScale;
            if (alpha.Active && _graphic != null) SetAlpha(_baseAlpha);
        }

        private void Update()
        {
            float time = Time.unscaledTime;

            if (rotation.Active)
                transform.localRotation = Quaternion.Euler(0f, 0f, _baseAngle + rotation.Evaluate(time, _seed));

            if (scaleX.Active || scaleY.Active)
                transform.localScale = new Vector3(
                    _baseScale.x * (1f + scaleX.Evaluate(time, _seed + 0.31f)),
                    _baseScale.y * (1f + scaleY.Evaluate(time, _seed + 0.57f)),
                    _baseScale.z);

            if (alpha.Active)
                SetAlpha(Mathf.Clamp01(_baseAlpha + alpha.Evaluate(time, _seed + 0.83f)));
        }

        private void SetAlpha(float value)
        {
            var color = _graphic.color;
            color.a = value;
            _graphic.color = color;
        }
    }
}
