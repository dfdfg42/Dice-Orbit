# 전투 진입 정리 (WaveManager 폐지) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** WaveManager를 폐지하고 전투 상태 권위를 CombatManager로 단일화, "Wave" 개념을 코드·데이터에서 제거한다. 스펙: `Docs/superpowers/specs/2026-07-21-combat-entry-encounter-cleanup-design.md`.

**Architecture:** `GameFlow → CombatManager.StartEncounter(몹세트, 층번호)` 직행 (핑퐁 제거). 스폰은 `EncounterSpawner` 도구 컴포넌트, 승패 판정은 `IsCombatFinished()` 한 곳, 시작 방송은 기존 `OnCombatStart` 재사용, 클리어는 `GameFlow.OnEncounterCleared()` 직접 호출. 유물 시작 회복은 `EventPhase.CombatStart` 파이프라인 방송에 LifeAmulet이 리액터로 반응.

**Tech Stack:** Unity 6000.3.8f1, C# (Assembly-CSharp 단일), Unity MCP (컴파일 게이트·에셋/씬 마이그레이션).

## Global Constraints

- 검증 게이트 = Unity MCP: `Unity_RunCommand`로 `AssetDatabase.Refresh()` → `Unity_GetConsoleLogs`(error) 0건. (브리지가 "Unity not detected"면 몇 초 대기 후 `GetState` 성공 확인하고 재시도.)
- **리액터 훅 규칙** (`combat_reactor_dispatch.md` §4.1): 베이스 상속 리액터의 훅은 반드시 `override`. `public void` 선언 금지.
- `EncounterDefinition`은 [Serializable] 평클래스 — 필드명(`MonsterPresets`, `BackgroundSprite`)을 유지해야 기존 에셋(YAML)이 구조적으로 로드된다. `SpawnCount` 필드는 삭제 (YAML 잔존값은 Unity가 무시).
- 씬 컴포넌트 교체·에셋 삭제는 **클래스가 살아있는 동안** 실행 후 파일 삭제 (Relic 철거 때와 같은 순서). `AssetDatabase.DeleteAsset`은 MCP에서 인터랙션 차단에 걸리므로 파일 삭제는 `git rm`으로.
- 유물 표시명/설명 등 한국어 카피 변경 금지 (생명의 부적 = "전투 시작 시 파티 전원 5 회복").
- 커밋 메시지 끝: `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.

---

### Task 1: WaveDefinition → EncounterDefinition 개명·이동

**Files:**
- Create: `Assets/Scripts/Core/Run/EncounterDefinition.cs`
- Modify: `Assets/Scripts/Data/Waves/WaveDatabase.cs` (WaveDefinition 클래스 제거, Waves 타입 교체)
- Modify: `Assets/Scripts/Core/Run/ActDefinition.cs`, `Assets/Scripts/Core/Run/MapGraph.cs:17`, `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/WaveManager.cs:46,68,189` (타입 참조 교체)

**Interfaces:**
- Produces: `DiceOrbit.Core.Run.EncounterDefinition` — `List<MonsterPreset> MonsterPresets`, `Sprite BackgroundSprite` (SpawnCount 없음). 이후 모든 태스크가 이 타입 사용.

- [ ] **Step 1: EncounterDefinition.cs 생성**

```csharp
using System.Collections.Generic;
using DiceOrbit.Data.Monsters;
using UnityEngine;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// 전투 1회의 몹 세트 (구 WaveDefinition 개명, 2026-07-21).
    /// ActDefinition의 티어/엘리트/보스 풀에 인라인 직렬화된다.
    /// BackgroundSprite는 막 기본 배경(ActDefinition.DefaultBackground)의 오버라이드 — 비면 막 기본.
    /// </summary>
    [System.Serializable]
    public class EncounterDefinition
    {
        public List<MonsterPreset> MonsterPresets;
        public Sprite BackgroundSprite;
    }
}
```

- [ ] **Step 2: WaveDatabase.cs에서 WaveDefinition 제거** — 파일 전체를 아래로 교체 (임시 존치 — Task 6 이관 후 삭제):

```csharp
using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core.Run;

