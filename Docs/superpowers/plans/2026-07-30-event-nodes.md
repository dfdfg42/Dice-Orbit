# 이벤트 노드 9종 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 이벤트 노드에서 등장하는 이벤트 9종(교정기/인챈트/회복기/ONE OR ALL/자판기/조화의 정령/제련소/보물상자/고대 요정)을 기존 EventDefinition 에셋 + EventOutcome 다형성 구조 위에 구현한다.

**Architecture:** 스펙 `Docs/superpowers/specs/2026-07-30-event-nodes-design.md` 참조. 신규 능력 4개(주사위 면 오버라이드, 대상 선택형 Outcome, 타일 설치 예약 매니저, 이벤트 단위 반복)를 인프라로 깔고, 이벤트 9종은 전부 "신규 Outcome 클래스 + 에셋 조합"으로 만든다.

**Tech Stack:** Unity 6000.3.8f1, C#, Unity MCP (RunCommand/GetConsoleLogs), ScriptableObject + [SerializeReference] 다형성.

## Global Constraints

- 자동 테스트 프레임워크 없음. **각 태스크의 게이트 = Unity MCP 컴파일 게이트**: `AssetDatabase.Refresh` 실행 → `GetConsoleLogs(LogType=error)` 0건. 로직 검증이 가능한 곳은 RunCommand 어서션 스크립트로 검증.
- RunCommand 에디터 스크립트는 반드시 `internal class CommandScript : IRunCommand` 형태. `AssetDatabase.DeleteAsset` 등 일부 API는 "User interactions are not supported"로 차단됨 — 파일 삭제는 git/파일시스템으로.
- 전투 중 HP 변경은 반드시 AttackContext/HealContext 파이프라인 경유. 전투 밖(이벤트 화면)의 회복/최대체력 조작은 직접 HP 관례 (기존 `HealParty` 방식).
- enum 멤버 추가는 **항상 끝에** (직렬화 순서 보존).
- 커밋 메시지에 큰따옴표 금지 (PowerShell 인자 깨짐). 끝에 `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.
- 새 Outcome/DieEffect/TileAttribute는 기존 [SerializeReference] + SubclassPicker 패턴 준수.
- 네임스페이스: Core/Run = `DiceOrbit.Core.Run`, Dices = `DiceOrbit.Data`, Tile = `DiceOrbit.Data.Tile`, UI = `DiceOrbit.UI`.

---

### Task 1: 주사위 면 오버라이드 + 면 조작 API

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Dices/DieDefinitionSO.cs` (DieInstance)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Dices/DiceDeckManager.cs`

**Interfaces:**
- Produces: `DieInstance.FaceOverride` (int[]), `DieInstance.Faces`가 오버라이드 우선, `DieInstance.RollFace()`가 인스턴스 Faces에서 굴림
- Produces: `DiceDeckManager.RandomizeFaces(int index, int count)`, `AddToRandomFaces(int index, int count, int delta)`, `ZeroRandomFace(int index)` — Task 7의 Outcome들이 호출

- [ ] **Step 1: DieInstance에 FaceOverride 추가 + 굴림 경로 교정**

`DieDefinitionSO.cs`의 `DieInstance` 클래스를 다음으로 교체:

```csharp
    /// <summary>
    /// 덱의 한 칸 (런타임). 베이스 주사위(SO) + 이벤트로 붙은 효과/면 변형(선택).
    /// 교체 = BaseDie 스왑 / 효과 부여 = AttachedEffect / 면 변형 = FaceOverride.
    /// </summary>
    public class DieInstance
    {
        public DieDefinitionSO BaseDie;
        public DieEffect AttachedEffect;   // 없으면 BaseDie.Effect 사용
        public int[] FaceOverride;         // 이벤트로 변형된 면 (null = 원본)

        public DieInstance(DieDefinitionSO baseDie) { BaseDie = baseDie; }

        public int[] Faces => FaceOverride ?? (BaseDie != null ? BaseDie.Faces : System.Array.Empty<int>());
        public DieEffect Effect => AttachedEffect ?? BaseDie?.Effect;

        /// <summary>인스턴스 면(변형 반영)에서 굴린다 — BaseDie 직행 금지.</summary>
        public int RollFace()
        {
            var faces = Faces;
            return faces.Length > 0 ? faces[Random.Range(0, faces.Length)] : 1;
        }

        /// <summary>면 변형 준비 — 오버라이드가 없으면 원본 복사본 생성 후 반환.</summary>
        public int[] EnsureFaceOverride()
        {
            if (FaceOverride == null)
                FaceOverride = (int[])Faces.Clone();
            return FaceOverride;
        }
    }
```

- [ ] **Step 2: DiceDeckManager에 면 조작 API 3종 추가**

`DiceDeckManager.cs`의 `AttachEffect` 아래에 추가:

```csharp
        /// <summary>무작위 면 count개를 각각 1~6 무작위 값으로 (서로 다른 면 — 교정기 1).</summary>
        public void RandomizeFaces(int index, int count)
        {
            var faces = FacesFor(index);
            if (faces == null) return;
            foreach (int f in PickDistinctFaces(faces.Length, count))
                faces[f] = Random.Range(1, 7);
        }

        /// <summary>무작위 면 count개에 delta (하한 0 — 교정기 2).</summary>
        public void AddToRandomFaces(int index, int count, int delta)
        {
            var faces = FacesFor(index);
            if (faces == null) return;
            foreach (int f in PickDistinctFaces(faces.Length, count))
                faces[f] = Mathf.Max(0, faces[f] + delta);
        }

        /// <summary>무작위 면 1개를 0으로 (인챈트/회복기 대가).</summary>
        public void ZeroRandomFace(int index)
        {
            var faces = FacesFor(index);
            if (faces == null || faces.Length == 0) return;
            faces[Random.Range(0, faces.Length)] = 0;
        }

        private int[] FacesFor(int index)
            => (index >= 0 && index < deck.Count) ? deck[index].EnsureFaceOverride() : null;

        /// <summary>0..total-1에서 서로 다른 인덱스 count개 (count >= total이면 전부).</summary>
        private static List<int> PickDistinctFaces(int total, int count)
        {
            var all = new List<int>(total);
            for (int i = 0; i < total; i++) all.Add(i);
            for (int i = all.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (all[i], all[j]) = (all[j], all[i]);
            }
            if (count < all.Count) all.RemoveRange(count, all.Count - count);
            return all;
        }
```

`Replace()`에 오버라이드 초기화 한 줄 추가 (기존 AttachedEffect 초기화 옆):

```csharp
            deck[index].AttachedEffect = null;   // 교체 시 붙은 효과 초기화
            deck[index].FaceOverride = null;     // 교체 시 면 변형 초기화
