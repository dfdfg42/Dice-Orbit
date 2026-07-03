# CombatContext / CombatAction 병합 + 서브클래스 — 구현 계획

> **⚠️ 상태 업데이트 (2026-07-03):** 이 계획은 **실행 완료**되었으나 최종 코드는 여기서 더 나아갔다. 리액터 분기는 `context is XContext` 패턴 매칭이 아니라 **타입별 DIM 훅**(`OnAttack`/`OnHeal`/`OnMove`/`OnTurnEvent`)으로 이전되었다 — 정확한 현행 문서는 [combat_reactor_dispatch.md](combat_reactor_dispatch.md) 참조.
> **Step 5(ActionType shim 제거)는 하지 않았다** — `ActionType Type`은 마이그레이션 shim으로 `CombatContext` 기반에 **여전히 존재**한다.
> 본문이 참조하는 `SampleMonster.cs`/`SampleTile.cs`는 **삭제됨(2026-07-03)**. 타일 지속시간 틱은 파이프라인 `TileTick` 방송이 아니라 **`CombatManager`의 직접 per-tick 호출**(~L545)로 수정되었다.
> 아래 체크리스트 본문은 **역사적 기록**으로 그대로 보존한다.

> **For agentic workers:** 이 계획은 task 단위로 실행한다. 각 task의 단계는 체크박스(`- [ ]`)로 추적.
> 설계 근거: [combat_context_action_merge_design.md](combat_context_action_merge_design.md)

**Goal:** `CombatAction`을 `CombatContext`로 병합하고 `CombatContext`를 추상 기반 + 행위별 서브클래스(`AttackContext`/`HealContext`/`MoveContext`/`TurnEventContext`)로 만들어, 리액터가 `context.Action.Type == X` 대신 `context is XContext`로 분기하도록 전환한다. 더불어 죽은 필드(`IsTiling`/`AddEffectToTarget`/`IgnoreDefense`/`IsCritical`/`OnActionSuccess`)를 제거하고 타일 지속시간 버그를 고친다.

**Architecture:** 단일 `CombatPipeline.Process` 4단계 + `NotifyReactors` 폴링 구조는 **그대로 유지**. `OnReact(CombatTrigger, CombatContext)` 시그니처도 불변(기반 타입을 받음). 바뀌는 것은 데이터 모델(컨텍스트 타입)과 그 타입을 읽는 분기뿐이다.

**Tech Stack:** Unity 2022.3.31f1, C# (Assembly-CSharp), 신규 Input System. 테스트 하니스 없음 → 검증은 헤드리스 컴파일 + 플레이테스트.

---

## 검증 방식 (이 프로젝트 적응)

이 리포지토리에는 유닛 테스트 프레임워크가 없다. 각 task의 "검증" 단계는 다음으로 대체한다:

- **컴파일 검증(필수, 모든 Step 종료 시):**
  ```bash
  "C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" \
    Assembly-CSharp.csproj \
    /p:FrameworkPathOverride="C:/Program Files/Unity/Hub/Editor/2022.3.31f1/Editor/Data/MonoBleedingEdge/lib/mono/4.7.1-api"
  ```
  기대: **EXIT 0**. CS0649/CS0105 경고는 무시. (Step 1~3은 동작 불변이므로 컴파일 통과 = 회귀 없음의 1차 방어선.)
- **플레이테스트(Step 4 전용, 동작 변화 있음):** 해당 Step의 체크리스트 참조.

> 한 Step 내부의 중간 상태는 컴파일이 깨질 수 있다(타입 리네임 특성). **컴파일+커밋은 각 Step의 마지막 task에서** 한다.

---

## 파일 인벤토리

**코어 타입 (구조 변경):**
- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Pipeline/CombatContext.cs`
- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Pipeline/CombatAction.cs` (클래스 삭제, enum+struct 잔존)
- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Pipeline/CombatPipeline.cs`
- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Pipeline/ICombatReactor.cs` (Step 4: `OnActionSuccess` 제거)

**생산자 (CombatContext 생성, 약 11곳):** Unit, Character, Monster, CombatManager(×3), SkillData(×2), SkillTargetSelector, CharacterActiveTemplate, Goblin, HoneyPawTile, SampleMonster, SampleTile

**리액터 (context 읽기만):** UnitStats, TileData, TileAttribute, StatusEffect, StatusEffectManager, BasicEffect, PositioningPassive, FocusPassive, BattleCryPassive, ReagentPrepPassive, PowerfullPunch, SolraPriest, SolraKnight, LunaPriest, LunaKnight, SnowMan, SnowGolem, FrostTotem, MommyBear, BabyBear, SampleMonster

