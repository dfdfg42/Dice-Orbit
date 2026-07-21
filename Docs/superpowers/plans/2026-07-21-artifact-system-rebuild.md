# Artifact 시스템 재구축 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 현 Relic 시스템(enum 규칙 효과)을 철거하고 구 Artifact 골격(유물 1개 = 클래스 1개, 런타임 인스턴스)으로 재구축한다. 스펙: `Docs/superpowers/specs/2026-07-21-artifact-system-rebuild-design.md`.

**Architecture:** `ArtifactData`(SO, 표시 + SubclassPicker 프로토타입) + `RuntimeArtifact`(추상, ICombatReactor + 규칙형 virtual 프로퍼티) + `ArtifactManager`(보유/풀/드랍/상점/세이브 창구). 획득 시 프로토타입을 `CreateInstance`(MemberwiseClone)로 복제해 런타임 인스턴스를 보유한다. 소비처 10곳은 기계적 치환.

**Tech Stack:** Unity 6000.3.8f1, C# (Assembly-CSharp 단일), JsonUtility 세이브.

## Global Constraints

- 네임스페이스: 시스템 3파일 = `DiceOrbit.Core.Run` (기존 소비처 using 유지 목적), 콘텐츠 = `DiceOrbit.Data.Artifacts`.
- **DIM 함정 회피(필수)**: `ICombatReactor`의 훅은 인터페이스 기본 구현(DIM)이라, 인터페이스를 나열한 클래스에 훅 메서드가 없으면 **파생 클래스의 동명 메서드는 호출되지 않는다**. `RuntimeArtifact`가 `OnReact`를 클래스 메서드로 구현하고 자체 virtual 훅으로 분기한다. 유물 서브클래스는 반드시 `override` 키워드로 훅을 구현한다 (`public void OnAttack` 금지 — `public override void OnAttack`).
- 직렬화 호환: UI의 `[SerializeField]` 필드명 변경 금지 (`relicRow`, `relicShelfRow`, `relicOfferCount` 등 유지). `EventOutcomeType.GainRandomRelic` enum 멤버명 유지 (인스펙터 직렬화 값).
- 세이브 호환: `RunSaveData.RelicNames` 필드 존치(로드 전용). 저장은 새 필드 `ArtifactNames`에만. 복원은 `EffectiveArtifactNames`.
- 폴백 유물 5종 표시명 현행 승계: 단골 도장 / 포근한 침낭 / 황금 주사위 / 불사조 깃털 / 생명의 부적.
- 리액터 Priority 11 유지.
- **검증 방식**: 이 프로젝트에는 테스트 인프라가 없다 (Unity, 에디터 플레이 검증 관행). 각 태스크의 게이트 = **Unity MCP 콘솔 컴파일 에러 0** (`Unity_GetConsoleLogs`, 필요 시 `Unity_RunCommand`로 `UnityEditor.AssetDatabase.Refresh()` 먼저). 최종 플레이 검증은 스펙 §7 체크리스트를 사용자가 에디터에서 수행.
- 커밋 메시지 끝에 `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.

---

### Task 1: RuntimeArtifact + ArtifactData (베이스 레이어)

**Files:**
- Create: `Assets/Scripts/Core/Run/Artifact/RuntimeArtifact.cs`
- Create: `Assets/Scripts/Core/Run/Artifact/ArtifactData.cs`

**Interfaces:**
- Consumes: `DiceOrbit.Core.Pipeline.ICombatReactor` (DIM 훅: OnAttack/OnHeal/OnMove/OnTurnEvent), `SubclassPickerAttribute` (`DiceOrbit.Core`, 상위 네임스페이스라 using 불필요)
- Produces: `RuntimeArtifact` (추상: `data` 필드, `Priority`=11, 규칙형 virtual 5종 `ShopDiscountPercent/RestHealBonusPercent/BattleGoldBonus/ReviveHpBonusPercent/BattleStartHeal`, virtual 훅 4종, `RuntimeArtifact CreateInstance(ArtifactData)`), `ArtifactData` (SO: `artifactName/artifactTooltip/artifactIcon/shopPrice/effect`)

- [ ] **Step 1: RuntimeArtifact.cs 작성**

```csharp
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// 유물 런타임 베이스 — 유물 1개 = 서브클래스 1개 (Data/Artifacts/).
    /// ArtifactData.effect에 프로토타입으로 인라인 직렬화되고, 획득 시 CreateInstance로 복제된다.
    ///
    /// 훅은 인터페이스 DIM이 아니라 클래스 virtual로 분기한다 — 파생 클래스가 인터페이스를
    /// 재나열하지 않으면 DIM 매핑에 걸리지 않는 C# 함정 회피. 서브클래스는 반드시 override로 구현.
    /// 프로토타입 필드는 값 타입/불변만 (얕은 복사). 상태 갖는 훅은 !context.IsSimulation 가드 필수.
    /// </summary>
    [System.Serializable]
    public abstract class RuntimeArtifact : ICombatReactor
    {
        [System.NonSerialized] public ArtifactData data;   // 획득 시 CreateInstance가 주입

        public virtual int Priority => 11;   // 패시브(50~100) 뒤, 모디파이어(10~30) 대역

        // ── 규칙형 질의 효과 (기본 0 — 필요한 것만 override) ──
        public virtual float ShopDiscountPercent  => 0f;   // 상점 가격 -N%
        public virtual float RestHealBonusPercent => 0f;   // 휴식 회복 +N%p
        public virtual int   BattleGoldBonus      => 0;    // 전투 보상 골드 +N
        public virtual float ReviveHpBonusPercent => 0f;   // 점감 부활 HP +N%p
        public virtual int   BattleStartHeal      => 0;    // 전투 시작 시 파티 회복 +N

        // ── 전투 반응 훅 (기본 무동작 — 필요한 것만 override) ──
        public virtual void OnReact(CombatTrigger trigger, CombatContext context)
        {
            switch (context)
            {
                case AttackContext a:    OnAttack(trigger, a);    break;
                case HealContext h:      OnHeal(trigger, h);      break;
                case MoveContext m:      OnMove(trigger, m);      break;
                case TurnEventContext e: OnTurnEvent(trigger, e); break;
            }
        }

        public virtual void OnAttack(CombatTrigger trigger, AttackContext context) { }
        public virtual void OnHeal(CombatTrigger trigger, HealContext context) { }
        public virtual void OnMove(CombatTrigger trigger, MoveContext context) { }
        public virtual void OnTurnEvent(CombatTrigger trigger, TurnEventContext context) { }

        /// <summary>프로토타입 → 획득용 런타임 인스턴스 (얕은 복사 + 데이터 주입).</summary>
        public RuntimeArtifact CreateInstance(ArtifactData source)
        {
            var clone = (RuntimeArtifact)MemberwiseClone();
            clone.data = source;
            return clone;
        }
    }
}
```

- [ ] **Step 2: ArtifactData.cs 작성**

```csharp
using UnityEngine;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// 유물 에셋: 표시 데이터 + 로직 프로토타입 (스펙 2026-07-21).
    /// effect에 SubclassPicker로 유물 클래스를 고르고 파라미터를 인라인 튜닝한다.
    /// 획득 시 effect.CreateInstance(this)로 런타임 인스턴스가 만들어진다.
    /// </summary>
    [CreateAssetMenu(fileName = "New ArtifactData", menuName = "DiceOrbit/ArtifactData")]
    public class ArtifactData : ScriptableObject
    {
        public string artifactName = "유물 이름";
        [TextArea(2, 4)] public string artifactTooltip = "유물 설명";
        public Sprite artifactIcon;
        [Min(1)] public int shopPrice = 120;

        [Header("효과 — 유물 1개 = 클래스 1개 (Data/Artifacts/)")]
        [SerializeReference, SubclassPicker] public RuntimeArtifact effect;
    }
}
```

- [ ] **Step 3: 컴파일 확인**

Unity MCP: `Unity_RunCommand`로 `UnityEditor.AssetDatabase.Refresh()` → `Unity_GetConsoleLogs`(error). Expected: 에러 0.

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/Core/Run/Artifact"
git commit -m "feat: RuntimeArtifact + ArtifactData - 유물 베이스 레이어 (클래스 virtual 훅으로 DIM 함정 회피)"
```

