using System.Collections;
using System.Collections.Generic;
using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    public enum TilePreviewStyle { Damage, Heal, Buff, Neutral }

    /// <summary>
    /// 타일 대상 스킬 사용 전, 타겟 타일에 외곽선 아우라 미리보기를 표시합니다.
    /// ShowPreview() / HidePreview() 로 제어하며 스킬 취소/확정 시 자동 정리됩니다.
    /// </summary>
    public class TileSkillPreviewManager : MonoBehaviour
    {
        public static TileSkillPreviewManager Instance { get; private set; }

        [Header("외곽선")]
        [SerializeField] private float outlineElevation = 0.12f;
        [SerializeField] private float outlineWidth     = 0.07f;

        [Header("펄스 애니메이션")]
        [SerializeField] private float pulseSpeed    = 3f;
        [SerializeField] private float pulseMinAlpha = 0.45f;

        [Header("스타일별 색상")]
        [SerializeField] private Color damageColor  = new Color(1f,   0.25f, 0.1f,  1f);
        [SerializeField] private Color healColor    = new Color(0.2f, 1f,   0.35f, 1f);
        [SerializeField] private Color buffColor    = new Color(0.3f, 0.7f, 1f,    1f);
        [SerializeField] private Color neutralColor = new Color(1f,   0.95f, 0.3f, 1f);

        private readonly List<LineRenderer> _outlines = new List<LineRenderer>();
        private Coroutine                   _pulse;
        private Color                       _baseColor;

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

        /// <summary>타일 목록에 외곽선 아우라를 표시합니다.</summary>
        public void ShowPreview(IEnumerable<TileData> tiles, TilePreviewStyle style)
        {
            HidePreview();
            _baseColor = ResolveColor(style);

            foreach (var tile in tiles)
            {
                if (tile == null) continue;
                _outlines.Add(BuildOutline(tile));
            }

            if (_outlines.Count > 0)
                _pulse = StartCoroutine(PulseLoop());
        }

        /// <summary>모든 타일 미리보기를 제거합니다.</summary>
        public void HidePreview()
        {
            if (_pulse != null) { StopCoroutine(_pulse); _pulse = null; }

            foreach (var lr in _outlines)
                if (lr != null) Destroy(lr.gameObject);
            _outlines.Clear();
        }

        // ── 외곽선 생성 ───────────────────────────────────────────────

        private LineRenderer BuildOutline(TileData tile)
        {
            var go = new GameObject("_TileOutline");
            var lr = go.AddComponent<LineRenderer>();

            var mat = new Material(Shader.Find("Sprites/Default"));
            lr.material              = mat;
            lr.useWorldSpace         = true;
            lr.loop                  = true;
            lr.startWidth            = outlineWidth;
            lr.endWidth              = outlineWidth;
            lr.shadowCastingMode     = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows        = false;

            var corners = ResolveCorners(tile);
            lr.positionCount = corners.Length;
            lr.SetPositions(corners);

            return lr;
        }

        private Vector3[] ResolveCorners(TileData tile)
        {
            var rend = tile.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                Bounds b = rend.bounds;
                float  y = b.max.y + outlineElevation;
                return new[]
                {
                    new Vector3(b.min.x, y, b.min.z),
                    new Vector3(b.max.x, y, b.min.z),
                    new Vector3(b.max.x, y, b.max.z),
                    new Vector3(b.min.x, y, b.max.z),
                };
            }

            // 렌더러가 없는 경우 타일 위치 기준 fallback
            Vector3 c = tile.Position + Vector3.up * outlineElevation;
            const float h = 0.75f;
            return new[]
            {
                c + new Vector3(-h, 0,  h),
                c + new Vector3( h, 0,  h),
                c + new Vector3( h, 0, -h),
                c + new Vector3(-h, 0, -h),
            };
        }

        // ── 펄스 루프 ─────────────────────────────────────────────────

        private IEnumerator PulseLoop()
        {
            while (true)
            {
                float t     = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f; // 0 ~ 1
                float alpha = Mathf.Lerp(pulseMinAlpha, 1f, t);
                Color c     = _baseColor;
                c.a = alpha;

                foreach (var lr in _outlines)
                {
                    if (lr == null) continue;
                    lr.startColor = c;
                    lr.endColor   = c;
                }
                yield return null;
            }
        }

        private Color ResolveColor(TilePreviewStyle style) => style switch
        {
            TilePreviewStyle.Damage  => damageColor,
            TilePreviewStyle.Heal    => healColor,
            TilePreviewStyle.Buff    => buffColor,
            _                        => neutralColor,
        };
    }
}