> **중요한 순서 규칙:** `IsTiling`은 `TileData`/`TileAttribute` 분기가 읽으므로 **Step 1~3 동안 기반에 살려둔다.** Step 4에서 그 두 분기를 `TileTick` 패턴으로 재작성하면서 `IsTiling` 필드를 함께 삭제한다. (`AddEffectToTarget`/`IgnoreDefense`/`IsCritical`은 읽는 곳이 없으므로 Step 1에서 안전하게 제거.)

---

# Step 1 — 병합 (CombatContext가 CombatAction을 흡수, 아직 concrete)

이 Step이 끝나면 `CombatAction` 클래스는 사라지고, 모든 코드가 `context.Type`/`context.HasTag` 등 단일 객체로 접근한다. 동작은 완전히 동일. **독립 출시 가능한 체크포인트.**

### Task 1.1: `CombatContext.cs` — CombatAction 멤버 흡수

**Files:** Modify: `.../Combat/Pipeline/CombatContext.cs`

- [ ] **Step 1: 파일 전체를 아래로 교체**

```csharp
using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Data; // EffectType

namespace DiceOrbit.Core.Pipeline
{
    /// <summary>
    /// 파이프라인을 통과하며 데이터를 공유하는 컨텍스트.
    /// (구 CombatAction 흡수: Name/Type/BaseValue/Tags/Effects가 여기에 있다.)
    /// </summary>
    public class CombatContext
    {
        public Unit SourceUnit;
        public Unit Target;

        public string Name;
        public ActionType Type;
        public float BaseValue;        // 기본 데미지/힐량

        public float OutputValue;      // 최종 결과값 (데미지/힐량)
        public bool IsCancelled;
        public bool IsEffected;
        public bool IsTiling;          // 사용처: TileData/TileAttribute (Step 4에서 제거)
        public bool IsSimulation;

        public HashSet<string> Tags = new HashSet<string>();
        public List<ActionEffectInfo> Effects = new List<ActionEffectInfo>();

        public CombatContext(Unit source, Unit target, string name, ActionType type, float baseValue)
        {
            SourceUnit = source;
            Target = target;
            Name = name;
            Type = type;
            BaseValue = baseValue;
            OutputValue = baseValue;
            IsCancelled = false;
            IsEffected = false;
            IsTiling = false;
        }

        public void AddTag(string tag) => Tags.Add(tag);
        public bool HasTag(string tag) => Tags.Contains(tag);
        public void AddEffect(EffectType type, int value, int duration)
            => Effects.Add(new ActionEffectInfo(type, value, duration));
    }
}
```

> 변경점: `Action` 필드 삭제 → `Name/Type/BaseValue/Tags/Effects`+메서드 흡수. `AddEffectToTarget` 스텁 **삭제**. `IsTiling`은 **유지**.

### Task 1.2: `CombatAction.cs` — 클래스 삭제, enum + struct 유지

**Files:** Modify: `.../Combat/Pipeline/CombatAction.cs`

- [ ] **Step 1: 파일 전체를 아래로 교체** (CombatAction 클래스 제거, `ActionType`/`ActionEffectInfo`만 남김)

```csharp
namespace DiceOrbit.Core.Pipeline
{
    public enum ActionType
    {
        Attack,
        Heal,
        Utility,
        None,
        Skill,
        Move,
        OnArrive,
        OnTreaverse,
        OnStartTurn,
        OnEndTurn,
    }

    /// <summary>효과 정보 구조체 (버프/디버프 등)</summary>
    public struct ActionEffectInfo
    {
        public DiceOrbit.Data.EffectType Type;
        public int Value;
        public int Duration;

        public ActionEffectInfo(DiceOrbit.Data.EffectType type, int value, int duration)
        {
            Type = type;
            Value = value;
            Duration = duration;
        }
    }
}
```

> `IgnoreDefense`/`IsCritical`은 CombatAction과 함께 사라진다(읽는 곳 없음 — 확인됨).

### Task 1.3: `CombatPipeline.cs` — `.Action.X` → `.X`, null 가드

**Files:** Modify: `.../Combat/Pipeline/CombatPipeline.cs`

- [ ] **Step 1: null 가드 2곳에서 `Action` 제거**
  - L33: `if (context == null || context.Action == null) return;` → `if (context == null) return;`
  - L56: `if (context == null || context.Action == null) return 0;` → `if (context == null) return 0;`
- [ ] **Step 2: `context.Action.Type` → `context.Type` (L61, L72, L90, L176, L184)**, `context.Action.HasTag` → `context.HasTag` (L179, L188), `context.Action.Effects` → `context.Effects` (L195, L197). L98 주석의 `context.Action.Type` → `context.Type` 또는 삭제.
- [ ] **Step 3: 컴파일 검증** (Step 1 종료 task에서 일괄 — Task 1.6)

### Task 1.4: 생산자 — `new CombatAction(...)` + `new CombatContext(...)` 쌍을 단일 ctor로

각 파일에서 아래 before→after 적용. (모두 `new CombatContext(src, tgt, name, type, base)` 형태로 축약.)