---

### Task 2: 유물 콘텐츠 6종 (Data/Artifacts/)

**Files:**
- Create: `Assets/Scripts/Data/Artifacts/PowerfullPunch/PowerfullPunch.cs`
- Create: `Assets/Scripts/Data/Artifacts/RegularStamp/RegularStamp.cs`
- Create: `Assets/Scripts/Data/Artifacts/CozyBedroll/CozyBedroll.cs`
- Create: `Assets/Scripts/Data/Artifacts/GoldenDice/GoldenDice.cs`
- Create: `Assets/Scripts/Data/Artifacts/PhoenixFeather/PhoenixFeather.cs`
- Create: `Assets/Scripts/Data/Artifacts/LifeAmulet/LifeAmulet.cs`

**Interfaces:**
- Consumes: `RuntimeArtifact` (Task 1 — 규칙형 프로퍼티/훅을 `override`)
- Produces: `DiceOrbit.Data.Artifacts` 네임스페이스의 6개 클래스 (Task 3의 폴백 풀이 `new RegularStamp()` 등으로 생성)

- [ ] **Step 1: PowerfullPunch.cs 작성** (구 유물 복원, 디버그용)

```csharp
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Run;
using UnityEngine;

namespace DiceOrbit.Data.Artifacts
{
    /// <summary>강력한 주먹 (디버그) — 캐릭터 공격의 출력을 고정값으로.</summary>
    [System.Serializable]
    public class PowerfullPunch : RuntimeArtifact
    {
        public int fixedOutput = 1000;

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.SourceUnit is not Character) return;
            Debug.Log("[Artifact] PowerfullPunch react");
            context.OutputValue = fixedOutput;
        }
    }
}
```

