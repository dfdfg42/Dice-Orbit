# 전투 시작 연출 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 전투 진입 시 타일 시계방향 순차 낙하 → 캐릭터 순차 팝인 → 몬스터 순차 소환(VFX)을 재생하는 연출 디렉터를 추가하고, 연출 종료 후 기존 턴 시작 흐름이 이어지게 한다.

**Architecture:** 스펙 `Docs/superpowers/specs/2026-07-31-combat-intro-sequence-design.md`. `CombatIntroDirector`(신규 코루틴 싱글톤)가 3단계 연출을 담당. `CombatManager.StartEncounter`를 코루틴화해 [연출] → BroadcastCombatStart → StartCombat 순으로 체이닝. 몬스터는 숨긴 채 전량 스폰 후 디렉터가 소환 VFX와 함께 순차로 드러냄. 입력은 `playerTurnActive=false` 게이트가 자동 차단.

**Tech Stack:** Unity 6000.3.8f1, C#, 코루틴(IEnumerator + WaitForSeconds), VfxService(기존), Unity MCP(RunCommand/GetConsoleLogs).

## Global Constraints

- 자동 테스트 프레임워크 없음. **각 태스크 게이트 = Unity MCP 컴파일 게이트**: `AssetDatabase.Refresh` → `GetConsoleLogs(error)` 0건. 연출은 플레이 확인 항목(자동 검증 불가) — 로직 검증 가능한 곳만 RunCommand 어서션.
- RunCommand 스크립트는 `internal class CommandScript : IRunCommand`. `Image` 타입은 `UnityEngine.UI.Image` 완전 수식.
- 타일 트랜스폼은 **반드시 원래 위치/스케일로 정확히 복원** (캐릭터 배치가 `tile.Position` 참조). 연출 중 캐릭터·몬스터는 숨김.
- 매 전투 재생, 스킵 없음. 연출은 게임 시간(`Time.deltaTime`) 기반.
- 신규 코드 네임스페이스 `DiceOrbit.Visuals`, 위치 `Assets/Scripts/Visuals/Vfx/`.
- 커밋 메시지 큰따옴표 금지. 끝에 `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.
- 브랜치: `feature/vfx-redesign-20260731` (현재 체크아웃됨).
- 확인된 기존 시그니처: `OrbitManager.Tiles`(List<TileData>), `PartyManager.Instance.Party`(List<Character>) / `GetAliveCharacters()`, `EncounterSpawner.Spawn(EncounterDefinition)`(List<Monster>), `CombatManager.StartEncounter(Run.EncounterDefinition, int)`, `VfxService.PlayOn(string, Unit)`, `VfxTags` 상수 클래스, `Monster : Unit`(transform 보유).

---

### Task 1: VfxTags.Summon + 라이브러리 summon 큐

**Files:**
- Modify: `Assets/Scripts/Visuals/Vfx/VfxTags.cs`
- Modify: `Assets/Resources/Skill/VFX/VfxLibrary.asset` (RunCommand)

**Interfaces:**
- Produces: `VfxTags.Summon` (= "summon"); 라이브러리에 `summon` Burst 큐 — Task 4가 `VfxService.PlayOn(VfxTags.Summon, monster)`로 사용.

- [ ] **Step 1: VfxTags에 Summon 상수 추가**

`VfxTags.cs`의 `Status` 상수 다음 줄에:

```csharp
        public const string Status = "status";
        public const string Summon = "summon";