- [ ] **Unit.cs** (`.../Units/Unit.cs`)
  - L65-66: `var action = new Pipeline.CombatAction("Turn Start", Pipeline.ActionType.OnStartTurn, 0); var context = new Pipeline.CombatContext(this, this, action);`
    → `var context = new Pipeline.CombatContext(this, this, "Turn Start", Pipeline.ActionType.OnStartTurn, 0);`
  - L79-80: `... "Turn End", OnEndTurn ...` 동일 패턴 → `var context = new Pipeline.CombatContext(this, this, "Turn End", Pipeline.ActionType.OnEndTurn, 0);`
- [ ] **Character.cs** (`.../Units/Character/Character.cs`, L272-278)
  ```csharp
  if (Pipeline.CombatPipeline.Instance != null)
  {
      var moveContext = new Pipeline.CombatContext(this, this, "Move", Pipeline.ActionType.Move, stepsTraveled);
      moveContext.AddTag("Move");
      Pipeline.CombatPipeline.Instance.Process(moveContext);
  }
  ```
- [ ] **Monster.cs** (`.../Units/Monster/Monster.cs`, L344-345)
  → `var simCtx = new CombatContext(this, repTarget, nextSkill.skillData.SkillName, ActionType.Attack, previewBase);`
- [ ] **CombatManager.cs** (`.../Combat/CombatManager.cs`)
  - L545: `new CombatContext(null, null, new CombatAction("Turn End", ActionType.None, 0));` → `new CombatContext(null, null, "Turn End", ActionType.None, 0);`
  - L619-620: `var action = new Pipeline.CombatAction("Direct Attack", Attack, damage); var context = new Pipeline.CombatContext(null, target, action);` → `var context = new Pipeline.CombatContext(null, target, "Direct Attack", Pipeline.ActionType.Attack, damage);`
  - L640-641: `... "Global Attack" ...` → `var context = new Pipeline.CombatContext(null, monster, "Global Attack", Pipeline.ActionType.Attack, damage);`
- [ ] **SkillData.cs** (`.../Combat/SkillData/SkillData.cs`)
  - L98-102 (AttackUnits): `new CombatContext(source, target, new CombatAction(SkillName, ActionType.Attack, damage))` → `new CombatContext(source, target, SkillName, ActionType.Attack, damage)`
  - L127-131 (AttackTiles): `new CombatContext(source, character, new CombatAction(SkillName, ActionType.Attack, damage))` → `new CombatContext(source, character, SkillName, ActionType.Attack, damage)`
- [ ] **SkillTargetSelector.cs** (`.../Combat/SkillTargetSelector.cs`, L282-283)
  → `var simContext = new Pipeline.CombatContext(sourceCharacter, targetUnit, tmpl.SkillName, Pipeline.ActionType.Attack, raw);`
- [ ] **CharacterActiveTemplate.cs** (`.../Combat/Skills/CharacterActiveTemplate.cs`, L110-114)
  ```csharp
  var context = new CombatContext(source, target, skillName, ActionType.Attack, rawDamage);
  if (vfxProfile != null) context.AddTag("CustomVfx");
  ```
- [ ] **Goblin.cs** (`.../Wave1/Goblin/Goblin.cs`, L118-121)
  ```csharp
  var context = new CombatContext(source, character, SkillName, ActionType.Attack, damage);
  if (vfxProfile != null) context.AddTag("CustomVfx");
  // (구 L121 'new CombatContext(..., action)' 줄 삭제)
  ```
- [ ] **HoneyPawTile.cs** (`.../Wave2/HoneyPawTile.cs`, L40)
  → `var heal = new CombatContext(null, target, "꿀", ActionType.Heal, healAmount);`
- [ ] **SampleMonster.cs** (`.../SamplePreset/SampleMonster.cs`, L50-53)
  → `var context = new CombatContext(source, target, SkillName, ActionType.Attack, damage);`
- [ ] **SampleTile.cs** (`.../SamplePreset/SampleTile.cs`, L53-57)
  → `var context = new CombatContext(null, target, "지뢰 폭발 발동", ActionType.Attack, Value);` *(원본 문자열은 깨진 한글 — 현재 리터럴 그대로 보존하고 `new CombatAction(...)` 래핑만 제거)*

### Task 1.5: 리액터 — `context.Action.Type` → `context.Type`, null 가드 정리

각 파일에서 `context.Action.Type` → `context.Type`, `context?.Action == null`/`context.Action == null` → `context == null`. **분기 논리는 그대로.** (TileData/TileAttribute는 `&& context.IsTiling == true`까지 그대로 유지.)