namespace DiceOrbit.Data.Waves
{
    // 구 웨이브 DB — Task 6에서 Act 풀로 이관 후 삭제 예정 (읽기 전용 존치)
    [CreateAssetMenu(fileName = "New Wave Database", menuName = "Dice Orbit/Waves/Wave Database")]
    public class WaveDatabase : ScriptableObject
    {
        public List<EncounterDefinition> Waves = new List<EncounterDefinition>();
    }
}
```

- [ ] **Step 3: 타입 참조 일괄 교체** — `WaveDefinition` → `EncounterDefinition`:
  - `ActDefinition.cs` 19, 54, 56, 64, 81, 88행 (EncounterTier.Encounters / ElitePool / BossPool / Resolve* 3종)
  - `MapGraph.cs` 17행: `[NonSerialized] public EncounterDefinition Encounter;`
  - `WaveManager.cs` 46행 `StartEncounter(EncounterDefinition ...)`, 68행 `SpawnMonsters(EncounterDefinition ...)`, 189행 `GetWaveDefinition` 반환 타입
  - 각 파일에 `using DiceOrbit.Core.Run;`이 없으면 추가 (ActDefinition/MapGraph는 같은 네임스페이스라 불필요)

- [ ] **Step 4: 컴파일 확인** — Refresh → 콘솔 에러 0. 1WaveDatabase.asset이 정상 로드되는지(콘솔 임포트 에러 없음) 확인.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/Core/Run/EncounterDefinition.cs" Assets/Scripts/Data/Waves/WaveDatabase.cs Assets/Scripts/Core/Run/ActDefinition.cs Assets/Scripts/Core/Run/MapGraph.cs "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/WaveManager.cs"
git commit -m "refactor: WaveDefinition을 EncounterDefinition으로 개명 (Core/Run 이동, SpawnCount 삭제)"
```

---

### Task 2: EncounterSpawner 생성

**Files:**
- Create: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/EncounterSpawner.cs`

**Interfaces:**
- Consumes: `EncounterDefinition` (Task 1), `Monster.InitializeFromPreset`, `WaveSpawnPoint`, `Visuals.MonsterIdentityManager`
- Produces: `EncounterSpawner` — `Instance`/`EnsureInstance()`, `List<Monster> Spawn(EncounterDefinition encounter)`. 장부/이벤트/판정 없음.

- [ ] **Step 1: EncounterSpawner.cs 작성** (스폰 로직은 WaveManager.SpawnMonsters/GetSpawnPoints/GetSpawnPosition 승계 — 등록·사망 구독 제외):

```csharp
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core.Run;
using DiceOrbit.Data.Waves;
using UnityEngine;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 몹 세트 스포너 — 인스턴스화 + 프리셋 초기화 + 위치 배치 + 정체성 색.
    /// 장부/승패 판정/이벤트 없음 (전투 상태 권위는 CombatManager, 스펙 2026-07-21).
    /// </summary>
    public class EncounterSpawner : MonoBehaviour
    {
        public static EncounterSpawner Instance { get; private set; }

        [SerializeField] private GameObject monsterPrefab;
        [SerializeField] private Transform spawnRoot;
        [SerializeField] private float fallbackSpawnRadius = 2.5f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static EncounterSpawner EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<EncounterSpawner>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            return new GameObject("EncounterSpawner").AddComponent<EncounterSpawner>();
        }

        /// <summary>몹 세트를 스폰해 목록으로 반환. 등록/전멸 감지는 호출자(CombatManager) 몫.</summary>
        public List<Monster> Spawn(EncounterDefinition encounter)
        {
            var spawned = new List<Monster>();

            var presets = encounter?.MonsterPresets?.Where(p => p != null).ToList();
            if (presets == null || presets.Count == 0)
            {
                Debug.LogWarning("[EncounterSpawner] 몹 세트가 비어 있습니다 — 스폰 생략.");
                return spawned;
            }
            if (monsterPrefab == null)
            {
                Debug.LogWarning("[EncounterSpawner] monsterPrefab 미지정 — 스폰 생략.");
                return spawned;
            }

            var points = Object.FindObjectsByType<WaveSpawnPoint>(FindObjectsSortMode.None)
                .OrderBy(_ => Random.value).ToList();

            for (int i = 0; i < presets.Count; i++)
            {
                var go = Object.Instantiate(monsterPrefab, GetSpawnPosition(points, i), Quaternion.identity, spawnRoot);
                var monster = go.GetComponent<Monster>() ?? go.GetComponentInChildren<Monster>();
                if (monster == null)
                {
                    Debug.LogWarning($"[EncounterSpawner] '{go.name}'에 Monster 컴포넌트가 없습니다.");
                    continue;
                }
                monster.InitializeFromPreset(presets[i]);
                spawned.Add(monster);
            }

            Debug.Log($"[EncounterSpawner] {spawned.Count}마리 스폰 완료.");

            Visuals.MonsterIdentityManager.EnsureInstance();
            Visuals.MonsterIdentityManager.Instance.Setup(spawned);
            return spawned;
        }

        private Vector3 GetSpawnPosition(List<WaveSpawnPoint> points, int index)
        {
            if (points != null && points.Count > 0)
            {
                if (index < points.Count) return points[index].transform.position;

                var basePoint = points[index % points.Count].transform.position;
                int overlapTier = index / points.Count;
                float angle = overlapTier * 137.5f * Mathf.Deg2Rad;
                float distance = 0.8f + overlapTier * 0.6f;
                return basePoint + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            }

            float fallbackAngle = index * 137.5f * Mathf.Deg2Rad;
            float fallbackDistance = Mathf.Min(fallbackSpawnRadius, 0.8f + index * 0.6f);
            return new Vector3(Mathf.Cos(fallbackAngle) * fallbackDistance, 0f, Mathf.Sin(fallbackAngle) * fallbackDistance);
        }
    }
}
```
(`using DiceOrbit.Data.Waves;`는 WaveSpawnPoint 네임스페이스 확인 후 불필요하면 제거 — WaveSpawnPoint는 `Assets/Scripts/Data/Waves/WaveSpawnPoint.cs`.)

- [ ] **Step 2: 컴파일 확인** — Refresh → 에러 0.
- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/EncounterSpawner.cs"
git commit -m "feat: EncounterSpawner - 스폰 전용 도구 컴포넌트 (구 WaveManager 스폰 로직 승계)"
```