```

- [ ] **Step 2: 컴파일 게이트**

RunCommand로 `AssetDatabase.Refresh()` → `GetConsoleLogs(error)` 0건.

- [ ] **Step 3: 라이브러리에 summon 큐 배선 (RunCommand)**

```csharp
using UnityEditor;
using UnityEngine;
using DiceOrbit.Visuals;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var lib = AssetDatabase.LoadAssetAtPath<VfxLibrary>("Assets/Resources/Skill/VFX/VfxLibrary.asset");
        if (lib == null) { result.LogError("VfxLibrary 없음"); return; }
        var so = new SerializedObject(lib);
        var cues = so.FindProperty("cues");

        // summon 큐 없으면 추가
        SerializedProperty target = null;
        for (int i = 0; i < cues.arraySize; i++)
            if (cues.GetArrayElementAtIndex(i).FindPropertyRelative("tag").stringValue == "summon")
                target = cues.GetArrayElementAtIndex(i);
        if (target == null) { cues.arraySize++; target = cues.GetArrayElementAtIndex(cues.arraySize - 1); }

        var prefab = Find("CFXR Magic Poof");
        target.FindPropertyRelative("tag").stringValue = "summon";
        target.FindPropertyRelative("play").enumValueIndex = 0; // Burst
        target.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        target.FindPropertyRelative("offset").vector3Value = new Vector3(0, 0.5f, 0);
        target.FindPropertyRelative("lifetime").floatValue = 1.5f;
        target.FindPropertyRelative("shake").FindPropertyRelative("amplitude").floatValue = 0f;
        target.FindPropertyRelative("hitStop").floatValue = 0f;

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        result.Log("summon 큐 배선: " + (prefab != null ? prefab.name : "NULL"));
    }

    private static GameObject Find(string namePart)
    {
        var guids = AssetDatabase.FindAssets(namePart + " t:GameObject");
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (path.Contains("CFXR") && System.IO.Path.GetFileNameWithoutExtension(path) == namePart)
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (path.Contains("CFXR")) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        return null;
    }
}
```
Expected: `summon 큐 배선: CFXR Magic Poof` 로그.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Visuals/Vfx/VfxTags.cs "Assets/Resources/Skill/VFX/VfxLibrary.asset"
git commit -m "feat: 소환 VFX 큐(summon) 추가 - 전투 시작 연출용"
```

---

### Task 2: EncounterSpawner.Spawn — 숨긴 채 스폰 옵션

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/EncounterSpawner.cs:41-71`

**Interfaces:**
- Consumes: 없음
- Produces: `EncounterSpawner.Spawn(EncounterDefinition encounter, bool startHidden = false)` — startHidden=true면 스폰된 몬스터 `localScale = Vector3.zero`. Task 4가 `startHidden:true`로 호출.

- [ ] **Step 1: Spawn 시그니처에 startHidden 추가 + 숨김 처리**

`EncounterSpawner.cs`의 `Spawn` 메서드 시그니처(`:41`)와 스폰 루프(`:60-71`) 교체:

```csharp
        /// <summary>몹 세트를 스폰해 목록으로 반환. 등록/전멸 감지는 호출자(CombatManager) 몫.
        /// startHidden=true면 스케일 0으로 숨겨 스폰 (전투 시작 연출이 순차로 드러냄).</summary>
        public List<Monster> Spawn(EncounterDefinition encounter, bool startHidden = false)
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
                if (startHidden) monster.transform.localScale = Vector3.zero;   // 연출이 순차로 드러냄
                spawned.Add(monster);
            }

            Debug.Log($"[EncounterSpawner] {spawned.Count}마리 스폰 완료.");

            Visuals.MonsterIdentityManager.EnsureInstance();
            Visuals.MonsterIdentityManager.Instance.Setup(spawned);
            return spawned;
        }
```

주의: `InitializeFromPreset`가 내부에서 스케일을 세팅하면 그 뒤에 0으로 덮어야 하므로 순서를 `InitializeFromPreset` **다음**에 둠 (위 코드대로).

- [ ] **Step 2: 컴파일 게이트**

Refresh → 에러 0건. (기존 `Spawn(encounter)` 호출부는 기본값 startHidden=false라 그대로 컴파일됨.)

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/EncounterSpawner.cs
git commit -m "feat: EncounterSpawner 숨긴 채 스폰 옵션(startHidden)"
```

---

### Task 3: CombatIntroDirector — 3단계 연출 코루틴

**Files:**
- Create: `Assets/Scripts/Visuals/Vfx/CombatIntroDirector.cs`

**Interfaces:**
- Consumes: `OrbitManager.Tiles`, `PartyManager.Instance.Party`, `VfxService.PlayOn(VfxTags.Summon, Monster)`, `Monster`/`Character`(Unit, transform)
- Produces: `CombatIntroDirector.EnsureInstance()` → `CombatIntroDirector`; `IEnumerator Play(IReadOnlyList<Monster> spawnedMonsters)` — Task 4가 `yield return`으로 소비.