- [ ] **UnitStats.cs** L27: `context.Action.Type == ActionType.OnStartTurn` → `context.Type == ActionType.OnStartTurn`
- [ ] **TileData.cs** L192: `context.Action.Type==ActionType.OnStartTurn && context.IsTiling == true` → `context.Type == ActionType.OnStartTurn && context.IsTiling == true`
- [ ] **TileAttribute.cs** L63: `context.Action.Type == ActionType.OnEndTurn && context.IsTiling == true` → `context.Type == ActionType.OnEndTurn && context.IsTiling == true`
- [ ] **StatusEffect.cs** L54: `context.Action.Type == ActionType.OnStartTurn` → `context.Type == ActionType.OnStartTurn`
- [ ] **StatusEffectManager.cs** L84: `context.Action.Type==ActionType.OnStartTurn` → `context.Type == ActionType.OnStartTurn`
- [ ] **BasicEffect.cs** L26: `context.Action.Type == ActionType.Attack` → `context.Type == ActionType.Attack`; L29 주석 `context.Action.Name` → `context.Name`
- [ ] **PositioningPassive.cs** L30: `context == null || context.Action == null` → `context == null`; L33/L41/L49: `context.Action.Type` → `context.Type`; L44: `context.Action.BaseValue` → `context.BaseValue`
- [ ] **FocusPassive.cs** L29: `... context.Action == null` → `context == null`; L34/L41: `context.Action.Type` → `context.Type`
- [ ] **BattleCryPassive.cs** L40: `... context.Action == null` → `context == null`; L42: `context.Action.Type != ActionType.Attack` → `context.Type != ActionType.Attack`
- [ ] **ReagentPrepPassive.cs** L72: `... context.Action == null` → `context == null`; L74: `context.Action.Type != ActionType.Attack` → `context.Type != ActionType.Attack`
- [ ] **PowerfullPunch.cs** L17: `context.Action.Type != ActionType.Attack` → `context.Type != ActionType.Attack`
- [ ] **SolraPriest.cs** L159: `context?.Action == null` → `context == null`; L163: `context.Action.Type == ActionType.OnEndTurn` → `context.Type == ActionType.OnEndTurn`
- [ ] **SolraKnight.cs** L110: `context?.Action == null` → `context == null`; L113/L127: `context.Action.Type == ActionType.Attack` → `context.Type == ActionType.Attack`
- [ ] **LunaPriest.cs** L169: `context?.Action == null` → `context == null`; L173: `...OnStartTurn` → `context.Type`; L229: `...Attack` → `context.Type`
- [ ] **LunaKnight.cs** L82: `context?.Action == null` → `context == null`; L86/L100: `context.Action.Type == ActionType.Attack` → `context.Type == ActionType.Attack`
- [ ] **SnowMan.cs** L176: `context?.Action == null` → `context == null`; L181/L191/L201: `context.Action.Type` → `context.Type`
- [ ] **SnowGolem.cs** L141: `context?.Action == null` → `context == null`; L144: `context.Action.Type` → `context.Type`
- [ ] **FrostTotem.cs** L27/L135: `context?.Action == null` → `context == null`; L31/L138: `context.Action.Type` → `context.Type`
- [ ] **MommyBear.cs** L102: `context?.Action == null` → `context == null`; L106: `context.Action.Type` → `context.Type`
- [ ] **BabyBear.cs** L102: `context?.Action == null` → `context == null`; L106: `context.Action.Type` → `context.Type`
- [ ] **SampleMonster.cs** L121: `context?.Action == null` → `context == null`; L126: `context.Action.Type` → `context.Type`

### Task 1.6: 컴파일 + 커밋

- [ ] **Step 1: 컴파일 검증** (위 명령, EXIT 0 기대)
- [ ] **Step 2: 커밋**
  ```bash
  git add -A
  git commit -m "refactor(combat): merge CombatAction into CombatContext (single type)"
  ```

---

# Step 2 — 서브클래스 도입

`CombatContext`를 추상 기반으로 바꾸고 5개 서브클래스를 도입한다. 생산자는 알맞은 서브클래스를 생성하도록 바꾼다. `Type`(shim)과 `IsTiling`(잔존)은 기반에 유지 → 미이전 리액터(`context.Type == X`)가 계속 컴파일·동작.

### Task 2.1: `CombatContext.cs` — 추상 기반 + 서브클래스

**Files:** Modify: `.../Combat/Pipeline/CombatContext.cs`

- [ ] **Step 1: 파일 전체를 아래로 교체**