---

### Task 3: CombatManager 진입점 + CombatStart 방송 + GameFlow 직행

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Pipeline/CombatContext.cs:6` (EventPhase)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/CombatManager.cs` (StartEncounter/BroadcastCombatStart/EndCombat/DestroyActiveMonsters)
- Modify: `Assets/Scripts/Core/GameFlowManager.cs:201-233, 285-317, 528-536`

**Interfaces:**
- Consumes: `EncounterSpawner.EnsureInstance().Spawn()` (Task 2), `EncounterDefinition` (Task 1)
- Produces: `CombatManager.StartEncounter(EncounterDefinition, int)`, `CurrentEncounter`(EncounterDefinition)/`CurrentFloorNumber`(int) 프로퍼티, `EventPhase.CombatStart`. `GameFlowManager.OnEncounterCleared()` (구 OnWaveCleared(int) 대체). Task 4·5가 의존.

- [ ] **Step 1: EventPhase에 CombatStart 추가** (CombatContext.cs 6행)

```csharp
public enum EventPhase { TurnStart, TurnEnd, TileTick, CombatStart }
```

- [ ] **Step 2: CombatManager 확장** — 프로퍼티 2개 + 메서드 3개 추가, EndCombat 수정:

Properties 구역(56행 근처)에 추가:
```csharp
public Run.EncounterDefinition CurrentEncounter { get; private set; }   // 배경 등 조회용
public int CurrentFloorNumber { get; private set; }                     // 표시용 (층+1)
```

`StartCombat()` 위에 추가:
```csharp
/// <summary>전투 진입점 (노드맵 흐름). 스폰 → CombatStart 방송 → 턴 시퀀스.</summary>
public void StartEncounter(Run.EncounterDefinition encounter, int floorNumber)
{
    if (inCombat) return;

    DestroyActiveMonsters();
    CurrentEncounter = encounter;
    CurrentFloorNumber = floorNumber;

    var spawned = EncounterSpawner.EnsureInstance().Spawn(encounter);
    foreach (var m in spawned)
        RegisterMonster(m);
    // 사망 구독 불필요 — Monster.cs:308이 사망 시 CombatManager.OnMonsterDefeated(this)를
    // 직접 호출한다 (OnDeath 이벤트를 또 구독하면 이중 호출 버그).

    BroadcastCombatStart();
    StartCombat();
}

/// <summary>이전 전투 잔여 몬스터 파괴 + 장부 초기화 (파괴 책임 = 장부 권위).</summary>
private void DestroyActiveMonsters()
{
    foreach (var m in activeMonsters)
    {
        if (m == null) continue;
        Destroy(m.gameObject);
    }
    activeMonsters.Clear();
}

/// <summary>전투 시작 사건을 파이프라인에 방송 — 유물 등 리액터가 반응 (개별 효과는 모름).</summary>
private void BroadcastCombatStart()
{
    if (Pipeline.CombatPipeline.Instance == null || PartyManager.Instance == null) return;
    foreach (var ch in PartyManager.Instance.Party)
    {
        if (ch == null || !ch.IsAlive) continue;
        var ctx = new Pipeline.TurnEventContext(ch, ch, Pipeline.EventPhase.CombatStart);
        Pipeline.CombatPipeline.Instance.Process(ctx);
    }
}
```

확인 완료: 사망 → 장부 제거 → 전멸 감지는 기존 경로(`Monster.Die → CombatManager.OnMonsterDefeated → IsCombatFinished`)가 이미 담당한다. WaveManager의 OnDeath 구독(중복 감지기)은 WaveManager와 함께 사라진다.