```

- [ ] **Step 3: 면 읽기 경로 전수 확인**

Grep으로 `BaseDie.Faces`와 `.RollFace()` 사용처를 찾아, DieInstance를 갖고 있으면서 `BaseDie.Faces`/`BaseDie.RollFace()`를 직접 읽는 곳이 있으면 인스턴스 `Faces`/`RollFace()`로 교정한다.

```
Grep pattern: BaseDie\.(Faces|RollFace)|Source\.(Faces|RollFace)  path: Assets/Scripts
```

알려진 사용처 (이미 인스턴스 경유 — 수정 불필요 확인만): `DiceManager.cs:80` `inst.RollFace()`, `DiceManager.cs:105` `die.Source.RollFace()`. `DiceHoverTooltipUI.cs`가 면 목록을 표시한다면 인스턴스 `Faces`를 읽는지 확인.

- [ ] **Step 4: 컴파일 게이트 + RunCommand 어서션**

`AssetDatabase.Refresh` 후 에러 0건 확인. 이어서 RunCommand로 로직 검증 (씬/플레이 불필요 — 순수 C#):

```csharp
using UnityEngine;
using DiceOrbit.Data;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var so = ScriptableObject.CreateInstance<DieDefinitionSO>();
        so.Faces = new[] { 1, 2, 3, 4, 5, 6 };
        var inst = new DieInstance(so);

        var ov = inst.EnsureFaceOverride();
        if (ReferenceEquals(ov, so.Faces)) { result.LogError("오버라이드가 원본과 같은 배열"); return; }

        for (int i = 0; i < ov.Length; i++) ov[i] = 0;
        for (int t = 0; t < 20; t++)
            if (inst.RollFace() != 0) { result.LogError("RollFace가 오버라이드를 안 읽음"); return; }
        if (so.Faces[0] != 1) { result.LogError("원본 SO 면이 오염됨"); return; }

        Object.DestroyImmediate(so);
        result.Log("PASS — FaceOverride/RollFace 동작 확인");
    }
}
```

Expected: `PASS` 로그.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Dices/DieDefinitionSO.cs Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Dices/DiceDeckManager.cs
git commit -m "feat: 주사위 면 오버라이드 + 면 조작 API (이벤트 노드 기반)"
```

---

### Task 2: 인챈트 DieEffect 2종

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Dices/DieEffect.cs`

**Interfaces:**
- Consumes: `EffectType.Power` + `StatusEffectManager.CreateEffect` (포션 작업에서 구현됨), `CombatNotifier.Notify`, `TooltipKeywordFormatter.BuildStatusDisplayData`
- Produces: `GainArmorOnUse`(amount=10), `EmpowerOnUse`(percent=10) — Task 9 에셋의 인챈트 지정에 사용

- [ ] **Step 1: DieEffect.cs 끝에 2클래스 추가**

```csharp
    /// <summary>사용 시 사용자에게 일시 방어도 +N (인챈트 이벤트 — 수호).</summary>
    [System.Serializable]
    public class GainArmorOnUse : DieEffect
    {
        public int amount = 10;
        public override string Apply(DieUseContext ctx)
        {
            var u = ctx?.User;
            if (u == null || !u.IsAlive || u.Stats == null) return "";
            u.Stats.TempArmor += amount;
            DiceOrbit.UI.CombatNotifier.Notify(u, $"방어도 +{amount}", new Color(0.6f, 0.75f, 1f));
            return $"방어도 +{amount}";
        }
        public override string Preview() => $"사용 시 방어도 +{amount}";
    }

    /// <summary>사용 시 사용자에게 파워(피해 +N%, 1턴) 부여 (인챈트 이벤트 — 공세).</summary>
    [System.Serializable]
    public class EmpowerOnUse : DieEffect
    {
        public int percent = 10;
        public override string Apply(DieUseContext ctx)
        {
            var u = ctx?.User;
            if (u == null || !u.IsAlive || u.StatusEffects == null) return "";
            u.StatusEffects.AddEffect(
                DiceOrbit.Systems.Effects.StatusEffectManager.CreateEffect(EffectType.Power, percent, 1));
            var data = DiceOrbit.UI.TooltipKeywordFormatter.BuildStatusDisplayData(EffectType.Power.ToString(), percent, 1);
            DiceOrbit.UI.CombatNotifier.NotifyStatus(u, data.Name, data.Color);
            return $"피해 +{percent}% (1턴)";
        }
        public override string Preview() => $"사용 시 피해 +{percent}% (1턴)";
    }
```

주의: `DieUseContext.User`는 `Character` 타입. `StatusEffects`/`Stats`는 Unit 공통 프로퍼티. 완전 수식 네임스페이스는 파일 상단 using과 겹치면 정리해도 됨 (WeakPotion.cs의 using 목록 참조).

- [ ] **Step 2: 컴파일 게이트**

Refresh → 에러 0건.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Dices/DieEffect.cs
git commit -m "feat: 인챈트 주사위 효과 2종 - 사용 시 방어도 10 / 파워 10% 1턴"
```

---

### Task 3: 타일 속성 5종 + 중화 포션 갱신

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/TileAttribute.cs` (enum + GetDisplayName)
- Create: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/EventTiles.cs`
- Modify: `Assets/Scripts/Data/Potions/NeutralizePotion/NeutralizePotion.cs`

**Interfaces:**
- Produces: `TileAttributeType.Sharp/Dull/Sturdy/Harmony/Disharmony`, 클래스 `SharpTile/DullTile/SturdyTile/HarmonyTile/DisharmonyTile` (생성자 인자 없음, 기본값 내장) — Task 4의 팩토리가 생성

- [ ] **Step 1: enum 끝에 5종 추가**

`TileAttribute.cs`의 `TileAttributeType`에서 `Reagent` 뒤에:

```csharp
        Sharp,          // 예리함: 이 타일에서 공격 시 피해 +V% (이벤트)
        Dull,           // 약화: 이 타일에서 공격 시 피해 -V% (이벤트)
        Sturdy,         // 단단함: 통과/턴 종료 시 방어도 +V (이벤트)
        Harmony,        // 조화: 턴 종료 시 최대체력 V% 회복 (이벤트)
        Disharmony,     // 부조화: 턴 종료 시 최대체력 V% 피해 (이벤트)
```

같은 파일 `GetDisplayName()` switch에 5줄 추가 (`_ => Type.ToString()` 앞):

```csharp
                TileAttributeType.Sharp => "예리함",
                TileAttributeType.Dull => "약화",
                TileAttributeType.Sturdy => "단단함",
                TileAttributeType.Harmony => "조화",
                TileAttributeType.Disharmony => "부조화",
```

- [ ] **Step 2: EventTiles.cs 신규 작성**

