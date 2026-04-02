# Animator Setup Guide (Character / Monster)

이 프로젝트는 이동/공격/피격/사망/조준을 모두 `Animator` 파라미터로 제어하도록 전환되었습니다.

## 1) 필수 Animator 파라미터

아래 이름을 기본값으로 사용합니다.

- `IsMoving` (bool)
- `IsAiming` (bool)
- `Attack` (trigger)
- `Hit` (trigger)
- `Death` (trigger)
- `IsDead` (bool)

> 파라미터 이름을 바꿀 경우, 해당 프리팹의 스크립트 필드(`CharacterSpriteVisual`, `Monster`)에서 문자열도 함께 수정해야 합니다.

## 2) 권장 상태(State)

- `Idle` (기본)
- `Move`
- `Aim`
- `Attack`
- `Hit`
- `Death`

## 3) 권장 전이(Transition)

- `Idle -> Move`: `IsMoving == true`
- `Move -> Idle`: `IsMoving == false`
- `Idle/Move -> Aim`: `IsAiming == true`
- `Aim -> Idle`: `IsAiming == false`
- `Idle/Move/Aim -> Attack`: `Attack` trigger
- `Any State -> Hit`: `Hit` trigger (단, `IsDead == false` 조건 권장)
- `Any State -> Death`: `IsDead == true` 또는 `Death` trigger

`Death` 상태는 Exit Time 없이 고정되는 구성이 안전합니다.

## 4) 코드 연결 지점

- 캐릭터: `Assets/Scripts/Visuals/CharacterSpriteVisual.cs`
	- `PlayMove()`, `PlaySkill()`, `PlayDamage()`, `PlayDeath()`, `SetAiming()`
- 몬스터: `Assets/Scripts/Core/Stage/BattleStage/Units/Monster/Monster.cs`
	- 공격/피격/사망 시 Animator trigger/bool 호출
- 조준 시작/종료:
	- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/SkillTargetSelector.cs`

## 5) 런타임 생성 캐릭터 주의사항

런타임 생성 캐릭터는 `CharacterSelectionUI`에서 Animator를 자동 추가합니다.

- 파일: `Assets/Scripts/UI/CharacterSelectionUI.cs`
- 캐릭터별 우선 필드: `CharacterPreset.AnimatorController`
- 폴백 필드: `characterAnimatorController`

즉, 캐릭터마다 다른 애니메이션을 쓰려면 각 프리셋 에셋에서 `AnimatorController`를 지정하면 됩니다.

### 권장 구성

- 공통 상태머신: `BaseCharacter.controller`
- 캐릭터별 클립 교체: `Animator Override Controller`
- 각 캐릭터 프리셋의 `AnimatorController`에 해당 Override 할당

