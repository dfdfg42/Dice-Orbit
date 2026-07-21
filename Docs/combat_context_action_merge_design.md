# CombatContext / CombatAction 병합 + 서브클래스 리팩토링 설계

> **[구현 완료 · 2026-07-03 갱신 노트]** 이 설계는 **구현되었다**: `CombatContext`는 추상 기반 + 서브클래스(`EffectContext`→`AttackContext`/`HealContext`, `MoveContext`, `TurnEventContext`)가 되었고 `CombatAction` 클래스는 병합·삭제되어 `CombatAction.cs`에는 `ActionType` enum + `ActionEffectInfo` 구조체만 남았다. 실제 코드와 **두 가지 차이**만 기록한다: (1) §6의 `context is XContext` 분기는 이후 **타입별 DIM 훅**(`ICombatReactor`의 `OnAttack`/`OnHeal`/`OnMove`/`OnTurnEvent`)으로 대체됨 — [Docs/combat_reactor_dispatch.md](combat_reactor_dispatch.md) 참조. (2) §9 Step 5의 `ActionType Type` 마이그레이션 심은 **2026-07-21에 제거 완료** — `ActionType` enum 자체(주사위 배정 장부 포함)도 함께 삭제되어 `CombatAction.cs`에는 `ActionEffectInfo`만 남았다. 아래 본문은 원본 설계 기록으로 보존한다.

> 상태: 설계안(검토 대기) · 작성일 2026-06-20 · 대상 브랜치 `feature/character-select-ui-20260526`
> 범위: 전투 파이프라인의 데이터 모델(`CombatContext` / `CombatAction`) 구조 개편. 트리거(`CombatTrigger`)는 최소 변경.

---

## 1. 배경과 문제

전투의 모든 행위(공격/힐/이동/턴시작·종료/타일틱)는 `CombatAction`(레시피)과 `CombatContext`(실행 상태)를 만들어 `CombatPipeline.Process()`로 흘려보낸다. 코드 전수조사로 확인한 문제들:

### 1-1. Action / Context 분리가 실익이 없다
- `new CombatAction(...)` **약 15곳 전부**가 만들어지자마자 `CombatContext`로 1:1 래핑된다. 레시피를 한 번 정의해 여러 번 재사용하는 곳이 **하나도 없다**.
- `CombatAction`을 Context 없이 단독으로 쓰는 곳이 **없다**.
- "불변 `BaseValue` vs 가변 `OutputValue`"를 *따로* 활용하는 리액터가 **없다**. 유일하게 `Action.BaseValue`를 직접 읽는 `PositioningPassive`(이동거리 누적)조차 Move라 `OutputValue`가 변하지 않아 두 값이 동일하다.

→ 분리의 명분(불변 레시피 vs 가변 상태)은 **이 코드베이스에서 활용되지 않는 의식(ceremony)**일 뿐이며, `context.Action.Type` 같은 2-홉 인디렉션만 남긴다.

### 1-2. 한 `CombatContext`가 모든 ActionType을 공유한다
`CombatContext`는 9종 ActionType 모두에 같은 필드 집합을 강요한다. 그 결과:
- **사건(턴시작/종료/이동/타일틱)이 `BaseValue=0`짜리 껍데기 액션으로** 파이프라인을 타며, `OutputValue` 같은 무의미한 필드를 짊어진다. 이들에게 파이프라인은 사실상 `NotifyReactors`(방송) 한 기능만 쓰인다(`ApplyAction`·회피·VFX는 전부 no-op).
- **`OutputValue`의 의미가 오버로드**된다: Attack=데미지 / Heal=힐량 / **Move=걸음 수**(`Character.cs`) / 사건=쓰레기. 한 `float`이 4가지 뜻을 겸한다.
- 타입만 봐선 어떤 필드가 유효한지 알 수 없어, **약 15개 리액터가 `Action.Type == X`로 방어 분기**를 하며 타입 구분 로직이 코드 전반에 흩어진다.

### 1-3. 죽은 코드 / 버그
- `IsTiling` — 어디서도 `true`로 설정되지 않는다 → `TileData`/`TileAttribute`의 지속시간 감소·만료 정리 분기가 영원히 도달 불가 = **타일 지속시간이 닳지 않는 버그**.
- `CombatContext.AddEffectToTarget()` — 호출처 0, `Debug.Log` 스텁.
- `CombatAction.IgnoreDefense` / `IsCritical` — 설정만 되고 읽는 곳 없음.
- `CombatTrigger.OnActionSuccess` — enum에 선언만, 파이프라인이 **한 번도 발화하지 않음**(`PositioningPassive`가 체크만 하므로 그 분기는 죽은 코드).

