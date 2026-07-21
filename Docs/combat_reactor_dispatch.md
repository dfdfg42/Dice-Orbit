# Combat 리액터 타입별 디스패치 (DIM Template Method)

> 상태: 적용 완료 · 작성일 2026-06-21 · 브랜치 `refactor/combat-context-merge-20260621`
> **⚠️ 2026-07-21 정정**: 이 개편은 **죽은 훅 21개**를 만든 채 "검증 완료"로 기록돼 있었다.
> 베이스(CharacterPassiveSkill/PassiveAbility/StatusEffect)가 훅을 선언하지 않은 상태에서
> 파생 클래스가 `public void OnAttack(...)`을 선언하면 **인터페이스 매핑에 포함되지 않아
> 절대 호출되지 않는다** (C# 인터페이스 매핑은 인터페이스를 나열한 클래스에서 확정 —
> 리플렉션 InterfaceMapping 조사로 확인). 수정: 세 베이스에 훅 4종을 `public virtual`로
> 선언하고 파생 21곳을 전부 `override`로 교정. **§4.1 필수 규칙** 참조.
> 선행: [combat_context_action_merge_design.md](combat_context_action_merge_design.md) (CombatContext 서브클래스화). 이 문서는 그 위에 올린 **리액터 디스패치** 개편을 다룬다.

---

## 1. 배경 / 문제

CombatContext가 `AttackContext`/`HealContext`/`MoveContext`/`TurnEventContext`로 서브클래스화된 뒤, 모든 `ICombatReactor`는 단일 `OnReact(trigger, context)` 안에서 **컨텍스트 타입을 직접 캐스팅**해 분기했다:

```csharp
public override void OnReact(CombatTrigger trigger, CombatContext context)
{
    if (context is not AttackContext atk) return;   // ← 리액터마다 반복되는 캐스팅
    ...
    atk.OutputValue *= multiplier;
}
```

리액터 ~25개가 전부 이 `if (context is XContext)` 보일러플레이트를 반복했고, "이 리액터가 무엇에 반응하는지"가 메서드 본문을 읽어야만 드러났다.

## 2. 결정 — Template Method를 **default interface method(DIM)**로

타입 디스패치를 **인터페이스 한 곳**에서 하고, 리액터는 **자기가 반응하는 타입의 훅만** 구현한다.

- **추상 클래스가 아니라 인터페이스**여야 한다. 리액터들이 이미 제각기 다른 베이스를 상속 중이라(`TileData : MonoBehaviour`, `StatusEffect`, `CharacterPassiveSkill`, `PassiveAbility`, `CharacterModifier`, `RuntimeArtifact` …) C# 단일 상속으로는 공통 베이스 클래스로 못 묶는다.
- C# 8의 **default interface method**가 인터페이스에 구현 본문을 넣을 수 있게 해주므로, 디스패치(템플릿)와 훅을 모두 인터페이스에 둔다.

## 3. 구조

```csharp
// ICombatReactor.cs
public interface ICombatReactor
{
    int Priority { get; }

    // 템플릿: 컨텍스트 구체 타입으로 디스패치 (기본 구현)
    void OnReact(CombatTrigger trigger, CombatContext context)
    {
        switch (context)
        {
            case AttackContext a:    OnAttack(trigger, a);    break;
            case HealContext h:      OnHeal(trigger, h);      break;
            case MoveContext m:      OnMove(trigger, m);      break;
            case TurnEventContext e: OnTurnEvent(trigger, e); break;
        }
    }

    // 타입별 훅 (기본 빈 구현). 자식은 필요한 것만 구현.
    void OnAttack(CombatTrigger trigger, AttackContext context) { }
    void OnHeal(CombatTrigger trigger, HealContext context) { }
    void OnMove(CombatTrigger trigger, MoveContext context) { }
    void OnTurnEvent(CombatTrigger trigger, TurnEventContext context) { }
}
```

**두 축**: `CombatTrigger`(언제 — OnPreAction/OnCalculateOutput/OnHit/OnPostAction)는 훅의 인자로 그대로 전달된다. 훅은 "무엇이(WHAT)"만 디스패치하고, 트리거 분기는 훅 안에서 `if (trigger == ...)`로 처리한다.

## 4. 리액터 작성법 (실무 가이드)

**단순 리액터** — 반응하는 타입의 훅만 구현:
```csharp
public class IncreaseAttackDamage : ICombatReactor   // 또는 CharacterPassiveSkill 등 상속
{
    public int Priority => 10;
    public void OnAttack(CombatTrigger trigger, AttackContext context)
    {
        if (trigger != CombatTrigger.OnCalculateOutput) return;
        context.OutputValue += 1;     // 캐스팅 불필요 — 이미 AttackContext
    }
}
```

**여러 타입에 반응** — 훅을 여러 개 구현 (예: Rogue PositioningPassive):
```csharp
public void OnTurnEvent(CombatTrigger t, TurnEventContext c) { /* 턴 시작 시 이동거리 리셋 */ }
public void OnMove(CombatTrigger t, MoveContext c)           { /* 이동 누적 */ }
public void OnAttack(CombatTrigger t, AttackContext c)       { /* 누적만큼 피해 증가 */ }
```

**특수/전파 리액터** — 모든 컨텍스트를 직접 처리하거나 자식에게 전파해야 하면 `OnReact` 자체를 override (예: `PassiveManager`, `StatusEffectManager`, `TileData`).

### 4.1 필수 규칙 — 훅 선언 위치 (2026-07-21 죽은 훅 사고 이후)

C# 인터페이스 매핑은 **인터페이스를 base list에 나열한 클래스에서 확정**된다. 파생 클래스의
동명 메서드는 인터페이스를 재나열하지 않는 한 매핑에 **들어가지 않는다** (컴파일 에러도 없이
조용히 죽는다).

