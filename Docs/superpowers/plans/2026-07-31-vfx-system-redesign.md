# VFX 시스템 재설계 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 파티클·카메라 연출을 GameplayCue 스타일 단일 서비스(VfxService) + 단일 라이브러리(VfxLibrary)로 통합하고, 비어 있던 연출 훅(타일 조준·상태·사망·포션·전투 시작종료 등)을 채운다.

**Architecture:** 스펙 `Docs/superpowers/specs/2026-07-31-vfx-system-redesign.md`. 스킬/효과가 **큐 태그(문자열)** 를 직접 지정하고, 태그는 점 계층 상향 폴백으로 라이브러리에서 프리팹을 해소한다. 전투 hit/heal은 기존대로 CombatPipeline이 자동 재생, 나머지는 각 훅에서 명시 호출. 기존 CFXR 프리팹은 그대로 쓰고 배선만 새 라이브러리로 이관.

**Tech Stack:** Unity 6000.3.8f1, C#, Unity MCP(RunCommand/GetConsoleLogs), ScriptableObject, CFXR(Cartoon FX Remaster) 프리팹.

## Global Constraints

- 자동 테스트 프레임워크 없음. **각 태스크 게이트 = Unity MCP 컴파일 게이트**: `AssetDatabase.Refresh` → `GetConsoleLogs(error)` 0건. 순수 로직은 RunCommand 어서션으로 검증.
- RunCommand 스크립트는 `internal class CommandScript : IRunCommand`. `Image` 타입은 `UnityEngine.UI.Image` 완전 수식. 파일 삭제는 git rm/파일시스템(일부 AssetDatabase API 차단됨).
- enum 멤버 추가는 **항상 끝에** (직렬화 순서 보존).
- 전투 중 HP 변경은 파이프라인 경유(이 작업 무관 — VFX만 추가).
- 새 코드 네임스페이스: `DiceOrbit.Visuals` (기존 VfxManager와 동일). 파일 위치 `Assets/Scripts/Visuals/Vfx/`.
- 커밋 메시지 큰따옴표 금지. 끝에 `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.
- 각 단계에서 게임은 **컴파일·플레이 가능** 상태 유지 (라이브러리 미배선 구간엔 VFX가 잠시 없을 수 있음 — 비파괴적, 허용).
- 브랜치: `feature/vfx-redesign-20260731` (이미 체크아웃됨).

---

### Task 1: 태그 + Cue 정의 + VfxLibrary (데이터 & 해소)

**Files:**
- Create: `Assets/Scripts/Visuals/Vfx/VfxTags.cs`
- Create: `Assets/Scripts/Visuals/Vfx/VfxCue.cs`
- Create: `Assets/Scripts/Visuals/Vfx/VfxLibrary.cs`

**Interfaces:**
- Produces: `VfxTags` 상수(Cast/Impact/Heal/TileImpact/Death/Potion/Artifact/CombatStart/Victory/Defeat/LevelUp/Status) + `VfxTags.Lineage(string)`; `enum VfxCuePlay{Burst,Looping}`; `class ShakePreset{float amplitude,duration}`; `class VfxCue{string tag; VfxCuePlay play; GameObject prefab; Vector3 offset; float lifetime; ShakePreset shake; float hitStop}`; `enum TileVfxTrigger{OnTraverse,OnArrive,OnEndTurn}`; `class TileAttributeEntry{TileAttributeType attributeType; TileVfxTrigger trigger; GameObject prefab; Vector3 offset; float lifetime}`; `VfxLibrary : ScriptableObject`에 `VfxCue ResolveCue(string tag)`, `bool TryGetTile(TileAttributeType, TileVfxTrigger, out TileAttributeEntry)` — Task 2가 사용.

- [ ] **Step 1: VfxTags.cs 작성**

```csharp
using System.Collections.Generic;

namespace DiceOrbit.Visuals
{
    /// <summary>VFX 큐 태그 상수 + 계층 폴백 헬퍼 (GAS GameplayTag 스타일).</summary>
    public static class VfxTags
    {
        public const string Cast = "cast";
        public const string Impact = "impact";
        public const string Heal = "heal";
        public const string TileImpact = "tileImpact";
        public const string Death = "death";
        public const string Potion = "potion";
        public const string Artifact = "artifact";
        public const string CombatStart = "combatStart";
        public const string Victory = "victory";
        public const string Defeat = "defeat";
        public const string LevelUp = "levelUp";
        public const string Status = "status";

        /// <summary>태그와 조상들을 구체→일반 순으로 반환. "impact.fire" → impact.fire, impact.</summary>
        public static IEnumerable<string> Lineage(string tag)
        {
            while (!string.IsNullOrEmpty(tag))
            {
                yield return tag;
                int i = tag.LastIndexOf('.');
                tag = i < 0 ? null : tag.Substring(0, i);
            }
        }
    }
}
```

- [ ] **Step 2: VfxCue.cs 작성**

```csharp
using System;
using UnityEngine;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Visuals
{
    public enum VfxCuePlay { Burst, Looping }

    /// <summary>타일 이벤트 트리거 (구 TileVfxDatabase에서 이전 — 이름 보존).</summary>
    public enum TileVfxTrigger { OnTraverse, OnArrive, OnEndTurn }

    [Serializable]
    public class ShakePreset
    {
        public float amplitude = 0f;   // 0 = 쉐이크 없음
        public float duration = 0f;
    }

    /// <summary>큐 정의 — 태그 하나 = 프리팹 + 재생 방식 + 임팩트 피드백.</summary>
    [Serializable]
    public class VfxCue
    {
        public string tag;
        public VfxCuePlay play = VfxCuePlay.Burst;
        public GameObject prefab;
        public Vector3 offset;
        public float lifetime = 2f;    // Burst 전용
        public ShakePreset shake = new ShakePreset();
        public float hitStop = 0f;     // 초(realtime), 0 = 없음
        // public AudioClip sound;     // 예약 — 이번 미사용
    }

    /// <summary>타일 속성 VFX 엔트리 (구 TileVfxDatabase.TileAttributeEntry 이전).</summary>
    [Serializable]
    public class TileAttributeEntry
    {
        public TileAttributeType attributeType = TileAttributeType.None;
        public TileVfxTrigger trigger = TileVfxTrigger.OnTraverse;
        public GameObject prefab;
        public Vector3 offset = new Vector3(0f, 0.2f, 0f);
        public float lifetime = 2f;
    }
}
```

- [ ] **Step 3: VfxLibrary.cs 작성**

```csharp
using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 모든 VFX 큐의 단일 출처 (구 CombatVfxProfile 5종 + TileVfxDatabase 통합).
    /// 태그 해소는 계층 상향 폴백. 타일 속성 VFX는 (attributeType, trigger) 키.
    /// </summary>
    [CreateAssetMenu(fileName = "VfxLibrary", menuName = "Dice Orbit/VFX/Vfx Library")]
    public class VfxLibrary : ScriptableObject
    {
        [SerializeField] private List<VfxCue> cues = new List<VfxCue>();
        [SerializeField] private List<TileAttributeEntry> attributeEntries = new List<TileAttributeEntry>();

        private Dictionary<string, VfxCue> cueCache;
        private Dictionary<(TileAttributeType, TileVfxTrigger), TileAttributeEntry> tileCache;

        /// <summary>태그를 구체→일반으로 폴백하며 첫 매칭 큐 반환. 없으면 null.</summary>
        public VfxCue ResolveCue(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;
            BuildCache();
            foreach (var t in VfxTags.Lineage(tag))
                if (cueCache.TryGetValue(t, out var cue)) return cue;
            return null;
        }

        public bool TryGetTile(TileAttributeType type, TileVfxTrigger trigger, out TileAttributeEntry entry)
        {
            BuildCache();
            return tileCache.TryGetValue((type, trigger), out entry);
        }

        private void BuildCache()
        {
            if (cueCache != null && tileCache != null) return;
            cueCache = new Dictionary<string, VfxCue>();
            foreach (var c in cues)
                if (c != null && !string.IsNullOrEmpty(c.tag)) cueCache[c.tag] = c;
            tileCache = new Dictionary<(TileAttributeType, TileVfxTrigger), TileAttributeEntry>();
            foreach (var e in attributeEntries)
                if (e != null) tileCache[(e.attributeType, e.trigger)] = e;
        }

        private void OnValidate() { cueCache = null; tileCache = null; }

        // 에디터 배선용 (RunCommand가 SerializedObject로 접근하므로 런타임 API는 최소)
        public IReadOnlyList<VfxCue> Cues => cues;
        public IReadOnlyList<TileAttributeEntry> AttributeEntries => attributeEntries;
    }
}
```

- [ ] **Step 4: 컴파일 게이트 + 해소 어서션 (RunCommand)**

```csharp
using UnityEngine;
using DiceOrbit.Visuals;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var lib = ScriptableObject.CreateInstance<VfxLibrary>();
        var so = new UnityEditor.SerializedObject(lib);
        var cues = so.FindProperty("cues");
        cues.arraySize = 1;
        cues.GetArrayElementAtIndex(0).FindPropertyRelative("tag").stringValue = "impact";
        so.ApplyModifiedPropertiesWithoutUndo();

        if (lib.ResolveCue("impact.fire") == null) { result.LogError("계층 폴백 실패 (impact.fire→impact)"); return; }
        if (lib.ResolveCue("impact") == null) { result.LogError("정확 매칭 실패"); return; }
        if (lib.ResolveCue("heal") != null) { result.LogError("미등록 태그가 매칭됨"); return; }
        if (lib.ResolveCue("") != null || lib.ResolveCue(null) != null) { result.LogError("빈 태그 처리 실패"); return; }

        Object.DestroyImmediate(lib);
        result.Log("PASS - 태그 계층 폴백 확인");
    }
}
```
Expected: `PASS` 로그, 컴파일 에러 0.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Visuals/Vfx/VfxTags.cs Assets/Scripts/Visuals/Vfx/VfxCue.cs Assets/Scripts/Visuals/Vfx/VfxLibrary.cs Assets/Scripts/Visuals/Vfx/*.meta
git commit -m "feat: VFX 태그/Cue/VfxLibrary - GameplayCue 스타일 데이터 계층"
```