---

## 2. 결정

1. **`CombatAction`을 `CombatContext`로 병합**하고, `CombatContext`를 **추상 기반(envelope) + 행위별 서브클래스**로 만든다.
2. 리액터는 `context.Action.Type == X` 대신 **`context is XContext`**로 분기한다. 타입이 필드 유효성을 강제한다 — `TurnEventContext`에는 `OutputValue` 필드가 **아예 없어** 접근 시 컴파일 에러.
3. **트리거(`CombatTrigger`)는 최소 변경**: 죽은 `OnActionSuccess` 제거(+ `OnHit` 개명은 선택). 서브클래스화가 `OnPreAction`/`OnPostAction`의 "사건 훅 vs 데미지 단계" 오버로드를 자동으로 해소하므로, 트리거 enum 구조 자체는 유지한다.
   - 사건 전용 단일-broadcast 도입("T-야심")은 순서 보장을 Priority로 옮기는 ~8개 리액터 감사 비용 대비 이득(턴제 cold-path perf)이 작아 **이번 범위에서 제외/보류**.

근거: 1-1에서 두-객체 분리가 실익이 없음을 확인했으므로, 다시 두 객체(봉투+화물)로 가는 payload 방식보다 **한 객체를 서브클래싱**하는 쪽이 일관적이고 인디렉션이 적다.

---

## 3. 목표 타입 구조

```csharp
namespace DiceOrbit.Core.Pipeline
{
    public enum EventPhase { TurnStart, TurnEnd, TileTick }

    // 봉투/본체 — 항상 존재. NotifyReactors가 나르는 타입.
    public abstract class CombatContext
    {
        public Unit SourceUnit;
        public Unit Target;
        public bool IsCancelled;
        public bool IsSimulation;
        // (마이그레이션 심) ActionType Type;  — 모든 리액터 이전 후 제거
    }

    // 효과 행위 공통 (공격/힐) — 레시피 + 계산상태
    public abstract class EffectContext : CombatContext
    {
        public string Name;
        public float  BaseValue;                       // 불변 시작값(보존)
        public float  OutputValue;                     // 가변 누적 결과
        public HashSet<string>      Tags    = new();
        public List<ActionEffectInfo> Effects = new();
        public void AddTag(string t);
        public bool HasTag(string t);
        public void AddEffect(EffectType type, int value, int duration);
    }

    public sealed class AttackContext : EffectContext { public bool IsEffected; }
    public sealed class HealContext   : EffectContext { }

    // 이동 사건 — 걸음 수만
    public sealed class MoveContext : CombatContext { public int Steps; }

    // 턴시작/종료/타일틱 — 숫자 없음
    public sealed class TurnEventContext : CombatContext { public EventPhase Phase; }
}
```

- `CombatAction` 클래스는 **삭제**. `ActionEffectInfo` 구조체는 유지(라이더 효과 명세).
- `ActionType` enum은 **주사위 배정 메타데이터**(`DiceData`/`DiceManager`/`SkillManager`)에서 별개로 계속 쓰이므로 enum 자체는 남긴다. 단 파이프라인 분기 용도로는 더 이상 쓰지 않는다.

---

## 4. 생산자 변경 (약 15곳)

| 기존 | 신규 | 위치(예) |
|---|---|---|
| `new CombatAction(name, Attack, dmg)` → `new CombatContext(s,t,action)` | `new AttackContext { SourceUnit=s, Target=t, Name=name, BaseValue=dmg }` | Goblin, SampleMonster, SkillData(×2), CombatManager(Direct/Global), CharacterActiveTemplate, SampleTile |
| 〃 (시뮬레이션) | `new AttackContext { ... }` + `SimulateCalculation` | Monster:345, SkillTargetSelector:282 |
| `new CombatAction("꿀", Heal, amt)` | `new HealContext { ..., BaseValue=amt }` | HoneyPawTile:40 |
| `new CombatAction("Turn Start", OnStartTurn, 0)` | `new TurnEventContext { SourceUnit=u, Target=u, Phase=TurnStart }` | Unit:65 |
| `new CombatAction("Turn End", OnEndTurn, 0)` | `new TurnEventContext { ..., Phase=TurnEnd }` | Unit:79 |
| `new CombatContext(null,null, CombatAction(None,0))` 타일틱 | `new TurnEventContext { SourceUnit=null, Target=null, Phase=TileTick }` | CombatManager:545 |
| `new CombatAction("Move", Move, stepsTraveled)` | `new MoveContext { SourceUnit=this, Target=this, Steps=stepsTraveled }` | Character:274 |

