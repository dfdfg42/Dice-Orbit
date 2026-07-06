using System.Collections.Generic;
using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 이동 프리뷰의 경로 표시: 경유 타일마다 진행 방향 체브론(V자)을 그린다.
    /// 목적지의 회전 트레일(TileSkillPreviewManager)과 함께 사용 —
    /// 트레일 = "어디 도착", 체브론 = "어느 방향으로 어떤 타일을 밟고 가는지".
    /// 경유 타일의 위험(지뢰/꿀 버블 아이콘)이 경로와 함께 읽히는 것이 목적.
    /// 알파 웨이브가 진행 방향으로 흐르며 방향감을 준다.
    /// </summary>
    public class MovePathPreview : MonoBehaviour
    {
        public static MovePathPreview Instance { get; private set; }

        [Header("체브론 모양")]
        [SerializeField] private float chevronSize = 0.5f;
        [SerializeField] private float thickness = 0.07f;
        [SerializeField] private float elevation = 0.15f;
        [SerializeField] private Color color = new Color(1f, 0.95f, 0.3f, 0.9f);   // 트레일 Neutral 색 계열

        [Header("진행 웨이브")]
        [SerializeField] private float pulseSpeed = 2.2f;      // 알파 웨이브 속도
        [SerializeField] private float phasePerTile = 0.9f;    // 타일당 위상 차 (클수록 물결이 또렷)
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.3f;

        private GameObject _container;
        private Material _mat;
        private readonly List<LineRenderer> _chevrons = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _mat = new Material(Shader.Find("Sprites/Default"));
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            new GameObject("[MovePathPreview]").AddComponent<MovePathPreview>();
        }

        // ── 공개 API ──────────────────────────────────────────────

        /// <summary>
        /// 경로(경유 타일 순서, 목적지 포함)를 표시.
        /// 마지막 타일(목적지)에는 체브론을 그리지 않는다 — 트레일 링이 담당.
        /// </summary>
        public void Show(IReadOnlyList<TileData> path)
        {
            Hide();
            if (path == null || path.Count < 2) return;   // 경유가 있어야 체브론 의미가 있음

            _container = new GameObject("_MovePath");
            _container.transform.SetParent(transform, false);

            for (int i = 0; i < path.Count - 1; i++)
            {
                var tile = path[i];
                var next = path[i + 1];
                if (tile == null || next == null) continue;
                CreateChevron(tile, next);
            }
        }

        public void Hide()
        {
            if (_container != null) Destroy(_container);
            _container = null;
            _chevrons.Clear();
        }

        // ── 진행 웨이브 ───────────────────────────────────────────

        private void Update()
        {
            if (_chevrons.Count == 0) return;

            for (int i = 0; i < _chevrons.Count; i++)
            {
                var lr = _chevrons[i];
                if (lr == null) continue;

                // 진행 방향으로 흐르는 알파 웨이브 (앞 타일일수록 위상이 늦음)
                float wave = (Mathf.Sin(Time.unscaledTime * pulseSpeed - i * phasePerTile) + 1f) * 0.5f;
                float alpha = Mathf.Lerp(minAlpha, 1f, wave) * color.a;
                var c = new Color(color.r, color.g, color.b, alpha);
                lr.startColor = c;
                lr.endColor = c;
            }
        }

        // ── 체브론 생성 ───────────────────────────────────────────

        private void CreateChevron(TileData tile, TileData next)
        {
            // 타일 윗면 중심 (코너 평균) + 진행 방향(수평)
            var corners = TileCornerResolver.ResolveTopCorners(tile, elevation);
            Vector3 center = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;

            Vector3 dir = next.Position - tile.Position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;
            dir.Normalize();
            Vector3 perp = Vector3.Cross(Vector3.up, dir);

            // V자: 뒤-왼쪽 → 꼭짓점(진행 방향) → 뒤-오른쪽
            Vector3 tip   = center + dir * (chevronSize * 0.5f);
            Vector3 backL = center - dir * (chevronSize * 0.35f) + perp * (chevronSize * 0.5f);
            Vector3 backR = center - dir * (chevronSize * 0.35f) - perp * (chevronSize * 0.5f);

            var go = new GameObject("_Chevron");
            go.transform.SetParent(_container.transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 3;
            lr.SetPositions(new[] { backL, tip, backR });
            lr.startWidth = thickness;
            lr.endWidth = thickness;
            lr.sharedMaterial = _mat;
            lr.startColor = color;
            lr.endColor = color;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            _chevrons.Add(lr);
        }
    }
}