---

### Task 2: VfxService (스폰 + 루프 + 타일 이벤트)

**Files:**
- Create: `Assets/Scripts/Visuals/Vfx/VfxService.cs`

**Interfaces:**
- Consumes: `VfxLibrary`, `VfxTags`, `TileVfxTrigger`, `Data.TileData`, `Core.Unit`
- Produces: `VfxService` 싱글톤. `static void Play(string tag, Vector3 at)`, `static void PlayOn(string tag, Unit unit)`, `static void PlayOn(string tag, TileData tile)`, `static void StartLoop(string tag, Unit unit)`, `static void StopLoop(Unit unit, string tag)`, `static void PlayTileEvent(TileData tile, TileVfxTrigger trigger)`, `static void EnsureInstance()` — Task 3~8이 사용. (임팩트 훅 `ImpactFeedback.Shake/HitStop`은 Task 3에서 채움 — 이 태스크에선 그 자리를 주석 자리표시로 두지 말고 조건 분기만 만들고 Task 3 전엔 no-op 헬퍼를 같은 파일에 임시로 두지 않는다. 대신 Task 3에서 호출 삽입.)

- [ ] **Step 1: VfxService.cs 작성**

```csharp
using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Data;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// VFX 스폰 단일 창구 (구 VfxManager + TileVfxManager 통합).
    /// 큐 태그 → VfxLibrary 해소 → 스폰. Burst=수명 뒤 Destroy, Looping=대상 부착 후 StopLoop까지 유지.
    /// </summary>
    public class VfxService : MonoBehaviour
    {
        private const string LibraryResourcePath = "Skill/VFX/VfxLibrary";

        public static VfxService Instance { get; private set; }

        [SerializeField] private VfxLibrary library;
        [SerializeField] private Transform vfxRoot;

        // 지속(Looping) 인스턴스 추적: (유닛 인스턴스ID, 태그) → 스폰된 오브젝트
        private readonly Dictionary<(int, string), GameObject> loops = new Dictionary<(int, string), GameObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (library == null) library = Resources.Load<VfxLibrary>(LibraryResourcePath);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            var existing = FindAnyObjectByType<VfxService>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return; }
            new GameObject("VfxService").AddComponent<VfxService>();
        }

        // ── Burst ────────────────────────────────────────────
        public static void Play(string tag, Vector3 at)
        {
            EnsureInstance();
            Instance?.SpawnBurst(tag, at);
        }

        public static void PlayOn(string tag, Unit unit)
        {
            if (unit == null) return;
            Play(tag, unit.transform.position);
        }

        public static void PlayOn(string tag, DiceOrbit.Data.TileData tile)
        {
            if (tile == null) return;
            Play(tag, tile.Position);
        }

        // ── Looping ──────────────────────────────────────────
        public static void StartLoop(string tag, Unit unit)
        {
            if (unit == null) return;
            EnsureInstance();
            Instance?.SpawnLoop(tag, unit);
        }

        public static void StopLoop(Unit unit, string tag)
        {
            if (unit == null || Instance == null) return;
            var key = (unit.GetInstanceID(), tag);
            if (Instance.loops.TryGetValue(key, out var go))
            {
                if (go != null) Destroy(go);
                Instance.loops.Remove(key);
            }
        }

        // ── 타일 속성 VFX (구 TileVfxManager) ─────────────────
        public static void PlayTileEvent(DiceOrbit.Data.TileData tile, TileVfxTrigger trigger)
        {
            if (tile == null) return;
            EnsureInstance();
            Instance?.PlayTileEventImpl(tile, trigger);
        }

        // ── 구현 ─────────────────────────────────────────────
        private void SpawnBurst(string tag, Vector3 at)
        {
            var cue = library != null ? library.ResolveCue(tag) : null;
            if (cue == null || cue.prefab == null) return;
            var go = Instantiate(cue.prefab, at + cue.offset, Quaternion.identity, vfxRoot);
            if (cue.lifetime > 0f) Destroy(go, cue.lifetime);
            // 임팩트 피드백 훅은 Task 3에서 여기 삽입
        }

        private void SpawnLoop(string tag, Unit unit)
        {
            var cue = library != null ? library.ResolveCue(tag) : null;
            if (cue == null || cue.prefab == null) return;
            var key = (unit.GetInstanceID(), tag);
            if (loops.TryGetValue(key, out var existing) && existing != null) return;   // 중복 방지
            var go = Instantiate(cue.prefab, unit.transform.position + cue.offset, Quaternion.identity, unit.transform);
            loops[key] = go;   // Looping은 lifetime 무시 — StopLoop까지 유지
        }

        private void PlayTileEventImpl(DiceOrbit.Data.TileData tile, TileVfxTrigger trigger)
        {
            if (library == null) return;
            foreach (var attribute in tile.GetAttributes())
            {
                if (attribute == null) continue;
                if (!library.TryGetTile(attribute.Type, trigger, out var entry)) continue;
                if (entry == null || entry.prefab == null) continue;
                var go = Instantiate(entry.prefab, tile.Position + entry.offset, Quaternion.identity, vfxRoot);
                if (entry.lifetime > 0f) Destroy(go, entry.lifetime);
                return;   // 기존 동작 보존: 첫 매칭 하나만
            }
        }
    }
}
```

