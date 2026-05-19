# 스킬 시스템 아키텍처 고도화 (Phase 2 - 완전 분리)

이 계획서는 데이터 유실을 감수하더라도 객체지향적으로 가장 깔끔하고 확장성 있는 구조를 완성하기 위한 리팩터링 작업입니다. 기존의 단일 `CharacterSkill` SO를 폐기하고, 목적에 맞는 상속 구조로 완전히 분리합니다.

## User Review Required

> [!CAUTION]
> **이 계획은 실행 즉시 기존 스킬 에셋 데이터의 유실을 동반합니다.**
> 작업이 완료된 후에는 유니티 에디터에서 `CharacterPreset`의 스킬 목록이 비워질 수 있으며, "알케미스트 액티브", "알케미스트 패시브" 등의 에셋을 새로 생성(Create -> Dice Orbit -> Skills -> Active/Passive)하여 다시 연결해 주어야 합니다.

## Proposed Changes

### 1. 에셋 상속 분리 (가장 핵심적인 변경)
불필요한 공백 필드(Active용 변수와 Passive용 변수가 혼재된 상태)를 제거하고 책임을 명확히 합니다.

#### [NEW] `SkillAsset.cs` (추상 클래스)
- `CharacterSkill`을 대체할 최상위 부모 클래스.
- 스킬의 기본 정보(이름, 아이콘, 텍스트 설명, `SkillLevelData`, `DiceRequirement` 등)만 보유.

#### [NEW] `ActiveSkillAsset.cs` & `PassiveSkillAsset.cs`
- `ActiveSkillAsset`: `SkillAsset`을 상속하며, `ActiveTemplate`, `TargetType`, `PreviewStyle` 필드를 가집니다.
- `PassiveSkillAsset`: `SkillAsset`을 상속하며, `PassiveTemplate` 필드만 가집니다.

#### [DELETE] `CharacterSkill.cs`
- 기존 단일 설계도 클래스는 삭제합니다.

### 2. ActiveTemplate의 상태(State) 지원 확장
액티브 스킬이 쿨타임이나 스택을 가질 수 있도록 복제(Clone) 기능을 추가합니다.

#### [MODIFY] `CharacterActiveTemplate.cs`
- 가상 메서드 `public virtual CharacterActiveTemplate Clone()` 추가.
- `MemberwiseClone()`을 사용해 인스턴스 복제 지원 (상태 저장용).

### 3. RuntimeAbility의 권한 및 책임 강화
프록시 역할만 하던 `RuntimeAbility`를 진정한 "스킬 런타임 통제소"로 만듭니다.

#### [MODIFY] `RuntimeAbility.cs`
- 생성자에서 `SkillAsset`을 받을 때, 타입이 `ActiveSkillAsset`이면 **`ActiveTemplate`을 복제(Clone)**하여 `RuntimeActiveInstance` 필드에 저장.
- 기존 패시브 복제 로직은 타입이 `PassiveSkillAsset`일 때 수행.
- `public bool CanUse(int diceValue)` 추가: `SkillAsset`의 조건 검사와 더불어, `RuntimeActiveInstance`의 상태(쿨타임 등) 검사까지 통합 수행.
- `public IEnumerator Execute(...)` 추가: 템플릿의 `Execute()`를 감싸서 직접 실행 권한 확보.

### 4. SkillManager 중앙 통제소화
UI와 파편화되어 있던 타겟팅 및 실행 권한을 `SkillManager`로 통합합니다.

#### [MODIFY] `SkillManager.cs`
- `PrepareSkill(...)`: 기존 유효성 검사를 `RuntimeAbility.CanUse()`로 위임.
- 타겟팅 검사 로직 추가: `ActiveSkillAsset`의 `TargetType`을 확인하고 타겟팅이 필요하면 여기서 `SkillTargetSelector`를 호출, 타겟팅이 필요 없으면 즉시 `ActionQueue`에 등록.

### 5. CharacterActionUI의 책임 축소
UI는 사용자의 "의도(Intent)"만 전달하도록 가벼워집니다.

#### [MODIFY] `CharacterActionUI.cs`
- 스킬 버튼 클릭 시(`OnSpecificSkillClicked`), 복잡한 타겟팅 분기문(`switch (TargetType)`)을 모두 삭제.
- 단지 `SkillManager.Instance.PrepareSkill(currentCharacter, index, currentDice)` 한 줄만 호출하고 주사위를 예약 상태로 넘기도록 간소화.

## Verification Plan

### Automated / Manual Tests
1. **에셋 재구성 (수동 작업):** 코드가 컴파일된 후, 유니티 에디터에서 `ActiveSkillAsset`과 `PassiveSkillAsset`을 생성하여 기존 캐릭터 프리셋(Goblin, Mage 등)에 다시 할당합니다.
2. 유니티 플레이 모드 진입 후, 타겟팅이 필요한 스킬과 필요 없는 스킬을 각각 사용해 봅니다.
3. 스킬 사용 시 주사위 눈금 조건이 정상적으로 동작하는지(`RuntimeAbility.CanUse()`) 확인합니다.
4. 스킬 실행 전후로 에러 로그가 없는지 파이프라인 흐름을 추적합니다.
