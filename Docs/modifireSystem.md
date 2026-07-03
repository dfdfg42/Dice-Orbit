---

# 🛠️ 스킬 런타임 갱신 시스템 (Modifier) 개편 요약 가이드

## 1. 개편 배경 및 목적
기존의 모디파이어 시스템은 스킬의 구조(타겟팅 범위, 타겟 수, 고유 배율 등)를 변경할 때 **스킬의 런타임 인스턴스(RuntimeInstance) 자체를 직접 영구적으로 변형**하는 방식을 사용했습니다. 
이 방식은 모디파이어가 추가/제거될 때마다 수치를 수동으로 더하고 빼야 해서 복원(Revert) 로직의 오류 발생 위험(상태 불일치)이 컸습니다. 이를 해결하기 위해, 원본 스킬은 유지하면서 장착 시점에 **휘발성 데이터 컨텍스트를 새로 덧씌워 계산(Caching)하는 구조**로 개편했습니다.

---

## 2. 개편 전(Before) vs 개편 후(After) 비교

### ❌ [개편 전] `OnEquipped` / `OnUnequipped` 직접 수정 방식
기존에는 모디파이어 장착/해제 시점에 스킬 인스턴스의 타겟 수치를 직접 더하고 뺐습니다.
*   **문제점:** 복합적인 모디파이어가 겹쳤을 때, 적용 순서 꼬임 및 해제 시 원본 수치로 완벽하게 되돌리기(Revert) 까다로움.

```csharp
// 개편 전: GreatswordWideSwing (광역 참격) 
public override void OnEquipped(Character character)
{
    var ri = GetRuntime(character);
    ri.targetCount++; // 장착 시 직접 더함
    if (ri.targetCount > 1) ri.targetType = CharacterSkillTargetType.MultiEnemy;
}

public override void OnUnequipped(Character character)
{
    var ri = GetRuntime(character);
    ri.targetCount--; // 해제 시 직접 뺌 (버그 발생 위험)
}
```

### ✅ [개편 후] `OnRefreshSkill(CharacterModfierContext)` 도출 방식
모디파이어가 장착 또는 해제될 때처럼 스킬 상태 갱신이 필요하다면 원본 스킬을 기반으로 `CharacterModfierContext` 객체를 **새로 생성한 뒤 빈 도화지 위에 모디파이어 로직을 일괄 적용(덮어쓰기)** 합니다.
*   **장점:** 해제 시 원상복구(Revert) 코드를 짤 필요가 전혀 없습니다. `OnRefreshSkill` 한 곳에서 "더해질 값의 최종 형태"만 선언하면 시스템이 항상 새 컨텍스트로 계산을 보장합니다.

```csharp
// 개편 후: GreatswordWideSwing (광역 참격)
public override void OnRefreshSkill(CharacterModfierContext context)
{
    // C# 패턴 매칭(Downcasting)으로 전사 대검 스킬인지 안전하게 확인
    if (context is WarriorGreatswordModifiedContext gsContext)
    {
        // 뺄 필요 없이, 이 모디파이어가 적용될 때 수행할 조작만 명시
        gsContext.TargetCount += 1;
        gsContext.TargetType = CharacterSkillTargetType.MultiEnemy;
    }
}
```

---

## 3. 핵심 변경 요소 및 구조

1. **`CharacterModfierContext` 추가 (Base 클래스)**
   - 모든 스킬이 공유하는 핵심 데이터(`TargetType`, `TargetCount`, `PreviewStyle` 등)를 담는 객체입니다. 스킬 실행 전 타겟팅 범위를 그리는 UI 등에서 이 객체를 읽어갑니다.
   - CharacterModfierContext 대신 Skill에서 타겟팅 범위를 읽을 수 있게 수정할 예정입니다.

2. **강타입(Typed) 서브 컨텍스트 (Derived 클래스)**
   - 캐릭터 전용 기믹 수치를 저장하기 위해 Base를 상속받아 생성합니다. 
   - 예: `WarriorGreatswordModifiedContext`는 대검 전용 기믹인 `BaseDamageMultiplier` 프로퍼티를 추가로 보유합니다.
   - 이를 통해 광역 공격으로 변경 등을 처리할 수 있습니다.

3. **`ModifierManager.ApplyTo(CharacterModfierContext)` 추가**
   - `IModifierManager`/`ModifierManager`에 정의되어 있으며, 모디파이어 목록을 순회하며 `OnRefreshSkill`을 호출해 컨텍스트를 완성시키는 파이프라인 메서드입니다(`context.IsCancelled`가 참이면 중단). 모디파이어의 변동이 생길 때 호출하여 컨텍스트를 새로고침합니다.
   - 스킬의 초기 상태를 선언하고 새로고침할 때 초기 상태에서 모디파이어를 적용하는 방식으로 개편할 예정입니다.

4. **`CharacterActiveSkill.GenerateContext()` 추가**
   - 원형(Template) 스킬 클래스가 자신의 초기 상태를 담은(Base 혹은 Derived) 컨텍스트를 생성하여 내보냅니다.

## 4. 작업자 적용 지침 (Action Item)
앞으로 새로운 기믹을 지닌 캐릭터 스킬과 시그니처 모디파이어를 제작하실 때는 다음 단계를 따라주세요.

*   스킬의 기초 데이터 이외에 모디파이어로 변경될 수 있는 전용 수치가 있다면, `CharacterModfierContext`를 상속받은 전용 컨텍스트(예: `MageFireballModifiedContext`)를 생성하세요.
*   `CharacterActiveSkill` 상속 클래스에서 `GenerateContext`를 _override_ 하여 해당 특수 컨텍스트를 반환하게 하세요.
*   모디파이어 스크립트에서는 기존의 `OnEquipped` 대신 **`OnRefreshSkill`**을 오버라이드하여 캐스팅(`if (context is 전용_컨텍스트_이름)`) 후 속성을 조작하시면 됩니다!