```csharp
using UnityEngine;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// 이벤트(조화의 정령/제련소)로 설치되는 타일 5종. 설치는 EventRunState가 전투 시작마다 수행.
    /// 공격 보정은 파이프라인 OnCalculateOutput에서 (IsSimulation 게이트 없음 — 예상 피해 프리뷰에도 반영),
    /// 회복/피해는 파이프라인 컨텍스트 경유 (직접 HP 대입 금지).
    /// 타일 훅(OnTraverse/OnEndTurn)은 Character 전용 — 몬스터에는 미적용 (현 구조).
    /// </summary>
    public class SharpTile : TileAttribute
    {
        public SharpTile(int percent = 10) : base(TileAttributeType.Sharp, percent, -1) { }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (!(context is AttackContext atk)) return;
            if (!(context.SourceUnit is Core.Character c) || c.CurrentTile != Owner) return;
            atk.OutputValue *= 1f + Value / 100f;
        }

        public override string GetDescription() => $"이 타일에서 공격 시 피해 +{Value}%";
    }

    public class DullTile : TileAttribute
    {
        public DullTile(int percent = 10) : base(TileAttributeType.Dull, percent, -1) { }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (!(context is AttackContext atk)) return;
            if (!(context.SourceUnit is Core.Character c) || c.CurrentTile != Owner) return;
            atk.OutputValue *= 1f - Value / 100f;
        }

        public override string GetDescription() => $"이 타일에서 공격 시 피해 -{Value}%";
    }

    /// <summary>통과(마지막 걸음 포함) 및 턴 종료 시 방어도. OnArrive는 쓰지 않는다 —
    /// 이동 루프가 도착 타일에도 OnTraverse를 호출하므로 (Character.cs) 겹치면 이중 지급.</summary>
    public class SturdyTile : TileAttribute
    {
        public SturdyTile(int armor = 10) : base(TileAttributeType.Sturdy, armor, -1) { }

        public override void OnTraverse(Core.Character character) => Grant(character);
        public override void OnEndTurn(Core.Character character) => Grant(character);

        private void Grant(Core.Character character)
        {
            if (character == null || !character.IsAlive || character.Stats == null) return;
            character.Stats.TempArmor += Value;
            DiceOrbit.UI.CombatNotifier.Notify(character, $"방어도 +{Value}", new Color(0.6f, 0.75f, 1f));
        }

        public override string GetDescription() => $"통과·턴 종료 시 방어도 +{Value}";
    }

    public class HarmonyTile : TileAttribute
    {
        public HarmonyTile(int percent = 5) : base(TileAttributeType.Harmony, percent, -1) { }

        public override void OnEndTurn(Core.Character character)
        {
            if (character == null || !character.IsAlive || character.Stats == null) return;
            int amount = Mathf.Max(1, Mathf.RoundToInt(character.Stats.MaxHP * Value / 100f));
            var heal = new HealContext(null, character, "조화", amount);
            CombatPipeline.Instance?.Process(heal);
        }

        public override string GetDescription() => $"이 타일에서 턴 종료 시 최대체력 {Value}% 회복";
    }

    public class DisharmonyTile : TileAttribute
    {
        public DisharmonyTile(int percent = 5) : base(TileAttributeType.Disharmony, percent, -1) { }

        public override void OnEndTurn(Core.Character character)
        {
            if (character == null || !character.IsAlive || character.Stats == null) return;
            int amount = Mathf.Max(1, Mathf.RoundToInt(character.Stats.MaxHP * Value / 100f));
            var hit = new AttackContext(null, character, "부조화", amount);
            CombatPipeline.Instance?.Process(hit);
        }

        public override string GetDescription() => $"이 타일에서 턴 종료 시 최대체력 {Value}% 피해";
    }
}
```

주의: `CombatPipeline.Instance` 프로퍼티명은 구현 시 실제 코드 확인 (포션 작업에서 `DiceOrbit.Core.Pipeline.CombatPipeline.Instance?.Process(...)` 사용 실적 있음).

- [ ] **Step 3: 중화 포션 디버프 목록에 2종 추가**

`NeutralizePotion.cs`의 `DebuffTypes` 배열에:

```csharp
            TileAttributeType.Dull,
            TileAttributeType.Disharmony,
```

- [ ] **Step 4: 컴파일 게이트 + 커밋**

Refresh → 에러 0건.

```bash
git add Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/TileAttribute.cs Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/EventTiles.cs Assets/Scripts/Data/Potions/NeutralizePotion/NeutralizePotion.cs
git commit -m "feat: 이벤트 타일 5종 - 예리함/약화/단단함/조화/부조화 + 중화 대상 추가"
```

(`.meta`는 Unity가 생성 — 같이 add)

---

### Task 4: EventRunState 매니저 (타일 예약 + seen 이벤트)

**Files:**
- Create: `Assets/Scripts/Core/Run/EventRunState.cs`
- Modify: `Assets/Scripts/Core/GameFlowManager.cs` (런 종료 시 클리어 1줄)

**Interfaces:**
- Consumes: Task 3의 타일 클래스 5종, `CombatManager.OnCombatStart` (PotionManager 훅 패턴), `OrbitManager.Tiles`
- Produces: `EventRunState.EnsureInstance()`, `EnqueueTileInstall(TileAttributeType)`, `HasSeen(string)`, `MarkSeen(string)`, `ResetSeen()`, `ClearAll()`, `TileInstallTypes` (IReadOnlyList<TileAttributeType>), `SeenEventNames` (IReadOnlyCollection<string>), `RestoreFrom(List<int>, List<string>)` — Task 5 세이브·Task 7 Outcome·Task 8 EventUI가 사용

- [ ] **Step 1: EventRunState.cs 작성**

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// 이벤트 노드의 런 수명 상태 (스펙 2026-07-30 §3.6~3.7).
    /// ① 타일 설치 예약 — 이벤트에서 Enqueue, 매 전투 시작(OnCombatStart)마다 무작위 타일에 배치 (런 내내 누적).
    /// ② 본 이벤트 기록 — 같은 이벤트가 한 런에 다시 안 나오게 (풀 소진 시 리셋).
    /// ArtifactManager식 씬 로컬 싱글톤 — 런 종료(EndRun)와 함께 ClearAll.
    /// </summary>
    public class EventRunState : MonoBehaviour
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

        /// <summary>세이브 복원 (RunSaveService → GameFlowManager 이어하기 경로).</summary>
        public void RestoreFrom(List<int> installTypes, List<string> seenNames)
        {
            ClearAll();
            if (installTypes != null)
                foreach (int t in installTypes) tileInstalls.Add((TileAttributeType)t);
            if (seenNames != null)
                foreach (var n in seenNames) seenEvents.Add(n);
            EnsureCombatHook();
        }
    }
}
```

주의: `CombatManager`의 네임스페이스는 PotionManager.cs가 무수식으로 `CombatManager.Instance`를 쓰는 것과 동일 조건 (`DiceOrbit.Core`) — 컴파일 에러 나면 using 확인.

- [ ] **Step 2: 런 종료 시 클리어**

`GameFlowManager.cs:588` 부근 `RunManager.Instance?.EndRun();` 바로 다음 줄에:

```csharp
            Run.EventRunState.Instance?.ClearAll();
