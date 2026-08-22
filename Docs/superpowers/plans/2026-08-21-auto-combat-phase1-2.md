# 전투 개편 Phase 1–2 구현 계획 — 사분면 구역 + 자동 기본공격

> **작업자 안내:** 이 계획은 Task 단위로 실행한다. 각 Task는 체크박스(`- [ ]`)로 진행을 추적한다.
> 스펙: `Docs/superpowers/specs/2026-08-21-auto-combat-redesign-design.md`
>
> **상태: 완료.** 이 계획의 "딜 = 캐릭터 공격력"이 현재 산식이다.
> 중간에 주사위 눈 × 배율로 바꿔 봤다가(2026-08-22) 낮은 눈 게이트 캐릭터가 약해지는 문제로
> 되돌렸다. Task 4에서 추가한 `CharacterPreset.Attack`이 피해의 기준값이다.

**목표:** 궤도를 4개 구역으로 나누고 구역마다 몬스터 1마리를 배치한 뒤, 캐릭터가 이동을 마치면 자기 구역 몬스터를 자동으로 때리게 만든다. 이 계획이 끝나면 "주사위 배정 → 이동 → 자동 피해"가 실제로 플레이된다.

**아키텍처:** 구역 판정의 단일 권위 `CombatZoneManager`(타일→구역, 유닛→구역, 구역→소유 몬스터)를 새로 만들고, 그 위에 시각화(`ZoneFloorRenderer`)와 자동공격(`AutoAttackSystem`)을 얹는다. 피해는 기존 `CombatPipeline`을 그대로 통과시켜 패시브·상태이상·유물 반응을 공짜로 받는다.

**기술 스택:** Unity 6000.3, C#, 기존 시스템 재사용 — `CombatPipeline`/`AttackContext`, `UnitStats.Attack`(기존 필드, 현재 미사용), `MonsterIdentityManager`(정체성 색), `ActionQueueManager`(행동 큐).

## 이 계획의 범위

**포함:** 스펙 §2(구역), §3(기본공격), §8의 "유지되는 것" 재사용 검증.
**제외 (별도 계획):** §5 위치 패시브 4종, §6 액티브 4종+게이트 UI, 밸런스 재조정.
이유 — Phase 1–2만으로 플레이 가능한 검증 단위가 되고, 이후 설계는 이 단계의 플레이 감각을 보고 정해야 한다.

## Global Constraints

