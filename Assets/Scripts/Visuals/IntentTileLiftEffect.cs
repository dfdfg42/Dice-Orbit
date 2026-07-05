using System.Collections;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 몬스터 조회(호버/핀) 시 그 몬스터가 공격할 타일을 살짝 띄워 강조.
    ///
    /// 실제 TileData 트랜스폼은 절대 건드리지 않는다 — 캐릭터 배치/이동/코너 계산이
    /// tile.Position을 참조하기 때문. 대신 메시 고스트를 복제해 띄우고
    /// 원본 렌더러를 잠시 끄는 방식으로 "타일이 떠오른" 착시를 만든다.
    ///
    /// 시각 채널: 움직임(리프트) = 위험 포커스 전용.
    /// 아군 패시브 범위(PassiveRangeIndicator 브래킷)와 언어가 겹치지 않는다.
    /// </summary>
    public class IntentTileLiftEffect : MonoBehaviour
    {
        public static IntentTileLiftEffect Instance { get; private set; }

        [Header("리프트 모양")]
        [SerializeField] private float liftHeight = 0.3f;
        [SerializeField] private float liftDuration = 0.12f;   // 올라가는 데 걸리는 시간 (ease-out)

        private Monster _current;
        private readonly HashSet<TileData> _currentTiles = new();
        private readonly List<(MeshRenderer original, GameObject ghost)> _lifted = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            new GameObject("[IntentTileLiftEffect]").AddComponent<IntentTileLiftEffect>();
        }

        // ── 공개 API ──────────────────────────────────────────────

        /// <summary>해당 몬스터의 공격 예정 타일들을 띄운다. 같은 상태면 재생성하지 않음(깜빡임 방지).</summary>
        public void Show(Monster monster)
        {
            var tiles = CollectIntentTiles(monster);

            // 같은 몬스터 + 같은 타일 구성이면 유지 (패널 0.5s 리프레시마다 재립 방지)
            if (ReferenceEquals(_current, monster) && _currentTiles.SetEquals(tiles))
                return;

            Clear();
            _current = monster;
            foreach (var t in tiles) _currentTiles.Add(t);

            foreach (var tile in tiles)
                LiftTile(tile);
        }

        public void Hide()
        {
            Clear();
        }

        // ── 내부 ─────────────────────────────────────────────────

        private void Clear()
        {
            StopAllCoroutines();
            foreach (var (original, ghost) in _lifted)
            {
                if (original != null) original.enabled = true;
                if (ghost != null) Destroy(ghost);
            }
            _lifted.Clear();

            // 색 오버레이도 원위치
            foreach (var tile in _currentTiles)
                if (tile != null) MonsterTileColorOverlayManager.Instance?.SetLiftOffset(tile, 0f);

            _currentTiles.Clear();
            _current = null;
        }

        /// <summary>인텐트의 대상 타일 + 대상 캐릭터가 밟고 있는 타일 수집.</summary>
        private static HashSet<TileData> CollectIntentTiles(Monster m)
        {
            var set = new HashSet<TileData>();
            var intent = m != null ? m.CurrentIntent : null;
            if (intent == null) return set;

            if (intent.TargetTiles != null)
                foreach (var t in intent.TargetTiles)
                    if (t != null) set.Add(t);

            if (intent.Targets != null)
                foreach (var u in intent.Targets)
                    if (u is Character ch && ch.CurrentTile != null) set.Add(ch.CurrentTile);

            return set;
        }

        private void LiftTile(TileData tile)
        {
            var renderer = tile.GetComponentInChildren<MeshRenderer>();
            var filter   = tile.GetComponentInChildren<MeshFilter>();
            if (renderer == null || filter == null || filter.sharedMesh == null) return;

            // 원본 포즈 그대로 고스트 생성
            var ghost = new GameObject("_TileLiftGhost");
            ghost.transform.SetParent(transform, true);
            var src = filter.transform;
            ghost.transform.SetPositionAndRotation(src.position, src.rotation);
            ghost.transform.localScale = src.lossyScale;

            var mf = ghost.AddComponent<MeshFilter>();
            mf.sharedMesh = filter.sharedMesh;
            var mr = ghost.AddComponent<MeshRenderer>();
            mr.sharedMaterials = renderer.sharedMaterials;   // 현재 머티리얼(타입/하이라이트) 그대로
            mr.shadowCastingMode = renderer.shadowCastingMode;

            renderer.enabled = false;                        // 원본은 숨김 (트랜스폼 무변경)
            _lifted.Add((renderer, ghost));

            StartCoroutine(AnimateLift(ghost.transform, src.position, tile));
        }

        private IEnumerator AnimateLift(Transform ghost, Vector3 basePos, TileData tile)
        {
            float elapsed = 0f;
            while (ghost != null && elapsed < liftDuration)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / liftDuration);
                k = 1f - (1f - k) * (1f - k);                // ease-out
                float lift = liftHeight * k;
                ghost.position = basePos + Vector3.up * lift;
                MonsterTileColorOverlayManager.Instance?.SetLiftOffset(tile, lift);   // 색 오버레이 동승
                yield return null;
            }
            if (ghost != null)
            {
                ghost.position = basePos + Vector3.up * liftHeight;
                MonsterTileColorOverlayManager.Instance?.SetLiftOffset(tile, liftHeight);
            }
        }
    }
}