```

(GameFlowManager의 네임스페이스 상황에 맞춰 `DiceOrbit.Core.Run.EventRunState` 수식 조정.)

- [ ] **Step 3: 컴파일 게이트 + 커밋**

```bash
git add Assets/Scripts/Core/Run/EventRunState.cs Assets/Scripts/Core/GameFlowManager.cs
git commit -m "feat: EventRunState - 이벤트 타일 설치 예약 + 본 이벤트 기록 매니저"
```

---

### Task 5: 세이브 통합

**Files:**
- Modify: `Assets/Scripts/Core/Run/RunSaveService.cs`
- Modify: `Assets/Scripts/Core/GameFlowManager.cs` (이어하기 복원 경로, ~463행 포션 복원 뒤)

**Interfaces:**
- Consumes: Task 4의 `EventRunState.TileInstallTypes/SeenEventNames/RestoreFrom`
- Produces: `RunSaveData.EventTileInstallTypes` (List<int>), `RunSaveData.SeenEventNames` (List<string>)

- [ ] **Step 1: RunSaveData 필드 2개 추가**

`RunSaveData` 클래스의 `Party` 필드 위에:

```csharp
        public List<int> EventTileInstallTypes = new List<int>();   // TileAttributeType 캐스팅 저장
        public List<string> SeenEventNames = new List<string>();
```

(JsonUtility는 없는 필드를 무시하므로 구 세이브와 호환 — 빈 리스트로 로드됨.)

- [ ] **Step 2: SaveCurrent에 스냅샷 추가**

`RunSaveService.SaveCurrent()`의 PotionManager 블록 다음에:

```csharp
            if (EventRunState.Instance != null)
            {
                data.EventTileInstallTypes.AddRange(EventRunState.Instance.TileInstallTypes.Select(t => (int)t));
                data.SeenEventNames.AddRange(EventRunState.Instance.SeenEventNames);
            }
```

- [ ] **Step 3: 이어하기 복원**

`GameFlowManager.cs` 이어하기 경로에서 포션 복원(`potions.TryAdd(...)` foreach) 바로 다음에:

```csharp
            // 이벤트 상태 (타일 설치 예약 + 본 이벤트)
            Run.EventRunState.EnsureInstance()
                .RestoreFrom(data.EventTileInstallTypes, data.SeenEventNames);
```

- [ ] **Step 4: 컴파일 게이트 + 커밋**

```bash
git add Assets/Scripts/Core/Run/RunSaveService.cs Assets/Scripts/Core/GameFlowManager.cs
git commit -m "feat: 이벤트 타일 예약/본 이벤트 목록 세이브 통합"
```

---

### Task 6: EventOutcome 확장 — CanApply + 대상 선택 인프라

**Files:**
- Modify: `Assets/Scripts/Core/Run/EventOutcomes.cs`

**Interfaces:**
- Produces: `EventOutcome.CanApply()` (virtual, 기본 true), `EventSelectionKind` (Die=0, Character=1), `EventTargetContext` {SelectedDieIndex, SelectedCharacter}, `TargetedEventOutcome` (abstract Kind + Apply(ctx), 무인자 Apply는 sealed), `EventOutcomes.Apply(List<EventOutcome>, EventTargetContext)` — Task 7/8이 사용

- [ ] **Step 1: EventOutcome에 CanApply 추가**

`EventOutcome` 클래스의 `Preview()` 아래에:

```csharp
        /// <summary>이 결과를 지금 적용할 수 있는가 — EventUI가 선택지 활성 판정에 AND로 사용
        /// (예: 고대 요정 = 유물 보유, ONE OR ALL = 파티 2인 이상).</summary>
        public virtual bool CanApply() => true;
```

- [ ] **Step 2: 대상 선택 인프라 추가**

`EventOutcome` 클래스 정의 바로 아래(같은 파일)에:

```csharp
    /// <summary>대상 선택 종류 — 순서가 선택 UI 순서 (Die 먼저).</summary>
    public enum EventSelectionKind { Die = 0, Character = 1 }

    /// <summary>이벤트 선택지에서 사용자가 고른 대상 묶음 — 같은 선택지의 Outcome들이 공유.</summary>
    public class EventTargetContext
    {
        public int SelectedDieIndex = -1;         // DiceDeckManager.Deck 인덱스
        public Character SelectedCharacter;
    }

    /// <summary>대상 지정이 필요한 결과. EventUI가 Kind별 선택 패널을 먼저 띄운 뒤 Apply(ctx)를 부른다.</summary>
    [System.Serializable]
    public abstract class TargetedEventOutcome : EventOutcome
    {
        public abstract EventSelectionKind Kind { get; }

        /// <summary>대상 없이 호출 금지 — 잘못 배선된 경우 빈 문자열 (로그로 표시).</summary>
        public sealed override string Apply()
        {
            UnityEngine.Debug.LogWarning($"[Event] {GetType().Name}: 대상 없이 Apply 호출됨 — EventTargetContext 경로를 쓰세요.");
            return "";
        }

        public abstract string Apply(EventTargetContext ctx);
    }
```

- [ ] **Step 3: Apply 유틸에 ctx 오버로드 추가**

`EventOutcomes.Apply(List<EventOutcome>)`를 다음으로 교체 (기존 시그니처는 위임으로 유지 — EventUI 외 호출처 호환):

```csharp
        /// <summary>결과를 전부 적용하고 합쳐진 요약을 돌려준다. 대상형은 ctx 경유.</summary>
        public static string Apply(List<EventOutcome> outcomes, EventTargetContext ctx)
        {
            if (outcomes == null || outcomes.Count == 0) return "";

            var sb = new StringBuilder();
            foreach (var outcome in outcomes)
            {
                if (outcome == null) continue;
                string line = outcome is TargetedEventOutcome targeted
                    ? (ctx != null ? targeted.Apply(ctx) : targeted.Apply())
                    : outcome.Apply();
                if (string.IsNullOrEmpty(line)) continue;
                if (sb.Length > 0) sb.Append("  ·  ");
                sb.Append(line);
            }
            return sb.ToString();
        }

        public static string Apply(List<EventOutcome> outcomes) => Apply(outcomes, null);