- **`git push` 절대 금지** (공모전 제출 ~2026-08-22). 커밋은 로컬만.
- **폴백 코드 금지.** 값이 없거나 배선이 잘못됐으면 기본값을 지어내지 말고 `Debug.LogError`로 드러낸다. 단, 파괴된 Unity 오브젝트 null 체크는 정상적인 생명주기 처리이므로 예외.
- **확장 지점을 열어 둘 것.** 구역 수·반지름·색 농도는 하드코딩하지 말고 `[SerializeField]` 설계 노브로 노출한다.
- **테스트 프레임워크 없음.** 이 프로젝트에는 Unity Test Framework·asmdef가 없다. 각 Task의 검증은 ① 컴파일 게이트(MCP `Unity_RunCommand`로 `typeof` 참조) ② 작업자가 직접 플레이해 관찰하는 기준으로 한다.
- **커밋 메시지 말미:** `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
- **커밋 제외 대상:** `Assets/Fonts/Pretendard-Regular SDF.asset`, `Assets/GabrielAguiarProductions/.../Materials/*.mat` (에디터 재생성 부수변경).

## 파일 구조

**신규**
| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Zone/CombatZoneManager.cs` | 구역 판정 단일 권위 — 타일→구역, 유닛→구역, 구역→소유 몬스터, 구역 기하(각도·중심 좌표) |
| `Assets/Scripts/Visuals/ZoneFloorRenderer.cs` | 구역 바닥 부채꼴 메쉬를 소유 몬스터 정체성 색으로 칠함 |
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/AutoAttackSystem.cs` | 매 턴 자동 기본공격 — 이동 후 발동 + 턴 종료 시 미발동자 일괄 처리 |

**수정**
| 파일 | 변경 |
|---|---|
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/EncounterSpawner.cs` | 몬스터를 구역 중심에 1마리씩 배치하고 구역 등록 (WaveSpawnPoint 배치 로직 제거) |
| `Assets/Scripts/Core/Stage/BattleStage/Units/Character/CharacterPreset.cs` | `Attack` 필드 추가 + `CreateStats()` 배선 |
| `Assets/Scripts/UI/CharacterActionUI.cs:258` | 이동 큐 등록을 `MoveThenAttackRoutine`으로 교체 |
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/CombatManager.cs:351, 379` | 턴 시작 시 자동공격 기록 초기화, 턴 종료 시 미발동자 일괄 공격 |

---

## Task 1: CombatZoneManager — 구역 판정 권위

**Files:**
- Create: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Zone/CombatZoneManager.cs`

**Interfaces:**
- Consumes: `OrbitManager.TileCount`, `TileData.TileIndex`, `Character.CurrentTile`, `Monster.IsAlive`, `GameManager.Instance.GetOrbitManager()`
- Produces (이후 Task가 의존):
  - `static CombatZoneManager EnsureInstance()`
  - `int ZoneCount { get; }`
  - `void ClearRegistrations()`
  - `void RegisterMonster(Monster monster, int zone)`
  - `int GetZoneOfTile(TileData tile)` — 실패 시 `-1`
  - `int GetZoneOf(Character character)` — 실패 시 `-1`
  - `Monster GetOwner(int zone)` — 그 구역의 생존 주인. 없으면 `null`(중립지대 — 흡수하지 않는다)
  - `Vector3 GetZoneCenterPosition(int zone, float radius)`
  - `void GetZoneAngularRangeDeg(int zone, out float startDeg, out float endDeg)`

- [ ] **Step 1: CombatZoneManager 작성**

```csharp
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

        // 스폰 시 배정된 구역 주인. 죽어도 유지한다 — 이 구역이 '누구의 자리였는지'가 기록이기 때문.
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
```

- [ ] **Step 2: 컴파일 게이트**

MCP `Unity_RunCommand`로 실행 (`AssetDatabase.Refresh()` 후 새 타입 참조):

```csharp
using UnityEngine; using UnityEditor;
internal class CommandScript : IRunCommand {
  public void Execute(ExecutionResult result){
    AssetDatabase.Refresh();
    var t = typeof(DiceOrbit.Core.Zones.CombatZoneManager);
    result.Log("compile OK — {0}", t.Name);
  }
}
```

기대: `compile OK — CombatZoneManager`. 컴파일 에러가 나면 이 Task를 벗어나지 않는다.

- [ ] **Step 3: 로컬 커밋 (푸시 금지)**

```bash
git add "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Zone/CombatZoneManager.cs"
git commit -m "feat(zone): 사분면 구역 판정 권위 CombatZoneManager 추가

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: 몬스터를 구역마다 1마리씩 배치

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/EncounterSpawner.cs`

**Interfaces:**
- Consumes: Task 1의 `CombatZoneManager.EnsureInstance()`, `ClearRegistrations()`, `RegisterMonster()`, `GetZoneCenterPosition()`, `ZoneCount`
- Produces: 스폰 직후 모든 몬스터가 구역에 등록된 상태

- [ ] **Step 1: 스폰 위치를 구역 중심으로 교체**

`EncounterSpawner.cs`의 `fallbackSpawnRadius` 필드를 구역 배치 반지름으로 교체한다.

변경 전:
```csharp
        [SerializeField] private float fallbackSpawnRadius = 2.5f;
```

변경 후:
```csharp
        [Tooltip("몬스터를 구역 중심 방향 이 거리에 배치한다 (궤도 안쪽).")]
        [SerializeField] private float monsterZoneRadius = 4f;
```

`Spawn` 메서드에서 `WaveSpawnPoint` 조회 블록을 삭제한다. 변경 전:
```csharp
            var points = Object.FindObjectsByType<WaveSpawnPoint>(FindObjectsSortMode.None)
                .OrderBy(_ => Random.value).ToList();

            for (int i = 0; i < presets.Count; i++)
            {
                var go = Object.Instantiate(monsterPrefab, GetSpawnPosition(points, i), Quaternion.identity, spawnRoot);
```

변경 후:
```csharp
            var zones = Zones.CombatZoneManager.EnsureInstance();
            zones.ClearRegistrations();

            if (presets.Count > zones.ZoneCount)
                Debug.LogError($"[EncounterSpawner] 몹 {presets.Count}마리는 구역 {zones.ZoneCount}개를 넘는다 — 구역당 1마리 규칙 위반. 몹 세트를 {zones.ZoneCount}마리 이하로 구성할 것.");

            for (int i = 0; i < presets.Count; i++)
            {
                int zone = i % zones.ZoneCount;
                var spawnPos = zones.GetZoneCenterPosition(zone, monsterZoneRadius);
                var go = Object.Instantiate(monsterPrefab, spawnPos, Quaternion.identity, spawnRoot);
```

- [ ] **Step 2: 구역 등록 추가**

`monster.InitializeFromPreset(presets[i]);` 바로 뒤에 등록을 넣는다. 변경 전:
```csharp
                monster.InitializeFromPreset(presets[i]);
                if (startHidden)
```

변경 후:
```csharp
                monster.InitializeFromPreset(presets[i]);
                zones.RegisterMonster(monster, zone);
                if (startHidden)
```

- [ ] **Step 3: 죽은 배치 코드 제거**

`GetSpawnPosition` 메서드 전체(파일 하단 `private Vector3 GetSpawnPosition(...)` 블록)를 삭제한다. 더 이상 호출되지 않으며, 남겨두면 "구역 배치"와 "스폰포인트 배치" 두 규칙이 공존하는 것처럼 읽힌다.

`using DiceOrbit.Data.Waves;`도 `WaveSpawnPoint` 참조가 사라졌으므로 삭제한다. `System.Linq`는 `presets` 필터(`Where`)에서 계속 쓰이므로 남긴다.

- [ ] **Step 4: 컴파일 게이트**

```csharp
using UnityEngine; using UnityEditor;
internal class CommandScript : IRunCommand {
  public void Execute(ExecutionResult result){
    AssetDatabase.Refresh();
    result.Log("compile OK — {0}", typeof(DiceOrbit.Core.EncounterSpawner).Name);
  }
}
```

- [ ] **Step 5: 플레이 검증 (작업자)**

전투에 진입해 확인한다:
- 몬스터가 궤도 안쪽에 **서로 겹치지 않고 사방으로 흩어져** 배치된다 (4마리면 상하좌우 네 방향).
- 콘솔에 `[CombatZone]` / `[EncounterSpawner]` 에러가 없다.

몬스터가 한쪽에 몰리거나 타일과 겹치면 `EncounterSpawner`의 `monsterZoneRadius`를 조정한다 (궤도 반지름 8 기준 3~5 권장).

- [ ] **Step 6: 로컬 커밋 (푸시 금지)**

```bash
git add "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/EncounterSpawner.cs"
git commit -m "feat(zone): 몬스터를 구역마다 1마리씩 배치하고 구역에 등록

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: 구역 바닥색 시각화

**Files:**
- Create: `Assets/Scripts/Visuals/ZoneFloorRenderer.cs`

**Interfaces:**
- Consumes: Task 1의 `CombatZoneManager`(`ZoneCount`, `GetOwner`, `GetZoneAngularRangeDeg`), `MonsterIdentityManager.Instance.GetColor(monster)`
- Produces: `static ZoneFloorRenderer EnsureInstance()` — 씬에 없으면 자기 자신을 만든다. 다른 Task가 호출하지 않으며 스스로 갱신한다.

- [ ] **Step 1: ZoneFloorRenderer 작성**

```csharp
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Zones;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 구역 바닥을 소유 몬스터의 정체성 색 부채꼴로 칠한다.
    /// "이 색 위에 서면 이 몬스터를 때린다"를 색 하나로 읽히게 하는 것이 목적이며,
    /// 몬스터 발밑 원형 마커(MonsterIdentityManager)와 같은 색 체계를 공유한다.
    ///
    /// 소유자 변화는 이벤트 배선 없이 매 프레임 비교로 감지한다 — 구역이 4개뿐이라 비용이 없고,
    /// 사망·흡수 같은 모든 경로를 자동으로 따라간다.
    /// </summary>
    public class ZoneFloorRenderer : MonoBehaviour
    {
        public static ZoneFloorRenderer Instance { get; private set; }

        [Header("부채꼴 크기")]
        [Tooltip("안쪽 반지름 — 몬스터가 서는 중앙부는 비워 둔다")]
        [SerializeField] private float innerRadius = 2.2f;
        [Tooltip("바깥 반지름 — 궤도 타일을 덮을 만큼")]
        [SerializeField] private float outerRadius = 9f;
        [Tooltip("바닥 높이. 타일 윗면보다 살짝 위여야 색이 타일에 얹힌다")]
        [SerializeField] private float floorY = 0.15f;
        [SerializeField] private int segmentsPerZone = 24;

        [Header("색")]
        [Range(0f, 1f)]
        [SerializeField] private float zoneAlpha = 0.13f;
        [Tooltip("몬스터가 없는 중립지대 색. 사분면은 항상 4개가 보이고 빈 구역만 이 색이 된다.")]
        [SerializeField] private Color emptyZoneColor = new Color(0.6f, 0.6f, 0.62f, 1f);
        [Range(0f, 1f)]
        [Tooltip("중립지대 투명도. 소유 구역보다 옅게 둬서 '비어 있음'이 읽히도록.")]
        [SerializeField] private float emptyZoneAlpha = 0.05f;
        [Tooltip("몬스터 스프라이트·타일보다 뒤에 그리기 위한 정렬 순서")]
        [SerializeField] private int sortingOrder = -50;

        private readonly List<MeshRenderer> _renderers = new List<MeshRenderer>();
        // 파괴된 오브젝트는 Unity의 == 비교가 null과 같다고 보고해 변화를 놓치므로 InstanceID로 추적한다.
        private int[] _lastOwnerIds;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static ZoneFloorRenderer EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<ZoneFloorRenderer>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            return new GameObject("[ZoneFloorRenderer]").AddComponent<ZoneFloorRenderer>();
        }

        private void LateUpdate()
        {
            var zones = CombatZoneManager.Instance;
            if (zones == null) return;

            if (_renderers.Count != zones.ZoneCount) BuildMeshes(zones);
            RefreshColors(zones);
        }

        private void BuildMeshes(CombatZoneManager zones)
        {
            foreach (var r in _renderers)
                if (r != null) Destroy(r.gameObject);
            _renderers.Clear();

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");

            for (int zone = 0; zone < zones.ZoneCount; zone++)
            {
                zones.GetZoneAngularRangeDeg(zone, out float startDeg, out float endDeg);

                var go = new GameObject($"_ZoneFloor_{zone}");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(0f, floorY, 0f);

                go.AddComponent<MeshFilter>().sharedMesh =
                    BuildSectorMesh(innerRadius, outerRadius, startDeg, endDeg, Mathf.Max(4, segmentsPerZone));

                var mr = go.AddComponent<MeshRenderer>();
                mr.material = new Material(shader) { mainTexture = Texture2D.whiteTexture };
                mr.sortingOrder = sortingOrder;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                _renderers.Add(mr);
            }

            _lastOwnerIds = new int[zones.ZoneCount];
            for (int i = 0; i < _lastOwnerIds.Length; i++) _lastOwnerIds[i] = int.MinValue;   // 첫 프레임에 반드시 칠하도록
        }

        private void RefreshColors(CombatZoneManager zones)
        {
            var identity = MonsterIdentityManager.Instance;
            if (identity == null) return;

            for (int zone = 0; zone < _renderers.Count; zone++)
            {
                var owner = zones.GetOwner(zone);
                int ownerId = owner != null ? owner.GetInstanceID() : 0;
                if (_lastOwnerIds[zone] == ownerId) continue;   // 변화 없음 — 머티리얼 건드리지 않는다
                _lastOwnerIds[zone] = ownerId;

                var mr = _renderers[zone];
                if (mr == null) continue;

                // 사분면은 항상 4개가 보인다. 주인이 없으면 중립색으로 남겨 '빈 구역'임을 드러낸다.
                Color c = owner != null ? identity.GetColor(owner) : emptyZoneColor;
                c.a = owner != null ? zoneAlpha : emptyZoneAlpha;
                if (mr.material.HasProperty("_Color")) mr.material.color = c;
            }
        }

        /// <summary>XZ 평면 위 도넛 부채꼴(안쪽 반지름~바깥 반지름, 시작각~끝각) 메쉬.</summary>
        private static Mesh BuildSectorMesh(float inner, float outer, float startDeg, float endDeg, int segments)
        {
            var verts = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[verts.Length];
            var tris = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float rad = Mathf.Lerp(startDeg, endDeg, t) * Mathf.Deg2Rad;
                float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);

                verts[i * 2]     = new Vector3(cos * inner, 0f, sin * inner);
                verts[i * 2 + 1] = new Vector3(cos * outer, 0f, sin * outer);
                uv[i * 2]        = new Vector2(t, 0f);
                uv[i * 2 + 1]    = new Vector2(t, 1f);
            }

            for (int i = 0; i < segments; i++)
            {
                int v = i * 2;
                int t = i * 6;
                tris[t]     = v;     tris[t + 1] = v + 1; tris[t + 2] = v + 2;
                tris[t + 3] = v + 1; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
            }

            var mesh = new Mesh { name = "ZoneFloorSector" };
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
```

- [ ] **Step 2: 스폰 직후 렌더러 기동**

`EncounterSpawner.cs`의 `Spawn` 말미, 정체성 색 설정 바로 뒤에 한 줄을 추가한다. 변경 전:
```csharp
            Visuals.MonsterIdentityManager.EnsureInstance();
            Visuals.MonsterIdentityManager.Instance.Setup(spawned);
            return spawned;
```

변경 후:
```csharp
            Visuals.MonsterIdentityManager.EnsureInstance();
            Visuals.MonsterIdentityManager.Instance.Setup(spawned);
            Visuals.ZoneFloorRenderer.EnsureInstance();
            return spawned;
```

- [ ] **Step 3: 컴파일 게이트**

```csharp
using UnityEngine; using UnityEditor;
internal class CommandScript : IRunCommand {
  public void Execute(ExecutionResult result){
    AssetDatabase.Refresh();
    result.Log("compile OK — {0}", typeof(DiceOrbit.Visuals.ZoneFloorRenderer).Name);
  }
}
```

- [ ] **Step 4: 플레이 검증 (작업자)**

전투에 진입해 확인한다:
- 궤도가 **항상 네 개의 부채꼴**로 나뉘어 보이고, 몬스터가 있는 구역은 그 몬스터의 발밑 색과 같은 색이다.
- 몬스터가 없는 구역은 **옅은 중립색**으로 비어 있다 (사분면 자체는 사라지지 않는다).
- 몬스터를 하나 죽이면 그 구역이 **중립색(옅은 회색)으로 비고**, 사분면 네 칸은 그대로 남는다.

색이 너무 진하거나 흐리면 `zoneAlpha`, 타일에 안 얹히면 `floorY`, 부채꼴이 타일을 못 덮으면 `outerRadius`를 조정한다.
부채꼴 경계가 화면 사분면(좌상·우상·좌하·우하)과 어긋나 보이면 `CombatZoneManager`의 `zoneTileOffset`을 1씩 바꿔 맞춘다.

- [ ] **Step 5: 로컬 커밋 (푸시 금지)**

```bash
git add "Assets/Scripts/Visuals/ZoneFloorRenderer.cs" "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/EncounterSpawner.cs"
git commit -m "feat(zone): 구역 바닥을 소유 몬스터 정체성 색 부채꼴로 표시

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: 캐릭터 공격력 스탯 배선

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/Units/Character/CharacterPreset.cs:28, 53-60`
- Modify (에디터 데이터): `Assets/Scripts/Data/Character Preset/{Warrior,Rogue,Mage,Alchemist}/*.asset`

**Interfaces:**
- Consumes: 기존 `UnitStats.Attack`(현재 값이 0으로 방치된 필드)
- Produces: `CharacterPreset.Attack` — Task 5의 자동공격이 읽는 기본 피해값

- [ ] **Step 1: 프리셋에 Attack 필드 추가**

변경 전:
```csharp
        [Header("Base Stats")]
        public int MaxHP = 30;
```

변경 후:
```csharp
        [Header("Base Stats")]
        public int MaxHP = 30;
        [Tooltip("기본 공격력. 매 턴 자동 기본공격의 피해량이며, 위치 패시브·모디파이어가 여기에 더해진다.")]
        public int Attack = 5;
```

- [ ] **Step 2: CreateStats에서 배선**

변경 전:
```csharp
            var stats = new CharacterStats
            {
                CharacterName = this.CharacterName,
                MaxHP         = this.MaxHP,
                CurrentHP     = this.MaxHP
            };
```

변경 후:
```csharp
            var stats = new CharacterStats
            {
                CharacterName = this.CharacterName,
                MaxHP         = this.MaxHP,
                CurrentHP     = this.MaxHP,
                Attack        = this.Attack
            };

            if (Attack <= 0)
                Debug.LogError($"[CharacterPreset] '{CharacterName}'의 Attack이 {Attack}이다 — 자동 기본공격이 피해를 주지 못한다. 프리셋에 공격력을 설정할 것.");
```

- [ ] **Step 3: 컴파일 게이트**

```csharp
using UnityEngine; using UnityEditor;
internal class CommandScript : IRunCommand {
  public void Execute(ExecutionResult result){
    AssetDatabase.Refresh();
    result.Log("compile OK — {0}", typeof(DiceOrbit.Core.CharacterPreset).Name);
  }
}
```

- [ ] **Step 4: 4개 프리셋에 시작 공격력 입력**

MCP `Unity_RunCommand`로 `SerializedObject`를 통해 설정한다 (System.Reflection·System.IO는 사용 불가):

```csharp
using UnityEngine; using UnityEditor; using System.Text;
internal class CommandScript : IRunCommand {
  public void Execute(ExecutionResult result){
    var values = new (string path, int atk)[] {
      ("Assets/Scripts/Data/Character Preset/Warrior/Warrior.asset", 6),
      ("Assets/Scripts/Data/Character Preset/Rogue/Rogue.asset", 4),
      ("Assets/Scripts/Data/Character Preset/Mage/Mage.asset", 5),
      ("Assets/Scripts/Data/Character Preset/Alchemist/Alchemist.asset", 4),
    };
    var sb = new StringBuilder();
    foreach (var v in values) {
      var asset = AssetDatabase.LoadAssetAtPath<DiceOrbit.Core.CharacterPreset>(v.path);
      if (asset == null) { sb.AppendLine("없음: " + v.path); continue; }
      var so = new SerializedObject(asset);
      var prop = so.FindProperty("Attack");
      if (prop == null) { sb.AppendLine("Attack 프로퍼티 없음: " + v.path); continue; }
      prop.intValue = v.atk;
      so.ApplyModifiedProperties();
      EditorUtility.SetDirty(asset);
      sb.AppendLine(asset.name + " Attack = " + v.atk);
    }
    AssetDatabase.SaveAssets();
    result.Log(sb.ToString());
  }
}
```

기대 출력: 4줄 모두 `<이름> Attack = <값>`. "없음"이나 "프로퍼티 없음"이 뜨면 경로·필드명을 확인한다.

값 6/4/5/4는 밸런스 시작점일 뿐이며, Phase 5에서 조정한다.

- [ ] **Step 5: 로컬 커밋 (푸시 금지)**

```bash
git add "Assets/Scripts/Core/Stage/BattleStage/Units/Character/CharacterPreset.cs" "Assets/Scripts/Data/Character Preset"
git commit -m "feat(combat): 캐릭터 프리셋에 기본 공격력 추가 및 스탯 배선

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: AutoAttackSystem — 자동 기본공격

**Files:**
- Create: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/AutoAttackSystem.cs`

**Interfaces:**
- Consumes: Task 1의 `CombatZoneManager`(`GetZoneOf`, `GetOwner`), Task 4의 `CharacterStats.Attack`, 기존 `CombatPipeline.Instance.Process(AttackContext)`, `OrbitManager.MoveRoutine(Character, int)`, `PartyManager.Instance.Party`
- Produces (Task 6·7이 의존):
  - `static AutoAttackSystem EnsureInstance()`
  - `void ResetTurn()`
  - `IEnumerator MoveThenAttackRoutine(Character character, int steps)`
  - `IEnumerator ResolveRemainingRoutine()`

- [ ] **Step 1: AutoAttackSystem 작성**

```csharp
using System.Collections;
using System.Collections.Generic;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Zones;
using UnityEngine;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 자동 기본공격. 캐릭터는 매 턴 반드시 한 번 자기 구역의 몬스터를 때린다 —
    /// 주사위 눈·액티브 사용 여부와 무관하며, 이것이 파티 피해량의 바닥을 보장한다(스펙 §3, 불변식 1).
    ///
    /// 발동 지점은 둘이지만 실제 처리는 ResolveRoutine 한 곳이다:
    ///  ① 이동 직후 (MoveThenAttackRoutine — 플레이어가 결과를 보고 다음 캐릭터를 정할 수 있도록)
    ///  ② 턴 종료 시 아직 안 때린 캐릭터 일괄 (ResolveRemainingRoutine — 주사위를 안 받은 캐릭터도 바닥은 보장)
    /// 중복 발동은 턴별 기록으로 막는다.
    /// </summary>
    public class AutoAttackSystem : MonoBehaviour
    {
        public static AutoAttackSystem Instance { get; private set; }

        [Header("연출")]
        [Tooltip("공격 사이 간격(초). 여러 캐릭터가 연달아 때릴 때 읽히도록.")]
        [SerializeField] private float attackInterval = 0.25f;
        [Tooltip("피해 팝업·로그에 표시할 이름")]
        [SerializeField] private string attackName = "기본 공격";

        private readonly HashSet<Character> _resolvedThisTurn = new HashSet<Character>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static AutoAttackSystem EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<AutoAttackSystem>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            return new GameObject("[AutoAttackSystem]").AddComponent<AutoAttackSystem>();
        }

        /// <summary>플레이어 턴 시작 시 호출 — 이번 턴 발동 기록을 비운다.</summary>
        public void ResetTurn() => _resolvedThisTurn.Clear();

        /// <summary>이동을 끝까지 수행한 뒤 도착 구역의 몬스터를 자동 공격한다.</summary>
        public IEnumerator MoveThenAttackRoutine(Character character, int steps)
        {
            var orbit = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbit == null)
            {
                Debug.LogError("[AutoAttack] OrbitManager가 없어 이동을 처리할 수 없다.");
                yield break;
            }

            yield return orbit.MoveRoutine(character, steps);
            yield return ResolveRoutine(character);
        }

        /// <summary>아직 공격하지 않은 생존 캐릭터 전원을 순서대로 공격시킨다.</summary>
        public IEnumerator ResolveRemainingRoutine()
        {
            var party = PartyManager.Instance != null ? PartyManager.Instance.Party : null;
            if (party == null) yield break;

            // 순회 중 파티 목록이 바뀔 수 있으므로 복사본으로 돈다.
            var snapshot = new List<Character>(party);
            foreach (var character in snapshot)
                yield return ResolveRoutine(character);
        }

        /// <summary>한 캐릭터의 자동 공격 1회. 이번 턴에 이미 때렸으면 아무 일도 하지 않는다.</summary>
        public IEnumerator ResolveRoutine(Character character)
        {
            if (character == null || !character.IsAlive) yield break;
            if (_resolvedThisTurn.Contains(character)) yield break;
            _resolvedThisTurn.Add(character);

            var targets = CollectTargets(character);
            if (targets.Count == 0) yield break;   // 사거리 안에 생존 몬스터 없음 — 정상 상태

            character.OnSkillExecutionStarted();

            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive) continue;

                var context = new AttackContext(character, target, attackName, character.Stats.Attack);
                CombatPipeline.Instance?.Process(context);
            }

            if (attackInterval > 0f) yield return new WaitForSeconds(attackInterval);
        }

        /// <summary>
        /// 이 캐릭터가 이번 공격으로 때릴 대상들.
        /// 지금은 자기가 선 구역의 소유 몬스터 하나뿐이다 —
        /// 사거리를 넓히는 패시브(마법사 원거리 등)는 이 메서드 한 곳만 확장하면 된다.
        /// </summary>
        private List<Unit> CollectTargets(Character character)
        {
            var result = new List<Unit>();

            var zones = CombatZoneManager.Instance;
            if (zones == null)
            {
                Debug.LogError("[AutoAttack] CombatZoneManager가 없어 공격 대상을 정할 수 없다.");
                return result;
            }

            int zone = zones.GetZoneOf(character);
            if (zone < 0) return result;   // 아직 타일에 배치되지 않음

            var owner = zones.GetOwner(zone);
            if (owner != null) result.Add(owner);   // 중립지대(주인 없음)면 이번 턴 공격 없음

            return result;
        }
    }
}
```

- [ ] **Step 2: 컴파일 게이트**

```csharp
using UnityEngine; using UnityEditor;
internal class CommandScript : IRunCommand {
  public void Execute(ExecutionResult result){
    AssetDatabase.Refresh();
    result.Log("compile OK — {0}", typeof(DiceOrbit.Core.AutoAttackSystem).Name);
  }
}
```

- [ ] **Step 3: 로컬 커밋 (푸시 금지)**

```bash
git add "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/AutoAttackSystem.cs"
git commit -m "feat(combat): 자동 기본공격 시스템 추가 (구역 소유 몬스터 대상)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 6: 이동 직후 자동공격 연결

**Files:**
- Modify: `Assets/Scripts/UI/CharacterActionUI.cs:258`

**Interfaces:**
- Consumes: Task 5의 `AutoAttackSystem.EnsureInstance().MoveThenAttackRoutine(Character, int)`

- [ ] **Step 1: 이동 큐 등록을 교체**

`OnMoveClicked()` 안에서 이동만 큐에 넣던 것을 "이동 후 공격" 루틴으로 바꾼다. 변경 전:
```csharp
                    // 이동 코루틴을 액션 큐에 등록합니다.
                    ActionQueueManager.Instance.EnqueueAction(orbitManager.MoveRoutine(currentCharacter, currentDice.Value));
```

변경 후:
```csharp
                    // 이동 후 도착 구역의 몬스터를 자동 공격하는 코루틴을 액션 큐에 등록합니다.
                    ActionQueueManager.Instance.EnqueueAction(
                        AutoAttackSystem.EnsureInstance().MoveThenAttackRoutine(currentCharacter, currentDice.Value));
```

- [ ] **Step 2: 컴파일 게이트**

```csharp
using UnityEngine; using UnityEditor;
internal class CommandScript : IRunCommand {
  public void Execute(ExecutionResult result){
    AssetDatabase.Refresh();
    result.Log("compile OK — {0}", typeof(DiceOrbit.UI.CharacterActionUI).Name);
  }
}
```

- [ ] **Step 3: 플레이 검증 (작업자)**

전투에서 캐릭터 하나에 주사위를 배정하고 이동시킨다:
- 이동이 끝나면 **곧바로 그 구역 몬스터에게 피해 숫자가 뜬다.**
- 몬스터 HP가 캐릭터 공격력만큼 줄어든다 (전사 6, 도적 4, 마법사 5, 연금술사 4).
- 다른 구역으로 이동하면 **때리는 몬스터가 바뀐다** — 바닥색과 일치하는지 확인한다.

- [ ] **Step 4: 로컬 커밋 (푸시 금지)**

```bash
git add "Assets/Scripts/UI/CharacterActionUI.cs"
git commit -m "feat(combat): 이동 완료 직후 자동 기본공격 발동

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 7: 턴 경계 연결 — 기록 초기화와 미발동자 일괄 공격

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/CombatManager.cs:351, 379`

**Interfaces:**
- Consumes: Task 5의 `AutoAttackSystem.EnsureInstance()`, `ResetTurn()`, `ResolveRemainingRoutine()`

- [ ] **Step 1: 턴 시작 시 발동 기록 초기화**

`StartPlayerTurn()` 안, 예산 초기화 바로 뒤에 넣는다. 변경 전:
```csharp
            // 플레이어 턴 시작 시 캐릭터별 이동/행동 횟수를 1로 초기화합니다.
            InitializePlayerTurnBudgets();
```

변경 후:
```csharp
            // 플레이어 턴 시작 시 캐릭터별 이동/행동 횟수를 1로 초기화합니다.
            InitializePlayerTurnBudgets();

            // 이번 턴 자동 기본공격 발동 기록을 비웁니다 (캐릭터당 1회 보장).
            AutoAttackSystem.EnsureInstance().ResetTurn();
```

- [ ] **Step 2: 턴 종료 시 미발동자 일괄 공격**

`EndPlayerTurnRoutine()`에서 상태를 바꾼 직후, 종료 패시브가 돌기 전에 넣는다. 변경 전:
```csharp
            combatStatus = CombatStatus.EndPlayerTurn;

            var partyManager = PartyManager.Instance;
```

변경 후:
```csharp
            combatStatus = CombatStatus.EndPlayerTurn;

            // 이동하지 않아 아직 때리지 않은 캐릭터도 기본공격은 반드시 한다 (피해 바닥 보장, 스펙 불변식 1).
            yield return AutoAttackSystem.EnsureInstance().ResolveRemainingRoutine();
            if (IsCombatFinished()) yield break;

            var partyManager = PartyManager.Instance;
```

- [ ] **Step 3: 컴파일 게이트**

```csharp
using UnityEngine; using UnityEditor;
internal class CommandScript : IRunCommand {
  public void Execute(ExecutionResult result){
    AssetDatabase.Refresh();
    result.Log("compile OK — {0}", typeof(DiceOrbit.Core.CombatManager).Name);
  }
}
```

- [ ] **Step 4: 플레이 검증 (작업자) — Phase 1–2 최종 확인**

한 전투를 끝까지 플레이하며 확인한다:

1. **바닥 보장** — 주사위를 아무에게도 배정하지 않고 턴 종료를 눌러도, 살아있는 캐릭터 전원이 각자 구역 몬스터를 한 번씩 때린다.
2. **중복 없음** — 이동해서 때린 캐릭터가 턴 종료 때 또 때리지 않는다 (한 턴에 캐릭터당 정확히 1회).
3. **구역 일치** — 캐릭터가 선 바닥색과 피해를 받는 몬스터가 항상 같다.
4. **중립지대 동작** — 몬스터를 죽이면 그 구역이 중립색으로 비고, 그 구역에 선 캐릭터는 이번 턴 아무도 때리지 않는다.
5. **템포** — 캐릭터 4명 턴을 처리하는 데 드는 클릭이 이전보다 확연히 줄었다 (목표: 캐릭터당 이동 관련 클릭만).
6. 콘솔에 `[CombatZone]`·`[AutoAttack]`·`[CharacterPreset]` 에러가 없다.

문제가 보이면 이 계획을 벗어나 수정하지 말고, 증상을 기록해 다음 Phase 계획의 입력으로 삼는다.

- [ ] **Step 5: 로컬 커밋 (푸시 금지)**

```bash
git add "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/CombatManager.cs"
git commit -m "feat(combat): 턴 시작 시 자동공격 기록 초기화, 턴 종료 시 미발동자 일괄 공격

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Phase 1–2 완료 후

이 시점에서 게임은 "주사위 배정 → 이동 → 자동 피해"로 돌아간다. 다음 계획을 쓰기 전에 플레이해 보고 아래를 판단한다:

- 템포가 실제로 개선됐는가 (목표였던 문제가 풀렸는가)
- 구역 배분이 퍼즐로 느껴지는가, 아니면 아무 데나 가도 비슷한가
- 스펙 §9 미결 중 실제로 문제가 된 것 — 빈 사분면 흡수가 자연스러운가, 몬스터가 움직여야 하는가, 이동 방향 선택이 필요한가

그 답을 반영해 Phase 3(위치 패시브 4종) 계획을 새로 작성한다. 기존 액티브 4종은 Phase 4까지 손대지 않는다 — 지금은 주사위 눈 × 배율로 동작하며, 자동 기본공격과 함께 그대로 발동한다(중복 피해). 이 중복은 Phase 4에서 액티브를 재설계하며 정리한다.