주의: `TileData.GetAttributes()`, `TileData.Position`은 기존 API (TileVfxManager가 사용 중). `Unit`은 `DiceOrbit.Core`.

- [ ] **Step 2: 컴파일 게이트**

Refresh → 에러 0건. (런타임 스폰은 플레이 모드 필요 — 여기선 컴파일만.)

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Visuals/Vfx/VfxService.cs Assets/Scripts/Visuals/Vfx/VfxService.cs.meta
git commit -m "feat: VfxService - 큐 태그 스폰 + Looping + 타일 이벤트 단일 창구"
```

---

### Task 3: ImpactFeedback + CameraShaker (쉐이크/히트스탑)

**Files:**
- Create: `Assets/Scripts/Visuals/Vfx/CameraShaker.cs`
- Create: `Assets/Scripts/Visuals/Vfx/ImpactFeedback.cs`
- Modify: `Assets/Scripts/Visuals/Vfx/VfxService.cs` (SpawnBurst에 피드백 호출 삽입)

**Interfaces:**
- Consumes: `VfxCue.shake`, `VfxCue.hitStop`
- Produces: `ImpactFeedback.Shake(float amplitude, float duration)`, `ImpactFeedback.HitStop(float seconds)`; `CameraShaker` (씬 카메라 부모 피벗에 부착)

- [ ] **Step 1: CameraShaker.cs 작성**

```csharp
using System.Collections;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 카메라 흔들림 — 전용 피벗(이 컴포넌트가 붙은 트랜스폼)을 감쇠 랜덤 오프셋으로 흔든다.
    /// 카메라를 이 피벗의 자식으로 두면 유닛 빌보드(카메라 참조)와 충돌 없이 흔들 수 있다.
    /// </summary>
    public class CameraShaker : MonoBehaviour
    {
        public static CameraShaker Instance { get; private set; }

        private Vector3 baseLocalPos;
        private Coroutine running;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            baseLocalPos = transform.localPosition;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Shake(float amplitude, float duration)
        {
            if (amplitude <= 0f || duration <= 0f) return;
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(ShakeRoutine(amplitude, duration));
        }

        private IEnumerator ShakeRoutine(float amplitude, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float falloff = 1f - (elapsed / duration);
                Vector2 rand = Random.insideUnitCircle * amplitude * falloff;
                transform.localPosition = baseLocalPos + new Vector3(rand.x, rand.y, 0f);
                elapsed += Time.unscaledDeltaTime;   // 히트스탑(timeScale=0) 중에도 흔들리도록 unscaled
                yield return null;
            }
            transform.localPosition = baseLocalPos;
            running = null;
        }
    }
}
```

- [ ] **Step 2: ImpactFeedback.cs 작성**

```csharp
using System.Collections;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>카메라 쉐이크 + 히트스탑 진입점. VfxService가 큐 설정에 따라 호출.</summary>
    public class ImpactFeedback : MonoBehaviour
    {
        public static ImpactFeedback Instance { get; private set; }

        private Coroutine hitStopRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            var existing = FindAnyObjectByType<ImpactFeedback>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return; }
            new GameObject("ImpactFeedback").AddComponent<ImpactFeedback>();
        }

        public static void Shake(float amplitude, float duration)
        {
            if (CameraShaker.Instance != null) CameraShaker.Instance.Shake(amplitude, duration);
        }

        public static void HitStop(float seconds)
        {
            if (seconds <= 0f) return;
            EnsureInstance();
            if (Instance == null) return;
            if (Instance.hitStopRoutine != null) Instance.StopCoroutine(Instance.hitStopRoutine);
            Instance.hitStopRoutine = Instance.StartCoroutine(Instance.HitStopRoutine(seconds));
        }

        private IEnumerator HitStopRoutine(float seconds)
        {
            float prev = Time.timeScale;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = prev == 0f ? 1f : prev;   // 중첩 대비 0 복원 방지
            hitStopRoutine = null;
        }
    }
}
```

- [ ] **Step 3: VfxService.SpawnBurst에 피드백 호출 삽입**

`VfxService.cs`의 `SpawnBurst` 끝 주석 자리(`// 임팩트 피드백 훅은 Task 3에서 여기 삽입`)를 교체:

```csharp
            if (cue.lifetime > 0f) Destroy(go, cue.lifetime);
            if (cue.shake != null && cue.shake.amplitude > 0f)
                ImpactFeedback.Shake(cue.shake.amplitude, cue.shake.duration);
            if (cue.hitStop > 0f)
                ImpactFeedback.HitStop(cue.hitStop);
```

- [ ] **Step 4: 컴파일 게이트 + 커밋**

Refresh → 에러 0건.

```bash
git add Assets/Scripts/Visuals/Vfx/CameraShaker.cs Assets/Scripts/Visuals/Vfx/ImpactFeedback.cs Assets/Scripts/Visuals/Vfx/VfxService.cs Assets/Scripts/Visuals/Vfx/*.meta
git commit -m "feat: ImpactFeedback + CameraShaker - 카메라 쉐이크/히트스탑"
```

---

### Task 4: 전투 이관 (파이프라인 hit/heal + cast → VfxService)

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Pipeline/CombatContext.cs:38`
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Pipeline/CombatPipeline.cs:175,180`
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/SkillData/SkillData.cs:34,85,110,140`
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Skills/CharacterActiveTemplate.cs:42,50,111,118`

**Interfaces:**
- Consumes: Task 2 `VfxService.PlayOn`, `VfxTags`
- Produces: `CombatContext.VfxCue` (string) 필드; `SkillData`에 `string castCue`/`string impactCue`; `CharacterActiveTemplate`에 동일. (구 `VfxProfile`/`vfxProfile` 제거.) — Task 9가 프리셋에 배선.

- [ ] **Step 1: CombatContext 필드 교체**

`CombatContext.cs:38` 교체:

```csharp
        public string VfxCue;   // 적중/힐 시 재생할 큐 태그 (스킬이 지정, 비면 파이프라인이 루트 사용)
```

- [ ] **Step 2: CombatPipeline.ApplyAction 교체**

`CombatPipeline.cs`의 두 줄 교체:

```csharp
                    if (atk.IsEffected)
                        DiceOrbit.Visuals.VfxService.PlayOn(
                            string.IsNullOrEmpty(atk.VfxCue) ? DiceOrbit.Visuals.VfxTags.Impact : atk.VfxCue, atk.Target);
```
```csharp
                    heal.Target.Heal(Mathf.RoundToInt(heal.OutputValue));
                    DiceOrbit.Visuals.VfxService.PlayOn(
                        string.IsNullOrEmpty(heal.VfxCue) ? DiceOrbit.Visuals.VfxTags.Heal : heal.VfxCue, heal.Target);
```
(파일 상단에 이미 `using DiceOrbit.Visuals;` 있음 — `VfxService`/`VfxTags`를 무수식으로 써도 되나, 명확성 위해 완전 수식 유지 가능.)