```

- [ ] **Step 4: 컴파일 게이트 + 커밋**

```bash
git add Assets/Scripts/Core/Run/EventOutcomes.cs
git commit -m "feat: EventOutcome 대상 선택 인프라 - TargetedEventOutcome/EventTargetContext/CanApply"
```

---

### Task 7: 신규 Outcome 9종

**Files:**
- Create: `Assets/Scripts/Core/Run/EventNodeOutcomes.cs`

**Interfaces:**
- Consumes: Task 1 면 API, Task 4 `EventRunState.EnqueueTileInstall`, Task 6 `TargetedEventOutcome`, `ArtifactManager.RemoveArtifact(RuntimeArtifact)`, `PotionManager.GrantRandomDrop()`, `DiceDeckManager.EnsureInstance()`
- Produces: 클래스 9종 (아래 코드) — Task 9 에셋 조립에 사용

- [ ] **Step 1: EventNodeOutcomes.cs 작성**

```csharp
using System.Linq;
using UnityEngine;
using DiceOrbit.Data;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Core.Run
{
    // ── 주사위 대상 (교정기/인챈트/회복기) ─────────────────────

    /// <summary>선택 주사위의 무작위 면 count개를 1~6 무작위 값으로 (교정기 1).</summary>
    [System.Serializable]
    public class RandomizeDieFaces : TargetedEventOutcome
    {
        public int count = 2;
        public override EventSelectionKind Kind => EventSelectionKind.Die;

        public override string Apply(EventTargetContext ctx)
        {
            DiceDeckManager.EnsureInstance()?.RandomizeFaces(ctx.SelectedDieIndex, count);
            return $"면 {count}개 무작위 변환";
        }
        public override string Preview() => $"무작위 면 {count}개 변환";
    }

    /// <summary>선택 주사위의 무작위 면 count개에 delta (교정기 2 — 기본 -1, 하한 0).</summary>
    [System.Serializable]
    public class AddToDieFaces : TargetedEventOutcome
    {
        public int count = 3;
        public int delta = -1;
        public override EventSelectionKind Kind => EventSelectionKind.Die;

        public override string Apply(EventTargetContext ctx)
        {
            DiceDeckManager.EnsureInstance()?.AddToRandomFaces(ctx.SelectedDieIndex, count, delta);
            return $"면 {count}개 {(delta >= 0 ? "+" : "")}{delta}";
        }
        public override string Preview() => $"무작위 면 {count}개 {(delta >= 0 ? "+" : "")}{delta}";
    }

    /// <summary>선택 주사위의 무작위 면 1개를 0으로 + 인챈트 부여(선택 — null이면 면만 0).
    /// 인챈트/회복기의 대가 겸용. 재부여 시 기존 인챈트 교체 (AttachEffect 덮어쓰기).</summary>
    [System.Serializable]
    public class ZeroDieFaceAndEnchant : TargetedEventOutcome
    {
        [SerializeReference, SubclassPicker] public DieEffect enchant;
        public override EventSelectionKind Kind => EventSelectionKind.Die;

        public override string Apply(EventTargetContext ctx)
        {
            var dm = DiceDeckManager.EnsureInstance();
            if (dm == null) return "";
            dm.ZeroRandomFace(ctx.SelectedDieIndex);
            if (enchant != null)
            {
                dm.AttachEffect(ctx.SelectedDieIndex, enchant);
                return $"면 1개 → 0, 인챈트 부여 — {enchant.Preview()}";
            }
            return "면 1개 → 0";
        }
        public override string Preview()
            => enchant != null ? $"면 1개 → 0 + {enchant.Preview()}" : "면 1개 → 0";
    }

    // ── 캐릭터 대상 (회복기/ONE OR ALL) ───────────────────────

    /// <summary>선택 캐릭터 최대체력의 percent% 회복 (전투 밖 — HealParty 관례의 직접 HP).</summary>
    [System.Serializable]
    public class HealSelectedCharacter : TargetedEventOutcome
    {
        [Range(1, 100)] public int percent = 10;
        public override EventSelectionKind Kind => EventSelectionKind.Character;

        public override string Apply(EventTargetContext ctx)
        {
            var c = ctx.SelectedCharacter;
            if (c == null || !c.IsAlive || c.Stats == null) return "";
            int heal = Mathf.Max(1, c.Stats.MaxHP * percent / 100);
            c.Stats.CurrentHP = Mathf.Min(c.Stats.MaxHP, c.Stats.CurrentHP + heal);
            return $"{c.Stats.CharacterName} {heal} 회복";
        }
        public override string Preview() => $"최대체력 {percent}% 회복";
    }

    /// <summary>ONE FOR ALL — 선택 제외 각자 MaxHP percent% 감소(최소 1, MaxHP 하한 1),
    /// 감소 총합만큼 선택 캐릭터 MaxHP·CurrentHP 증가.</summary>
    [System.Serializable]
    public class OneForAll : TargetedEventOutcome
    {
        [Range(1, 100)] public int percent = 10;
        public override EventSelectionKind Kind => EventSelectionKind.Character;
        public override bool CanApply() => AliveParty().Count() >= 2;

        public override string Apply(EventTargetContext ctx)
        {
            var chosen = ctx.SelectedCharacter;
            if (chosen == null || chosen.Stats == null) return "";

            int total = 0;
            foreach (var c in AliveParty())
            {
                if (c == chosen) continue;
                int loss = Mathf.Max(1, c.Stats.MaxHP * percent / 100);
                loss = Mathf.Min(loss, c.Stats.MaxHP - 1);          // MaxHP 하한 1
                if (loss <= 0) continue;
                c.Stats.MaxHP -= loss;
                c.Stats.CurrentHP = Mathf.Clamp(c.Stats.CurrentHP, 1, c.Stats.MaxHP);
                total += loss;
            }
            chosen.Stats.MaxHP += total;
            chosen.Stats.CurrentHP += total;
            return $"{chosen.Stats.CharacterName} 최대체력 +{total}";
        }
        public override string Preview() => $"다른 아군 최대체력 -{percent}% → 선택 캐릭터에 합산";
    }

    /// <summary>ALL FOR ONE — 선택 캐릭터 MaxHP percent% 감소(MaxHP 하한 1),
    /// 감소분을 나머지에게 균등 분배 (나머지 몫은 앞 순서부터 +1).</summary>
    [System.Serializable]
    public class AllForOne : TargetedEventOutcome
    {
        [Range(1, 100)] public int percent = 30;
        public override EventSelectionKind Kind => EventSelectionKind.Character;
        public override bool CanApply() => AliveParty().Count() >= 2;

        public override string Apply(EventTargetContext ctx)
        {
            var chosen = ctx.SelectedCharacter;
            if (chosen == null || chosen.Stats == null) return "";

            var others = AliveParty().Where(c => c != chosen).ToList();
            if (others.Count == 0) return "";

            int loss = Mathf.Max(1, chosen.Stats.MaxHP * percent / 100);
            loss = Mathf.Min(loss, chosen.Stats.MaxHP - 1);
            if (loss <= 0) return "";
            chosen.Stats.MaxHP -= loss;
            chosen.Stats.CurrentHP = Mathf.Clamp(chosen.Stats.CurrentHP, 1, chosen.Stats.MaxHP);

            int share = loss / others.Count, rem = loss % others.Count;
            for (int i = 0; i < others.Count; i++)
            {
                int gain = share + (i < rem ? 1 : 0);
                others[i].Stats.MaxHP += gain;
                others[i].Stats.CurrentHP += gain;
            }
            return $"{chosen.Stats.CharacterName} 최대체력 -{loss} → 아군 분배";
        }
        public override string Preview() => $"선택 캐릭터 최대체력 -{percent}% → 아군에 분배";
    }

    // ── 무대상 (자판기/타일/고대 요정) ─────────────────────────

    /// <summary>무작위 포션 count개 (슬롯 남는 만큼 — 자판기).</summary>
    [System.Serializable]
    public class GainRandomPotions : EventOutcome
    {
        public int count = 2;

        public override string Apply()
        {
            var pm = PotionManager.EnsureInstance();
            var names = new System.Collections.Generic.List<string>();
            for (int i = 0; i < count; i++)
            {
                var p = pm.GrantRandomDrop();
                if (p == null) break;
                names.Add(p.PotionName);
            }
            if (names.Count == 0) return "포션 슬롯이 가득...";
            string got = string.Join(", ", names);
            return names.Count < count ? $"포션 획득 — {got} (슬롯 부족)" : $"포션 획득 — {got}";
        }
        public override string Preview() => $"무작위 포션 {count}개";
    }

    /// <summary>타일 설치 예약 — 다음 전투부터 런 내내 무작위 타일에 배치 (정령/제련소).</summary>
    [System.Serializable]
    public class QueueTileInstall : EventOutcome
    {
        public TileAttributeType installType = TileAttributeType.Sharp;

        public override string Apply()
        {
            EventRunState.EnsureInstance().EnqueueTileInstall(installType);
            return $"{DisplayName()} 타일 설치 예약";
        }
        public override string Preview() => $"{DisplayName()} 타일 설치";

        private string DisplayName() => new TileAttribute(installType, 0, -1).GetDisplayName();
    }

    /// <summary>무작위 유물 상실 + 전 파티원 MaxHP percent% 증가 (고대 요정).</summary>
    [System.Serializable]
    public class LoseRandomRelicGainPartyMaxHp : EventOutcome
    {
        [Range(1, 100)] public int percent = 10;

        public override bool CanApply()
            => ArtifactManager.Instance != null && ArtifactManager.Instance.Artifacts.Count > 0;

        public override string Apply()
        {
            var am = ArtifactManager.EnsureInstance();
            if (am.Artifacts.Count == 0) return "";
            var picked = am.Artifacts[Random.Range(0, am.Artifacts.Count)];
            string relicName = picked.data != null ? picked.data.artifactName : picked.GetType().Name;
            am.RemoveArtifact(picked);

            foreach (var c in AliveParty())
            {
                int gain = Mathf.Max(1, c.Stats.MaxHP * percent / 100);
                c.Stats.MaxHP += gain;
                c.Stats.CurrentHP += gain;
            }
            return $"{relicName} 상실 — 파티 최대체력 +{percent}%";
        }
        public override string Preview() => $"무작위 유물 상실, 파티 최대체력 +{percent}%";
    }
}
```

주의: `RuntimeArtifact.data` 필드명·`artifactName`은 ArtifactManager.cs 실사용 기준 (`a.data.artifactName`). `SubclassPickerAttribute`는 `DiceOrbit.Core`에 있음 — using 확인.

- [ ] **Step 2: 컴파일 게이트 + 커밋**

```bash
git add Assets/Scripts/Core/Run/EventNodeOutcomes.cs
git commit -m "feat: 이벤트 노드 Outcome 9종 - 면 조작/대상 회복/MaxHP 이동/포션/타일 예약/유물 교환"
```

---

### Task 8: EventDefinition.UseLimit + EventUI 개편

**Files:**
- Modify: `Assets/Scripts/Core/Run/EventDefinition.cs`
- Modify: `Assets/Scripts/UI/EventUI.cs`

**Interfaces:**
- Consumes: Task 4 `EventRunState`(seen), Task 6 선택 인프라, Task 7 outcome들의 CanApply
- Produces: `EventDefinition.UseLimit` — Task 9 에셋이 세팅

- [ ] **Step 1: EventDefinition.UseLimit 추가**

`EventDefinition` 클래스 `Choices` 필드 위에:

```csharp
        [Tooltip("방문당 선택 가능 횟수 (0 = 무제한). 결과 없는 넘어가기 선택지는 카운트 안 함")]
        public int UseLimit = 0;
