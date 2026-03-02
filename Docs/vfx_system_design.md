# Combat VFX 설계 문서

이 문서는 현재 전투 스킬 VFX 구조를 정리한 문서입니다.  
범위는 **캐릭터 스킬 VFX** 기준이며, 몬스터 스킬 전용 설계는 별도 확장 대상으로 둡니다.

## 1. 목표

- 스킬 로직에서 VFX 하드코딩 제거
- 스킬 데이터(SO) 단위로 VFX를 교체 가능하게 구성
- 파이프라인 기반 데미지/힐 처리와 충돌 없이 동작
- 커스텀 VFX가 없는 경우 기본 fallback VFX 자동 표시

## 2. 핵심 구성 요소

### 2.1 SkillEffectBase

파일: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/SkillData/SkillEffectBase.cs`

- 모든 스킬 이펙트의 베이스 클래스
- `vfxProfile` 필드를 가짐
- 각 Effect(`Execute`)가 이 프로필을 사용해 Cast/Hit/Heal/Tile VFX를 재생

```csharp
[SerializeField] protected DiceOrbit.Visuals.CombatVfxProfile vfxProfile;
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

1. `CharacterSkillData.Execute()`가 등록된 `SkillEffectBase` 목록 실행
2. 개별 Effect (`DiceMultiplierDamageEffect`, `MageStackDamageEffect`)에서:
   - `VfxManager.PlayCast(vfxProfile, source)` 호출
   - `CombatAction` 생성 후 `CombatPipeline.Process(context)` 실행
   - `context.IsEffected == true`일 때 `VfxManager.PlayHit(vfxProfile, target)` 호출
3. `CombatPipeline.ApplyAction()`에서:
   - Action에 `CustomVfx` 태그가 없으면 fallback VFX 표시
   - 태그가 있으면 fallback 표시 생략 (중복 방지)

## 4. CustomVfx 태그 규칙

파일:  
- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/SkillData/DiceMultiplierDamageEffect.cs`  
- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/SkillData/MageStackDamageEffect.cs`  
- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Pipeline/CombatPipeline.cs`

규칙:

- 커스텀 프로필(`vfxProfile != null`) 사용 시:
  - `action.AddTag("CustomVfx")`
- 파이프라인 fallback은 `!context.Action.HasTag("CustomVfx")`일 때만 재생

## 5. 현재 캐릭터 스킬 매핑 (적용 상태)

프로필 폴더: `Assets/Resources/Skill/VFX/`

- Warrior
  - Effect: `Assets/Resources/Skill/Effects/Warrior_Damage_Eff.asset`
  - Profile: `Warrior_CombatVfxProfile.asset`
- Rogue
  - Effect: `Assets/Resources/Skill/Effects/Rogue_Damage_Eff.asset`
  - Profile: `Rogue_CombatVfxProfile.asset`
- Alchemist
  - Effect: `Assets/Resources/Skill/Effects/Alchemist_Damage_Eff.asset`
  - Profile: `Alchemist_CombatVfxProfile.asset`
- Mage
  - Effect: `Assets/Resources/Skill/Effects/Mage_Damage_Eff.asset`
  - Profile: `Mage_CombatVfxProfile.asset`

## 6. 새 스킬 VFX 추가 방법

1. `CombatVfxProfile` 에셋 생성
2. cast/hit/heal/tile 프리팹과 오프셋 지정
3. 해당 스킬 Effect SO의 `vfxProfile`에 연결
4. Effect 코드에서:
   - 실행 전 `PlayCast`
   - 적중 시 `PlayHit`
   - `CustomVfx` 태그 추가
5. 플레이 모드에서 중복 재생(커스텀 + fallback) 없는지 확인

## 7. 설계 원칙

- VFX 데이터는 SO에서 관리하고, 로직 코드와 분리
- 데미지 적용 여부 기준(`context.IsEffected`)으로 Hit VFX를 제어
- fallback은 안전망으로 유지하되, 커스텀 적용 시 자동 비활성
- 캐릭터/몬스터를 같은 인터페이스(`SkillEffectBase`)로 확장 가능하게 유지