- [ ] **Step 3: SkillData 필드/호출 교체**

`SkillData.cs:34` 교체:

```csharp
        [Tooltip("이 스킬의 큐 태그. 비우면 루트(cast/impact) 폴백")]
        [SerializeField] protected string castCue = "";
        [SerializeField] protected string impactCue = "";
```

`:85` 교체:

```csharp
            DiceOrbit.Visuals.VfxService.PlayOn(
                string.IsNullOrEmpty(castCue) ? DiceOrbit.Visuals.VfxTags.Cast : castCue, source);
```

`:110` 과 `:140` (둘 다 `context.VfxProfile = vfxProfile;`) 교체:

```csharp
                context.VfxCue = impactCue;   // 비면 파이프라인이 루트 impact 사용
```

- [ ] **Step 4: CharacterActiveTemplate 필드/프로퍼티/호출 교체**

`:42` 교체:

```csharp
        [SerializeField] protected string castCue = "";
        [SerializeField] protected string impactCue = "";
```

`:50` (`public CombatVfxProfile VfxProfile => vfxProfile;`) — **삭제** (사용처 없음, grep 확인됨).

`:111` 교체:

```csharp
            DiceOrbit.Visuals.VfxService.PlayOn(
                string.IsNullOrEmpty(castCue) ? DiceOrbit.Visuals.VfxTags.Cast : castCue, source);
```

`:118` 교체:

```csharp
                context.VfxCue = impactCue;
```

파일 상단 `using`에서 `CombatVfxProfile`가 더는 안 쓰이면 정리(있다면). `using DiceOrbit.Visuals;`가 없으면 완전 수식이라 무방.

- [ ] **Step 5: 컴파일 게이트 + 커밋**

Refresh → 에러 0건. (이 시점: 라이브러리 미배선이라 전투 VFX가 잠시 안 나옴 — Task 5에서 복구. 컴파일·플레이는 정상.)

```bash
git add Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Pipeline/CombatContext.cs Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Pipeline/CombatPipeline.cs Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/SkillData/SkillData.cs Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Skills/CharacterActiveTemplate.cs
git commit -m "feat: 전투 VFX를 VfxService 큐 태그로 이관 (VfxProfile 제거)"
```

---

### Task 5: 라이브러리 에셋 생성 + 기존 배선 이관 + 씬 배치 (RunCommand)

**Files:**
- Create: `Assets/Resources/Skill/VFX/VfxLibrary.asset`
- Modify: `Assets/Scenes/BattleScene.unity` (VfxService/ImpactFeedback/CameraShaker 배치)
- Modify: 캐릭터 프리셋 4종 (castCue/impactCue)

**Interfaces:**
- Consumes: Task 1 VfxLibrary, 기존 `TileVfxDatabase.asset`(속성 엔트리), 씬 VfxManager fallback 프리팹, `CombatVfxProfile` 4종의 hit/cast 프리팹
- Produces: 배선된 VfxLibrary.asset (impact/heal 루트 + impact.slash/arcane/blade/toxin + cast 루트 + 타일 속성 엔트리), 씬 VfxService

- [ ] **Step 1: RunCommand — 라이브러리 생성 + 이관**