- [ ] **Step 2: 규칙형 5종 작성** (파일 5개, 같은 패턴)

`RegularStamp/RegularStamp.cs`:
```csharp
using DiceOrbit.Core.Run;

namespace DiceOrbit.Data.Artifacts
{
    /// <summary>단골 도장 — 상점 가격 -N%.</summary>
    [System.Serializable]
    public class RegularStamp : RuntimeArtifact
    {
        public float percent = 20f;
        public override float ShopDiscountPercent => percent;
    }
}
```

`CozyBedroll/CozyBedroll.cs`:
```csharp
using DiceOrbit.Core.Run;

namespace DiceOrbit.Data.Artifacts
{
    /// <summary>포근한 침낭 — 휴식 회복량 +N%p.</summary>
    [System.Serializable]
    public class CozyBedroll : RuntimeArtifact
    {
        public float percent = 20f;
        public override float RestHealBonusPercent => percent;
    }
}
```

`GoldenDice/GoldenDice.cs`:
```csharp
using DiceOrbit.Core.Run;

namespace DiceOrbit.Data.Artifacts
{
    /// <summary>황금 주사위 — 전투 보상 골드 +N.</summary>
    [System.Serializable]
    public class GoldenDice : RuntimeArtifact
    {
        public int amount = 25;
        public override int BattleGoldBonus => amount;
    }
}
```

`PhoenixFeather/PhoenixFeather.cs`:
```csharp
using DiceOrbit.Core.Run;

namespace DiceOrbit.Data.Artifacts
{
    /// <summary>불사조 깃털 — 점감 부활 HP +N%p.</summary>
    [System.Serializable]
    public class PhoenixFeather : RuntimeArtifact
    {
        public float percent = 15f;
        public override float ReviveHpBonusPercent => percent;
    }
}
```

`LifeAmulet/LifeAmulet.cs`:
```csharp
using DiceOrbit.Core.Run;

namespace DiceOrbit.Data.Artifacts
{
    /// <summary>생명의 부적 — 전투 시작 시 파티 전원 +N 회복.</summary>
    [System.Serializable]
    public class LifeAmulet : RuntimeArtifact
    {
        public int amount = 5;
        public override int BattleStartHeal => amount;
    }
}
```

- [ ] **Step 3: 컴파일 확인**