`EndCombat(true)`의 Wave 처리 교체 (234-241행):
```csharp
// 변경 전
if (victory)
{
    if (WaveManager.Instance != null)
    {
        WaveManager.Instance.CheckWaveClear();
    }
}
// 변경 후 (패배 쪽 OnCombatDefeat 직접 호출과 대칭)
if (victory)
{
    if (GameFlowManager.Instance != null)
    {
        GameFlowManager.Instance.OnEncounterCleared();
    }
}
```

- [ ] **Step 3: GameFlowManager 수정**

`StartCombat()` (201-233행) — 노드맵 분기 교체:
```csharp
// 변경 전
if (WaveManager.Instance != null && !WaveManager.Instance.IsWaveActive)
{
    if (run.CurrentNode.Encounter == null)
    {
        Debug.LogError("[GameFlow] 이 노드에 몹 세트가 없습니다 — ActDefinition의 티어 풀/폴백 DB를 확인하세요.");
        ChangeState(GameState.Map);
        return;
    }
    WaveManager.Instance.StartEncounter(run.CurrentNode.Encounter, run.CurrentNode.Floor + 1);
}
// 변경 후
if (CombatManager.Instance != null && !CombatManager.Instance.InCombat)
{
    if (run.CurrentNode.Encounter == null)
    {
        Debug.LogError("[GameFlow] 이 노드에 몹 세트가 없습니다 — ActDefinition의 티어 풀을 확인하세요.");
        ChangeState(GameState.Map);
        return;
    }
    CombatManager.Instance.StartEncounter(run.CurrentNode.Encounter, run.CurrentNode.Floor + 1);
}
```

`OnWaveCleared(int wave)` (285행) → 시그니처 변경 + 본문 유지:
```csharp
public void OnEncounterCleared()
{
    Debug.Log("[GameFlow] Encounter Cleared.");
    // (이하 본문 동일: ReviveRetiredMembers → run.OnBattleCleared → Boss면 Victory → Reward)
```

`OnWaveStarted(int)` (310-317행) 메서드 통째로 삭제 (핑퐁 제거 — StartCombat은 StartEncounter가 직접 호출).

구독 블록 (528-536행) 삭제:
```csharp
// 삭제
// Subscribe to WaveManager events
if (WaveManager.Instance != null)
{
    WaveManager.Instance.OnWaveStart -= OnWaveStarted;
    WaveManager.Instance.OnWaveStart += OnWaveStarted;
    WaveManager.Instance.OnWaveClear -= OnWaveCleared;
    WaveManager.Instance.OnWaveClear += OnWaveCleared;
}
```

- [ ] **Step 4: 컴파일 확인** — Refresh → 에러 0. (WaveManager는 아직 존재 — 구독자들이 남아 있어 정상.)
- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Pipeline/CombatContext.cs" "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/CombatManager.cs" Assets/Scripts/Core/GameFlowManager.cs
git commit -m "feat: CombatManager.StartEncounter - 전투 진입 단일화 + CombatStart 파이프라인 방송"
```

---

### Task 4: LifeAmulet 리액터 전환 (질의 → 파이프라인)

**Files:**
- Modify: `Assets/Scripts/Data/Artifacts/LifeAmulet/LifeAmulet.cs`
- Modify: `Assets/Scripts/Core/Run/Artifact/RuntimeArtifact.cs` (BattleStartHeal virtual 삭제)
- Modify: `Assets/Scripts/Core/Run/Artifact/ArtifactManager.cs` (BattleStartHeal 질의 삭제)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/WaveManager.cs:54-63` (구 시작 회복 블록 삭제)

**Interfaces:**
- Consumes: `EventPhase.CombatStart` (Task 3), `HealContext`, `CombatTrigger.OnPreAction` (방송의 첫 트리거 — 1회만 반응하도록 트리거 고정)
- Produces: 없음. `ArtifactManager.BattleStartHeal` 소비처는 이 태스크에서 전부 사라짐.

- [ ] **Step 1: LifeAmulet.cs 전체 교체**

```csharp
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Run;
using UnityEngine;

namespace DiceOrbit.Data.Artifacts
{
    /// <summary>생명의 부적 — 전투 시작 시 파티 전원 +N 회복 (CombatStart 방송에 반응).</summary>
    [System.Serializable]
    public class LifeAmulet : RuntimeArtifact
    {
        public int amount = 5;

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (context.Phase != EventPhase.CombatStart) return;
            if (trigger != CombatTrigger.OnPreAction) return;   // 방송당 1회만 (트리거 4회 방지)
            if (context.IsSimulation) return;
            if (context.Target == null) return;

            var heal = new HealContext(null, context.Target, "생명의 부적", amount);
            CombatPipeline.Instance?.Process(heal);
        }
    }
}
```