핵심: (a) VfxLibrary.asset 생성, (b) 씬 VfxManager의 defaultAttackHitVfx/defaultHealVfx → `impact`/`heal` 루트 Cue, (c) 4개 CombatVfxProfile의 hitVfxPrefab/castVfxPrefab → `impact.<theme>`/`cast.<theme>`, (d) TileVfxDatabase의 attributeEntries 복사.

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DiceOrbit.Visuals;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        // (a) 라이브러리 에셋
        var lib = AssetDatabase.LoadAssetAtPath<VfxLibrary>("Assets/Resources/Skill/VFX/VfxLibrary.asset");
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<VfxLibrary>();
            AssetDatabase.CreateAsset(lib, "Assets/Resources/Skill/VFX/VfxLibrary.asset");
        }
        var so = new SerializedObject(lib);
        var cues = so.FindProperty("cues");
        var attrs = so.FindProperty("attributeEntries");
        cues.arraySize = 0; attrs.arraySize = 0;

        // (b) 씬 VfxManager fallback → impact/heal 루트
        var vm = Object.FindFirstObjectByType<DiceOrbit.Visuals.VfxManager>(FindObjectsInactive.Include);
        GameObject hitFallback = null, healFallback = null;
        if (vm != null)
        {
            var vmso = new SerializedObject(vm);
            hitFallback = vmso.FindProperty("defaultAttackHitVfx").objectReferenceValue as GameObject;
            healFallback = vmso.FindProperty("defaultHealVfx").objectReferenceValue as GameObject;
        }
        AddCue(cues, "impact", hitFallback, new Vector3(0,1,0), 2.5f);
        AddCue(cues, "heal", healFallback, new Vector3(0,1,0), 2.5f);
        AddCue(cues, "cast", null, new Vector3(0,1,0), 2.5f);

        // (c) 캐릭터 프로필 → impact.<theme>/cast.<theme>
        WireProfile(cues, "Warrior_CombatVfxProfile", "slash", result);
        WireProfile(cues, "Mage_CombatVfxProfile", "arcane", result);
        WireProfile(cues, "Rogue_CombatVfxProfile", "blade", result);
        WireProfile(cues, "Alchemist_CombatVfxProfile", "toxin", result);

        // (d) TileVfxDatabase 속성 엔트리 복사
        var tdb = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Resources/Skill/VFX/TileVfxDatabase.asset");
        if (tdb != null)
        {
            var tso = new SerializedObject(tdb);
            var src = tso.FindProperty("attributeEntries");
            for (int i = 0; i < src.arraySize; i++)
            {
                var e = src.GetArrayElementAtIndex(i);
                attrs.arraySize++;
                var d = attrs.GetArrayElementAtIndex(attrs.arraySize - 1);
                d.FindPropertyRelative("attributeType").enumValueIndex = e.FindPropertyRelative("attributeType").enumValueIndex;
                d.FindPropertyRelative("trigger").enumValueIndex = e.FindPropertyRelative("trigger").enumValueIndex;
                d.FindPropertyRelative("prefab").objectReferenceValue = e.FindPropertyRelative("prefab").objectReferenceValue;
                d.FindPropertyRelative("offset").vector3Value = e.FindPropertyRelative("offset").vector3Value;
                d.FindPropertyRelative("lifetime").floatValue = e.FindPropertyRelative("lifetime").floatValue;
            }
            result.Log("타일 속성 엔트리 " + src.arraySize + "개 이전");
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(lib);

        // (e) 씬에 VfxService/ImpactFeedback 배치 + 라이브러리 배선
        var svcGo = new GameObject("VfxService");
        var svc = svcGo.AddComponent<VfxService>();
        var svcso = new SerializedObject(svc);
        svcso.FindProperty("library").objectReferenceValue = lib;
        svcso.ApplyModifiedPropertiesWithoutUndo();
        svcGo.AddComponent<ImpactFeedback>();
        Undo.RegisterCreatedObjectUndo(svcGo, "VfxService");

        var scene = svcGo.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        result.Log("VfxLibrary 배선 + 씬 VfxService 배치 완료");
    }

    private static void AddCue(SerializedProperty cues, string tag, GameObject prefab, Vector3 offset, float lifetime)
    {
        cues.arraySize++;
        var c = cues.GetArrayElementAtIndex(cues.arraySize - 1);
        c.FindPropertyRelative("tag").stringValue = tag;
        c.FindPropertyRelative("play").enumValueIndex = 0; // Burst
        c.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        c.FindPropertyRelative("offset").vector3Value = offset;
        c.FindPropertyRelative("lifetime").floatValue = lifetime;
        c.FindPropertyRelative("hitStop").floatValue = 0f;
    }

    private static void WireProfile(SerializedProperty cues, string profileName, string theme, ExecutionResult result)
    {
        var guids = AssetDatabase.FindAssets(profileName + " t:CombatVfxProfile");
        if (guids.Length == 0) { result.LogWarning(profileName + " 없음"); return; }
        var prof = AssetDatabase.LoadAssetAtPath<DiceOrbit.Visuals.CombatVfxProfile>(AssetDatabase.GUIDToAssetPath(guids[0]));
        if (prof == null) return;
        if (prof.hitVfxPrefab != null) AddCue(cues, "impact." + theme, prof.hitVfxPrefab, prof.hitOffset, prof.defaultLifetime);
        if (prof.castVfxPrefab != null) AddCue(cues, "cast." + theme, prof.castVfxPrefab, prof.castOffset, prof.defaultLifetime);
        result.Log("프로필 이관: " + profileName + " → impact." + theme);
    }
}
```

- [ ] **Step 2: 캐릭터 프리셋에 castCue/impactCue 배선 (RunCommand)**

캐릭터 4종의 액티브 스킬(직렬화 필드 `castCue`/`impactCue`)에 테마 태그 지정. 프리셋 내 스킬은 `[SerializeReference]`라 경로가 프리셋마다 다를 수 있으므로, 프리셋 SO를 열어 `impactCue`/`castCue` 프로퍼티를 재귀 탐색해 세팅.

```csharp
using UnityEditor;
using UnityEngine;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        Wire("Assets/Scripts/Data/Character Preset/Warrior/Warrior.asset", "slash", result);
        Wire("Assets/Scripts/Data/Character Preset/Mage/Mage.asset", "arcane", result);
        Wire("Assets/Scripts/Data/Character Preset/Rogue/Rogue.asset", "blade", result);
        Wire("Assets/Scripts/Data/Character Preset/Alchemist/Alchemist.asset", "toxin", result);
        AssetDatabase.SaveAssets();
        result.Log("캐릭터 큐 태그 배선 완료");
    }

    private static void Wire(string path, string theme, ExecutionResult result)
    {
        var obj = AssetDatabase.LoadMainAssetAtPath(path);
        if (obj == null) { result.LogWarning(path + " 없음"); return; }
        var so = new SerializedObject(obj);
        var p = so.GetIterator();
        int hits = 0;
        while (p.NextVisible(true))
        {
            if (p.propertyType != SerializedPropertyType.String) continue;
            if (p.name == "impactCue") { p.stringValue = "impact." + theme; hits++; }
            else if (p.name == "castCue") { p.stringValue = "cast." + theme; hits++; }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(obj);
        result.Log(path + " → " + theme + " (" + hits + "개 필드)");
    }
}
```

- [ ] **Step 3: 컴파일 게이트 + 검증 로그 확인**

각 RunCommand 실행 후 로그에 이관 건수 확인. `GetConsoleLogs(error)` 0건.

- [ ] **Step 4: 커밋**

```bash
git add "Assets/Resources/Skill/VFX/VfxLibrary.asset" "Assets/Resources/Skill/VFX/VfxLibrary.asset.meta" Assets/Scenes/BattleScene.unity "Assets/Scripts/Data/Character Preset"
git commit -m "feat: VfxLibrary 에셋 생성 + 기존 프로필/타일/fallback 이관 + 씬 VfxService 배치"
```

---

### Task 6: 타일 VFX 흡수 + TileVfxManager/Database 제거

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/TileData.cs:200,210,220`
- Modify: `Assets/Scripts/Data/MonsterPresets/Wave1/Goblin/Goblin.cs:111`
- Delete: `Assets/Scripts/Visuals/TileVfxManager.cs`, `Assets/Scripts/Visuals/TileVfxDatabase.cs`, `Assets/Resources/Skill/VFX/TileVfxDatabase.asset`

**Interfaces:**
- Consumes: Task 2 `VfxService.PlayTileEvent`, `TileVfxTrigger` (이제 VfxCue.cs에 정의)
- Produces: TileData가 VfxService 경유

- [ ] **Step 1: TileData의 타일 이벤트 호출 교체**

`TileData.cs`의 세 줄 교체 (`TileVfxManager.PlayTileEvent` → `DiceOrbit.Visuals.VfxService.PlayTileEvent`, enum은 `DiceOrbit.Visuals.TileVfxTrigger`):

```csharp
            DiceOrbit.Visuals.VfxService.PlayTileEvent(this, DiceOrbit.Visuals.TileVfxTrigger.OnArrive);
```
```csharp
            DiceOrbit.Visuals.VfxService.PlayTileEvent(this, DiceOrbit.Visuals.TileVfxTrigger.OnTraverse);
```
```csharp
            DiceOrbit.Visuals.VfxService.PlayTileEvent(this, DiceOrbit.Visuals.TileVfxTrigger.OnEndTurn);
```

- [ ] **Step 2: Goblin 지뢰 설치 VFX 교체**

`Goblin.cs:111` (`VfxManager.PlayTile(vfxProfile, tile);`) 교체 — 지뢰 타일은 이미 TileImpact가 아니라 설치 연출이므로 `tileImpact` 태그로 타일 위치에 재생:

```csharp
                DiceOrbit.Visuals.VfxService.PlayOn(DiceOrbit.Visuals.VfxTags.TileImpact, tile);
```
(주변 `context.VfxProfile = vfxProfile;`(Goblin.cs:119)는 Task 4에서 이미 `SkillData` 필드가 바뀌었으므로 `context.VfxCue = impactCue;`로 되어 있어야 함 — Goblin이 SkillData 파생이면 자동. Goblin.cs가 직접 `vfxProfile`을 참조하는지 확인: `:119`는 SkillData 상속 필드 사용. Task 4에서 필드명이 `impactCue`로 바뀌었으니 `context.VfxCue = impactCue;`로 교체 필요.)

`Goblin.cs:119` 교체:

```csharp
                context.VfxCue = impactCue;
```

- [ ] **Step 3: TileVfxManager/Database 삭제 (파일시스템)**

```bash
git rm Assets/Scripts/Visuals/TileVfxManager.cs Assets/Scripts/Visuals/TileVfxManager.cs.meta
git rm Assets/Scripts/Visuals/TileVfxDatabase.cs Assets/Scripts/Visuals/TileVfxDatabase.cs.meta
git rm "Assets/Resources/Skill/VFX/TileVfxDatabase.asset" "Assets/Resources/Skill/VFX/TileVfxDatabase.asset.meta"
```

- [ ] **Step 4: 컴파일 게이트**

Refresh → 에러 0건. (TileVfxTrigger 참조가 전부 새 enum으로 옮겨졌는지 확인 — 남은 `TileVfxManager`/`TileVfxDatabase` 참조 없어야 함.)

- [ ] **Step 5: 커밋**

```bash
git add -A
git commit -m "refactor: 타일 VFX를 VfxService로 흡수 + TileVfxManager/Database 제거"
```