```

- [ ] **Step 2: EventUI 개편**

`EventUI.cs`를 다음과 같이 수정한다. using에 `System.Linq`(이미 있음), `DiceOrbit.Data` 추가.

필드 추가 (`_resolving` 옆):

```csharp
        private int _usesThisVisit;
```

`Show()`의 풀 선택부를 seen 필터로 교체:

```csharp
            _resolving = false;
            _usesThisVisit = 0;
            var runState = DiceOrbit.Core.Run.EventRunState.EnsureInstance();
            var pool = eventPool.Where(e => e != null && e.Choices.Count > 0 && !runState.HasSeen(e.name)).ToList();
            if (pool.Count == 0)
            {
                runState.ResetSeen();   // 풀 소진 — 전체 리셋 (스펙 §3.7)
                pool = eventPool.Where(e => e != null && e.Choices.Count > 0).ToList();
            }
            _current = pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
            if (_current == null) { /* 기존 통과 처리 유지 */ }
            runState.MarkSeen(_current.name);
```

`ShowChoices()` 교체:

```csharp
        private void ShowChoices()
        {
            ClearChoices();
            RefreshTitle();
            foreach (var choice in _current.Choices)
            {
                var captured = choice;
                if (IsExitChoice(choice))
                {
                    CreateChoiceBar(choice.Label, primary: false,
                        () => { Hide(); GameFlowManager.Instance?.OnEventComplete(); });
                    continue;
                }
                bool enabled = RemainingUses() > 0 && CanApplyAll(choice);
                CreateChoiceBar(BuildChoiceLabel(choice),
                    primary: choice.Resolution == EventResolution.DiceCheck,
                    () => OnChoicePicked(captured), enabled);
            }
        }

        /// <summary>결과 없는 Instant 선택지 = 이벤트 종료 버튼 (카운트 미소모).</summary>
        private static bool IsExitChoice(EventChoice c)
            => c.Resolution == EventResolution.Instant
               && (c.SuccessOutcomes == null || c.SuccessOutcomes.Count == 0);

        private int RemainingUses()
            => _current.UseLimit <= 0 ? int.MaxValue : Mathf.Max(0, _current.UseLimit - _usesThisVisit);

        private static bool CanApplyAll(EventChoice c)
        {
            foreach (var o in c.SuccessOutcomes)
                if (o != null && !o.CanApply()) return false;
            return true;
        }

        private void RefreshTitle()
        {
            if (titleText == null || _current == null) return;
            titleText.text = _current.UseLimit > 0
                ? $"{_current.Title}  <size=55%>[{RemainingUses()}회 남음]</size>"
                : _current.Title;
        }