```csharp
using System.Collections.Generic;
using DiceOrbit.Data; // EffectType

namespace DiceOrbit.Core.Pipeline
{
    public enum EventPhase { TurnStart, TurnEnd, TileTick }

    /// <summary>파이프라인을 통과하는 봉투(본체). NotifyReactors가 나르는 타입.</summary>
    public abstract class CombatContext
    {
        public Unit SourceUnit;
        public Unit Target;
        public bool IsCancelled;
        public bool IsSimulation;
        public bool IsTiling;       // 잔존 — Step 4에서 타일 수정과 함께 제거
        public ActionType Type;     // 마이그레이션 shim — Step 5에서 제거

        protected CombatContext(Unit source, Unit target, ActionType type)
        {
            SourceUnit = source;
            Target = target;
            Type = type;
        }
    }

    /// <summary>효과 행위 공통 (공격/힐) — 레시피 + 계산상태.</summary>
    public abstract class EffectContext : CombatContext
    {
        public string Name;
        public float BaseValue;
        public float OutputValue;
        public HashSet<string> Tags = new HashSet<string>();
        public List<ActionEffectInfo> Effects = new List<ActionEffectInfo>();

        protected EffectContext(Unit source, Unit target, ActionType type, string name, float baseValue)
            : base(source, target, type)
        {
            Name = name;
            BaseValue = baseValue;
            OutputValue = baseValue;
        }

        public void AddTag(string tag) => Tags.Add(tag);
        public bool HasTag(string tag) => Tags.Contains(tag);
        public void AddEffect(EffectType type, int value, int duration)
            => Effects.Add(new ActionEffectInfo(type, value, duration));
    }

    public sealed class AttackContext : EffectContext
    {
        public bool IsEffected;
        public AttackContext(Unit source, Unit target, string name, float baseValue)
            : base(source, target, ActionType.Attack, name, baseValue) { }
    }

    public sealed class HealContext : EffectContext
    {
        public HealContext(Unit source, Unit target, string name, float baseValue)
            : base(source, target, ActionType.Heal, name, baseValue) { }
    }

    public sealed class MoveContext : CombatContext
    {
        public int Steps;
        public MoveContext(Unit source, Unit target, int steps)
            : base(source, target, ActionType.Move) { Steps = steps; }
    }

    public sealed class TurnEventContext : CombatContext
    {
        public EventPhase Phase;
        public TurnEventContext(Unit source, Unit target, EventPhase phase)
            : base(source, target, PhaseToType(phase)) { Phase = phase; }

        private static ActionType PhaseToType(EventPhase phase) => phase switch
        {
            EventPhase.TurnStart => ActionType.OnStartTurn,
            EventPhase.TurnEnd   => ActionType.OnEndTurn,
            _                    => ActionType.None, // TileTick
        };
    }
}
```

> 핵심: 각 서브클래스 ctor가 shim `Type`을 올바른 enum으로 세팅 → Step 3 이전까지 `context.Type == X` 분기가 정확히 동작. `OutputValue`는 `EffectContext`에만, `IsEffected`는 `AttackContext`에만.

### Task 2.2: `CombatPipeline.cs` — ApplyAction 타입 switch + 가드

**Files:** Modify: `.../Combat/Pipeline/CombatPipeline.cs`

- [ ] **Step 1: `ApplyAction`을 타입 switch로 교체** (L171-202)
  ```csharp
  private void ApplyAction(CombatContext context)
  {
      switch (context)
      {
          case AttackContext atk:
              if (atk.Target.TakeDamage(Mathf.RoundToInt(atk.OutputValue)) != 0) atk.IsEffected = true;
              if (atk.IsEffected && !atk.HasTag("CustomVfx")) VfxManager.PlayDefaultAttackHit(atk.Target);
              break;
          case HealContext heal:
              heal.Target.Heal(Mathf.RoundToInt(heal.OutputValue));
              if (!heal.HasTag("CustomVfx")) VfxManager.PlayDefaultHeal(heal.Target);
              break;
          // MoveContext / TurnEventContext: 순수 방송 — Apply 없음 (의도적 no-op)
      }

      if (context is EffectContext efx && efx.Effects != null && efx.Effects.Count > 0)
      {
          foreach (var effect in efx.Effects)
              ApplyEffect(context.Target, effect);
      }
  }
  ```
- [ ] **Step 2: 회피/클램프 분기를 타입 패턴으로** (동작 동일)
  - L61 (Simulate): `if (context.Type == ActionType.Attack && context.OutputValue < 0)` → `if (context is AttackContext simAtk && simAtk.OutputValue < 0) simAtk.OutputValue = 0;`
  - L64 (Simulate 반환): `return Mathf.RoundToInt(context.OutputValue);` → `return context is EffectContext e ? Mathf.RoundToInt(e.OutputValue) : 0;`
  - L72 (dodge): `if (context.Type == ActionType.Attack && context.Target?.Stats?.DodgeChance > 0f)` → `if (context is AttackContext && context.Target?.Stats?.DodgeChance > 0f)`
  - L90 (Calc clamp): `if (context.Type == ActionType.Attack) { if (context.OutputValue < 0) context.OutputValue = 0; }` → `if (context is AttackContext clampAtk && clampAtk.OutputValue < 0) clampAtk.OutputValue = 0;`