---

### Task 7: 신규 전투 훅 (타일 조준 + 상태 Looping + 사망)

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/SkillData/SkillData.cs` (AttackTiles)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/StatusEffect.cs:79-87`
- Modify: `Assets/Scripts/Core/Stage/BattleStage/Units/Unit.cs:117-121`

**Interfaces:**
- Consumes: `VfxService.PlayOn(tag, TileData)`, `VfxService.StartLoop/StopLoop`, `VfxTags`
- Produces: 없음 (훅만)

- [ ] **Step 1: 타일 조준 공격 — 모든 타일에 TileImpact**

`SkillData.AttackTiles` (`SkillData.cs:117`)의 유닛 수집 **전에** 조준 타일 순회 추가. 메서드 시작부 `if (source == null || !source.IsAlive) return;` 다음에:

```csharp
            // 조준된 모든 타일(빈 칸 포함)에 착탄 연출 — 유닛 히트는 아래 파이프라인이 별도 처리
            foreach (var tile in targetTiles)
            {
                if (tile == null) continue;
                string tileCue = string.IsNullOrEmpty(impactCue) ? DiceOrbit.Visuals.VfxTags.TileImpact : impactCue;
                DiceOrbit.Visuals.VfxService.PlayOn(tileCue, tile);
            }
```

- [ ] **Step 2: 상태이상 Looping — StatusEffect 베이스**

`StatusEffect.cs`의 `EffectApplied`/`EffectExpired` 본문 교체:

```csharp
        public virtual void EffectApplied()
        {
            if (Owner == null) return;
            DiceOrbit.Visuals.VfxService.StartLoop(StatusCueTag(), Owner);
        }

        public virtual void EffectExpired()
        {
            if (Owner == null) return;
            DiceOrbit.Visuals.VfxService.StopLoop(Owner, StatusCueTag());
        }

        /// <summary>상태별 큐 태그. 예: status.poison. 미등록이면 라이브러리가 status로 폴백.</summary>
        protected string StatusCueTag()
            => DiceOrbit.Visuals.VfxTags.Status + "." + Type.ToString().ToLowerInvariant();
```

주의: `EffectType`은 `DiceOrbit.Data`. `Type`은 이 클래스 필드. StartLoop 태그가 라이브러리에 없으면 no-op(폴백도 없으면). Looping 대상이 사망 시 오브젝트 부착이라 함께 정리됨(StopLoop 미호출이어도 부모 파괴로 소멸).

- [ ] **Step 3: 사망 연출 — Unit.HandleDeath 베이스**

`Unit.cs:117-121` 교체:

```csharp
        protected virtual void HandleDeath()
        {
            Debug.Log($"{name} has died.");
            DiceOrbit.Visuals.VfxService.PlayOn(DiceOrbit.Visuals.VfxTags.Death, this);
        }
```
(Monster/Character override는 `base.HandleDeath()`를 호출하므로 자동 상속 — 확인만.)

- [ ] **Step 4: 컴파일 게이트 + 커밋**

Refresh → 에러 0건.

```bash
git add Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/SkillData/SkillData.cs Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/StatusEffect.cs Assets/Scripts/Core/Stage/BattleStage/Units/Unit.cs
git commit -m "feat: 신규 전투 VFX 훅 - 타일 착탄/상태 Looping/사망"
```

---

### Task 8: 신규 런 훅 (포션 + 전투 시작종료 + 레벨업)

**Files:**
- Modify: `Assets/Scripts/Core/Run/PotionManager.cs` (TryUse/TryUseOn/TryUseOnTile 성공 분기)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/CombatManager.cs` (StartCombat/EndCombat)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/TileData.cs` (OnArrive index 0)

**Interfaces:**
- Consumes: `VfxService.Play/PlayOn`, `VfxTags`
- Produces: 없음

- [ ] **Step 1: 포션 사용 연출**

`PotionManager.cs`의 세 사용 메서드 성공 분기(각 `_slots.RemoveAt(index);` 직전)에 추가.

`TryUse` (`potion.Use()` 성공 후):
```csharp
            var user = PartyManager.Instance?.Party?.FirstOrDefault(c => c != null && c.IsAlive);
            if (user != null) DiceOrbit.Visuals.VfxService.PlayOn(DiceOrbit.Visuals.VfxTags.Potion, user);
```
`TryUseOn` (성공 후, `target` 있음):
```csharp
            DiceOrbit.Visuals.VfxService.PlayOn(DiceOrbit.Visuals.VfxTags.Potion, target);
```
`TryUseOnTile` (성공 후, `tile` 있음):
```csharp
            DiceOrbit.Visuals.VfxService.PlayOn(DiceOrbit.Visuals.VfxTags.Potion, tile);
```
(파일 상단에 `using System.Linq;` 이미 있음 — FirstOrDefault 사용 가능.)

- [ ] **Step 2: 전투 시작/종료 연출**

`CombatManager.StartCombat`의 `OnCombatStart?.Invoke();`(`:209`) 다음 줄:
```csharp
            DiceOrbit.Visuals.VfxService.Play(DiceOrbit.Visuals.VfxTags.CombatStart, Camera.main != null ? Camera.main.transform.position + Camera.main.transform.forward * 6f : Vector3.zero);
```

`EndCombat`의 결과 분기 — 승리/패배 판정 지점에 각각 (파일에서 승패 분기 위치 확인 후):
```csharp
            DiceOrbit.Visuals.VfxService.Play(DiceOrbit.Visuals.VfxTags.Victory, Camera.main != null ? Camera.main.transform.position + Camera.main.transform.forward * 6f : Vector3.zero);
```
```csharp
            DiceOrbit.Visuals.VfxService.Play(DiceOrbit.Visuals.VfxTags.Defeat, Camera.main != null ? Camera.main.transform.position + Camera.main.transform.forward * 6f : Vector3.zero);
```
(EndCombat에 승/패 구분이 없으면 승리만 배선하고 패배는 GameResultUI/GameFlow 패배 경로에 — 구현 시 실제 분기 확인. 분기 불명확 시 승리 하나만 배선하고 로그로 남긴다.)

- [ ] **Step 3: 레벨업 타일 연출**

`TileData.OnArrive`(`:198`) 안, 기존 `TileVfxManager`→`VfxService`로 바뀐 `PlayTileEvent` 호출 부근에 index 0 판정 추가:

```csharp
            if (tileIndex == 0)
                DiceOrbit.Visuals.VfxService.PlayOn(DiceOrbit.Visuals.VfxTags.LevelUp, this);
```
(`tileIndex`는 TileData 필드 — `TileData.cs:29` 확인됨.)

- [ ] **Step 4: 컴파일 게이트 + 커밋**

Refresh → 에러 0건.

```bash
git add Assets/Scripts/Core/Run/PotionManager.cs Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/CombatManager.cs Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/TileData.cs
git commit -m "feat: 신규 런 VFX 훅 - 포션/전투 시작종료/레벨업"
```

---

### Task 9: 라이브러리 저작 — CFXR 프리팹 + 쉐이크/히트스탑 + 몬스터 큐 (RunCommand)

**Files:**
- Modify: `Assets/Resources/Skill/VFX/VfxLibrary.asset` (신규 큐 + 쉐이크)
- Modify: 몬스터 프리셋 14종 (impactCue/castCue)
- Modify: `Assets/Scenes/BattleScene.unity` (CameraShaker 피벗 배치)