| 리액터 위치 | 규칙 |
|---|---|
| 인터페이스를 **직접 나열**하는 클래스 (`class X : ICombatReactor`) | `public void OnAttack(...)` 그대로 OK |
| **베이스를 상속**하는 클래스 (`class X : CharacterPassiveSkill` 등) | 반드시 `public override void OnAttack(...)` — 베이스의 virtual 훅을 override |

이를 위해 `CharacterPassiveSkill` / `PassiveAbility` / `StatusEffect` / `RuntimeArtifact` 베이스는
훅 4종(`OnAttack/OnHeal/OnMove/OnTurnEvent`)을 `public virtual` 빈 구현으로 선언해 두었다.
**새 리액터 훅에 `override`가 안 붙으면 컴파일러가 CS0114(숨김) 경고를 낸다 — 경고를 무시하지 말 것.**

> ⚠️ **DIM은 인터페이스 참조로만 호출 가능하다.** 리액터의 `OnReact`/훅을 *클래스 타입* 변수로 호출하면 컴파일되지 않는다 → `((ICombatReactor)x).OnReact(...)`로 캐스팅하거나, 컬렉션을 `ICombatReactor`/`IPassive`(인터페이스)로 다뤄야 한다.

## 5. 마이그레이션 결과 (패밀리별)

| 베이스 | 처리 | 비고 |
|---|---|---|
| `ICombatReactor` | default `OnReact`+훅 추가 | 순수 추가 (`3af4596`) |
| `CharacterPassiveSkill : IPassive` | `abstract OnReact` 제거 → DIM | BattleCry/ReagentPrep/Focus/Positioning 훅화 (`b5376b4`) |
| `PassiveAbility : IPassive` | `abstract OnReact` 제거 → DIM | 몬스터 패시브 12종 훅화 (`468434f`) |
| `StatusEffect` | `OnReact`(지속시간) → `OnTurnEvent`; 효과별 → `OnAttack` | `StatusEffectManager`가 `((ICombatReactor)effect)` 캐스팅 (`bc3508f`) |
| `RuntimeArtifact` | `abstract OnReact` 제거 → DIM | PowerfullPunch → `OnAttack` (`bc3508f`) |
| `UnitStats` | `OnReact` → `OnTurnEvent` | 방어도 리셋 (`bc3508f`) |
| `CharacterModifier` | `OnReact` → `OnAttack` (`OnAttackWithActive` 유지) | 모디파이어는 `ModifierManager.CollectReactors`로 인터페이스 수집 (`bc3508f`) |

**포워더(자식 리액터에게 전파):**
- `PassiveManager` → `passive.OnReact`인데 `passive`가 `IPassive`(인터페이스)라 **무캐스팅** OK.
- `StatusEffectManager` → `effect`가 `StatusEffect`(클래스)라 `((ICombatReactor)effect)` 캐스팅 필요.
- `TileData` → `attribute`가 `TileAttribute`(클래스). `TileAttribute`는 전투 훅이 없어(지속시간은 `TickTurnEnd` 직접 틱) `OnReact`를 no-op으로 그대로 두었다 → 캐스팅 불필요.

## 6. DIM 도입 시 다룬 3가지 제약

1. **추상 재선언이 DIM을 가린다.** 베이스가 `public abstract void OnReact(...)`를 선언하면 인터페이스 기본구현 대신 그게 구현이 되어 자식이 OnReact를 강제 구현하게 된다 → 베이스에서 그 선언을 제거해 DIM을 살린다.
2. **DIM은 인터페이스 참조로만 호출.** 클래스 참조로 `.OnReact()`를 부르는 포워더는 인터페이스로 캐스팅(§5).
3. **`base.OnReact()` 체이닝이 자연 소멸.** 예전엔 `StatusEffect` 자식이 `base.OnReact()`로 공용 지속시간 로직을 호출했는데, 이제 베이스의 로직은 `OnTurnEvent`에, 자식 로직은 `OnAttack`에 있고 **타입 디스패치가 각 컨텍스트를 알맞은 훅으로 라우팅**하므로 자식이 base를 부를 필요가 없다. (같은 컨텍스트 타입을 베이스+자식이 모두 처리해야 하는 경우만 일반 virtual 체이닝 필요 — 현재 그런 케이스 없음.)

## 7. 검증 / ⚠️ IL2CPP 주의

- **에디터/Standalone = Mono** → DIM 런타임 지원됨. 에디터 컴파일 + 플레이 검증 완료.
- 각 패밀리 헤드리스 컴파일 EXIT 0 (참고: 로컬 csproj 스테일 이슈는 [memory] 참조 — 임시 주입으로 검증).
- **Android = IL2CPP** (`ProjectSettings` scriptingBackend Android:1). IL2CPP의 DIM 런타임 지원은 **실제 Android 빌드로 한 번 확인 필요** (이번 작업에서 미검증한 유일한 부분).
- **DIM이 IL2CPP에서 문제가 되면 폴백**: 디스패치를 인터페이스가 아니라 각 베이스 클래스의 concrete `OnReact`(+ virtual 훅)로 옮긴다. 보일러플레이트(switch)가 베이스별로 중복되지만 DIM 비의존 → 견고. 단 그 경우 `base.OnReact` 호출/클래스참조 호출이 다시 가능해진다.

## 8. 커밋
```
3af4596  add typed-dispatch hooks to ICombatReactor (DIM)
b5376b4  migrate CharacterPassive reactors to typed hooks
468434f  migrate PassiveAbility (monster passives) to typed hooks
bc3508f  migrate StatusEffect/Artifact/UnitStats/Modifier reactors to typed hooks
```