- [ ] **Step 3: `SimulateCalculation` 시그니처를 `EffectContext`로 좁힘** (L54)
  - `public int SimulateCalculation(CombatContext context)` → `public int SimulateCalculation(EffectContext context)`
  - 본문 `context.OutputValue` 접근은 그대로 유효(EffectContext 멤버).

### Task 2.3: 생산자 → 서브클래스 생성

- [ ] **Unit.cs** L65: `new Pipeline.TurnEventContext(this, this, Pipeline.EventPhase.TurnStart)`; L79: `... EventPhase.TurnEnd`
- [ ] **Character.cs** L272-278:
  ```csharp
  if (Pipeline.CombatPipeline.Instance != null)
  {
      var moveContext = new Pipeline.MoveContext(this, this, stepsTraveled);
      Pipeline.CombatPipeline.Instance.Process(moveContext);
  }
  ```
  > `AddTag("Move")`는 드롭(MoveContext엔 Tags 없음). "Move" 태그를 읽는 리액터 없음 확인됨 — 이동 반응은 `context is MoveContext`로 분기(Step 3 PositioningPassive).
- [ ] **Monster.cs** L344-345: `var simCtx = new AttackContext(this, repTarget, nextSkill.skillData.SkillName, previewBase) { IsSimulation = true };`
- [ ] **CombatManager.cs**:
  - L545: `new TurnEventContext(null, null, EventPhase.TileTick);`
  - L619: `new Pipeline.AttackContext(null, target, "Direct Attack", damage);`
  - L640: `new Pipeline.AttackContext(null, monster, "Global Attack", damage);`
- [ ] **SkillData.cs** L98-102 → `new AttackContext(source, target, SkillName, damage)`; L127-131 → `new AttackContext(source, character, SkillName, damage)`
- [ ] **SkillTargetSelector.cs** L282-283 → `var simContext = new Pipeline.AttackContext(sourceCharacter, targetUnit, tmpl.SkillName, raw);`
- [ ] **CharacterActiveTemplate.cs** L110: `var context = new AttackContext(source, target, skillName, rawDamage);` (다음 줄 `if (vfxProfile != null) context.AddTag("CustomVfx");` 유지)
- [ ] **Goblin.cs** L118: `var context = new AttackContext(source, character, SkillName, damage);` (`if (vfxProfile != null) context.AddTag("CustomVfx");` 유지)
- [ ] **HoneyPawTile.cs** L40: `new HealContext(null, target, "꿀", healAmount);`
- [ ] **SampleMonster.cs** L50: `var context = new AttackContext(source, target, SkillName, damage);`
- [ ] **SampleTile.cs** L53-57: `var context = new AttackContext(null, target, "지뢰 폭발 발동", Value);` *(깨진 한글 리터럴은 원본 보존)*

### Task 2.4: 컴파일 + 커밋

- [ ] **Step 1: 컴파일 검증** (EXIT 0)
- [ ] **Step 2: 커밋**
  ```bash
  git add -A
  git commit -m "refactor(combat): introduce CombatContext subclasses (Attack/Heal/Move/TurnEvent)"
  ```

> 이 시점: 생산자는 서브클래스를 만들지만 리액터는 아직 `context.Type == X` shim으로 분기. 동작 동일.

---

# Step 3 — 리액터 이전 (`context.Type == X` → `context is XContext`)

**TileData/TileAttribute는 제외**(Step 4의 버그 수정 대상). 나머지 리액터를 타입 패턴으로 전환. 동작 보존.

각 파일 before→after (모든 분기는 trigger/owner 조건과 함께 유지, 타입 테스트만 교체; 본문의 `context.OutputValue`/`context.IsEffected`는 캡처한 typed 변수로):

- [ ] **UnitStats.cs** L27: `context.Type == ActionType.OnStartTurn` → `context is TurnEventContext { Phase: EventPhase.TurnStart }`
- [ ] **StatusEffect.cs** L54: `context.Type == ActionType.OnStartTurn` → `context is TurnEventContext { Phase: EventPhase.TurnStart }`
- [ ] **StatusEffectManager.cs** L84: `context.Type == ActionType.OnStartTurn` → `context is TurnEventContext { Phase: EventPhase.TurnStart }`
- [ ] **BasicEffect.cs** L26: `context.SourceUnit == Owner && context.Type == ActionType.Attack` → `context is AttackContext atk && context.SourceUnit == Owner`; L28 본문 `context.OutputValue += Value;` → `atk.OutputValue += Value;`
- [ ] **PositioningPassive.cs**
  - L33: `context.Type == ActionType.OnStartTurn` → `context is TurnEventContext { Phase: EventPhase.TurnStart }`
  - L41: `context.Type == ActionType.Move` → `context is MoveContext mv`; L44 본문 `Mathf.RoundToInt(context.BaseValue)` → `mv.Steps`
  - L49: `context.Type == ActionType.Attack` → `context is AttackContext atk`; L55 본문 `context.OutputValue *= multiplier;` → `atk.OutputValue *= multiplier;`