```

주의: 기존 `Hide()`가 이벤트 종료 흐름에서 호출되는 위치를 확인해 이중 호출을 피할 것 — 기존 코드는 `OnEventComplete`가 상태 전환하며 Hide를 부르는 구조면 exit 콜백에서 `Hide()` 생략.

`OnChoicePicked` + 선택 시퀀스 교체:

```csharp
        private void OnChoicePicked(EventChoice choice)
        {
            if (_resolving) return;
            _resolving = true;
            ClearChoices();

            var kinds = choice.SuccessOutcomes.Concat(choice.FailOutcomes)
                .OfType<DiceOrbit.Core.Run.TargetedEventOutcome>()
                .Select(o => o.Kind).Distinct().OrderBy(k => (int)k).ToList();

            RunSelection(choice, kinds, new DiceOrbit.Core.Run.EventTargetContext());
        }

        /// <summary>필요한 대상 종류를 순서대로 고르게 한 뒤 해소. 취소 = 선택지로 복귀 (카운트 미소모).</summary>
        private void RunSelection(EventChoice choice,
            List<DiceOrbit.Core.Run.EventSelectionKind> pending,
            DiceOrbit.Core.Run.EventTargetContext ctx)
        {
            if (pending.Count == 0)
            {
                if (choice.Resolution == EventResolution.DiceCheck) StartCoroutine(RollRoutine(choice, ctx));
                else Resolve(choice, success: true, sum: -1, ctx);
                return;
            }

            var kind = pending[0];
            var rest = pending.GetRange(1, pending.Count - 1);
            System.Action cancel = () => { _resolving = false; ShowChoices(); };

            if (kind == DiceOrbit.Core.Run.EventSelectionKind.Die)
                ShowDieSelection(i => { ctx.SelectedDieIndex = i; RunSelection(choice, rest, ctx); }, cancel);
            else
                ShowCharacterSelection(c => { ctx.SelectedCharacter = c; RunSelection(choice, rest, ctx); }, cancel);
        }

        private void ShowDieSelection(System.Action<int> onPicked, System.Action onCancel)
        {
            ClearChoices();
            var dm = DiceDeckManager.EnsureInstance();
            var deck = dm != null ? dm.Deck : null;
            if (deck == null || deck.Count == 0) { onCancel(); return; }

            CreateChoiceBar("주사위를 선택하세요", primary: false, () => { }, interactable: false);
            for (int i = 0; i < deck.Count; i++)
            {
                int idx = i;
                var inst = deck[i];
                string dieName = inst.BaseDie != null ? inst.BaseDie.Name : "주사위";
                string faces = string.Join(" ", inst.Faces);
                string suffix = inst.Effect != null ? $" · {inst.Effect.Preview()}" : "";
                CreateChoiceBar($"{dieName}  <size=65%>[{faces}]{suffix}</size>",
                    primary: false, () => onPicked(idx));
            }
            CreateChoiceBar("취소", primary: true, () => onCancel());
        }

        private void ShowCharacterSelection(System.Action<Character> onPicked, System.Action onCancel)
        {
            ClearChoices();
            var party = PartyManager.Instance?.Party;
            var alive = party?.Where(c => c != null && c.IsAlive && c.Stats != null).ToList();
            if (alive == null || alive.Count == 0) { onCancel(); return; }

            CreateChoiceBar("캐릭터를 선택하세요", primary: false, () => { }, interactable: false);
            foreach (var c in alive)
            {
                var captured = c;
                CreateChoiceBar($"{c.Stats.CharacterName}  <size=65%>[HP {c.Stats.CurrentHP}/{c.Stats.MaxHP}]</size>",
                    primary: false, () => onPicked(captured));
            }
            CreateChoiceBar("취소", primary: true, () => onCancel());
        }
```

`RollRoutine`에 ctx 파라미터 추가 (`RollRoutine(EventChoice choice, DiceOrbit.Core.Run.EventTargetContext ctx)`), 끝의 `Resolve(choice, sum >= ..., sum)` 호출에 ctx 전달.

`Resolve` 교체 — 시그니처에 ctx 추가, [확인]→종료 대신 선택지 재표시:

```csharp
        private void Resolve(EventChoice choice, bool success, int sum, DiceOrbit.Core.Run.EventTargetContext ctx)
        {
            _usesThisVisit++;

            var outcomes = success ? choice.SuccessOutcomes : choice.FailOutcomes;
            string summary = DiceOrbit.Core.Run.EventOutcomes.Apply(outcomes, ctx);
            string flavor = success ? choice.SuccessText : choice.FailText;

            if (resultText != null)
            {
                var sb = new System.Text.StringBuilder();
                if (sum >= 0) sb.Append(success ? $"합 {sum} — 성공!  " : $"합 {sum} — 실패...  ");
                if (!string.IsNullOrWhiteSpace(flavor)) sb.Append(flavor).Append("  ");
                sb.Append(summary);
                resultText.color = success ? Gold : Danger;
                resultText.text = sb.ToString();
            }

            _resolving = false;
            ShowChoices();   // 화면 유지 — 반복 선택. 종료는 넘어간다 선택지로.
        }
```

`CreateChoiceBar`에 `bool interactable = true` 파라미터 추가, 본문에:

```csharp
            btn.interactable = interactable;
            cb.disabledColor = new Color(fill.r * 0.45f, fill.g * 0.45f, fill.b * 0.45f);
