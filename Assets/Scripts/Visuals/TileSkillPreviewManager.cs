using System.Collections.Generic;
using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    public enum TilePreviewStyle { Damage, Heal, Buff, Neutral }

    /// <summary>
    /// 타일 대상 스킬 타게팅 프리뷰: 스타일 색 면 채움 + 외곽선, 은은한 숨쉬기 펄스.
    /// ShowPreview() / HidePreview() 로 제어합니다.
    ///
    /// 모든 타일이 같은 위상으로 함께 숨쉬어 "이 타일들이 대상"이 한 몸으로 읽힌다
    /// (AllTiles 스킬에서 특히 중요). 면 = 영향 영역, 외곽선 = 경계.
    /// 구 회전 혜성 트레일은 은퇴 — 읽기가 느리고 다중 타일에서 시각 혼란 (2026-07).
    /// </summary>
    public class TileSkillPreviewManager : MonoBehaviour
    {
        public static TileSkillPreviewManager Instance { get; private set; }

        [Header("모양")]
        [SerializeField] private float fillElevation = 0.09f;     // 몬스터 색 오버레이(0.05)보다 위
        [SerializeField] private float outlineThickness = 0.08f;

        [Header("숨쉬기 펄스 (전 타일 동일 위상)")]
        [SerializeField] private float pulsePeriod = 1.8f;
        [SerializeField, Range(0f, 1f)] private float fillAlphaMin = 0.22f;
        [SerializeField, Range(0f, 1f)] private float fillAlphaMax = 0.45f;
        [SerializeField, Range(0f, 1f)] private float outlineAlphaMin = 0.55f;
        [SerializeField, Range(0f, 1f)] private float outlineAlphaMax = 1f;

        [Header("스타일별 색상")]
        [SerializeField] private Color damageColor  = new Color(1f,   0.25f, 0.1f,  1f);
        [SerializeField] private Color healColor    = new Color(0.2f, 1f,   0.35f, 1f);
        [SerializeField] private Color buffColor    = new Color(0.3f, 0.7f, 1f,    1f);
        [SerializeField] private Color neutralColor = new Color(1f,   0.95f, 0.3f, 1f);

        private GameObject _container;
        private Material _mat;
        private MaterialPropertyBlock _mpb;
        private Color _currentColor;
        private readonly List<MeshRenderer> _fills = new();
        private readonly List<LineRenderer> _outlines = new();
        private readonly List<Mesh> _meshes = new();

        private static readonly int ColorProp = Shader.PropertyToID("_Color");

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _mat = new Material(Shader.Find("Sprites/Default"));
            _mpb = new MaterialPropertyBlock();
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
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
            _currentColor = ResolveColor(style);

            _container = new GameObject("_TilePreview");
            _container.transform.SetParent(transform, false);

            foreach (var tile in tiles)
            {
                if (tile == null) continue;
                BuildTilePreview(tile);
            }
        }

        public void HidePreview()
        {
            foreach (var mesh in _meshes)
                if (mesh != null) Destroy(mesh);
            _meshes.Clear();
            _fills.Clear();
            _outlines.Clear();

            if (_container != null) Destroy(_container);
            _container = null;
        }

        // ── 숨쉬기 펄스 ───────────────────────────────────────────────

        private void Update()
        {
            if (_fills.Count == 0 && _outlines.Count == 0) return;

            // 전 타일 동일 위상 — "대상 전체"가 한 몸으로 숨쉼
            float wave = (Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / Mathf.Max(0.1f, pulsePeriod))) + 1f) * 0.5f;

            float fillAlpha = Mathf.Lerp(fillAlphaMin, fillAlphaMax, wave);
            var fillColor = new Color(_currentColor.r, _currentColor.g, _currentColor.b, fillAlpha);
            _mpb.SetColor(ColorProp, fillColor);
            foreach (var mr in _fills)
                if (mr != null) mr.SetPropertyBlock(_mpb);

            float outlineAlpha = Mathf.Lerp(outlineAlphaMin, outlineAlphaMax, wave);
            var outlineColor = new Color(_currentColor.r, _currentColor.g, _currentColor.b, outlineAlpha);
            foreach (var lr in _outlines)
            {
                if (lr == null) continue;
                lr.startColor = outlineColor;
                lr.endColor = outlineColor;
            }
        }

        // ── 생성 ─────────────────────────────────────────────────────

        private void BuildTilePreview(TileData tile)
        {
            var corners = TileCornerResolver.ResolveTopCorners(tile, fillElevation);

            // 면 채움 쿼드 (월드 좌표 메시)
            var fillGo = new GameObject("_Fill");
            fillGo.transform.SetParent(_container.transform, false);

            var mesh = new Mesh { name = "TilePreviewFill" };
            mesh.vertices = corners;
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _meshes.Add(mesh);

            var mf = fillGo.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = fillGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _fills.Add(mr);

            // 외곽선 루프 (면보다 살짝 위)
            var lineGo = new GameObject("_Outline");
            lineGo.transform.SetParent(_container.transform, false);

            var lr = lineGo.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 4;
            lr.loop = true;
            var outlinePts = new Vector3[4];
            for (int i = 0; i < 4; i++)
                outlinePts[i] = corners[i] + Vector3.up * 0.01f;
            lr.SetPositions(outlinePts);
            lr.startWidth = outlineThickness;
            lr.endWidth = outlineThickness;
            lr.sharedMaterial = _mat;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            _outlines.Add(lr);
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