- [ ] **FocusPassive.cs** L34: `...OnStartTurn` → `context is TurnEventContext { Phase: EventPhase.TurnStart }`; L41: `...OnEndTurn` → `context is TurnEventContext { Phase: EventPhase.TurnEnd }`
- [ ] **BattleCryPassive.cs** L42: `if (context.Type != ActionType.Attack) return;` → `if (context is not AttackContext atk) return;`; L49 본문 `context.OutputValue *= multiplier;` → `atk.OutputValue *= multiplier;`
- [ ] **ReagentPrepPassive.cs** L74: `if (context.Type != ActionType.Attack) return;` → `if (context is not AttackContext atk) return;`; L79: `context.OutputValue *= multiplier;` → `atk.OutputValue *= multiplier;`
- [ ] **PowerfullPunch.cs** L17: `if (context.Type != ActionType.Attack) return;` → `if (context is not AttackContext atk) return;`; L21: `context.OutputValue = 1000;` → `atk.OutputValue = 1000;`
- [ ] **SolraPriest.cs** L163: `context.Type == ActionType.OnEndTurn` → `context is TurnEventContext { Phase: EventPhase.TurnEnd }`
- [ ] **SolraKnight.cs** (메서드 내 두 Attack 분기 — 패턴 변수 이름 분리)
  - L113: `context.Type == ActionType.Attack` → `context is AttackContext atk`; L121: `context.OutputValue` (×2) → `atk.OutputValue`
  - L127: `context.Type == ActionType.Attack` → `context is AttackContext atk2`; L135: `context.OutputValue` (×2) → `atk2.OutputValue`
- [ ] **LunaKnight.cs** (동일 — atk / atk2)
  - L86 → `context is AttackContext atk`; L93 `context.OutputValue` → `atk.OutputValue`
  - L100 → `context is AttackContext atk2`; L107 `context.OutputValue` → `atk2.OutputValue`
- [ ] **LunaPriest.cs** L173: `...OnStartTurn` → `context is TurnEventContext { Phase: EventPhase.TurnStart }`; L229 (LunaPriestBuff): `...Attack` → `context is AttackContext atk`; L232 `context.OutputValue` (×2) → `atk.OutputValue`
- [ ] **SnowMan.cs** (atk / atk2)
  - L180-184: `context.Type == ActionType.Attack` → `context is AttackContext atk` (본문 L186 `context.OutputValue` → `atk.OutputValue`; 조건의 `context.Target/IsSimulation/IsEffected`는 `atk.`로 읽어도 무방)
  - L190-194: → `context is AttackContext atk2` (`atk2.SourceUnit/IsSimulation/IsEffected`)
  - L200-202: `context.Type == ActionType.OnEndTurn` → `context is TurnEventContext { Phase: EventPhase.TurnEnd }`
- [ ] **SnowGolem.cs** L144: `...OnStartTurn` → `context is TurnEventContext { Phase: EventPhase.TurnStart }`
- [ ] **FrostTotem.cs** L31: `...Attack` → `context is AttackContext atk` (L34 `context.OutputValue` → `atk.OutputValue`); L138: `...OnEndTurn` → `context is TurnEventContext { Phase: EventPhase.TurnEnd }`
- [ ] **MommyBear.cs** L106: `...Attack` → `context is AttackContext atk` (L110 `context.OutputValue` → `atk.OutputValue`)
- [ ] **BabyBear.cs** L106: `...Attack` → `context is AttackContext atk` (L109 `context.OutputValue` → `atk.OutputValue`)
- [ ] **SampleMonster.cs** L126: `...OnStartTurn` → `context is TurnEventContext { Phase: EventPhase.TurnStart }` (본문은 owner.Heal만 읽음 — 캡처 불필요)

- [ ] **컴파일 + 커밋**
  ```bash
  git add -A
  git commit -m "refactor(combat): migrate reactors to context-type pattern matching"
  ```

> 한 파일씩 커밋해도 됨(각 파일 독립적). TileData/TileAttribute는 아직 `context.Type == OnStartTurn/OnEndTurn && context.IsTiling`로 남아 있음(IsTiling이 false라 여전히 미발화 — 현 동작 보존).

---

# Step 4 — 버그 수정 + 죽은 코드 제거 (별도 PR + 플레이테스트)

타일 지속시간이 닳기 시작하는 **실제 밸런스 변화**이므로 독립 PR로 분리.

### Task 4.1: 타일 지속시간 재게이팅

- [ ] **TileData.cs** L192: `if (context.Type == ActionType.OnStartTurn && context.IsTiling == true)` → `if (context is TurnEventContext { Phase: EventPhase.TileTick })`
- [ ] **TileAttribute.cs** L63: `if (context.Type == ActionType.OnEndTurn && context.IsTiling == true)` → `if (context is TurnEventContext { Phase: EventPhase.TileTick })`