- [ ] **Step 1: CombatIntroDirector.cs 작성**

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 전투 시작 연출 (스펙 2026-07-31). 타일 시계방향 낙하 → 캐릭터 팝인 → 몬스터 소환.
    /// CombatManager.StartEncounter가 yield return Play(...)로 소비. 연출 동안 playerTurnActive=false라 입력 차단됨.
    /// 타일/캐릭터/몬스터의 원래 위치·스케일을 캐시했다 정확히 복원 (캐릭터 배치가 tile.Position 참조).
    /// </summary>
    public class CombatIntroDirector : MonoBehaviour
    {
        public static CombatIntroDirector Instance { get; private set; }

        [Header("타일 낙하")]
        [SerializeField] private float tileDropHeight = 6f;
        [SerializeField] private float tileDropDuration = 0.22f;
        [SerializeField] private float tileStagger = 0.03f;
        [SerializeField] private float tileStartScale = 0.6f;
        [Tooltip("화면상 시계방향이 되도록 순회 방향 (플레이로 맞춤)")]
        [SerializeField] private bool clockwise = true;

        [Header("캐릭터 팝인")]
        [SerializeField] private float charPopDuration = 0.25f;
        [SerializeField] private float charStagger = 0.10f;

        [Header("몬스터 소환")]
        [SerializeField] private float monsterPopDuration = 0.25f;
        [SerializeField] private float monsterStagger = 0.20f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public static CombatIntroDirector EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<CombatIntroDirector>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            return new GameObject("CombatIntroDirector").AddComponent<CombatIntroDirector>();
        }

        /// <summary>3단계 순차 연출. 완료 후 반환 → 호출자가 BroadcastCombatStart/StartCombat 진행.</summary>
        public IEnumerator Play(IReadOnlyList<Monster> spawnedMonsters)
        {
            yield return TileDropPhase();
            yield return CharacterPopPhase();
            yield return MonsterSummonPhase(spawnedMonsters);
        }

        // ── Phase 1: 타일 시계방향 낙하 ──────────────────────
        private IEnumerator TileDropPhase()
        {
            var orbit = FindAnyObjectByType<OrbitManager>();
            if (orbit == null || orbit.Tiles == null || orbit.Tiles.Count == 0) yield break;

            var tiles = new List<DiceOrbit.Data.TileData>(orbit.Tiles);
            if (clockwise) tiles.Reverse();

            // 원래 값 캐시 + 시작값(위·축소)으로 즉시 세팅
            int n = tiles.Count;
            var origPos = new Vector3[n];
            var origScale = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                var t = tiles[i];
                if (t == null) continue;
                origPos[i] = t.transform.position;
                origScale[i] = t.transform.localScale;
                t.transform.position = origPos[i] + Vector3.up * tileDropHeight;
                t.transform.localScale = origScale[i] * tileStartScale;
            }

            // 순차 낙하 (겹쳐서 진행 — stagger로 하나씩 시작)
            var routines = new List<Coroutine>();
            for (int i = 0; i < n; i++)
            {
                if (tiles[i] == null) continue;
                routines.Add(StartCoroutine(DropOne(tiles[i].transform, origPos[i], origScale[i])));
                if (tileStagger > 0f) yield return new WaitForSeconds(tileStagger);
            }
            // 마지막 낙하까지 대기
            yield return new WaitForSeconds(tileDropDuration);

            // 안전: 전부 정확히 원위치·원스케일로 확정
            for (int i = 0; i < n; i++)
            {
                if (tiles[i] == null) continue;
                tiles[i].transform.position = origPos[i];
                tiles[i].transform.localScale = origScale[i];
            }
        }

        private IEnumerator DropOne(Transform t, Vector3 targetPos, Vector3 targetScale)
        {
            Vector3 startPos = t.position;
            Vector3 startScale = t.localScale;
            float elapsed = 0f;
            while (elapsed < tileDropDuration)
            {
                if (t == null) yield break;
                float k = EaseOutQuad(elapsed / tileDropDuration);
                t.position = Vector3.LerpUnclamped(startPos, targetPos, k);
                t.localScale = Vector3.LerpUnclamped(startScale, targetScale, k);
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (t != null) { t.position = targetPos; t.localScale = targetScale; }
        }

        // ── Phase 2: 캐릭터 순차 팝인 ────────────────────────
        private IEnumerator CharacterPopPhase()
        {
            var party = PartyManager.Instance != null ? PartyManager.Instance.Party : null;
            if (party == null) yield break;

            foreach (var c in party)
            {
                if (c == null || !c.IsAlive) continue;
                StartCoroutine(PopIn(c.transform, c.transform.localScale));
                if (charStagger > 0f) yield return new WaitForSeconds(charStagger);
            }
            yield return new WaitForSeconds(charPopDuration);
        }

        // ── Phase 3: 몬스터 순차 소환 ────────────────────────
        private IEnumerator MonsterSummonPhase(IReadOnlyList<Monster> monsters)
        {
            if (monsters == null || monsters.Count == 0) yield break;

            foreach (var m in monsters)
            {
                if (m == null) continue;
                VfxService.PlayOn(VfxTags.Summon, m);
                StartCoroutine(PopIn(m.transform, Vector3.one));   // 숨길 때 0 → 원스케일 1로
                if (monsterStagger > 0f) yield return new WaitForSeconds(monsterStagger);
            }
            yield return new WaitForSeconds(monsterPopDuration);
        }

        /// <summary>스케일 0 → target 팝인 (EaseOutBack).</summary>
        private IEnumerator PopIn(Transform t, Vector3 target)
        {
            if (t == null) yield break;
            t.localScale = Vector3.zero;
            float elapsed = 0f;
            while (elapsed < charPopDuration)   // 캐릭터/몬스터 공용 — 길이 동일
            {
                if (t == null) yield break;
                float k = EaseOutBack(elapsed / charPopDuration);
                t.localScale = Vector3.LerpUnclamped(Vector3.zero, target, k);
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (t != null) t.localScale = target;
        }

        // ── 이징 ──────────────────────────────────────────────
        private static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);
        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = 1.70158f + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
```

주의: `Monster`/`Character`는 `DiceOrbit.Core`. `TileData`는 `DiceOrbit.Data`. `PopIn`은 캐릭터·몬스터 공용이며 길이는 `charPopDuration` 하나로 통일(monsterPopDuration은 phase 끝 대기용). 몬스터 소환 팝인도 자연스러움을 위해 같은 길이 사용 — 필요 시 후속 조정.

- [ ] **Step 2: 컴파일 게이트**

Refresh → 에러 0건.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Visuals/Vfx/CombatIntroDirector.cs Assets/Scripts/Visuals/Vfx/CombatIntroDirector.cs.meta
git commit -m "feat: CombatIntroDirector - 타일 낙하/캐릭터 팝인/몬스터 소환 코루틴"
```

---

### Task 4: CombatManager.StartEncounter 코루틴화 (연출 삽입)

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/CombatManager.cs:157-172`

**Interfaces:**
- Consumes: Task 2 `EncounterSpawner.Spawn(encounter, startHidden:true)`, Task 3 `CombatIntroDirector.EnsureInstance().Play(spawned)`
- Produces: 없음 (오케스트레이션)

- [ ] **Step 1: StartEncounter를 연출 코루틴 체이닝으로 교체**

`CombatManager.cs`의 `StartEncounter`(`:157-172`) 교체:

```csharp
        /// <summary>전투 진입점 (노드맵 흐름). 몬스터 숨겨 스폰 → 시작 연출 → CombatStart 방송 → 턴 시퀀스.</summary>
        public void StartEncounter(Run.EncounterDefinition encounter, int floorNumber)
        {
            if (inCombat) return;

            DestroyActiveMonsters();
            CurrentEncounter = encounter;
            CurrentFloorNumber = floorNumber;

            // 숨긴 채 전량 스폰 (전멸 판정/인텐트 로직은 즉시 유효, 연출이 순차로 드러냄)
            var spawned = EncounterSpawner.EnsureInstance().Spawn(encounter, startHidden: true);
            foreach (var m in spawned)
                RegisterMonster(m);
            // 사망 구독 불필요 — Monster가 사망 시 OnMonsterDefeated를 직접 호출한다 (이중 호출 금지).

            StartCoroutine(IntroThenStart(spawned));
        }

        /// <summary>시작 연출(타일→캐릭터→몬스터) 후 전투 개시.</summary>
        private System.Collections.IEnumerator IntroThenStart(System.Collections.Generic.List<Monster> spawned)
        {
            yield return Visuals.CombatIntroDirector.EnsureInstance().Play(spawned);
            BroadcastCombatStart();
            StartCombat();
        }
```

주의: `Monster` 타입은 이 파일에서 이미 사용 중(activeMonsters). `Visuals` 네임스페이스는 완전 수식(`Visuals.CombatIntroDirector`)으로 접근 — 파일 상단에 `using DiceOrbit.Visuals;`가 없어도 무방(`DiceOrbit.Core` 안에서 `Visuals`는 `DiceOrbit.Visuals`로 해석). 안 되면 `DiceOrbit.Visuals.CombatIntroDirector`로 완전 수식.

- [ ] **Step 2: 컴파일 게이트**

Refresh → 에러 0건.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/CombatManager.cs
git commit -m "feat: 전투 진입을 시작 연출 코루틴으로 체이닝 (StartEncounter)"
```

---

### Task 5: 씬 배치 + 검증 + 푸시

**Files:**
- Modify: `Assets/Scenes/BattleScene.unity` (CombatIntroDirector 배치 — RunCommand)

**Interfaces:**
- Consumes: Task 3 `CombatIntroDirector`

- [ ] **Step 1: 씬에 CombatIntroDirector 배치 (RunCommand)**

`EnsureInstance`가 없으면 런타임 자동 생성하지만, 튜닝값 인스펙터 노출 위해 씬에 배치.

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DiceOrbit.Visuals;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (Object.FindFirstObjectByType<CombatIntroDirector>(FindObjectsInactive.Include) != null)
        { result.Log("CombatIntroDirector 이미 존재"); return; }

        var go = new GameObject("CombatIntroDirector");
        go.AddComponent<CombatIntroDirector>();
        Undo.RegisterCreatedObjectUndo(go, "CombatIntroDirector");

        var scene = go.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        result.Log("CombatIntroDirector 배치 + 씬 저장");
    }
}
```

- [ ] **Step 2: 소환 큐 해소 검증 (RunCommand 어서션)**

```csharp
using UnityEditor;
using UnityEngine;
using DiceOrbit.Visuals;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var lib = AssetDatabase.LoadAssetAtPath<VfxLibrary>("Assets/Resources/Skill/VFX/VfxLibrary.asset");
        var cue = lib.ResolveCue(VfxTags.Summon);
        if (cue == null || cue.prefab == null) { result.LogError("summon 큐 미해소"); return; }
        result.Log("PASS - summon 큐 해소: " + cue.prefab.name);
    }
}
```
Expected: `PASS - summon 큐 해소: ...`

- [ ] **Step 3: 최종 컴파일 게이트 + 커밋 + 푸시**

Refresh → 에러 0건 (기존 무관 경고 허용).

```bash
git add Assets/Scenes/BattleScene.unity
git commit -m "feat: 씬에 CombatIntroDirector 배치"
git push origin feature/vfx-redesign-20260731
```

- [ ] **Step 4: 플레이 검증 인계** — 스펙 §7 체크리스트를 사용자에게 인계 (자동 검증 불가): 타일 시계방향 낙하 → 캐릭터 팝인 → 몬스터 소환, 연출 중 입력 불가, 종료 후 배너+주사위, 매 전투 반복 시 위치·스케일 정상 복원, 시계방향 방향(아니면 clockwise 토글), 몬스터 0/파티 특수 케이스.

## Self-Review 결과

- 스펙 커버리지: §3.1 디렉터→Task 3, §3.2 3단계→Task 3, §3.3 숨긴 스폰→Task 2, §3.4 오케스트레이션→Task 4, §3.5 summon VFX→Task 1, §4 파일구조→전 태스크, §5 엣지케이스(몬스터0/파티0/타일없음→Task 3 가드; 재진입 복원→Task 3 캐시/확정), §6 타이밍→Task 3 [SerializeField], §7 검증→Task 5 Step 4. 누락 없음.
- 타입 일관성: `VfxTags.Summon`(T1↔T3), `Spawn(encounter, startHidden)`(T2↔T4), `CombatIntroDirector.EnsureInstance().Play(IReadOnlyList<Monster>)`(T3↔T4), `Play` 인자를 T4가 `List<Monster>`로 넘김(List는 IReadOnlyList 구현 — 호환) 교차 확인 완료.
- 알려진 리스크: (1) `PopIn` 길이를 `charPopDuration` 하나로 통일 — 몬스터도 같은 길이 사용, 어색하면 후속 분리(주의사항 명시). (2) `clockwise` 방향은 카메라 의존이라 플레이로 토글 확정(검증 항목). (3) 몬스터 원스케일을 `Vector3.one`으로 가정 — 프리셋이 1이 아닌 스케일을 쓰면 조정 필요(현 스폰은 프리팹 스케일 유지, 대부분 1). (4) 타일 트랜스폼 이동이 다른 씬 참조와 충돌하지 않도록 Phase 끝에서 원값 확정(캐릭터·몬스터 숨김 상태라 안전).