**Interfaces:**
- Consumes: CFXR 프리팹(이름 검색), Task 1~8의 태그
- Produces: 완성된 VfxLibrary + 몬스터 배선

- [ ] **Step 1: RunCommand — 신규 큐 엔트리 + 쉐이크/히트스탑 + Looping 상태**

CFXR 프리팹을 이름으로 찾아 tileImpact/death/potion/combatStart/victory/defeat/levelUp/status.* 및 impact.<몬스터테마> 엔트리 추가. 기존 impact/heal/cast 루트 엔트리엔 쉐이크 부여.

```csharp
using UnityEditor;
using UnityEngine;
using DiceOrbit.Visuals;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var lib = AssetDatabase.LoadAssetAtPath<VfxLibrary>("Assets/Resources/Skill/VFX/VfxLibrary.asset");
        if (lib == null) { result.LogError("VfxLibrary 없음 — Task 5 먼저"); return; }
        var so = new SerializedObject(lib);
        var cues = so.FindProperty("cues");

        // 신규 Burst 큐 (CFXR 이름 검색, 없으면 null → 무재생이지만 엔트리는 남김)
        Upsert(cues, "tileImpact", Find("CFXR Hit"), new Vector3(0,0.2f,0), 1.5f, 0.05f, 0.06f, 0f);
        Upsert(cues, "death", Find("CFXR Explosion Smoke"), new Vector3(0,1,0), 2f, 0.15f, 0.12f, 0.04f);
        Upsert(cues, "potion", Find("CFXR Magic Poof"), new Vector3(0,1,0), 1.5f, 0f, 0f, 0f);
        Upsert(cues, "combatStart", Find("CFXR _BANG_"), new Vector3(0,0,0), 1.5f, 0f, 0f, 0f);
        Upsert(cues, "victory", Find("CFXR Firework"), new Vector3(0,0,0), 2.5f, 0f, 0f, 0f);
        Upsert(cues, "defeat", Find("CFXR Explosion"), new Vector3(0,0,0), 2f, 0.25f, 0.2f, 0.05f);
        Upsert(cues, "levelUp", Find("CFXR Magical Source"), new Vector3(0,0.5f,0), 2f, 0f, 0f, 0f);

        // 몬스터 테마 impact (계층 폴백이라 다른 것만; 없으면 impact 루트 사용)
        Upsert(cues, "impact.fire", Find("CFXR Fire Explosion"), new Vector3(0,1,0), 2f, 0.08f, 0.08f, 0f);
        Upsert(cues, "impact.frost", Find("CFXR Hit B (Blue)"), new Vector3(0,1,0), 2f, 0.06f, 0.06f, 0f);

        // Looping 상태 오라 (play=1)
        UpsertLoop(cues, "status.poison", Find("CFXR Gas Leak"), new Vector3(0,0.5f,0));

        // 루트 impact/death에 쉐이크 보강 (Task 5에서 만든 엔트리)
        SetShake(cues, "impact", 0.05f, 0.06f);

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        result.Log("라이브러리 저작 완료 (cues=" + cues.arraySize + ")");
    }

    private static GameObject Find(string namePart)
    {
        var guids = AssetDatabase.FindAssets(namePart + " t:GameObject");
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (path.Contains("CFXR")) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
    }

    private static SerializedProperty FindOrAdd(SerializedProperty cues, string tag)
    {
        for (int i = 0; i < cues.arraySize; i++)
            if (cues.GetArrayElementAtIndex(i).FindPropertyRelative("tag").stringValue == tag)
                return cues.GetArrayElementAtIndex(i);
        cues.arraySize++;
        var c = cues.GetArrayElementAtIndex(cues.arraySize - 1);
        c.FindPropertyRelative("tag").stringValue = tag;
        return c;
    }

    private static void Upsert(SerializedProperty cues, string tag, GameObject prefab, Vector3 offset, float life, float shakeAmp, float shakeDur, float hitStop)
    {
        var c = FindOrAdd(cues, tag);
        c.FindPropertyRelative("play").enumValueIndex = 0;
        c.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        c.FindPropertyRelative("offset").vector3Value = offset;
        c.FindPropertyRelative("lifetime").floatValue = life;
        c.FindPropertyRelative("shake").FindPropertyRelative("amplitude").floatValue = shakeAmp;
        c.FindPropertyRelative("shake").FindPropertyRelative("duration").floatValue = shakeDur;
        c.FindPropertyRelative("hitStop").floatValue = hitStop;
    }

    private static void UpsertLoop(SerializedProperty cues, string tag, GameObject prefab, Vector3 offset)
    {
        var c = FindOrAdd(cues, tag);
        c.FindPropertyRelative("play").enumValueIndex = 1; // Looping
        c.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        c.FindPropertyRelative("offset").vector3Value = offset;
    }

    private static void SetShake(SerializedProperty cues, string tag, float amp, float dur)
    {
        var c = FindOrAdd(cues, tag);
        c.FindPropertyRelative("shake").FindPropertyRelative("amplitude").floatValue = amp;
        c.FindPropertyRelative("shake").FindPropertyRelative("duration").floatValue = dur;
    }
}
```
(CFXR 정확 프리팹명은 프로젝트마다 다를 수 있음 — `Find`가 substring 검색이라 유연. 못 찾으면 prefab=null로 엔트리만 남고 무재생, 로그 확인 후 수동 교체 가능.)

- [ ] **Step 2: 몬스터 프리셋 큐 태그 배선 (RunCommand)**

Task 5 Step 2의 `Wire` 방식으로 몬스터 14종 프리셋에 impactCue/castCue 지정 (테마: goblin/bone/beast/frost/lunar/solar/fire — 파동별).

```csharp
using UnityEditor;
using UnityEngine;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var map = new (string path, string theme)[]
        {
            ("Assets/Scripts/Data/MonsterPresets/Wave1/Goblin/W1_Goblin.asset", "goblin"),
            ("Assets/Scripts/Data/MonsterPresets/Wave2/MommyBear/MommyBear.asset", "beast"),
            ("Assets/Scripts/Data/MonsterPresets/Wave4/LunaKnight/LunaKnight.asset", "lunar"),
            ("Assets/Scripts/Data/MonsterPresets/Wave4/LunaPriest/LunaPriest.asset", "lunar"),
            ("Assets/Scripts/Data/MonsterPresets/Wave4/SolraKnight/SolraKnight.asset", "solar"),
            ("Assets/Scripts/Data/MonsterPresets/Wave4/SolraPriest/SolraPriest.asset", "solar"),
            ("Assets/Scripts/Data/MonsterPresets/Wave5/FlameDoll/FlameDoll.asset", "fire"),
            ("Assets/Scripts/Data/MonsterPresets/Wave5/FlameFairy/FlameFairy.asset", "fire"),
            ("Assets/Scripts/Data/MonsterPresets/Wave5/FlameGirl/FlameGirl.asset", "fire"),
            ("Assets/Scripts/Data/MonsterPresets/Wave5/FlameMusicBox/FlameMusicBox.asset", "fire"),
        };
        foreach (var (path, theme) in map)
        {
            var obj = AssetDatabase.LoadMainAssetAtPath(path);
            if (obj == null) { result.LogWarning(path + " 없음"); continue; }
            var so = new SerializedObject(obj);
            var p = so.GetIterator();
            int hits = 0;
            while (p.NextVisible(true))
            {
                if (p.propertyType != SerializedPropertyType.String) continue;
                if (p.name == "impactCue") { p.stringValue = "impact." + theme; hits++; }
                else if (p.name == "castCue") { p.stringValue = "cast." + theme; hits++; }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(obj);
            result.Log(path + " → " + theme + " (" + hits + ")");
        }
        AssetDatabase.SaveAssets();
        result.Log("몬스터 큐 배선 완료");
    }
}
```
(Skeleton/Bear아기/SnowGolem 등 프리셋 파일명이 위 목록과 다르면 실제 경로로 조정 — Glob `Assets/Scripts/Data/MonsterPresets/**/*.asset`로 확인 후.)

