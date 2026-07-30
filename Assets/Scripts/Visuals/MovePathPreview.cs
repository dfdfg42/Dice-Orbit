using System.Collections;
using System.Collections.Generic;
using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 이동 프리뷰: 경유 체브론 + 목적지 소나 핑/리프트.
    ///
    /// ── 시각 언어 ─────────────────────────────────────────────
    /// - 경유 타일: 진행 방향 V자 체브론, 알파 웨이브가 진행 방향으로 흐름
    ///   (경유 타일의 지뢰/꿀 위험이 경로와 함께 읽히는 것이 목적)
    /// - 목적지 타일: 살짝 떠오름(TileLift) + 외곽 링이 퍼지는 소나 핑 반복
    ///   ("여기 서게 된다"는 정착의 언어 — 회전 트레일은 스킬 조준 전용으로 분리)
    /// </summary>
    public class MovePathPreview : MonoBehaviour
    {
        public static MovePathPreview Instance { get; private set; }

        [Header("체브론 모양")]
        [SerializeField] private float chevronSize = 0.5f;
        [SerializeField] private float thickness = 0.12f;
        [SerializeField] private float elevation = 0.15f;
        [SerializeField] private Color color = new Color(1f, 0.95f, 0.3f, 0.9f);   // 트레일 Neutral 색 계열

        [Header("진행 웨이브")]
        [SerializeField] private float pulseSpeed = 2.2f;      // 알파 웨이브 속도
        [SerializeField] private float phasePerTile = 0.9f;    // 타일당 위상 차 (클수록 물결이 또렷)
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.3f;

        [Header("목적지 (소나 핑 + 리프트)")]
        [SerializeField] private float destLiftHeight = 0.3f;
        [SerializeField] private float destLiftDuration = 0.12f;
        [SerializeField] private float pingDuration = 0.8f;    // 링 1회 퍼지는 시간
        [SerializeField] private float pingInterval = 1.1f;    // 핑 반복 주기
        [SerializeField] private float pingScale = 1.25f;      // 링 최대 확장 배율
        [SerializeField] private float pingThickness = 0.15f;

        private GameObject _container;
        private Material _mat;
        private readonly List<LineRenderer> _chevrons = new();
        private TileLift.LiftHandle _destLift;

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

        /// <summary>경로(경유 타일 순서, 목적지 포함)를 표시.</summary>
        public void Show(IReadOnlyList<TileData> path)
        {
            Hide();
            if (path == null || path.Count == 0) return;

            _container = new GameObject("_MovePath");
            _container.transform.SetParent(transform, false);

            // 경유 체브론 (목적지 제외 — 목적지는 핑/리프트 담당)
            for (int i = 0; i < path.Count - 1; i++)
            {
                var tile = path[i];
                var next = path[i + 1];
                if (tile == null || next == null) continue;
                CreateChevron(tile, next);
            }

            // 목적지: 리프트 + 소나 핑
            var dest = path[path.Count - 1];
            if (dest != null)
            {
                _destLift = TileLift.Lift(dest, _container.transform);
                if (_destLift != null) StartCoroutine(AnimateDestLift());
                StartCoroutine(PingLoop(dest));
            }
        }

        public void Hide()
        {
            StopAllCoroutines();
            _destLift?.Release();
            _destLift = null;
            if (_container != null) Destroy(_container);
            _container = null;
            _chevrons.Clear();
        }

        // ── 진행 웨이브 (체브론) ──────────────────────────────────

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

        // ── 목적지 리프트 + 소나 핑 ───────────────────────────────

        private IEnumerator AnimateDestLift()
        {
            float elapsed = 0f;
            while (elapsed < destLiftDuration)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / destLiftDuration);
                k = 1f - (1f - k) * (1f - k);                // ease-out
                _destLift?.SetHeight(destLiftHeight * k);
                yield return null;
            }
            _destLift?.SetHeight(destLiftHeight);
        }

        private IEnumerator PingLoop(TileData dest)
        {
            // 링은 리프트가 끝난 높이 기준으로 그린다 (리프트 실패 시 0)
            float ringElevation = elevation + (_destLift != null ? destLiftHeight : 0f);
            var corners = TileCornerResolver.ResolveTopCorners(dest, ringElevation);
            Vector3 center = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;

            // 링 LineRenderer (사각 루프)
            var go = new GameObject("_DestPing");
            go.transform.SetParent(_container.transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 4;
            lr.loop = true;
            lr.startWidth = pingThickness;
            lr.endWidth = pingThickness;
            lr.sharedMaterial = _mat;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            var scaled = new Vector3[4];
            while (true)
            {
                float elapsed = 0f;
                while (elapsed < pingDuration)
                {
                    elapsed += Time.deltaTime;
                    float k = Mathf.Clamp01(elapsed / pingDuration);

                    // 확장하며 페이드 아웃
                    float s = Mathf.Lerp(1f, pingScale, k);
                    for (int i = 0; i < 4; i++)
                        scaled[i] = center + (corners[i] - center) * s;
                    lr.SetPositions(scaled);

                    float alpha = (1f - k) * color.a;
                    var c = new Color(color.r, color.g, color.b, alpha);
                    lr.startColor = c;
                    lr.endColor = c;

                    yield return null;
                }

                // 다음 핑까지 숨김
                var hidden = new Color(color.r, color.g, color.b, 0f);
                lr.startColor = hidden;
                lr.endColor = hidden;
                yield return new WaitForSeconds(Mathf.Max(0f, pingInterval - pingDuration));
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
