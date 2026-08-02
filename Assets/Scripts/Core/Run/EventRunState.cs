using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DiceOrbit.Data.Tile;
using DiceOrbit.Core.Run.Save;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// 이벤트 노드의 런 수명 상태 (스펙 2026-07-30 §3.6~3.7).
    /// ① 타일 설치 예약 — 이벤트에서 Enqueue, 매 전투 시작(OnCombatStart)마다 무작위 타일에 배치 (런 내내 누적).
    /// ② 본 이벤트 기록 — 같은 이벤트가 한 런에 다시 안 나오게 (풀 소진 시 리셋).
    /// ArtifactManager식 씬 로컬 싱글톤 — 런 종료(EndRun)와 함께 ClearAll. 세이브 참가자.
    /// </summary>
    public class EventRunState : MonoBehaviour, IRunSaveParticipant
    {
        public static EventRunState Instance { get; private set; }

        private readonly List<TileAttributeType> tileInstalls = new List<TileAttributeType>();
        private readonly HashSet<string> seenEvents = new HashSet<string>();

        public IReadOnlyList<TileAttributeType> TileInstallTypes => tileInstalls;
        public IReadOnlyCollection<string> SeenEventNames => seenEvents;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_hookedCombat != null) _hookedCombat.OnCombatStart -= PlaceAll;
        }

        public static EventRunState EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<EventRunState>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            return new GameObject("EventRunState").AddComponent<EventRunState>();
        }

        // ── 타일 설치 예약 ────────────────────────────────────

        private CombatManager _hookedCombat;

        /// <summary>전투 시작 구독 보장 — CombatManager가 바뀌어도 재구독 (PotionManager 훅 패턴).</summary>
        private void EnsureCombatHook()
        {
            var cm = CombatManager.Instance;
            if (cm == null || _hookedCombat == cm) return;
            if (_hookedCombat != null) _hookedCombat.OnCombatStart -= PlaceAll;
            cm.OnCombatStart += PlaceAll;
            _hookedCombat = cm;
        }

        private void Start() => EnsureCombatHook();

        public void EnqueueTileInstall(TileAttributeType type)
        {
            tileInstalls.Add(type);
            EnsureCombatHook();
            Debug.Log($"[EventRunState] 타일 설치 예약: {type} (총 {tileInstalls.Count}건)");
        }

        /// <summary>예약 전부를 무작위 타일에 배치. 셔플 순회로 한 전투 내 같은 타일 중복 회피
        /// (예약 수 > 타일 수면 순환해 중복 허용 — 스펙 §3.6).</summary>
        private void PlaceAll()
        {
            if (tileInstalls.Count == 0) return;
            var orbit = FindAnyObjectByType<OrbitManager>();
            var pool = orbit != null ? orbit.Tiles.Where(t => t != null).OrderBy(_ => Random.value).ToList() : null;
            if (pool == null || pool.Count == 0) return;

            for (int i = 0; i < tileInstalls.Count; i++)
                pool[i % pool.Count].AddAttribute(CreateAttribute(tileInstalls[i]));
            Debug.Log($"[EventRunState] 이벤트 타일 {tileInstalls.Count}건 배치");
        }

        private static TileAttribute CreateAttribute(TileAttributeType type) => type switch
        {
            TileAttributeType.Sharp => new SharpTile(),
            TileAttributeType.Dull => new DullTile(),
            TileAttributeType.Sturdy => new SturdyTile(),
            TileAttributeType.Harmony => new HarmonyTile(),
            TileAttributeType.Disharmony => new DisharmonyTile(),
            _ => new TileAttribute(type, 0, -1),
        };

        // ── 본 이벤트 기록 ────────────────────────────────────

        public bool HasSeen(string eventName) => seenEvents.Contains(eventName);
        public void MarkSeen(string eventName) { if (!string.IsNullOrEmpty(eventName)) seenEvents.Add(eventName); }
        public void ResetSeen() => seenEvents.Clear();

        // ── 런 수명 ──────────────────────────────────────────

        public void ClearAll()
        {
            tileInstalls.Clear();
            seenEvents.Clear();
        }

        /// <summary>세이브 복원 (참가자 Apply / 레거시 경로 공용).</summary>
        public void RestoreFrom(List<int> installTypes, List<string> seenNames)
        {
            ClearAll();
            if (installTypes != null)
                foreach (int t in installTypes) tileInstalls.Add((TileAttributeType)t);
            if (seenNames != null)
                foreach (var n in seenNames) seenEvents.Add(n);
            EnsureCombatHook();
        }

        // ── 세이브 참가자 ──────────────────────────────────────

        public void Capture(RunSaveData data)
        {
            data.EventState.TileInstalls = tileInstalls.Select(t => (int)t).ToList();
            data.EventState.SeenEvents = seenEvents.ToList();
        }

        /// <summary>타일 타입(enum)·이벤트 이름은 카탈로그 조회가 없어 항상 유효 — 검증 불필요.</summary>
        public void Validate(RunSaveData data, RunRestoreContext ctx) { }

        public void Apply(RunSaveData data, RunRestoreContext ctx)
        {
            var ev = data.EventState;
            RestoreFrom(ev != null ? ev.TileInstalls : null, ev != null ? ev.SeenEvents : null);
        }
    }
}
