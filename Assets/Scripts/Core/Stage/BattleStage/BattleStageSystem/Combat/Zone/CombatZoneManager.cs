using System.Collections.Generic;
using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Core.Zones
{
    /// <summary>
    /// 전투 구역(사분면) 단일 권위. 궤도를 zoneCount개 부채꼴로 나누고 구역마다 몬스터 1마리를 소유자로 둔다.
    /// "어느 구역에 서 있는가"가 곧 "누구를 때리는가"이므로 구역 질의는 전부 여기로 모은다.
    ///
    /// 구역 분할은 몬스터 수와 무관하게 항상 고정이며, 몬스터가 없거나 죽은 구역은
    /// 주인 없는 '중립지대'로 남는다 — 그곳에 선 캐릭터는 때릴 대상이 없다(2026-08-21 결정).
    /// 소유권은 저장하지 않고 질의 시점에 계산한다 — 사망 이벤트 배선 없이 항상 최신이다.
    /// </summary>
    public class CombatZoneManager : MonoBehaviour
    {
        public static CombatZoneManager Instance { get; private set; }

        [Header("구역 분할")]
        [Tooltip("궤도를 몇 개 구역으로 나눌지. 타일 수가 이 값으로 나누어떨어져야 한다.")]
        [SerializeField] private int zoneCount = 4;
        [Tooltip("구역 경계를 타일 몇 칸만큼 돌릴지. 화면 사분면과 시각적으로 맞추는 용도.")]
        [SerializeField] private int zoneTileOffset = 0;

        // 스폰 시 배정된 원 소유자. 죽어도 유지한다 — 흡수 계산의 기준점이기 때문.
        private readonly Dictionary<int, Monster> _assignedOwners = new Dictionary<int, Monster>();

        public int ZoneCount => Mathf.Max(1, zoneCount);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static CombatZoneManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<CombatZoneManager>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            return new GameObject("[CombatZoneManager]").AddComponent<CombatZoneManager>();
        }

        // ── 등록 ──────────────────────────────────────────────

        public void ClearRegistrations() => _assignedOwners.Clear();

        public void RegisterMonster(Monster monster, int zone)
        {
            if (monster == null) return;
            if (zone < 0 || zone >= ZoneCount)
            {
                Debug.LogError($"[CombatZone] 구역 번호 {zone}가 범위를 벗어남 (0~{ZoneCount - 1}). '{monster.name}' 등록 실패.");
                return;
            }
            if (_assignedOwners.TryGetValue(zone, out var existing) && existing != null)
                Debug.LogError($"[CombatZone] 구역 {zone}에 이미 '{existing.name}'가 있는데 '{monster.name}'가 덮어씀 — 구역당 1마리 규칙 위반.");

            _assignedOwners[zone] = monster;
        }

        // ── 구역 판정 ─────────────────────────────────────────

        private int ResolveTileCount()
        {
            var orbit = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbit == null)
            {
                Debug.LogError("[CombatZone] OrbitManager를 찾을 수 없어 구역을 계산할 수 없다.");
                return 0;
            }
            int count = orbit.TileCount;
            if (count <= 0 || count % ZoneCount != 0)
            {
                Debug.LogError($"[CombatZone] 타일 {count}개는 구역 {ZoneCount}개로 나누어떨어지지 않는다. OrbitManager.tileCount를 {ZoneCount}의 배수로 맞출 것.");
                return 0;
            }
            return count;
        }

        /// <summary>타일이 속한 구역 번호. 계산 불가 시 -1.</summary>
        public int GetZoneOfTile(TileData tile)
        {
            if (tile == null) return -1;
            int tileCount = ResolveTileCount();
            if (tileCount == 0) return -1;

            int shifted = ((tile.TileIndex - zoneTileOffset) % tileCount + tileCount) % tileCount;
            return shifted / (tileCount / ZoneCount);
        }

        /// <summary>캐릭터가 서 있는 구역 번호. 타일 미배치 등으로 판정 불가 시 -1.</summary>
        public int GetZoneOf(Character character)
        {
            if (character == null) return -1;
            return GetZoneOfTile(character.CurrentTile);
        }

        /// <summary>
        /// 이 구역의 주인 몬스터. 배정된 몬스터가 없거나 죽었으면 null —
        /// 옆 구역이 흡수하지 않고 중립지대로 남는다(딜이 나가지 않는 피난처).
        /// </summary>
        public Monster GetOwner(int zone)
        {
            if (zone < 0 || zone >= ZoneCount) return null;
            if (!_assignedOwners.TryGetValue(zone, out var monster)) return null;
            if (monster == null || !monster.IsAlive) return null;
            return monster;
        }

        // ── 구역 기하 ─────────────────────────────────────────

        /// <summary>구역이 차지하는 각도 범위(도). 타일 중심이 구역 안에 들어오도록 반 칸 밀어 둔다.</summary>
        public void GetZoneAngularRangeDeg(int zone, out float startDeg, out float endDeg)
        {
            int tileCount = ResolveTileCount();
            float halfTileStep = tileCount > 0 ? (360f / tileCount) * 0.5f : 0f;
            float span = 360f / ZoneCount;
            float shift = zoneTileOffset * (tileCount > 0 ? 360f / tileCount : 0f);

            startDeg = zone * span - halfTileStep + shift;
            endDeg = startDeg + span;
        }

        /// <summary>구역 중심 방향으로 radius만큼 떨어진 월드 좌표 (몬스터 배치용).</summary>
        public Vector3 GetZoneCenterPosition(int zone, float radius)
        {
            GetZoneAngularRangeDeg(zone, out float startDeg, out float endDeg);
            float centerRad = (startDeg + endDeg) * 0.5f * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(centerRad) * radius, 0f, Mathf.Sin(centerRad) * radius);
        }
    }
}
