using System.Collections;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 몬스터 조회(호버/핀) 시 그 몬스터가 공격할 타일을 살짝 띄워 강조.
    /// 리프트 본체는 TileLift 공용 헬퍼 (고스트 방식 — TileData 트랜스폼 무변경,
    /// 색 오버레이/속성 아이콘 동승 포함).
    ///
    /// 시각 채널: 움직임(리프트) = 포커스 강조 전용.
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
        private readonly List<TileLift.LiftHandle> _lifted = new();

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
            {
                var handle = TileLift.Lift(tile, transform);
                if (handle == null) continue;   // 렌더러 없음 or 다른 리프트가 소유 중
                _lifted.Add(handle);
                StartCoroutine(AnimateLift(handle));
            }
        }

        public void Hide()
        {
            Clear();
        }

        // ── 내부 ─────────────────────────────────────────────────

        private void Clear()
        {
            StopAllCoroutines();
            foreach (var handle in _lifted)
                handle?.Release();
            _lifted.Clear();
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

        private IEnumerator AnimateLift(TileLift.LiftHandle handle)
        {
            float elapsed = 0f;
            while (elapsed < liftDuration)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / liftDuration);
                k = 1f - (1f - k) * (1f - k);                // ease-out
                handle.SetHeight(liftHeight * k);
                yield return null;
            }
            handle.SetHeight(liftHeight);
        }
    }
}
