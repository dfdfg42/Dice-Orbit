# 전투 플로팅 알림 시스템

전투 중 패시브가 발동하거나 상태이상이 부여될 때 해당 유닛(캐릭터 / 몬스터) 위에 텍스트 버블이 떠오르도록 구현한 시스템.

---

## 구성 파일

| 파일 | 역할 |
|------|------|
| `Assets/Scripts/UI/FloatingLabelPopup.cs` | 월드 공간 TMP 텍스트 팝업 — 위로 이동하며 페이드 아웃 |
| `Assets/Scripts/UI/CombatNotifier.cs` | 알림 생성 진입점 — 스태거 오프셋 계산 |
| `Assets/Scripts/.../Combat/Passive/CharacterPassive.cs` | 모든 패시브가 상속하는 베이스 — `Notify()` 헬퍼 보유 |
| `Assets/Scripts/.../Combat/Pipeline/CombatPipeline.cs` | 상태이상 부여 경로 — `ApplyEffect()` 내 훅 |

---

## 흐름 개요

패시브는 이제 손으로 작성한 `OnReact` 대신 타입별 DIM(default interface method) 훅
(`OnAttack` / `OnHeal` / `OnMove` / `OnTurnEvent`)을 오버라이드한다. `ICombatReactor.OnReact`는
전달받은 concrete context 타입에 따라 해당 훅으로 라우팅하는 디스패치 진입점일 뿐이며,
`Notify()`는 이 타입별 훅 내부에서 호출된다.

```
패시브 타입별 훅 (OnAttack / OnHeal / OnMove / OnTurnEvent)
  └─ Notify()                         ← CharacterPassive 보호 메서드
        └─ CombatNotifier.NotifyPassive(unit, passiveName)
              └─ CombatNotifier.Notify(unit, text, color)
                    └─ FloatingLabelPopup.Create(text, color, worldPos)

CombatPipeline.ApplyEffect() 기본 케이스
  └─ StatusEffectManager.CreateEffect + AddEffect(...)
  └─ CombatNotifier.NotifyStatus(target, statusData.Name, statusData.Color)
        └─ FloatingLabelPopup.Create(...)
```

---

## FloatingLabelPopup

`Assets/Scripts/UI/FloatingLabelPopup.cs`

- `TextMeshPro`를 런타임에 `AddComponent`로 붙여 Canvas 없이 월드 공간에 렌더링
- `Animate()` 코루틴: `Lifetime = 1.2f`초 동안 위로 이동(`MoveSpeed = 1.6f`) + 알파 페이드
- 매 프레임 `transform.rotation = Camera.main.transform.rotation`으로 빌보드 처리
- `Create(text, color, worldPos)` 정적 팩토리로 생성 후 자동 소멸(`Destroy`)

```csharp
FloatingLabelPopup.Create("패시브 발동", Color.yellow, unit.transform.position + Vector3.up * 2f);
```

---

## CombatNotifier

`Assets/Scripts/UI/CombatNotifier.cs`

같은 유닛에 짧은 시간 내 여러 알림이 몰릴 때 Y 오프셋을 쌓아 겹치지 않게 처리.

### 핵심 상수

| 상수 | 값 | 설명 |
|------|----|------|
| `BaseHeight` | 2.2f | 유닛 기준 최소 높이 |
| `StaggerStep` | 0.65f | 버블 간 Y 간격 |
| `BatchResetSec` | 0.45f | 이 시간 내 도착하면 같은 배치로 처리 |

### 로직

```csharp
// 같은 유닛에 0.45초 이내 알림이 오면 index 누적
if (Counters.TryGetValue(unit, out var state) && now - state.lastTime < BatchResetSec)
    index = state.count;

Vector3 pos = unit.transform.position + Vector3.up * (BaseHeight + index * StaggerStep);
```

### 공개 메서드

```csharp
CombatNotifier.Notify(unit, text, color);              // 직접 지정
CombatNotifier.NotifyPassive(unit, passiveName);       // 황금색 (1f, 0.85f, 0.35f)
CombatNotifier.NotifyStatus(unit, statusName, color);  // 색상 지정 (보통 연청색)
```

---

## CharacterPassive 헬퍼

`Assets/Scripts/.../Combat/Passive/CharacterPassive.cs` (라인 61-66)

모든 `CharacterPassiveSkill` 서브클래스에서 패시브가 실제로 발동한 지점(타입별 DIM 훅 내부)에 `Notify()`를 호출하면 됨.

```csharp
protected void Notify()                              // PassiveName 사용, 황금색
protected void Notify(string text)                   // 텍스트 직접 지정, 황금색
protected void Notify(string text, Color color)      // 텍스트·색상 모두 지정
```

### 패시브별 호출 위치

| 패시브 | 발동 조건 | Notify 위치 |
|--------|-----------|-------------|
| `BattleCryPassive` (전사) | `OnAttack`(OnCalculateOutput), 인접 아군 수만큼 배율 | `context.OutputValue *= multiplier` 직후 `if (!context.IsSimulation) Notify()` |
| `FocusPassive` (마법사) | `OnTurnEvent`(TurnEnd) 집중 획득 / `OnAttack`(OnPostAction) 피격 시 감소 | `GainFocus()`·`ReduceFocus()`의 `AddEffect(...)` 직후 |
| `PositioningPassive` (도적) | `OnAttack`(OnCalculateOutput) && `movedDistanceThisTurn > 0` | `context.OutputValue *= multiplier` 직후 `!IsSimulation`이면 `Notify()` 후 `movedDistanceThisTurn = 0` 리셋 |
| `ReagentPrepPassive` (연금술사) | 시약 타일 접촉 시 스택 증가 | `AddReagentStack()` 내 `reagentStacks++` 직후 (`Notify($"{PassiveName} +..%")`) |

---

## 상태이상 훅 (CombatPipeline)

`Assets/Scripts/.../Combat/Pipeline/CombatPipeline.cs` (라인 216-217)

`ApplyEffect()` 기본 케이스에서 `StatusEffectManager.CreateEffect + AddEffect()` 호출 직후 알림 발생. 캐릭터와 몬스터가 동일한 파이프라인을 공유하므로 별도 처리 없이 양쪽 모두 커버됨.

```csharp
var statusData = TooltipKeywordFormatter.BuildStatusDisplayData(
    effectInfo.Type.ToString(), effectInfo.Value, effectInfo.Duration);
CombatNotifier.NotifyStatus(target, statusData.Name, statusData.Color);
```

`TooltipKeywordFormatter.BuildStatusDisplayData()`가 상태이상 타입 → 한글 이름 + 색상을 반환하므로 enum 문자열이 그대로 노출되지 않음.

---

## 새 패시브에 알림 추가하는 방법

1. `CharacterPassiveSkill`을 상속하는 패시브라면 발동 지점(타입별 DIM 훅)에 `Notify()` 한 줄 추가
2. 아군 버프처럼 `owner`가 아닌 다른 유닛에게 알릴 때는 `CombatNotifier.NotifyPassive(targetUnit, "버프명")` 직접 호출
3. 색상을 커스텀하려면 `Notify("텍스트", myColor)` 또는 `CombatNotifier.Notify(unit, text, color)`

---

## 색상 기준

| 유형 | 색상 | 값 |
|------|------|----|
| 패시브 발동 | 황금색 | `(1f, 0.85f, 0.35f)` |
| 상태이상 부여 | 연청색 (기본) | `(0.65f, 0.9f, 1f)` |
| 상태이상별 고유색 | `TooltipKeywordFormatter` 반환값 | — |
