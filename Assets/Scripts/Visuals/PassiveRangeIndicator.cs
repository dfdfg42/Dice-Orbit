using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data;
using DiceOrbit.Data.Passives;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 패시브 영향 범위를 타일 모서리 ㄱ자 브래킷으로 표시.
    ///
    /// ── 표시 규칙 (시각 채널 분리) ─────────────────────────────
    /// - 조회 시에만: 정보 패널이 캐릭터를 표시할 때 Show, 아니면 Hide (상시 표시 없음)
    /// - 선(브래킷) 채널 사용 — 타일 면 색은 몬스터 인텐트/타게팅 프리뷰 전용으로 남김
    /// - 타게팅 모드 중엔 자동 숨김 (위험/조준 > 정보 우선순위)
    ///
    /// 구 방식(TileSkillPreviewManager.ShowPassiveRange 상시 회전 트레일)을 대체한다.
    /// 범위 데이터는 IPassiveRangeProvider를 구현한 패시브가 제공.
    /// </summary>
    public class PassiveRangeIndicator : MonoBehaviour
    {
        public static PassiveRangeIndicator Instance { get; private set; }

        [Header("브래킷 모양")]
        [Tooltip("변 길이 대비 ㄱ자 팔 길이 비율 (모서리에서 양쪽 변을 따라 뻗는 길이)")]
        [SerializeField, Range(0.05f, 0.5f)] private float bracketLength = 0.3f;
        [SerializeField] private float thickness = 0.18f;
        [SerializeField] private float elevation = 0.16f;   // 타일 윗면에서 띄우는 높이
        [SerializeField] private Color bracketColor = new Color(0.3f, 0.9f, 1f, 0.9f);

        private Character  _current;
        private GameObject _container;
        private Material   _lineMaterial;   // 모든 브래킷이 공유 (누수 방지)

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _lineMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        private void OnDestroy()
        {
            if (_lineMaterial != null) Destroy(_lineMaterial);
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            new GameObject("[PassiveRangeIndicator]").AddComponent<PassiveRangeIndicator>();
        }

        // ── 공개 API ──────────────────────────────────────────────

        /// <summary>해당 캐릭터의 범위 패시브(IPassiveRangeProvider) 타일들에 브래킷 표시.</summary>
        public void Show(Character character)
        {
            _current = character;
            Rebuild();
        }

        public void Hide()
        {
            _current = null;
            Clear();
        }

        // ── 타게팅 중 자동 숨김 (프레임 단위 즉시 반응) ───────────

        private void Update()
        {
            if (_container == null) return;

            bool targeting = SkillTargetSelector.Instance != null && SkillTargetSelector.Instance.IsSelectingTarget;
            if (_container.activeSelf == targeting)
                _container.SetActive(!targeting);
        }

        // ── 브래킷 생성 ───────────────────────────────────────────

        private void Rebuild()
        {
            Clear();
            if (_current == null || _current.Passives?.ActivePassives == null) return;

            // 캐릭터의 모든 범위 패시브에서 타일 수집 (중복 제거)
            var tiles = new HashSet<TileData>();
            foreach (var p in _current.Passives.ActivePassives)
            {
                if (p is not IPassiveRangeProvider provider) continue;
                var list = provider.GetRangeTiles();
                if (list == null) continue;
                foreach (var t in list)
                    if (t != null) tiles.Add(t);
            }

            if (tiles.Count == 0) return;

            _container = new GameObject("_PassiveBrackets");
            _container.transform.SetParent(transform, false);

            foreach (var tile in tiles)
                BuildBracketsForTile(tile);
        }

        private void Clear()
        {
            if (_container != null) Destroy(_container);
            _container = null;
        }

        /// <summary>타일 1개의 4모서리에 ㄱ자 브래킷 생성.</summary>
        private void BuildBracketsForTile(TileData tile)
        {
            var corners = TileCornerResolver.ResolveTopCorners(tile, elevation);

            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = corners[i];
                Vector3 prev   = corners[(i + 3) % 4];
                Vector3 next   = corners[(i + 1) % 4];

                // 모서리에서 양쪽 변을 따라 bracketLength 비율만큼 뻗는 ㄱ자
                Vector3 armA = corner + (prev - corner) * bracketLength;
                Vector3 armB = corner + (next - corner) * bracketLength;

                CreateBracketLine(new[] { armA, corner, armB });
            }
        }

        private void CreateBracketLine(Vector3[] points)
        {
            var go = new GameObject("_Bracket");
            go.transform.SetParent(_container.transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = points.Length;
            lr.SetPositions(points);
            lr.startWidth = thickness;
            lr.endWidth   = thickness;
            lr.sharedMaterial = _lineMaterial;         // 공유 머티리얼 (인스턴스 생성 안 함)
            lr.startColor = bracketColor;
            lr.endColor   = bracketColor;
            lr.numCapVertices    = 2;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows    = false;
        }
    }
}