> 편의 생성자(`AttackContext(Unit src, Unit tgt, string name, float baseValue)` 등)를 두면 호출부가 더 짧아진다. 위 표는 필드 의미를 드러내려 객체 초기화 문법으로 표기.

---

## 5. 파이프라인(`CombatPipeline`) 변경

`Process`의 시그니처, 4단계 순서, `NotifyReactors`(수집·Priority 정렬·Distinct·취소 중단)는 **변경 없음**. `OnReact(CombatTrigger, CombatContext)` 시그니처도 그대로(기반 타입을 받음). 바뀌는 곳은 타입 분기뿐이다.

### ApplyAction — enum if → 타입 switch
```csharp
private void ApplyAction(CombatContext ctx)
{
    switch (ctx)
    {
        case AttackContext a:
            if (a.Target.TakeDamage(Mathf.RoundToInt(a.OutputValue)) != 0) a.IsEffected = true;
            if (a.IsEffected && !a.HasTag("CustomVfx")) VfxManager.PlayDefaultAttackHit(a.Target);
            ApplyEffects(a);     // a.Effects 적용
            break;
        case HealContext h:
            h.Target.Heal(Mathf.RoundToInt(h.OutputValue));
            if (!h.HasTag("CustomVfx")) VfxManager.PlayDefaultHeal(h.Target);
            ApplyEffects(h);
            break;
        case MoveContext:
        case TurnEventContext:
            break;   // 순수 방송 — Apply 없음(의도적 no-op)
    }
}
```
- 회피 RNG(`HandlePreAction`)와 음수 보정(`HandleCalculate`)의 `Action.Type == Attack` 게이트 → `ctx is AttackContext`.
- `ApplyEffects`는 `EffectContext.Effects` 루프(기존 `ApplyEffect` 로직 그대로).

### SimulateCalculation
```csharp
public int SimulateCalculation(EffectContext ctx)   // 대상은 효과 컨텍스트로 좁힘
{
    ctx.IsSimulation = true;
    NotifyReactors(ctx, CombatTrigger.OnCalculateOutput);
    if (ctx is AttackContext && ctx.OutputValue < 0) ctx.OutputValue = 0;
    return Mathf.RoundToInt(ctx.OutputValue);
}
```
호출처 2곳(Monster, SkillTargetSelector)은 모두 `AttackContext`를 만들므로 무리 없이 적용. `IsSimulation` 플래그와 정수 반환은 그대로 보존.

---

## 6. 리액터 변경 패턴 (약 15개 파일)

```
trigger==OnPreAction  && Action.Type==OnStartTurn     →  trigger==OnPreAction  && ctx is TurnEventContext { Phase: TurnStart }
trigger==OnPostAction && Action.Type==OnEndTurn       →  trigger==OnPostAction && ctx is TurnEventContext { Phase: TurnEnd }
trigger==OnCalculateOutput && Action.Type==Attack ...  →  trigger==OnCalculateOutput && ctx is AttackContext a … a.OutputValue
Action.Type==Move … Action.BaseValue (걸음수)          →  ctx is MoveContext mv … mv.Steps
```

> 이 전환으로 `OnPreAction`/`OnPostAction`의 "사건 vs 데미지" 오버로드가 자연 해소된다(WHAT축이 타입으로 빠지므로). 따라서 트리거 enum은 손대지 않는다.

---

## 7. 죽은 코드 / 버그 처리

| 항목 | 처리 |
|---|---|
| `IsTiling` | **삭제.** 타일 지속시간 로직을 `TileData`/`TileAttribute`에서 `ctx is TurnEventContext { Phase: TileTick }`로 재게이팅해 **버그 수정**. 타일은 매 `Process`마다 수집되므로, per-unit OnStartTurn/OnEndTurn이 아니라 **타일 전용 TileTick** 판별자를 써야 유닛 수만큼 과다 발동하지 않는다. → 밸런스 변화(타일이 만료되기 시작)이므로 **별도 PR + 플레이테스트**. |
| `AddEffectToTarget` | 삭제(스텁). 상태이상 적용은 `ApplyEffects` → `StatusEffectManager` 경로 유지. |
| `IgnoreDefense` / `IsCritical` | 삭제(미사용). |
| `CombatTrigger.OnActionSuccess` | 삭제(미발화) + `PositioningPassive`의 죽은 `|| OnActionSuccess` 분기 정리. |