⚠️ 사전 확인 (스펙의 중첩 Process 안전성): `CombatPipeline.Process(TurnEventContext)` 디스패치 도중 리액터가 새 `HealContext`를 Process하는 패턴은 타일(`StartHealTile.OnTraverse` → HealContext, `HoneyPawTile`, `RandMineTile`)이 이미 쓰는 선례가 있다. 단 타일은 파이프라인 **밖**(OnTraverse)에서 호출 — 리액터 **안**에서의 중첩은 `CombatPipeline.Process`가 재진입 가드/공유 상태를 갖는지 `CombatPipeline.cs`를 읽고 확인할 것. 공유 리스트를 재사용한다면 ActionQueueManager로 우회:
`ActionQueueManager.Instance.EnqueueAction(HealRoutine(...))` 대신 **가장 단순한 검증**은 Step 3의 플레이 확인 — 시작 회복이 발동하고 예외가 없으면 통과.

- [ ] **Step 2: 질의 표면 삭제**
  - `RuntimeArtifact.cs`: `public virtual int BattleStartHeal => 0;` 줄 삭제 (주석의 5종 → 4종 갱신)
  - `ArtifactManager.cs`: `public int BattleStartHeal => artifacts.Sum(a => a.BattleStartHeal);` 줄 삭제
  - `WaveManager.cs` 54-63행 (유물 시작 회복 블록) 삭제 — Task 3에서 새 경로가 이미 방송하므로 중복 회복 방지:
```csharp
// 삭제 (StartEncounter 내)
// 유물: 전투 시작 시 파티 회복 (예: 생명의 부적)
int startHeal = Run.ArtifactManager.Instance?.BattleStartHeal ?? 0;
if (startHeal > 0 && PartyManager.Instance != null) { ... }
```

- [ ] **Step 3: 컴파일 + 플레이 스모크** — Refresh → 에러 0. 플레이 모드 진입 → 시작 유물에 LifeAmulet 에셋 지정(또는 폴백 풀 "생명의 부적" Grant) 후 전투 노드 진입 → 콘솔에서 "생명의 부적" 힐 처리 + 예외 없음 확인 (Play/Stop은 Unity MCP `Unity_ManageEditor`).
- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/Data/Artifacts/LifeAmulet/LifeAmulet.cs" "Assets/Scripts/Core/Run/Artifact/RuntimeArtifact.cs" "Assets/Scripts/Core/Run/Artifact/ArtifactManager.cs" "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/WaveManager.cs"
git commit -m "refactor: 생명의 부적을 CombatStart 리액터로 - 시작 회복이 파이프라인 경유 (질의 표면 삭제)"
```

---

### Task 5: 구독자 이사 (OnWaveStart → OnCombatStart)

**Files:**
- Modify: `Assets/Scripts/Data/Character Preset/Mage/FocusPassive.cs:31-43`
- Modify: `Assets/Scripts/Data/Character Preset/Alchemist/ReagentPrepPassive.cs:33-61`
- Modify: `Assets/Scripts/Data/MonsterPresets/Wave1/Skeleton/Skeleton.cs:91,103-128`
- Modify: `Assets/Scripts/Data/MonsterPresets/Wave1/Goblin/Goblin.cs:20-43`
- Modify: `Assets/Scripts/Data/MonsterPresets/Wave2/BearPackTracker.cs:18,59-75`
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/BackgroundManager.cs`

**Interfaces:**
- Consumes: `CombatManager.OnCombatStart`(System.Action, 전투 1회당 1번·스폰 후 발화), `CombatManager.InCombat`, `CombatManager.CurrentEncounter`, `RunManager.CurrentAct.DefaultBackground` (Task 6에서 필드 추가 — 이 태스크에서는 null 허용 코드로 작성)
- Produces: 없음

각 구독자의 패턴은 동일: `WaveManager.Instance` → `CombatManager.Instance`, `OnWaveStart -= / +=` → `OnCombatStart -= / +=`, 핸들러 시그니처 `(int wave)` → `()`, `IsWaveActive` → `InCombat`, `hookedManager` 타입 `WaveManager` → `CombatManager`.

- [ ] **Step 1: FocusPassive.cs** (31-43행)

```csharp
// 변경 전
if (WaveManager.Instance != null)
{
    WaveManager.Instance.OnWaveStart -= HandleWaveStart;
    WaveManager.Instance.OnWaveStart += HandleWaveStart;
}
...
private void HandleWaveStart(int wave)
// 변경 후
if (CombatManager.Instance != null)
{
    CombatManager.Instance.OnCombatStart -= HandleCombatStart;
    CombatManager.Instance.OnCombatStart += HandleCombatStart;
}
...
private void HandleCombatStart()
```

- [ ] **Step 2: ReagentPrepPassive.cs** — 같은 패턴 + 39행 `IsWaveActive` → `InCombat`:

```csharp
// 39행 변경 전
if (WaveManager.Instance != null && WaveManager.Instance.IsWaveActive)
// 변경 후
if (CombatManager.Instance != null && CombatManager.Instance.InCombat)
// 46-51행 SubscribeWaveStart → CombatManager.OnCombatStart 구독으로 (Step 1과 동일 패턴)
// 53행 HandleWaveStart(int wave) → HandleCombatStart()
```

- [ ] **Step 3: Skeleton.cs** — 91행 `private WaveManager hookedManager;` → `private CombatManager hookedManager;`, 109행 `IsWaveActive` → `InCombat`, `SubscribeWaveStart` 내부 `WaveManager.Instance` → `CombatManager.Instance` + `OnWaveStart` → `OnCombatStart`, `HandleWaveStart(int wave)` → `HandleCombatStart()`.

- [ ] **Step 4: Goblin.cs (MineFieldCleaner)** — 22행 static 필드 타입 교체, `EnsureWaveHook` 내부 교체, `OnWaveStart(int wave)` → `OnCombatStart()` (핸들러명도 교체 — 주석 17-18행의 WaveManager 언급 갱신).

- [ ] **Step 5: BearPackTracker.cs** — 18행 static 필드 타입 교체, `EnsureWaveHook` 내부 교체, `OnWaveStart(int wave)` → `OnCombatStart()`.

- [ ] **Step 6: BackgroundManager.cs** — Start/OnDestroy 구독 + 배경 결정 로직 교체:

```csharp
// Start() 33-39행 변경 전
if (WaveManager.Instance != null)
{
    WaveManager.Instance.OnWaveStart += OnWaveStart;

    if (WaveManager.Instance.IsWaveActive)
        OnWaveStart(WaveManager.Instance.CurrentWave);
}
// 변경 후
if (CombatManager.Instance != null)
{
    CombatManager.Instance.OnCombatStart += OnCombatStart;

    if (CombatManager.Instance.InCombat)
        OnCombatStart();
}

// OnDestroy() 42-46행: 구독 해제도 CombatManager.OnCombatStart -= OnCombatStart 로

// 84-89행 변경 전
private void OnWaveStart(int waveIndex)
{
    var waveDef = WaveManager.Instance.GetWaveDefinition(waveIndex);
    if (waveDef != null && waveDef.BackgroundSprite != null)
        SetBackground(waveDef.BackgroundSprite);
}
// 변경 후 — 몹세트 오버라이드 ?? 막 기본 (스펙 §4 배경)
private void OnCombatStart()
{
    var encounter = CombatManager.Instance != null ? CombatManager.Instance.CurrentEncounter : null;
    Sprite sprite = encounter != null ? encounter.BackgroundSprite : null;
    if (sprite == null)
        sprite = Run.RunManager.Instance?.CurrentAct?.DefaultBackground;
    if (sprite != null)
        SetBackground(sprite);
}
```
(`DefaultBackground` 필드는 Task 6에서 추가 — 이 시점에는 컴파일을 위해 Task 6 Step 1을 먼저 적용해도 되고, 순서 유지가 필요하면 이 태스크에서 `ActDefinition`에 필드만 먼저 추가해도 무방. **권장: 이 태스크에서 `ActDefinition.cs`에 `public Sprite DefaultBackground;` 추가**를 포함.)

- [ ] **Step 7: ActDefinition.cs에 막 기본 배경 필드 추가** (§3 "맵 구조" 헤더 아래):

```csharp
[Header("연출")]
[Tooltip("막 기본 전투 배경 — 몹 세트의 BackgroundSprite가 비어 있으면 이걸 사용")]
public Sprite DefaultBackground;
```

- [ ] **Step 8: 컴파일 확인** — Refresh → 에러 0. **경고에 CS0114가 없어야 함** (구독자 이동은 훅과 무관하지만 확인 습관).
- [ ] **Step 9: Commit**

```bash
git add "Assets/Scripts/Data/Character Preset/Mage/FocusPassive.cs" "Assets/Scripts/Data/Character Preset/Alchemist/ReagentPrepPassive.cs" "Assets/Scripts/Data/MonsterPresets/Wave1/Skeleton/Skeleton.cs" "Assets/Scripts/Data/MonsterPresets/Wave1/Goblin/Goblin.cs" "Assets/Scripts/Data/MonsterPresets/Wave2/BearPackTracker.cs" "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/BackgroundManager.cs" Assets/Scripts/Core/Run/ActDefinition.cs
git commit -m "refactor: 전투 시작 구독자를 CombatManager.OnCombatStart로 이사 + 배경 막기본/오버라이드"
```

---

### Task 6: 데이터 이관 + 폴백 제거 + WaveManager/WaveDatabase 철거 + 씬 교체

