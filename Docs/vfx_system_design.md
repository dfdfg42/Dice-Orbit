# Combat VFX 설계 문서

> **⚠ 2026-07-07 갱신 — 호출 방식 통일.** 아래 본문 중 "스킬이 Hit VFX를 인라인 재생 + CustomVfx 태그로 기본 억제" 부분은 구식이다. 현재 규약:
>
> ```
> 실행부(스킬)는  context.VfxProfile = vfxProfile;  로 지정만 한다.
> 재생 판단은 CombatPipeline.ApplyAction 한 곳:
>   프로필에 hit/heal 프리팹이 있으면 그걸, 없으면 전역 기본 (VfxManager.PlayAttackHit/PlayHealEffect)
> Cast VFX만 실행부 시작 시 1회 (캐릭터: CharacterActiveTemplate / 몬스터: SkillData.ExecuteSkillWithIntent 공통)
> ```
> - `CustomVfx` 태그는 철거됨 ("프로필은 있는데 hit 칸이 비면 아무것도 안 나오는" 함정 제거)
> - `vfxProfile` 필드는 **몬스터 스킬 베이스(SkillData)에도 승격** — 캐릭터/몬스터 대칭. 몬스터 스킬 에셋에 프로필만 꽂으면 시전/히트 커스텀이 작동한다.

이 문서는 현재 전투 스킬 VFX 구조를 정리한 문서입니다.  
범위는 **캐릭터 스킬 VFX** 기준이며, 몬스터 스킬 전용 설계는 별도 확장 대상으로 둡니다.

## 1. 목표

- 스킬 로직에서 VFX 하드코딩 제거
- 스킬 데이터(SO) 단위로 VFX를 교체 가능하게 구성
- 파이프라인 기반 데미지/힐 처리와 충돌 없이 동작
- 커스텀 VFX가 없는 경우 기본 fallback VFX 자동 표시

## 2. 핵심 구성 요소

### 2.1 CharacterActiveSkill

파일: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Skills/CharacterActiveTemplate.cs`

- 모든 캐릭터 액티브 스킬의 추상 베이스 클래스 (`[Serializable]`, `SerializeReference`, ScriptableObject 아님)
- `vfxProfile` 필드를 가짐
- `Execute(...)` 내부에서 이 프로필을 사용해 Cast/Hit VFX를 인라인으로 재생

```csharp
[SerializeField] protected CombatVfxProfile vfxProfile;
```

### 2.2 CombatVfxProfile (ScriptableObject)

파일: `Assets/Scripts/Visuals/CombatVfxProfile.cs`

- 스킬별 VFX 프리팹 묶음 데이터
- 포함 항목:
  - `castVfxPrefab`
  - `hitVfxPrefab`
  - `healVfxPrefab`
  - `tileVfxPrefab`
  - 오프셋(`castOffset`, `hitOffset`, `healOffset`, `tileOffset`)
  - `defaultLifetime`

### 2.3 VfxManager

파일: `Assets/Scripts/Visuals/VfxManager.cs`

- 실제 프리팹 인스턴스 생성 담당
- 제공 API:
  - `PlayCast(profile, source)`
  - `PlayHit(profile, target)`
  - `PlayHeal(profile, target)`
  - `PlayTile(profile, tile)`
  - `PlayDefaultAttackHit(target)`
  - `PlayDefaultHeal(target)`

## 3. 실행 흐름

1. `CharacterActiveSkill.Execute()`가 `CalculateRawDamage(...)`로 데미지를 계산
2. 시전 시점에 `VfxManager.PlayCast(vfxProfile, source)` 호출
3. 대상별 루프에서:
   - `new AttackContext(source, target, skillName, rawDamage)` 생성
   - `vfxProfile != null`이면 `context.AddTag("CustomVfx")`
   - `CombatPipeline.Instance?.Process(context)` 실행
   - `context.IsEffected == true`일 때 `VfxManager.PlayHit(vfxProfile, target)` 호출
4. `CombatPipeline`의 Apply 단계에서:
   - `AttackContext`에 `CustomVfx` 태그가 없으면 fallback VFX 표시
   - 태그가 있으면 fallback 표시 생략 (중복 방지)

## 4. CustomVfx 태그 규칙

파일:  
- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Skills/CharacterActiveTemplate.cs`  
- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Pipeline/CombatPipeline.cs`

규칙:

- 커스텀 프로필(`vfxProfile != null`) 사용 시:
  - `context.AddTag("CustomVfx")`
- 파이프라인 fallback은 `atk.IsEffected && !atk.HasTag("CustomVfx")`일 때만 재생

## 5. 현재 캐릭터 스킬 매핑 (적용 상태)

프로필 폴더: `Assets/Resources/Skill/VFX/`

각 클래스의 액티브 스킬(`WarriorGreatswordActive`, `RogueAmbushActive`, `AlchemistThrowActive`, `MageEnergyBallActive`)의 `vfxProfile` 필드에 아래 프로필을 연결합니다. (별도의 per-class Effect SO 개념은 폐기됨 — 현재 `Assets/Resources/Skill/`에는 레거시 잔여물인 `Warrior_Damage_Eff.asset` 하나만 남아 있으며 사용되지 않습니다.)

- Warrior → `VFX/Warrior_CombatVfxProfile.asset`
- Rogue → `VFX/Rogue_CombatVfxProfile.asset`
- Alchemist → `VFX/Alchemist_CombatVfxProfile.asset`
- Mage → `VFX/Mage_CombatVfxProfile.asset`
- (몬스터) Goblin → `VFX/Goblin_CombatVfxProfile.asset`

## 6. 새 스킬 VFX 추가 방법

1. `CombatVfxProfile` 에셋 생성
2. cast/hit/heal/tile 프리팹과 오프셋 지정
3. 해당 `CharacterActiveSkill`의 `vfxProfile` 필드에 연결
4. `Execute(...)` 흐름에서:
   - 실행 전 `PlayCast`
   - 적중 시 `PlayHit`
   - `CustomVfx` 태그 추가
5. 플레이 모드에서 중복 재생(커스텀 + fallback) 없는지 확인

## 7. 설계 원칙

- VFX 데이터는 SO에서 관리하고, 로직 코드와 분리
- 데미지 적용 여부 기준(`context.IsEffected`)으로 Hit VFX를 제어
- fallback은 안전망으로 유지하되, 커스텀 적용 시 자동 비활성
- VFX 재생은 `CharacterActiveSkill.Execute` 내부에 인라인으로 유지하되, 프로필 교체만으로 연출 변경이 가능하도록 데이터 주도 방식 유지