---

## 8. 보존해야 할 제약 (load-bearing)

1. `NotifyReactors`의 수집(Source+Target+파티+유물+타일)·Priority 내림차순 정렬·`Distinct()`·`IsCancelled` 시 중단 로직은 **그대로**.
2. `OnReact(CombatTrigger, CombatContext)` 시그니처 유지 — 기반 타입을 받으므로 모든 리액터가 계속 도달 가능.
3. 사건(턴/틱)의 트리거 발화 **순서**(OnPreAction→OnCalculateOutput→OnHit→OnPostAction) 유지 — 지속시간 감소(StatusEffect), 방어도 리셋(UnitStats) 등이 의존.
4. 시뮬레이션 경로: `IsSimulation` 플래그 + 정수 반환 보존.
5. `OutputValue` 누적은 같은 `float`을 같은 Priority 순서로 가공 — 데미지 밸런스/미리보기 불변.
6. `IsEffected`는 Attack(0 아닌 피해)에서만 true — VFX/패시브 게이팅 의미 유지(`AttackContext` 전용).
7. 타일 도착/통과 직접호출 경로(`TileData.OnArrive/OnTraverse`)는 파이프라인과 무관 — 건드리지 않음.

---

## 9. 마이그레이션 (빌드 안전 단계)

각 단계가 컴파일되는 상태로 끝난다. 컴파일 검증은 헤드리스(VS2022 MSBuild + mono 4.7.1-api)로.

- **Step 0 — 베이스라인.** 현재 그린 컴파일 확인.
- **Step 1 — 병합만(서브클래스 전, 독립 출시 가능 체크포인트).** `CombatAction`의 필드를 *아직 concrete인* `CombatContext`로 흡수(`Name`/`Type`/`BaseValue`/`Tags`/`Effects` + 메서드). 생산자 ~15곳과 리액터의 `context.Action.X` → `context.X`로 일괄 치환. `CombatAction` 삭제. **동작 동일.** → 여기서 멈춰도 "의식 제거 + 2-홉 제거"라는 완결된 개선.
- **Step 2 — 서브클래스 도입.** `CombatContext`를 추상으로 전환, `EffectContext`/`AttackContext`/`HealContext`/`MoveContext`/`TurnEventContext` 추가. 타입별 필드를 하위로 이동. 생산자 ~15곳이 알맞은 서브클래스를 생성하도록 수정. `Type`은 기반에 **심으로 잠시 유지**(미이전 리액터의 `context.Type == X` 호환). `ApplyAction`을 타입 switch로.
- **Step 3 — 리액터 이전.** `context.Type == X` → `context is XContext`로 한 파일씩 전환(심 덕분에 이전/미이전 공존).
- **Step 4 — 버그픽스 + 죽은 코드 제거(별도 PR + 플레이테스트).** `IsTiling`/`AddEffectToTarget`/`IgnoreDefense`/`IsCritical`/`OnActionSuccess` 삭제, 타일 지속시간 재게이팅.
- **Step 5 — 최종 정리.** 모든 리액터 이전 후 기반의 `Type` 심 제거.

---

## 10. 범위 밖 / 미결 결정

**범위 밖**
- 트리거 "T-야심"(사건 전용 단일-broadcast) — 보류.
- 리액터 double-dispatch(`Unit.CollectReactors` + `PassiveManager`가 같은 패시브를 중복 수집, `Distinct()`로 무해화) — 별개 이슈.

**미결(검토 시 결정)**
1. **Step 1에서 멈출지(병합만) vs 끝까지(서브클래스)** — 토론에선 서브클래스로 합의. Step 1이 자연스러운 중단점.
2. **타일 만료 밸런스** — 지속시간이 닳기 시작하는 건 실제 게임 변화. 고치고 재튜닝 vs 타일별 의도수명 선감사.
3. **`OnHit` 개명**(`OnApplied` 등) 여부 — 순수 명명, 선택.
4. **끝에서 `ActionType.Type` 완전 제거** 여부 — 주사위 메타데이터용 enum은 별개로 잔존.
