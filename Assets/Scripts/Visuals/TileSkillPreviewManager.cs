using System.Collections;
using System.Collections.Generic;
using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    public enum TilePreviewStyle { Damage, Heal, Buff, Neutral }

    /// <summary>
    /// 타일 대상 스킬 미리보기: 사각형 외곽선을 따라 트레일이 이동하는 효과.
    /// ShowPreview() / HidePreview() 로 제어합니다.
    /// </summary>
    public class TileSkillPreviewManager : MonoBehaviour
    {
        public static TileSkillPreviewManager Instance { get; private set; }

        [Header("트레일 이동")]
        [SerializeField] private float trailSpeed    = 8f;    // 외곽선 이동 속도 (units/sec)
        [SerializeField] private float trailCoverage = 0.35f; // 한 번에 보이는 외곽선 비율 (0~1)
        [SerializeField] private float trailElevation = 0.14f;

        [Header("트레일 모양")]
        [SerializeField] private float trailWidthHead = 0.10f;
        [SerializeField] private float trailWidthTail = 0.02f;

        [Header("스타일별 색상")]
        [SerializeField] private Color damageColor  = new Color(1f,   0.25f, 0.1f,  1f);
        [SerializeField] private Color healColor    = new Color(0.2f, 1f,   0.35f, 1f);
        [SerializeField] private Color buffColor    = new Color(0.3f, 0.7f, 1f,    1f);
        [SerializeField] private Color neutralColor = new Color(1f,   0.95f, 0.3f, 1f);

        private readonly List<GameObject> _roots = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            new GameObject("[TileSkillPreviewManager]").AddComponent<TileSkillPreviewManager>();
        }

        // ── 공개 API ──────────────────────────────────────────────────

        public void ShowPreview(IEnumerable<TileData> tiles, TilePreviewStyle style)
        {
            HidePreview();
            Color color = ResolveColor(style);

            foreach (var tile in tiles)
            {
                if (tile == null) continue;

                var corners   = ResolveCorners(tile);
                float perim   = Perimeter(corners);
                float time    = perim / trailSpeed * trailCoverage;

                var root = new GameObject("_TileTrail");
                _roots.Add(root);

                var ctrl = root.AddComponent<TileTrailController>();
                ctrl.Setup(corners, color, trailSpeed, time, trailWidthHead, trailWidthTail);
            }
        }

        public void HidePreview()
        {
            foreach (var r in _roots)
                if (r != null) Destroy(r);
            _roots.Clear();
        }

        // (구 ShowPassiveRange/HidePassiveRange 상시 트레일 API는 철거됨 — 2026-07.
        //  패시브 범위는 PassiveRangeIndicator가 조회 시 모서리 브래킷으로 표시한다.)

        // ── 내부 헬퍼 ─────────────────────────────────────────────────

        private Vector3[] ResolveCorners(TileData tile)
            => TileCornerResolver.ResolveTopCorners(tile, trailElevation);

        private static float Perimeter(Vector3[] corners)
        {
            float total = 0f;
            for (int i = 0; i < corners.Length; i++)
                total += Vector3.Distance(corners[i], corners[(i + 1) % corners.Length]);
            return total;
        }

        private Color ResolveColor(TilePreviewStyle style) => style switch
        {
            TilePreviewStyle.Damage  => damageColor,
            TilePreviewStyle.Heal    => healColor,
            TilePreviewStyle.Buff    => buffColor,
            _                        => neutralColor,
        };

        // ── 타일별 트레일 컨트롤러 ────────────────────────────────────

        private sealed class TileTrailController : MonoBehaviour
        {
            private Vector3[]     _corners;
            private float         _speed;
            private TrailRenderer _trail;

            public void Setup(Vector3[] corners, Color color, float speed, float trailTime, float widthHead, float widthTail)
            {
                _corners = corners;
                _speed   = speed;

                var pivot = new GameObject("_Pivot");
                pivot.transform.SetParent(transform);
                pivot.transform.position = corners[0];

                _trail = pivot.AddComponent<TrailRenderer>();
                ConfigureTrail(_trail, color, trailTime, widthHead, widthTail);

                StartCoroutine(MoveLoop(pivot.transform));
            }

            private void OnDestroy()
            {
                if (_trail != null && _trail.material != null)
                    Destroy(_trail.material);
            }

            private static void ConfigureTrail(TrailRenderer trail, Color color, float time, float widthHead, float widthTail)
            {
                trail.material = new Material(Shader.Find("Sprites/Default"));
                trail.time     = time;
                trail.minVertexDistance  = 0.015f;
                trail.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows     = false;
                trail.generateLightingData = false;

                // 너비: 헤드(1) → 테일(0)
                trail.widthCurve = new AnimationCurve(
                    new Keyframe(0f, widthTail),
                    new Keyframe(1f, widthHead)
                );

                // 색상: 테일 투명 → 헤드 불투명
                var gradient = new Gradient();
                gradient.SetKeys(
                    new GradientColorKey[] { new(color, 0f), new(color, 1f) },
                    new GradientAlphaKey[] { new(0f, 0f), new(0.55f, 0.45f), new(1f, 1f) }
                );
                trail.colorGradient = gradient;
            }

            private IEnumerator MoveLoop(Transform pivot)
            {
                int   n   = _corners.Length;
                int   seg = Random.Range(0, n);
                float t   = Random.value;

                while (true)
                {
                    Vector3 from   = _corners[seg % n];
                    Vector3 to     = _corners[(seg + 1) % n];
                    float   segLen = Vector3.Distance(from, to);

                    pivot.position = Vector3.Lerp(from, to, t);
                    t += Time.deltaTime * _speed / segLen;

                    if (t >= 1f)
                    {
                        t  -= 1f;
                        seg = (seg + 1) % n;
                    }

                    yield return null;
                }
            }
        }
    }
}
