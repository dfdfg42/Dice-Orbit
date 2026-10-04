using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 몬스터 조회(호버/핀) 시, 그 몬스터가 공격할 타일들의 '둘레 전체'를 정체성 색 외곽선으로 감싼다.
    ///
    /// 아군 패시브 구역(PassiveZoneIndicator, 구역을 통째로 감싸는 하늘색 테두리)과는 별개의 몬스터 위협 전용 채널이다.
    /// (구역 테두리 = 아군 패시브 / 이 타일 둘레 외곽선 = 몬스터가 이번에 칠 타일)
    /// 범위 타일은 IntentTileLiftEffect.CollectIntentTiles(m)와 동일 소스(몬스터 인텐트)를 쓴다.
    /// </summary>
    public class MonsterThreatOutline : MonoBehaviour
    {
        public static MonsterThreatOutline Instance { get; private set; }

        [Header("외곽선")]
        [SerializeField] private float thickness = 0.16f;
        [SerializeField] private float elevation = 0.14f;   // 타일 윗면에서 띄우는 높이(z-fighting 방지)

        private GameObject _container;
        private Material _lineMaterial;   // 모든 외곽선이 공유 (누수 방지)
        private Color _color = Color.red;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _lineMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_lineMaterial != null) Destroy(_lineMaterial);
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            new GameObject("[MonsterThreatOutline]").AddComponent<MonsterThreatOutline>();
        }

        // ── 공개 API ──────────────────────────────────────────────

        // 몬스터 공격 실행 중 강제 표시 플래그 — true인 동안 호버/패널 채널(ShowTiles/Hide)이 덮어쓰지 못한다.
        private bool _forced;

        /// <summary>타일 집합의 각 타일 '둘레 전체'에 지정 색 외곽선 표시 (호버/패널 채널).</summary>
        public void ShowTiles(IEnumerable<TileData> tiles, Color color)
        {
            if (_forced) return;   // 실행 강제 표시가 우선
            RenderTiles(tiles, color);
        }

        public void Hide()
        {
            if (_forced) return;   // 실행 강제 표시가 우선
            Clear();
        }

        /// <summary>몬스터 공격 실행 중 강제 표시. 호버/패널 갱신보다 우선하며 ClearForced까지 유지된다.</summary>
        public void ShowForced(IEnumerable<TileData> tiles, Color color)
        {
            RenderTiles(tiles, color);
            _forced = true;
        }

        /// <summary>강제 표시 해제 (이후 호버/패널 채널이 다시 외곽선을 소유).</summary>
        public void ClearForced()
        {
            _forced = false;
            Clear();
        }

        private void RenderTiles(IEnumerable<TileData> tiles, Color color)
        {
            _color = color;
            Clear();
            if (tiles == null) return;

            var set = new HashSet<TileData>();
            foreach (var t in tiles) if (t != null) set.Add(t);
            if (set.Count == 0) return;

            _container = new GameObject("_ThreatOutlines");
            _container.transform.SetParent(transform, false);
            foreach (var tile in set)
                BuildOutlineForTile(tile);
        }

        // ── 타게팅 중 자동 숨김 (조준 > 정보 우선) ─────────────────

        private void Update()
        {
            if (_container == null) return;

            bool targeting = SkillTargetSelector.Instance != null && SkillTargetSelector.Instance.IsSelectingTarget;
            if (_container.activeSelf == targeting)
                _container.SetActive(!targeting);
        }

        // ── 외곽선 생성 ───────────────────────────────────────────

        private void Clear()
        {
            if (_container != null) Destroy(_container);
            _container = null;
        }

        /// <summary>타일 윗면 4코너를 닫힌 루프로 이어 둘레 외곽선 1개 생성.</summary>
        private void BuildOutlineForTile(TileData tile)
        {
            var corners = TileCornerResolver.ResolveTopCorners(tile, elevation);
            if (corners == null || corners.Length < 4) return;

            var go = new GameObject("_Outline");
            go.transform.SetParent(_container.transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;                              // 둘레 전체를 닫음
            lr.positionCount = 4;
            lr.SetPositions(new[] { corners[0], corners[1], corners[2], corners[3] });
            lr.startWidth = thickness;
            lr.endWidth   = thickness;
            lr.sharedMaterial = _lineMaterial;           // 공유 머티리얼(색은 정점색으로)
            lr.startColor = _color;
            lr.endColor   = _color;
            lr.numCapVertices    = 2;
            lr.numCornerVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows    = false;
        }
    }
}