Unity MCP: Refresh → `Unity_GetConsoleLogs`(error). Expected: 에러 0.

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/Data/Artifacts"
git commit -m "feat: 유물 콘텐츠 6종 - 유물 1개 = 클래스 1개 (PowerfullPunch 복원 + 규칙형 5종)"
```

---

### Task 3: ArtifactManager

**Files:**
- Create: `Assets/Scripts/Core/Run/Artifact/ArtifactManager.cs`

**Interfaces:**
- Consumes: `ArtifactData`/`RuntimeArtifact` (Task 1), 콘텐츠 5종 (Task 2, 폴백 풀)
- Produces: `ArtifactManager` — `Instance`/`EnsureInstance()`, `IReadOnlyList<RuntimeArtifact> Artifacts`, `event System.Action OnArtifactsChanged`, `Owns(ArtifactData)`, `Grant(ArtifactData)`, `AddArtifact(RuntimeArtifact)`, `RemoveArtifact<T>()`, `RemoveArtifact(RuntimeArtifact)`, `ArtifactData GrantRandom()`, `List<ArtifactData> GetShopOfferings(int)`, `ArtifactData FindInPool(string)`, 질의 5종 `float ShopDiscount01`/`float RestHealBonus01`/`int BattleGoldBonus`/`float ReviveHpBonus01`/`int BattleStartHeal`

- [ ] **Step 1: ArtifactManager.cs 작성**

```csharp
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Data.Artifacts;
using UnityEngine;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// 보유 유물의 단일 출처 + 효과 질의 창구 (스펙 2026-07-21).
    /// 유물 = ArtifactData(에셋) + RuntimeArtifact(획득 시 CreateInstance로 복제한 런타임 인스턴스).
    /// 보유 인스턴스 자체가 ICombatReactor라 CombatPipeline이 Artifacts를 그대로 수집한다.
    /// 규칙형 효과는 소비처(상점/휴식/보상/부활/전투 시작)가 프로퍼티로 합산값을 읽는다.
    /// artifactPool이 비어 있으면 기본 5종을 런타임 생성 (에셋 셋업 전에도 동작).
    /// </summary>
    public class ArtifactManager : MonoBehaviour
    {
        public static ArtifactManager Instance { get; private set; }

        [Header("유물 풀 — 획득 후보 (엘리트 드랍/상점 진열). 비우면 기본 세트 런타임 생성")]
        [SerializeField] private List<ArtifactData> artifactPool = new List<ArtifactData>();

        [Header("시작 유물 — 게임 시작 시 바로 보유 (테스트/디버그용)")]
        [SerializeField] private List<ArtifactData> startingArtifacts = new List<ArtifactData>();

        private readonly List<RuntimeArtifact> artifacts = new List<RuntimeArtifact>();

        public IReadOnlyList<RuntimeArtifact> Artifacts => artifacts;
        public event System.Action OnArtifactsChanged;

        // ── 규칙형 질의 (소비처가 읽는다) ──
        public float ShopDiscount01  => Mathf.Clamp01(artifacts.Sum(a => a.ShopDiscountPercent) / 100f);
        public float RestHealBonus01 => artifacts.Sum(a => a.RestHealBonusPercent) / 100f;
        public int   BattleGoldBonus => artifacts.Sum(a => a.BattleGoldBonus);
        public float ReviveHpBonus01 => artifacts.Sum(a => a.ReviveHpBonusPercent) / 100f;
        public int   BattleStartHeal => artifacts.Sum(a => a.BattleStartHeal);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            EnsureDefaultPool();

            foreach (var data in startingArtifacts)
                Grant(data);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static ArtifactManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<ArtifactManager>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            return new GameObject("ArtifactManager").AddComponent<ArtifactManager>();
        }

        // ── 획득/제거 ──────────────────────────────────────────

        public bool Owns(ArtifactData data)
            => data != null && artifacts.Any(a => a.data == data);

        /// <summary>에셋 기준 획득 — effect 프로토타입을 복제해 보유 목록에 추가.</summary>
        public void Grant(ArtifactData data)
        {
            if (data == null || Owns(data)) return;
            if (data.effect == null)
            {
                Debug.LogError($"[Artifact] '{data.artifactName}' 에셋에 effect가 없습니다 — SubclassPicker로 유물 클래스를 지정하세요.");
                return;
            }
            artifacts.Add(data.effect.CreateInstance(data));
            Debug.Log($"[Artifact] 획득: {data.artifactName}");
            OnArtifactsChanged?.Invoke();
        }

        /// <summary>런타임 인스턴스 직접 추가 (디버그/특수 — data 없는 유물 허용, HUD는 클래스명 표시).</summary>
        public void AddArtifact(RuntimeArtifact artifact)
        {
            if (artifact == null) return;
            artifacts.Add(artifact);
            Debug.Log($"[Artifact] Added: {artifact.GetType().Name}");
            OnArtifactsChanged?.Invoke();
        }

        /// <summary>T 타입(상속 포함) 유물 전부 제거.</summary>
        public bool RemoveArtifact<T>() where T : RuntimeArtifact
        {
            int removed = artifacts.RemoveAll(a => a is T);
            if (removed == 0) return false;
            Debug.Log($"[Artifact] Removed {removed} × {typeof(T).Name}");
            OnArtifactsChanged?.Invoke();
            return true;
        }

        public bool RemoveArtifact(RuntimeArtifact artifact)
        {
            if (artifact == null || !artifacts.Remove(artifact)) return false;
            Debug.Log($"[Artifact] Removed: {artifact.GetType().Name}");
            OnArtifactsChanged?.Invoke();
            return true;
        }

        // ── 풀 (드랍/상점/세이브) ─────────────────────────────

        /// <summary>미보유 풀에서 랜덤 1개 획득. 없으면 null.</summary>
        public ArtifactData GrantRandom()
        {
            var candidates = artifactPool.Where(d => d != null && d.effect != null && !Owns(d)).ToList();
            if (candidates.Count == 0) return null;
            var picked = candidates[Random.Range(0, candidates.Count)];
            Grant(picked);
            return picked;
        }

        /// <summary>상점 진열용: 미보유 유물 랜덤 count개.</summary>
        public List<ArtifactData> GetShopOfferings(int count)
            => artifactPool.Where(d => d != null && d.effect != null && !Owns(d))
                .OrderBy(_ => Random.value).Take(count).ToList();

        /// <summary>이름으로 풀에서 찾기 (세이브 복원용).</summary>
        public ArtifactData FindInPool(string artifactName)
            => artifactPool.FirstOrDefault(d => d != null && d.artifactName == artifactName);

        // ── 기본 풀 (에셋 미지정 폴백) ─────────────────────────

        private void EnsureDefaultPool()
        {
            if (artifactPool.Count > 0) return;

            artifactPool.Add(CreateDefault("단골 도장", "상점 가격 20% 할인", new RegularStamp(), 100));
            artifactPool.Add(CreateDefault("포근한 침낭", "휴식 회복량 +20%p", new CozyBedroll(), 110));
            artifactPool.Add(CreateDefault("황금 주사위", "전투 보상 골드 +25", new GoldenDice(), 130));
            artifactPool.Add(CreateDefault("불사조 깃털", "부활 HP +15%p", new PhoenixFeather(), 150));
            artifactPool.Add(CreateDefault("생명의 부적", "전투 시작 시 파티 전원 5 회복", new LifeAmulet(), 120));
            Debug.Log("[ArtifactManager] 유물 풀이 비어 있어 기본 5종을 런타임 생성했습니다 (에셋으로 교체 권장).");
        }

        private static ArtifactData CreateDefault(string name, string desc, RuntimeArtifact effect, int price)
        {
            var data = ScriptableObject.CreateInstance<ArtifactData>();
            data.name = name;
            data.artifactName = name;
            data.artifactTooltip = desc;
            data.effect = effect;
            data.shopPrice = price;
            return data;
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

Unity MCP: Refresh → `Unity_GetConsoleLogs`(error). Expected: 에러 0.

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/Core/Run/Artifact"
git commit -m "feat: ArtifactManager - 보유/풀/드랍/상점/질의 창구 (구 Add/Remove API 승계)"
```

---

### Task 4: 전투/런 소비처 치환 (질의 프로퍼티 4곳 + 파이프라인)

**Files:**
- Modify: `Assets/Scripts/Core/GameFlowManager.cs:272`
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/WaveManager.cs:55`
- Modify: `Assets/Scripts/Core/Stage/BattleStage/Units/Character/Character.cs:450`
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Pipeline/CombatPipeline.cs:129-136`

**Interfaces:**
- Consumes: `ArtifactManager.Instance` 질의 프로퍼티 + `Artifacts` (Task 3)
- Produces: 없음 (소비처 전환). 이 태스크 후에도 RelicManager는 남은 소비처(UI/세이브)를 위해 존치 — 두 시스템 일시 공존은 정상.

- [ ] **Step 1: GameFlowManager.cs 272행** — 휴식 보너스

```csharp
// 변경 전
float ratio = restHealRatio + (RelicManager.Instance?.RestHealBonus01 ?? 0f);
// 변경 후
float ratio = restHealRatio + (ArtifactManager.Instance?.RestHealBonus01 ?? 0f);
```

- [ ] **Step 2: WaveManager.cs 55행** — 전투 시작 회복

```csharp
// 변경 전
int startHeal = Run.RelicManager.Instance?.BattleStartHeal ?? 0;
// 변경 후
int startHeal = Run.ArtifactManager.Instance?.BattleStartHeal ?? 0;
```

- [ ] **Step 3: Character.cs 450행** — 부활 HP 보너스

```csharp
// 변경 전
ratio = Mathf.Clamp01(ratio + (Run.RelicManager.Instance?.ReviveHpBonus01 ?? 0f));
// 변경 후
ratio = Mathf.Clamp01(ratio + (Run.ArtifactManager.Instance?.ReviveHpBonus01 ?? 0f));
```

- [ ] **Step 4: CombatPipeline.cs D단계 (129-136행)** — 리액터 수집

```csharp
// 변경 전
// D. 유물에서 Reactor 수집 (RelicManager — 구 Artifact 시스템은 유물로 통합됨, 2026-07)
if (Core.Run.RelicManager.Instance != null)
{
    foreach (var reactor in Core.Run.RelicManager.Instance.CombatReactors)
    {
        reactors.Add(reactor);
    }
}
// 변경 후
// D. 유물에서 Reactor 수집 (ArtifactManager — 보유 런타임 인스턴스 자체가 ICombatReactor)
if (Core.Run.ArtifactManager.Instance != null)
{
    foreach (var artifact in Core.Run.ArtifactManager.Instance.Artifacts)
    {
        reactors.Add(artifact);
    }
}
```

- [ ] **Step 5: 컴파일 확인**

Unity MCP: Refresh → `Unity_GetConsoleLogs`(error). Expected: 에러 0.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Core/GameFlowManager.cs "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/WaveManager.cs" "Assets/Scripts/Core/Stage/BattleStage/Units/Character/Character.cs" "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Pipeline/CombatPipeline.cs"
git commit -m "refactor: 전투/런 소비처를 ArtifactManager로 전환 (휴식/시작회복/부활/파이프라인)"
```

---

### Task 5: UI 소비처 치환 (RewardUI / ShopUI / RunHudUI / EventDefinition)

**Files:**
- Modify: `Assets/Scripts/UI/RewardUI.cs:118,136-144`
- Modify: `Assets/Scripts/UI/ShopUI.cs:194,203,213,241-255`
- Modify: `Assets/Scripts/UI/RunHudUI.cs:76,138-152`
- Modify: `Assets/Scripts/Core/Run/EventDefinition.cs:120-124`

**Interfaces:**
- Consumes: `ArtifactManager` API + `ArtifactData` 필드(`artifactName/artifactTooltip/artifactIcon/shopPrice`) + `RuntimeArtifact.data` (Task 1, 3)
- Produces: 없음. `[SerializeField]` 필드명(`relicRow`, `relicShelfRow`, `relicOfferCount`)과 `EventOutcomeType.GainRandomRelic` enum명은 직렬화 호환 위해 유지.

- [ ] **Step 1: RewardUI.cs** — 골드 보너스(118행) + 엘리트 유물 드랍(136-144행)

```csharp
// 118행 변경 전
int goldAmount = goldPerReward + (RelicManager.Instance?.BattleGoldBonus ?? 0);
// 변경 후
int goldAmount = goldPerReward + (ArtifactManager.Instance?.BattleGoldBonus ?? 0);
```

```csharp
// 136-144행 변경 전
var relic = RelicManager.EnsureInstance().GetShopOfferings(1).FirstOrDefault();
if (relic != null)
{
    AddRewardRow($"🏺  유물 — {relic.RelicName}", row =>
    {
        RelicManager.Instance.Grant(relic);
        Destroy(row);
    });
}
// 변경 후
var artifact = ArtifactManager.EnsureInstance().GetShopOfferings(1).FirstOrDefault();
if (artifact != null)
{
    AddRewardRow($"🏺  유물 — {artifact.artifactName}", row =>
    {
        ArtifactManager.Instance.Grant(artifact);
        Destroy(row);
    });
}
```

- [ ] **Step 2: ShopUI.cs** — 할인(194행), 진열 목록(203, 213행), 유물 카드(241-255행)

```csharp
// 194행 변경 전
float discount = RelicManager.Instance?.ShopDiscount01 ?? 0f;
// 변경 후
float discount = ArtifactManager.Instance?.ShopDiscount01 ?? 0f;
```

```csharp
// 203행 변경 전
private readonly List<RelicDefinition> _relicOffers = new List<RelicDefinition>();
// 변경 후
private readonly List<ArtifactData> _relicOffers = new List<ArtifactData>();
```

```csharp
// 213행 변경 전
_relicOffers.AddRange(RelicManager.EnsureInstance().GetShopOfferings(relicOfferCount));
// 변경 후
_relicOffers.AddRange(ArtifactManager.EnsureInstance().GetShopOfferings(relicOfferCount));
```

```csharp
// 241-255행(유물 카드 루프) 변경 전
foreach (var relic in _relicOffers)
{
    var captured = relic;
    int price = ApplyDiscount(relic.ShopPrice);
    AddStockCard(relicShelfRow, relic.RelicName, relic.Description, price,
        sold: _soldOut.Contains(relic),
        affordable: gold >= price,
        onBuy: () =>
        {
            if (!GoldManager.Instance.TrySpend(ApplyDiscount(captured.ShopPrice))) return;
            RelicManager.Instance.Grant(captured);
            _soldOut.Add(captured);
            RefreshGold();
            RenderStock();
        });
}
// 변경 후
foreach (var artifact in _relicOffers)
{
    var captured = artifact;
    int price = ApplyDiscount(artifact.shopPrice);
    AddStockCard(relicShelfRow, artifact.artifactName, artifact.artifactTooltip, price,
        sold: _soldOut.Contains(artifact),
        affordable: gold >= price,
        onBuy: () =>
        {
            if (!GoldManager.Instance.TrySpend(ApplyDiscount(captured.shopPrice))) return;
            ArtifactManager.Instance.Grant(captured);
            _soldOut.Add(captured);
            RefreshGold();
            RenderStock();
        });
}
```

- [ ] **Step 3: RunHudUI.cs** — 구독(76행) + 칩 리빌드(138-152행)

```csharp
// 76행 변경 전
RelicManager.EnsureInstance().OnRelicsChanged += RebuildRelics;
// 변경 후
ArtifactManager.EnsureInstance().OnArtifactsChanged += RebuildRelics;
```

```csharp
// 138-152행 변경 전
private void RebuildRelics()
{
    if (relicRow == null) return;
    Clear(relicRow);

    var owned = RelicManager.Instance?.Owned;
    if (owned == null) return;

    foreach (var relic in owned)
    {
        if (relic == null) continue;
        var chip = CreateChip(relicRow, relic.Icon, relic.RelicName, RelicTint);
        AddHoverTooltip(chip, $"<b>[{relic.RelicName}]</b>\n{relic.Description}");
    }
}
// 변경 후 (data 없는 직접 추가 인스턴스는 클래스명 표시 — 스펙 §5)
private void RebuildRelics()
{
    if (relicRow == null) return;
    Clear(relicRow);

    var owned = ArtifactManager.Instance?.Artifacts;
    if (owned == null) return;

    foreach (var artifact in owned)
    {
        if (artifact == null) continue;
        var data = artifact.data;
        string title = data != null ? data.artifactName : artifact.GetType().Name;
        var chip = CreateChip(relicRow, data != null ? data.artifactIcon : null, title, RelicTint);
        AddHoverTooltip(chip, $"<b>[{title}]</b>\n{(data != null ? data.artifactTooltip : "")}");
    }
}
```

- [ ] **Step 4: EventDefinition.cs** — 랜덤 유물 이벤트(120-124행)

```csharp
// 변경 전
case EventOutcomeType.GainRandomRelic:
{
    var relic = RelicManager.EnsureInstance().GrantRandom();
    return relic != null ? $"유물 획득 — {relic.RelicName}" : "";
}
// 변경 후 (enum 멤버명 GainRandomRelic은 직렬화 호환 위해 유지)
case EventOutcomeType.GainRandomRelic:
{
    var artifact = ArtifactManager.EnsureInstance().GrantRandom();
    return artifact != null ? $"유물 획득 — {artifact.artifactName}" : "";
}
```

- [ ] **Step 5: 컴파일 확인**

Unity MCP: Refresh → `Unity_GetConsoleLogs`(error). Expected: 에러 0.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/UI/RewardUI.cs Assets/Scripts/UI/ShopUI.cs Assets/Scripts/UI/RunHudUI.cs Assets/Scripts/Core/Run/EventDefinition.cs
git commit -m "refactor: UI 소비처를 ArtifactManager/ArtifactData로 전환 (보상/상점/HUD/이벤트)"
```

---

### Task 6: 세이브 호환 (RunSaveService + 복원부)

**Files:**
- Modify: `Assets/Scripts/Core/Run/RunSaveService.cs:18,83-84`
- Modify: `Assets/Scripts/Core/GameFlowManager.cs:465-468`

**Interfaces:**
- Consumes: `ArtifactManager.Artifacts`/`FindInPool`/`Grant` (Task 3)
- Produces: `RunSaveData.ArtifactNames`(신규 저장 필드), `RunSaveData.EffectiveArtifactNames`(복원용 게터 — 신 필드 우선, 비면 구 `RelicNames` 폴백)

- [ ] **Step 1: RunSaveData 필드 추가** (18행 주변)

```csharp
// 변경 전
public int Gold;
public List<string> RelicNames = new List<string>();
// 변경 후
public int Gold;
public List<string> ArtifactNames = new List<string>();  // 저장은 여기에
public List<string> RelicNames = new List<string>();     // 개편 이전 세이브 로드 전용 (쓰지 않음)

/// <summary>복원 시 이걸 읽는다 — 신 필드 우선, 비어 있으면 구 필드 폴백.</summary>
public List<string> EffectiveArtifactNames
    => ArtifactNames.Count > 0 ? ArtifactNames : RelicNames;
```

- [ ] **Step 2: SaveCurrent의 유물 저장** (83-84행)

```csharp
// 변경 전
if (RelicManager.Instance != null)
    data.RelicNames.AddRange(RelicManager.Instance.Owned.Where(r => r != null).Select(r => r.RelicName));
// 변경 후
if (ArtifactManager.Instance != null)
    data.ArtifactNames.AddRange(ArtifactManager.Instance.Artifacts
        .Where(a => a != null && a.data != null)
        .Select(a => a.data.artifactName));
```

- [ ] **Step 3: GameFlowManager 복원부** (465-468행)

```csharp
// 변경 전
// 유물 / 포션 (이름 매칭)
var relics = RelicManager.EnsureInstance();
foreach (var name in data.RelicNames)
    relics.Grant(relics.FindInPool(name));
// 변경 후
// 유물 / 포션 (이름 매칭 — 구 세이브는 RelicNames 폴백)
var artifactManager = ArtifactManager.EnsureInstance();
foreach (var name in data.EffectiveArtifactNames)
    artifactManager.Grant(artifactManager.FindInPool(name));
```

- [ ] **Step 4: 컴파일 확인**

Unity MCP: Refresh → `Unity_GetConsoleLogs`(error). Expected: 에러 0.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/Run/RunSaveService.cs Assets/Scripts/Core/GameFlowManager.cs
git commit -m "feat: 세이브 호환 - ArtifactNames 저장 + 구 RelicNames 로드 폴백"
```

---

### Task 7: Relic 시스템 철거 + 씬 정리

**Files:**
- Delete: `Assets/Scripts/Core/Run/RelicManager.cs` (+ `.meta`)
- Delete: `Assets/Scripts/Core/Run/RelicDefinition.cs` (+ `.meta`)
- Delete: `Assets/Scripts/Core/Run/RelicCombatEffect.cs` (+ `.meta`)

**Interfaces:**
- Consumes: Task 4~6 완료 상태 (RelicManager 참조 0이어야 함)
- Produces: 없음

- [ ] **Step 1: 잔존 참조 0 확인**

```bash
grep -rn "RelicManager\|RelicDefinition\|RelicCombatEffect\|PowerfulPunchEffect" "Assets/Scripts" --include="*.cs"
```
Expected: 매치 0건 (있으면 해당 소비처 먼저 전환).

- [ ] **Step 2: 씬의 RelicManager 컴포넌트 확인**

`RelicManager.cs.meta`에서 guid를 읽고 두 씬을 검사:
```bash
grep "guid" "Assets/Scripts/Core/Run/RelicManager.cs.meta"
grep -l "<그 guid>" "Assets/Scenes/BattleScene.unity" "Assets/Scenes/MainMenu.unity" "Assets/Scenes/TestScene.unity"
```
- 매치 0건이면 (RelicManager가 런타임 EnsureInstance로만 생성됨) 그대로 진행.
- 매치가 있으면: Unity MCP `Unity_ManageGameObject`로 해당 GameObject에 `ArtifactManager` 컴포넌트를 추가하고 RelicManager 컴포넌트를 제거한 뒤 씬 저장 (`Unity_ManageScene` save). 인스펙터에 있던 startingRelics 목록은 에셋 부재로 이관 불가 — 사용자에게 보고.

- [ ] **Step 3: 파일 삭제**

```bash
git rm "Assets/Scripts/Core/Run/RelicManager.cs" "Assets/Scripts/Core/Run/RelicManager.cs.meta" "Assets/Scripts/Core/Run/RelicDefinition.cs" "Assets/Scripts/Core/Run/RelicDefinition.cs.meta" "Assets/Scripts/Core/Run/RelicCombatEffect.cs" "Assets/Scripts/Core/Run/RelicCombatEffect.cs.meta"
```

- [ ] **Step 4: 컴파일 확인**

Unity MCP: Refresh → `Unity_GetConsoleLogs`(error). Expected: 에러 0, "Missing script" 경고 0.

- [ ] **Step 5: Commit**

```bash
git commit -m "refactor: 구 Relic 시스템 철거 - ArtifactManager로 완전 대체"
```

---

### Task 8: 문서 갱신

**Files:**
- Modify: `Docs/run_structure_system.md` (§1 흐름의 RelicManager 언급, §2 파일 지도 3행, §3 결정 ⑥)

**Interfaces:**
- Consumes: 최종 구현 상태 (Task 1~7)
- Produces: 없음

- [ ] **Step 1: run_structure_system.md의 유물 관련 기술 갱신**

- §1 흐름도의 `RelicManager.GrantRandom` → `ArtifactManager.GrantRandom`
- §2 파일 지도에서 `RelicDefinition.cs`/`RelicCombatEffect.cs`/`RelicManager.cs` 3행을 다음으로 교체:

```markdown
| `Artifact/ArtifactData.cs` | 유물 에셋: 표시 데이터 + SubclassPicker 로직 프로토타입 |
| `Artifact/RuntimeArtifact.cs` | 유물 베이스: ICombatReactor + 규칙형 virtual 프로퍼티, 획득 시 CreateInstance 복제 |
| `Artifact/ArtifactManager.cs` | 유물 보유/풀/드랍/상점 진열 + 효과 질의 창구 |
```

- §2 개조된 기존 파일 표의 `CombatPipeline.cs` 행: `유물 리액터 수집 D단계 → RelicManager` → `유물 리액터 수집 D단계 → ArtifactManager (보유 인스턴스 = 리액터)`
- §3 결정 ⑥을 다음으로 교체:

```markdown
**⑥ 유물 = 클래스 기반 재구축** (2026-07-21, 스펙 `2026-07-21-artifact-system-rebuild-design.md`):
- 유물 1개 = `RuntimeArtifact` 서브클래스 1개 (`Data/Artifacts/`). 규칙형 효과는 virtual
  프로퍼티 오버라이드, 전투 반응은 virtual 훅 오버라이드 — 한 유물이 둘 다 가능.
- `ArtifactData`(SO)가 표시 데이터 + 프로토타입을 들고, 획득 시 `CreateInstance`로
  런타임 인스턴스를 복제 (상태가 에셋에 오염되지 않음).
- 세이브: `ArtifactNames` 저장, 개편 이전 `RelicNames`는 로드 폴백으로 지원.
```

- [ ] **Step 2: Commit**

```bash
git add Docs/run_structure_system.md
git commit -m "docs: 런 구조 문서에 Artifact 재구축 반영"
```

---

## 최종 검증 (사용자, 에디터 플레이 — 스펙 §7)

① 엘리트 클리어 → 유물 드랍 + HUD 칩 ② 상점 진열/구매/할인 ③ 휴식 보너스 ④ 부활 HP 보너스 ⑤ 전투 시작 회복 ⑥ 보상 골드 보너스 ⑦ PowerfullPunch(시작 유물로 지정 시) 공격 출력 고정 ⑧ 세이브→이어하기 유물 복원 ⑨ 개편 이전 세이브(RelicNames) 이어하기 복원.