- [ ] **Step 3: CameraShaker 피벗 배치 (RunCommand)**

메인 카메라를 새 빈 게임오브젝트("CameraRig")의 자식으로 넣고, CameraRig에 CameraShaker 부착. (카메라 로컬 위치 보존.)

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DiceOrbit.Visuals;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var cam = Camera.main;
        if (cam == null) { result.LogError("메인 카메라 없음"); return; }
        if (cam.transform.parent != null && cam.transform.parent.GetComponent<CameraShaker>() != null)
        { result.Log("이미 CameraShaker 피벗 있음"); return; }

        var rig = new GameObject("CameraRig");
        rig.transform.SetParent(cam.transform.parent, false);
        rig.transform.position = cam.transform.position;
        rig.transform.rotation = cam.transform.rotation;
        cam.transform.SetParent(rig.transform, true);
        rig.AddComponent<CameraShaker>();
        Undo.RegisterCreatedObjectUndo(rig, "CameraRig");

        EditorSceneManager.MarkSceneDirty(rig.scene);
        EditorSceneManager.SaveScene(rig.scene);
        result.Log("CameraRig + CameraShaker 배치");
    }
}
```

- [ ] **Step 4: 컴파일 게이트 + 커밋**

Refresh → 에러 0건. 라이브러리 cues 개수 로그 확인.

```bash
git add "Assets/Resources/Skill/VFX/VfxLibrary.asset" Assets/Scenes/BattleScene.unity Assets/Scripts/Data/MonsterPresets
git commit -m "feat: VfxLibrary 저작 - CFXR 큐/쉐이크/히트스탑 + 몬스터 큐 태그 + CameraShaker"
```

---

### Task 10: 레거시 제거 + 최종 게이트

**Files:**
- Delete: `Assets/Scripts/Visuals/VfxManager.cs`, `Assets/Scripts/Visuals/CombatVfxProfile.cs`, `Assets/Scripts/Visuals/CombatVfxProfile.asset`, `Assets/Resources/Skill/VFX/*_CombatVfxProfile.asset` (5종)
- Modify: `Assets/Scenes/BattleScene.unity` (VfxManager 컴포넌트 제거)

**Interfaces:**
- Consumes: 전 태스크 (모든 참조가 VfxService로 이관됐음)

- [ ] **Step 1: 잔존 참조 확인**

```
Grep pattern: VfxManager|CombatVfxProfile  path: Assets/Scripts  (glob *.cs)
```
Expected: 0건 (전부 이관됨). 남으면 해당 파일 먼저 교정.

- [ ] **Step 2: 씬에서 VfxManager 컴포넌트 제거 (RunCommand)**

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var vm = Object.FindFirstObjectByType<DiceOrbit.Visuals.VfxManager>(FindObjectsInactive.Include);
        if (vm == null) { result.Log("씬에 VfxManager 없음"); return; }
        var scene = vm.gameObject.scene;
        Object.DestroyImmediate(vm);   // 컴포넌트만 제거 (게임오브젝트가 VfxManager 전용이면 오브젝트째 제거 판단)
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        result.Log("씬 VfxManager 컴포넌트 제거");
    }
}
```

- [ ] **Step 3: 스크립트/에셋 삭제 (파일시스템)**

```bash
git rm Assets/Scripts/Visuals/VfxManager.cs Assets/Scripts/Visuals/VfxManager.cs.meta
git rm Assets/Scripts/Visuals/CombatVfxProfile.cs Assets/Scripts/Visuals/CombatVfxProfile.cs.meta
git rm Assets/Scripts/Visuals/CombatVfxProfile.asset Assets/Scripts/Visuals/CombatVfxProfile.asset.meta
git rm "Assets/Resources/Skill/VFX/Warrior_CombatVfxProfile.asset" "Assets/Resources/Skill/VFX/Warrior_CombatVfxProfile.asset.meta"
git rm "Assets/Resources/Skill/VFX/Mage_CombatVfxProfile.asset" "Assets/Resources/Skill/VFX/Mage_CombatVfxProfile.asset.meta"
git rm "Assets/Resources/Skill/VFX/Rogue_CombatVfxProfile.asset" "Assets/Resources/Skill/VFX/Rogue_CombatVfxProfile.asset.meta"
git rm "Assets/Resources/Skill/VFX/Alchemist_CombatVfxProfile.asset" "Assets/Resources/Skill/VFX/Alchemist_CombatVfxProfile.asset.meta"
git rm "Assets/Resources/Skill/VFX/Goblin_CombatVfxProfile.asset" "Assets/Resources/Skill/VFX/Goblin_CombatVfxProfile.asset.meta"
```

- [ ] **Step 4: 최종 컴파일 게이트**

Refresh → 에러 0건 (기존 무관 경고 2건: TileInfoPanelUI panelWidth/dockAnchorX 허용). 신규 경고 확인.

- [ ] **Step 5: 커밋 + 푸시**

```bash
git add -A
git commit -m "refactor: 레거시 VfxManager/CombatVfxProfile 제거 - VFX 통합 완료"
git push origin feature/vfx-redesign-20260731
```

- [ ] **Step 6: 플레이 검증 인계** — 스펙 §10 체크리스트를 사용자에게 인계 (자동 검증 불가: 실제 연출 확인).

## Self-Review 결과

- 스펙 커버리지: §3.1 태그→T1, §3.2 큐 지정→T4, §3.3 Burst/Looping→T1/T2, §3.4 라이브러리→T1/T5, §3.5 서비스→T2, §3.6 임팩트→T3, §4 재생책임→T4, §5 훅(cast/impact/heal→T4, tileImpact/status/death→T7, potion/combat/levelUp→T8), §6 마이그레이션→T4/T5/T6/T10, §8 저작→T5/T9, §9 순서→태스크 1:1. 누락 없음.
- 타입 일관성: `VfxCue`(정의), `VfxCuePlay`, `ShakePreset`, `TileVfxTrigger`(T1↔T2/T6), `VfxLibrary.ResolveCue/TryGetTile`(T1↔T2), `VfxService.Play/PlayOn/StartLoop/StopLoop/PlayTileEvent`(T2↔T4/T6/T7/T8), `CombatContext.VfxCue`(T4↔T7 Goblin), `castCue/impactCue`(T4↔T5/T7/T9) 교차 확인 완료.
- 알려진 리스크: (1) CFXR 정확 프리팹명 — Find substring이라 유연, 못 찾으면 엔트리만 남고 로그. (2) EndCombat 승/패 분기 위치 — 구현 시 실제 확인, 불명확 시 승리만. (3) CameraShaker 피벗화가 기존 카메라 스크립트(위치 세팅)와 충돌 가능 — GameManager.cs:82 카메라 위치 세팅이 로컬/월드 어느 쪽인지 T9 Step3에서 확인. (4) 몬스터 프리셋 실제 파일명 — T9에서 Glob 확인.