**Files:**
- Modify: `Assets/Scripts/Core/Run/ActDefinition.cs` (폴백 필드/브랜치 삭제)
- Modify: `Assets/Scripts/Data/Run/Act.asset` (이관 — 에디터 스크립트)
- Modify: `Assets/Scenes/BattleScene.unity` (WaveManager → EncounterSpawner 컴포넌트 교체)
- Delete: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/WaveManager.cs`(+.meta), `Assets/Scripts/Data/Waves/WaveDatabase.cs`(+.meta), `Assets/Scripts/Data/MonsterPresets/1WaveDatabase.asset`(+.meta), `Assets/Scripts/Data/MonsterPresets/TestWaveDB.asset`(+.meta)

**Interfaces:**
- Consumes: Task 1~5 완료 상태 (이 시점에 WaveManager 참조는 자기 자신뿐이어야 — Step 3에서 검증)
- Produces: 없음

- [ ] **Step 1: 데이터 이관 (Unity MCP `Unity_RunCommand`)** — WaveDatabase 클래스가 살아있는 동안 실행:

```csharp
// 구 1WaveDatabase(웨이브 3개, 배경 전부 미지정)를 Act 풀로 이관.
// 규칙: W3(3몹)→BossPool, W2(2몹)→ElitePool(+후반 티어), W1(1몹)→초반 티어. 기존 "asdf" 세트 보존.
// FloorCount 12 → 일반 전투 층 0~10 전체 커버 (초반 0~5 / 후반 6~10) — 9~11층 구멍 봉합.
using UnityEditor; using UnityEngine; using DiceOrbit.Core.Run; using DiceOrbit.Data.Waves;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var db  = AssetDatabase.LoadAssetAtPath<WaveDatabase>("Assets/Scripts/Data/MonsterPresets/1WaveDatabase.asset");
        var act = AssetDatabase.LoadAssetAtPath<ActDefinition>("Assets/Scripts/Data/Run/Act.asset");
        if (db == null || act == null || db.Waves.Count < 3) { result.LogError("에셋 로드 실패 또는 웨이브 부족"); return; }

        var legacy = (act.BattleTiers.Count > 0 && act.BattleTiers[0].Encounters.Count > 0)
            ? act.BattleTiers[0].Encounters[0] : null;

        act.BattleTiers.Clear();
        var early = new EncounterTier { Name = "초반", MinFloor = 0, MaxFloor = 5 };
        if (legacy != null) early.Encounters.Add(legacy);
        early.Encounters.Add(db.Waves[0]);
        var late = new EncounterTier { Name = "후반", MinFloor = 6, MaxFloor = 10 };
        late.Encounters.Add(db.Waves[1]);
        act.BattleTiers.Add(early);
        act.BattleTiers.Add(late);

        act.ElitePool.Clear(); act.ElitePool.Add(db.Waves[1]);
        act.BossPool.Clear();  act.BossPool.Add(db.Waves[2]);

        EditorUtility.SetDirty(act);
        AssetDatabase.SaveAssets();
        result.Log("이관 완료 — 초반 " + early.Encounters.Count + "세트 / 후반 " + late.Encounters.Count + " / 엘리트 1 / 보스 1");
    }
}
```

- [ ] **Step 2: 씬 컴포넌트 교체 (Unity MCP)** — WaveManager 클래스가 살아있는 동안, BattleScene 활성 상태에서:

```csharp
using UnityEditor; using UnityEditor.SceneManagement; using UnityEngine; using DiceOrbit.Core;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "BattleScene") { result.LogError("BattleScene을 열고 실행하세요: " + scene.name); return; }

        var wms = Object.FindObjectsByType<WaveManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var wm in wms)
        {
            var go = wm.gameObject;
            result.RegisterObjectModification(go);
            var spawner = go.GetComponent<EncounterSpawner>();
            if (spawner == null) spawner = go.AddComponent<EncounterSpawner>();

            // 직렬화 참조 이식: monsterPrefab / spawnRoot / fallbackSpawnRadius
            var src = new SerializedObject(wm);
            var dst = new SerializedObject(spawner);
            dst.FindProperty("monsterPrefab").objectReferenceValue = src.FindProperty("monsterPrefab").objectReferenceValue;
            dst.FindProperty("spawnRoot").objectReferenceValue = src.FindProperty("spawnRoot").objectReferenceValue;
            dst.FindProperty("fallbackSpawnRadius").floatValue = src.FindProperty("fallbackSpawnRadius").floatValue;
            dst.ApplyModifiedPropertiesWithoutUndo();

            Object.DestroyImmediate(wm);
            result.Log("교체: {0}", go);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        result.Log("교체 " + wms.Length + "건 + 씬 저장");
    }
}
```

- [ ] **Step 3: 잔존 참조 0 확인 후 폴백 제거·파일 삭제**

```bash
grep -rn "WaveManager\|WaveDatabase\|GetWaveDefinition\|IsWaveActive\|OnWaveStart\|OnWaveClear" Assets/Scripts --include="*.cs" | grep -v "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/WaveManager.cs" | grep -v "Assets/Scripts/Data/Waves/WaveDatabase.cs"
```
Expected: `ActDefinition.cs`의 폴백 참조만 남음. ActDefinition 수정:
  - `using DiceOrbit.Data.Waves;` 삭제, `public WaveDatabase WaveDatabase;` + `WaveCount` 삭제
  - `ResolveBattleEncounter`: 폴백 브랜치(73-78행) → `Debug.LogError($"[Act] {floor}층 일반 전투 풀 미매칭 — BattleTiers 커버리지를 확인하세요."); return null;`
  - `ResolveEliteEncounter`/`ResolveBossEncounter`: 폴백 반환 → `Debug.LogError("[Act] ElitePool/BossPool이 비어 있습니다."); return null;`

```bash
git rm "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/WaveManager.cs" "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/WaveManager.cs.meta" "Assets/Scripts/Data/Waves/WaveDatabase.cs" "Assets/Scripts/Data/Waves/WaveDatabase.cs.meta" "Assets/Scripts/Data/MonsterPresets/1WaveDatabase.asset" "Assets/Scripts/Data/MonsterPresets/1WaveDatabase.asset.meta" "Assets/Scripts/Data/MonsterPresets/TestWaveDB.asset" "Assets/Scripts/Data/MonsterPresets/TestWaveDB.asset.meta"
```

- [ ] **Step 4: 컴파일 확인** — Refresh → 에러 0, "Missing script" 경고 0.
- [ ] **Step 5: 최종 grep** — `grep -rn "Wave" Assets/Scripts --include="*.cs"`에서 게임플레이 의미의 "Wave" 잔존이 `WaveSpawnPoint`(스폰 지점 마커 — 존치)와 몬스터 폴더명 주석뿐인지 확인.
- [ ] **Step 6: Commit** (씬 포함)

```bash
git add Assets/Scripts/Core/Run/ActDefinition.cs "Assets/Scripts/Data/Run/Act.asset" Assets/Scenes/BattleScene.unity
git commit -m "refactor: WaveManager/WaveDatabase 철거 - 몹 세트 이관 + 씬 EncounterSpawner 교체 + 폴백 제거"
```

---

### Task 7: 문서 갱신

**Files:**
- Modify: `Docs/run_structure_system.md` (§1 흐름의 WaveManager.StartEncounter 언급, §2 개조된 기존 파일 표의 WaveManager 행, 씬 요구사항)
- Modify: `Docs/TurnSystem.md` (흐름에 WaveManager가 있으면 교체 — 열어서 확인)

- [ ] **Step 1: run_structure_system.md 갱신**
  - §1 흐름도: `WaveManager.StartEncounter(node.WaveIndex+1)` → `CombatManager.StartEncounter(node.Encounter, 층+1)`
  - §2 "개조된 기존 파일" 표: `WaveManager.cs` 행을 `CombatManager.cs | 전투 진입점 StartEncounter + 장부/승패/방송 단일 권위 (WaveManager 철거, 2026-07-21)` + `EncounterSpawner.cs | 스폰 전용 도구` 로 교체
  - §6 씬 요구사항: WaveManager 언급이 있으면 EncounterSpawner로
  - §3 핵심 설계 결정 ①: "WaveManager는 순차 진행 철거되고 StartEncounter 실행기만" → "WaveManager는 폐지, CombatManager가 전투 진입점·장부·승패·방송의 단일 권위 (스펙 `2026-07-21-combat-entry-encounter-cleanup-design.md`)"
- [ ] **Step 2: TurnSystem.md 열어 WaveManager 언급 확인·교체** (mermaid 플로차트)
- [ ] **Step 3: Commit**

```bash
git add Docs/run_structure_system.md Docs/TurnSystem.md
git commit -m "docs: 전투 진입 정리 반영 - WaveManager 폐지, CombatManager 단일 권위"
```

---

## 최종 검증 (스펙 §8 — 사용자 에디터 플레이)

① 일반/엘리트/보스 전투 진입·스폰 ② 클리어 → 부활/보상, 보스 → Victory ③ 전멸 → GameOver
④ 배경 막기본/오버라이드 ⑤ 집중·시약 리셋 ⑥ 스켈레톤 뼈/고블린 지뢰 정리 ⑦ 9~11층(후반 티어) 몹 배정
⑧ "Wave" grep 잔존 확인 ⑨ 생명의 부적 파이프라인 힐 (알림 표시, 중첩 Process 예외 없음).
