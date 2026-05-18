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
        private readonly Dictionary<object, List<GameObject>> _passiveRootsByOwner = new();
        private static readonly object _globalPassiveKey = new();

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

        // 키 없이 호출 시 전역 키 사용 (하위 호환)
        public void ShowPassiveRange(IEnumerable<TileData> tiles, TilePreviewStyle style)
            => ShowPassiveRange(_globalPassiveKey, tiles, style);

        // 소유자 키를 지정하면 해당 소유자 프리뷰만 갱신, 다른 소유자 프리뷰는 유지
        public void ShowPassiveRange(object ownerKey, IEnumerable<TileData> tiles, TilePreviewStyle style)
        {
            ClearPassiveRangeForKey(ownerKey);
            Color color = ResolveColor(style);

            if (!_passiveRootsByOwner.ContainsKey(ownerKey))
                _passiveRootsByOwner[ownerKey] = new List<GameObject>();

            var roots = _passiveRootsByOwner[ownerKey];
            foreach (var tile in tiles)
            {
                if (tile == null) continue;

                var corners = ResolveCorners(tile);
                float perim  = Perimeter(corners);
                float time   = perim / trailSpeed * trailCoverage;

                var root = new GameObject("_PassiveTrail");
                roots.Add(root);

                var ctrl = root.AddComponent<TileTrailController>();
                ctrl.Setup(corners, color, trailSpeed, time, trailWidthHead, trailWidthTail);
            }
        }

        public void HidePassiveRange()
        {
            foreach (var list in _passiveRootsByOwner.Values)
                foreach (var r in list)
                    if (r != null) Destroy(r);
            _passiveRootsByOwner.Clear();
        }

        public void HidePassiveRange(object ownerKey)
        {
            ClearPassiveRangeForKey(ownerKey);
            _passiveRootsByOwner.Remove(ownerKey);
        }

        private void ClearPassiveRangeForKey(object key)
        {
            if (_passiveRootsByOwner.TryGetValue(key, out var list))
            {
                foreach (var r in list)
                    if (r != null) Destroy(r);
                list.Clear();
            }
        }

        // ── 내부 헬퍼 ─────────────────────────────────────────────────

        private Vector3[] ResolveCorners(TileData tile)
        {
            var mf = tile.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds local = mf.sharedMesh.bounds;
                Transform t  = mf.transform;
                float topY   = local.center.y + local.extents.y;
                float ex     = local.extents.x;
                float ez     = local.extents.z;
                Vector3 c    = local.center;

                Vector3[] localCorners =
                {
                    c + new Vector3(-ex, topY - c.y,  ez),
                    c + new Vector3( ex, topY - c.y,  ez),
                    c + new Vector3( ex, topY - c.y, -ez),
                    c + new Vector3(-ex, topY - c.y, -ez),
                };

                var corners = new Vector3[4];
                for (int i = 0; i < 4; i++)
                {
                    corners[i] = t.TransformPoint(localCorners[i]);
                    corners[i] += Vector3.up * trailElevation;
                }
                return corners;
            }

            Vector3 center = tile.Position + Vector3.up * trailElevation;
            const float h = 0.75f;
            return new[]
            {
                center + new Vector3(-h, 0,  h),
                center + new Vector3( h, 0,  h),
                center + new Vector3( h, 0, -h),
                center + new Vector3(-h, 0, -h),
            };
        }

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
            private Vector3[] _corners;
            private float     _speed;

            public void Setup(Vector3[] corners, Color color, float speed, float trailTime, float widthHead, float widthTail)
            {
                _corners = corners;
                _speed   = speed;

                var pivot = new GameObject("_Pivot");
                pivot.transform.SetParent(transform);
                pivot.transform.position = corners[0];

                var trail = pivot.AddComponent<TrailRenderer>();
                ConfigureTrail(trail, color, trailTime, widthHead, widthTail);

                StartCoroutine(MoveLoop(pivot.transform));
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