```

(`cb` 조립부에 추가. 라벨 텍스트도 비활성 시 `txt.color = new Color(Ink.r, Ink.g, Ink.b, 0.45f);`)

주의: 기존 런타임 폴백 3종(도박/마차)은 UseLimit 0 (필드 기본값) — Instant 결과 있는 선택지를 해소하면 화면이 유지되고 "지나간다"로 나가는 동작으로 바뀐다. 폴백은 곧 에셋 풀로 대체되므로 허용.

- [ ] **Step 3: 컴파일 게이트 + 커밋**

```bash
git add Assets/Scripts/Core/Run/EventDefinition.cs Assets/Scripts/UI/EventUI.cs
git commit -m "feat: EventUI 개편 - 방문 내 반복(UseLimit)/대상 선택 패널/재등장 제외"
```

---

### Task 9: 이벤트 에셋 9개 생성 + 씬 배선 (Unity MCP RunCommand)

**Files:**
- Create: `Assets/Scripts/Data/Events/*.asset` (9개, 에디터 스크립트로)
- Modify: `Assets/Scenes/BattleScene.unity` (EventUI.eventPool 배선, 씬 저장)

**Interfaces:**
- Consumes: 지금까지의 모든 클래스. 에셋 생성은 CreateInstance — **Reset() 미호출이므로 모든 필드 명시 세팅**.

- [ ] **Step 1: RunCommand로 에셋 9개 생성 + 배선**

핵심 요약 (전체 스크립트는 아래 내용대로 조립):

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DiceOrbit.Core.Run;
using DiceOrbit.Data;
using DiceOrbit.Data.Tile;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scripts/Data/Events"))
            AssetDatabase.CreateFolder("Assets/Scripts/Data", "Events");

        var events = new List<EventDefinition>
        {
            Def("DiceCalibrator", "주사위 교정기", 3,
                "오래된 기계가 있습니다. 기계를 가동하자 굉음을 내면서 빛을 내보내고 있습니다.",
                Choice("가동한다", new RandomizeDieFaces { count = 2 }),
                Choice("미세 조정", new AddToDieFaces { count = 3, delta = -1 }),
                Exit("넘어간다")),

            Def("Enchanter", "인챈트", 0,
                "주사위에 능력을 부여해주는 것 같습니다.",
                Choice("수호 인챈트", new ZeroDieFaceAndEnchant { enchant = new GainArmorOnUse { amount = 10 } }),
                Choice("공세 인챈트", new ZeroDieFaceAndEnchant { enchant = new EmpowerOnUse { percent = 10 } }),
                Exit("넘어간다")),

            Def("HealingMachine", "회복기", 0,
                "상처를 낫게하는 회복기입니다. 공짜는 아닌듯 합니다.",
                Choice("가동한다", new ZeroDieFaceAndEnchant(), new HealSelectedCharacter { percent = 10 }),
                Exit("넘어간다")),

            Def("OneOrAll", "ONE OR ALL", 1,
                "ONE FOR ALL? ALL FOR ONE? 이게 뭘까요?",
                Choice("ONE FOR ALL", new OneForAll { percent = 10 }),
                Choice("ALL FOR ONE", new AllForOne { percent = 30 })),

            Def("BrokenVendingMachine", "고장난 자판기", 1,
                "포션 자판기 입니다. 고장나보이는데요. 버튼을 눌러볼까요?",
                Choice("버튼을 누른다", new GainRandomPotions { count = 2 }),
                Exit("넘어간다")),

            Def("SpiritOfHarmony", "조화의 정령", 3,
                "정령이 말을 걸어 옵니다. 조화는 유지되어야 한다고 합니다.",
                Choice("힘의 조화",
                    new QueueTileInstall { installType = TileAttributeType.Sharp },
                    new QueueTileInstall { installType = TileAttributeType.Dull }),
                Choice("생명의 조화",
                    new QueueTileInstall { installType = TileAttributeType.Harmony },
                    new QueueTileInstall { installType = TileAttributeType.Disharmony }),
                Exit("넘어간다")),

            Def("Smeltery", "제련소", 3,
                "무엇이든 제련하는 장소입니다. 어떤 걸 좋게 만들까요?",
                Choice("예리함", new QueueTileInstall { installType = TileAttributeType.Sharp }),
                Choice("단단함", new QueueTileInstall { installType = TileAttributeType.Sturdy }),
                Exit("넘어간다")),

            Def("TreasureChest", "보물상자", 1,
                "야호",
                Choice("보물상자를 연다", new GainRandomRelic()),
                Exit("넘어간다")),

            Def("AncientFairy", "고대 요정", 3,
                "고대 요정이 말을 걸어 옵니다. 유물을 달라고 하네요. 대신 보답하겠다고 합니다.",
                Choice("준다", new LoseRandomRelicGainPartyMaxHp { percent = 10 }),
                Exit("넘어간다")),
        };

        // 씬 EventUI.eventPool 배선
        var ui = Object.FindFirstObjectByType<DiceOrbit.UI.EventUI>(FindObjectsInactive.Include);
        if (ui != null)
        {
            var so = new SerializedObject(ui);
            var pool = so.FindProperty("eventPool");
            pool.arraySize = events.Count;
            for (int i = 0; i < events.Count; i++)
                pool.GetArrayElementAtIndex(i).objectReferenceValue = events[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
            EditorSceneManager.SaveScene(ui.gameObject.scene);
            result.Log("eventPool " + events.Count + "종 배선 + 씬 저장");
        }
        else result.LogError("씬에 EventUI 없음 — eventPool 수동 배선 필요");

        AssetDatabase.SaveAssets();
        result.Log("이벤트 에셋 " + events.Count + "개 준비 완료");
    }

    private static EventDefinition Def(string file, string title, int useLimit, string flavor, params EventChoice[] choices)
    {
        string path = "Assets/Scripts/Data/Events/" + file + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<EventDefinition>(path);
        if (existing != null) return existing;

        var def = ScriptableObject.CreateInstance<EventDefinition>();
        def.Title = title;
        def.UseLimit = useLimit;
        def.FlavorText = flavor;
        def.Choices.AddRange(choices);
        AssetDatabase.CreateAsset(def, path);
        return def;
    }

    private static EventChoice Choice(string label, params EventOutcome[] outcomes)
    {
        var c = new EventChoice { Label = label };
        c.SuccessOutcomes.AddRange(outcomes);
        return c;
    }

    private static EventChoice Exit(string label) => new EventChoice { Label = label };
}
```

- [ ] **Step 2: 검증 RunCommand**

에셋 9개 로드해 Title/UseLimit/Choices 수·Outcome 타입을 로그로 덤프, 기대와 다르면 LogError. (예: OneOrAll → Choices 2, UseLimit 1, 첫 선택지 Outcome이 OneForAll.)

- [ ] **Step 3: 커밋 + 푸시**

```bash
git add Assets/Scripts/Data/Events Assets/Scenes/BattleScene.unity
git commit -m "feat: 이벤트 노드 에셋 9종 생성 + 씬 eventPool 배선"
git push origin feature/event-outcomes-refactor-20260729
```

---

### Task 10: 최종 게이트 + 플레이 검증 인계

- [ ] **Step 1: 전체 컴파일 게이트** — Refresh → 에러 0건, 신규 경고도 확인 (기존 경고 2건: TileInfoPanelUI panelWidth/dockAnchorX — 무관).
- [ ] **Step 2: 스펙 §7 검증 체크리스트를 사용자에게 인계** — 플레이 확인 항목: 면 변형 반영, 인챈트 발동/교체, 2단 선택, MaxHP 이동, 타일 배치·효과, 재등장 제외, 세이브/이어하기, 중화 확장. 자동 검증 불가 항목임을 명시.

## Self-Review 결과

- 스펙 커버리지: §3.1→Task 8, §3.2→Task 6+8, §3.3→Task 1, §3.4→Task 2, §3.5→Task 3, §3.6→Task 4, §3.7→Task 4+5+8, §4/§5→Task 7+9, §6→Task 9. 누락 없음.
- 타입 일관성: `EnsureFaceOverride`(T1↔T1 내부), `EnqueueTileInstall`(T4↔T7), `RestoreFrom(List<int>, List<string>)`(T4↔T5), `Apply(List<EventOutcome>, EventTargetContext)`(T6↔T8), 에셋 조립의 필드명(T7↔T9) 교차 확인 완료.
- 알려진 리스크: EventUI의 기존 Hide/OnEventComplete 이중 호출 여부(Task 8 주의사항), CombatNotifier/StatusEffectManager 네임스페이스 수식(각 태스크 주의사항에 명시) — 컴파일 게이트에서 걸러짐.