> 둘 다 동일한 **TileTick** 방송에 반응하게 되어, 타일은 매 Process가 아니라 타일틱 1회당 한 번만 감소/정리된다(per-unit 과다발동 방지). 원래 OnStartTurn/OnEndTurn로 어긋나 둘 다 못 발동하던 미스매치도 해소.

### Task 4.2: 죽은 필드/값 제거

- [ ] **CombatContext.cs**: 기반에서 `public bool IsTiling;` 줄 삭제 (이제 읽는 곳 없음)
- [ ] **ICombatReactor.cs** L17: `OnActionSuccess,` 제거
- [ ] **PositioningPassive.cs** L40: `(trigger == CombatTrigger.OnPostAction || trigger == CombatTrigger.OnActionSuccess)` → `(trigger == CombatTrigger.OnPostAction)`

### Task 4.3: 컴파일 + 플레이테스트 + 커밋

- [ ] **컴파일 검증** (EXIT 0)
- [ ] **플레이테스트 체크리스트:**
  - 타일 속성(예: RandMine/Bone/빙결 계열)에 duration이 있는 것을 설치 → 매 턴 지속시간이 **정확히 1씩** 감소하고 0에서 사라지는지 확인 (유닛 수만큼 과다 감소하지 않는지)
  - 영구 타일(duration -1: 지뢰/뼈)은 만료되지 않는지 확인
  - 상태이상 지속시간(StatusEffect)·방어도 리셋(UnitStats)·턴시작 패시브(고블린 지뢰설치 등)가 종전대로 1회씩 동작하는지
- [ ] **커밋**
  ```bash
  git add -A
  git commit -m "fix(combat): tile durations now tick via TileTick event; remove dead IsTiling/OnActionSuccess"
  ```

---

# Step 5 — 최종 정리 (Type shim 제거)

모든 리액터가 `context is XContext`로 이전된 뒤(Step 3 + Step 4 완료) shim 제거.

- [ ] **확인:** `git grep "\.Type == ActionType"` 와 `git grep "context\.Type"`로 파이프라인 분기에 남은 `Type` 사용이 없는지 검사. (DiceData/DiceManager/SkillManager의 `ActionType` 주사위 메타데이터는 **별개** — 남겨둠.)
- [ ] **CombatContext.cs**: 기반에서 `public ActionType Type;` 및 ctor의 `Type = type;`, ctor의 `ActionType type` 파라미터 제거. 각 서브클래스 ctor의 `base(...)` 호출에서 `ActionType.X` 인자 제거. `TurnEventContext.PhaseToType` 제거.
- [ ] **컴파일 검증** (EXIT 0)
- [ ] **커밋**
  ```bash
  git add -A
  git commit -m "refactor(combat): drop ActionType shim from CombatContext"
  ```

---

## 범위 밖 (이 계획에 포함하지 않음)
- 트리거 "T-야심"(사건 전용 단일-broadcast).
- `CombatManager.AttackMonster/AttackAllMonsters`의 미사용 `ignoreDefense` 파라미터 제거 — 선택적 별도 cleanup.
- 리액터 double-dispatch(`Unit.CollectReactors` + `PassiveManager` 중복, `Distinct()`로 무해) — 별개 이슈.
- `OnHit` → `OnApplied` 개명 — 선택.

---

## Self-Review

**Spec coverage** (설계 문서 §2 결정 항목 대조):
- 병합 + 추상기반/서브클래스 → Step 1, 2 ✓
- 리액터 `is XContext` 분기 → Step 3 ✓
- 트리거 `OnActionSuccess` 제거(나머지 유지) → Step 4 ✓
- `IsTiling` 삭제 + 타일버그 수정(TileTick) → Step 4 ✓
- `AddEffectToTarget`/`IgnoreDefense`/`IsCritical` 제거 → Step 1 ✓
- 보존 제약(NotifyReactors/OnReact 시그니처/시뮬/OutputValue 순서/IsEffected/타일 직접경로) → 구조 미변경으로 보존 ✓

**Placeholder scan:** "TBD"/"적절히"/막연한 지시 없음. 모든 편집에 정확한 before/after. (깨진 한글 리터럴 2곳은 "원본 보존"으로 명시 — 의도된 미변경.)

**Type consistency:** 서브클래스 ctor 시그니처 `AttackContext(src,tgt,name,base)` / `HealContext(src,tgt,name,base)` / `MoveContext(src,tgt,steps)` / `TurnEventContext(src,tgt,phase)` — Step 2.3 생산자 호출과 일치. `EffectContext.OutputValue`/`AttackContext.IsEffected` 위치가 Step 3 본문 캡처(`atk.OutputValue`, `atk.IsEffected`)와 일치. SolraKnight/LunaKnight/SnowMan의 동일 메서드 내 2개 패턴은 `atk`/`atk2`로 이름 분리.
