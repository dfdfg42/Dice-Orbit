# VFX 시스템 재설계 — GameplayCue 스타일 태그 시스템

- 날짜: 2026-07-31
- 브랜치: feature/vfx-redesign-20260731
- 상태: 승인됨 (구두 — GameplayCue 계층 태그, 테마 개념 제거)

## 1. 목적 / 범위

파티클·카메라 연출을 **단일 서비스 + 단일 라이브러리**로 통합하고, 비어 있는 연출 훅을 채운다. 데이터 모델은 언리얼 GAS의 **GameplayCue** 패턴 — 스킬/효과가 **큐 태그를 직접 지정**하고, 태그는 점 계층 + 상향 폴백으로 해소된다. (별도 "테마" 개념 없음 — 테마는 태그 꼬리로 표현.)

**포함 (전투 코어 + 런 레이어 + 임팩트)**
- 전투: 스킬 시전·적중·힐, 타일 조준 공격(타일+유닛), 상태이상(지속), 사망
- 런: 포션 사용, 유물 발동, 전투 시작/승리/패배, 레벨업 타일
- 임팩트: 카메라 쉐이크 + 히트스탑 + 화면 플래시

**제외**: 이동 트레일/대시, 주사위 소비 연출, 사운드(엔트리 슬롯만 예약), 오브젝트 풀링(스폰 경로 단일화만).

## 2. 현행 조사 요약 (재설계 근거)

- 파티클 스폰 2계통: `VfxManager`(전투, `CombatVfxProfile` 5종) + `TileVfxManager`(타일, `TileVfxDatabase`). 텍스트는 별도 `CombatNotifier`.
- 재생 책임: 시전=실행부, 적중/힐=`CombatPipeline.ApplyAction` 한 곳 (`CombatPipeline.cs:174-180`).
- **부재 지점**: 몬스터 프로필 14종 전부 미배선 / 타일 조준 공격은 유닛에만(빈 타일 0) / 상태 부여·만료 순간 연출 없음(EffectApplied·Expired 빈 스텁) / 사망 파티클·페이드 없음 / 포션·유물·전투시작종료·레벨업 연출 없음 / 카메라 쉐이크·히트스탑·플래시 전무.
- 자산: CFXR 팩 ~244 프리팹. 자체 VFX 없음. DOTween 미사용(손코딩 코루틴).

## 3. 핵심 개념 — GameplayCue 태그

### 3.1 태그 (string, 점 계층)
사건을 계층 문자열로 표현. 예:
```
impact.fire.critical
impact.fire
impact              ← 못 찾으면 뒤 세그먼트를 떼며 상향 폴백
```
**해소 규칙**: 정확 매칭 → 마지막 `.세그먼트` 제거 후 재시도 → … → 루트 → 없으면 무재생. 폴백이 자동이라 세부 태그는 **다르게 할 것만** 등록한다 — `impact.fire`가 없으면 `impact`로 자동 폴백.

베이스 태그 상수는 `VfxTags` static 클래스에 모아 오타 방지:
```
Cast, Impact, Heal, TileImpact, Death,
Potion, Artifact, CombatStart, Victory, Defeat, LevelUp,
Status(= "status")   // 하위: status.poison, status.weak, status.power ...
```
세부 세그먼트(fire/lunar/frost/slash…)는 디자이너가 짓는 자유 문자열이며, 큐 태그 안에 그대로 들어간다.

### 3.2 소스가 큐 태그를 직접 지정 (테마 없음)
연출을 내는 주체(스킬/효과/유닛)가 **자기가 터뜨릴 큐 태그**를 직접 문자열로 갖는다. GAS 어빌리티가 GameplayCueTag를 직접 명시하는 것과 같음.
- 스킬(SkillData / CharacterActiveTemplate): `string castCue`, `string impactCue` (비우면 루트 `cast` / `impact`).
- 상태이상(StatusEffect): 큐 태그를 스스로 안다 (`status.<effectkey>`).
- 사망 등 유닛 단위: 몬스터 프리셋이 `string deathCue`를 두거나 비우면 루트 `death`.

예: 불꽃 몬스터의 공격 스킬은 `impactCue = "impact.fire"`, `castCue = "cast.fire"`. 비워두면 전부 루트로 폴백돼 기본 연출이 나온다. → 스킬별로 원하는 것만 채우면 되고, 라이브러리는 태그별로 프리팹을 한 곳에서 관리.

### 3.3 Cue 정의 — Burst / Looping (GAS의 핵심 이점)
```
enum VfxCuePlay { Burst, Looping }

[Serializable] class ShakePreset { float amplitude = 0f; float duration = 0f; }  // 0 = 없음

[Serializable] class VfxCue {
    string tag;                 // "impact.fire", "status.poison"
    VfxCuePlay play = Burst;
    GameObject prefab;
    Vector3 offset;
    float lifetime = 2f;        // Burst 전용 (수명 뒤 Destroy)
    ShakePreset shake;          // 카메라 쉐이크
    float hitStop = 0f;         // 히트스탑(초, realtime), 0 = 없음
    // AudioClip sound;         // 예약, 이번 미사용
}
```
- **Burst**: 한 번 터지고 수명 뒤 소멸 (명중/사망/포션).
- **Looping**: 대상에 부착돼 지속, 명시적으로 멈출 때까지 유지 (**상태이상**). 부여 시 시작, 만료 시 제거 — "부여/만료 2개 이벤트"를 지속 Cue 1개로 처리.

### 3.4 VfxLibrary (단일 SO — CombatVfxProfile 5종 + TileVfxDatabase 통합)
```
List<VfxCue> cues;                       // Burst + Looping, 태그 키
List<TileAttributeEntry> attributeEntries;  // TileVfxDatabase에서 이전 (attributeType, trigger, prefab, offset, lifetime)
```
태그 해소는 딕셔너리 캐시 + 상향 폴백. `OnValidate`에서 캐시 무효화(TileVfxDatabase 패턴 계승). 경로: `Assets/Resources/Skill/VFX/VfxLibrary.asset`.

### 3.5 VfxService (VfxManager + TileVfxManager 통합, 단일 싱글톤)
```
static void Play(string tag, Vector3 at)                 // Burst, 위치
static void PlayOn(string tag, Unit unit)                // Burst, 유닛 위치 (tag 빈 문자열/null이면 no-op)
static void PlayOn(string tag, TileData tile)            // Burst, 타일 위치
static VfxHandle StartLoop(string tag, Unit unit)        // Looping 부착, 핸들 반환
static void StopLoop(Unit unit, string tag)              // Looping 제거
static void PlayTileEvent(TileData tile, TileVfxTrigger) // 기존 타일속성 VFX (attributeEntries 조회)
```
동작: 라이브러리에서 태그 해소(상향 폴백) → Cue 없으면 return → prefab 스폰(위치+offset) → Burst면 lifetime 뒤 Destroy / Looping이면 대상 자식으로 부착 + `(unit, tag)` 핸들 등록 → `shake.amplitude>0`면 `ImpactFeedback.Shake`, `hitStop>0`면 `ImpactFeedback.HitStop`. Looping 대상이 사망·파괴되면 핸들 정리. 빈/누락 태그는 안전하게 no-op.

### 3.6 ImpactFeedback + CameraShaker (신설, 임팩트 계열)
- `CameraShaker`: `Shake(amp, dur)` — 전용 쉐이크 피벗(카메라 부모)을 감쇠 랜덤 오프셋으로 흔들고 복원. 유닛 빌보드가 카메라 참조하므로 카메라 자체가 아닌 피벗을 흔들어 충돌 회피.
- `ImpactFeedback`: `Shake` 위임 + `HitStop(sec)` — `Time.timeScale` 순간 딥 후 `WaitForSecondsRealtime` 복원 (전투 코루틴이 scaled wait이라 히트스탑 동안 자연 정지). 화면 플래시는 풀스크린 CFXR을 `Play`로 카메라 앞 스폰.

## 4. 재생 책임 규칙 (기존 패턴 계승)

- **파이프라인 자동**: `impact`, `heal`. `CombatContext.VfxProfile`(SO) → **`CombatContext.VfxCue`(string)** 로 교체. 스킬이 컨텍스트에 자기 `impactCue`를 실어 보내고, `ApplyAction`이 `VfxService.PlayOn(ctx.VfxCue ?? VfxTags.Impact, target)` / `PlayOn(VfxTags.Heal, target)` 호출 (`CombatPipeline.cs:174-180` 교체). 공격은 기존대로 `IsEffected` 시에만.
- **실행부 명시**: `cast`, `tileImpact`, 상태(StartLoop/StopLoop), `death`, `potion`, `artifact`, `combatStart/victory/defeat`, `levelUp`.

## 5. 훅 배선 (부재 지점 채우기)

| 태그 | 호출 위치 | 종류 | 비고 |
|---|---|---|---|
| castCue → cast | `SkillData.ExecuteSkillWithIntent:85`, `CharacterActiveTemplate.Execute:111` | Burst | PlayOn(castCue ?? Cast, source) |
| impactCue → impact | `CombatPipeline.ApplyAction:175` | Burst | 스킬이 context.VfxCue로 전달, 파이프라인 자동 |
| heal | `CombatPipeline.ApplyAction:180` | Burst | 파이프라인 자동 |
| tileImpact | `SkillData.AttackTiles` 시작부(+캐릭터 타일 스킬) | Burst | **조준된 모든 타일(빈 칸 포함)** 순회. 그다음 기존 유닛 히트 |
| status.\<key\> | `StatusEffect.EffectApplied()`/`EffectExpired()` 베이스 | Looping | 부여=StartLoop, 만료=StopLoop. key=EffectType 소문자. 미등록이면 `status` 폴백 |
| deathCue → death | `Unit.HandleDeath`(또는 파생) despawn 직전 | Burst | 쉐이크 동반 |
| potion | `PotionManager.TryUse/TryUseOn/TryUseOnTile` 성공 분기 | Burst | 공용 |
| artifact | 유물 발동 헬퍼 (선택적) | Burst | 눈에 띄는 유물만, 과도 배선 금지 |
| combatStart / victory / defeat | `CombatManager.StartCombat:209` / `EndCombat` 결과 분기 | Burst | 카메라 앞/중앙 |
| levelUp | `TileData.OnArrive` index 0 판정 | Burst | 기존 타일 VFX와 별개 |

CombatNotifier(텍스트 버블)는 그대로 공존 — VFX 통합 대상 아님.

## 6. 마이그레이션 (레거시 정리)

- `CombatVfxProfile.cs` + 5개 프로필 에셋 **제거**. `TileVfxDatabase` → VfxLibrary `attributeEntries`로 흡수 후 제거, `TileVfxManager` → VfxService로 흡수 후 제거.
- SkillData / CharacterActiveTemplate: `CombatVfxProfile vfxProfile` 필드 → `string castCue` + `string impactCue`.
- `CombatContext.VfxProfile` → `string VfxCue`.
- Goblin `VfxManager.PlayTile` (지뢰 설치) → `VfxService` 이관.
- 씬 VfxManager 전역 fallback 프리팹 2종 → VfxLibrary `impact` / `heal` 루트 Cue로 이전 후 컴포넌트 정리.
- `Scripts/Visuals/CombatVfxProfile.asset` (미사용 잉여) 삭제.

## 7. 파일 구조

**신규** (`Assets/Scripts/Visuals/Vfx/`)
- `VfxTags.cs` (베이스 태그 상수 + 해소 헬퍼), `VfxCue.cs`(정의 + VfxCuePlay), `VfxLibrary.cs`(SO), `VfxService.cs`(싱글톤 + VfxHandle), `ImpactFeedback.cs`, `CameraShaker.cs`
- `Assets/Resources/Skill/VFX/VfxLibrary.asset`

**수정** — CombatPipeline.cs, CombatContext.cs, SkillData.cs, CharacterActiveTemplate.cs, StatusEffect.cs, Unit/Monster/Character.cs(사망), PotionManager.cs, CombatManager.cs, TileData.cs, Goblin.cs

**제거** — VfxManager.cs, CombatVfxProfile.cs(+5 asset), TileVfxManager.cs, TileVfxDatabase.cs(+asset), Visuals/CombatVfxProfile.asset

## 8. 배선 데이터 (라이브러리 저작)

CFXR 프리팹 시작 매핑 (계층 폴백이라 루트부터 채우고 세부는 다른 것만):
- `impact`(루트): Hit A (Red) · `impact.slash`: Slash (Blue) · `impact.fire`: Fire 계열 · `impact.frost`: 하늘색 Impact · `impact.lunar`/`impact.solar`: 색 변형
- `heal`: 초록 Magical Source
- `cast`(루트) + 세부 소수
- `tileImpact`: 소형 Impact + 약한 쉐이크
- `death`: Explosion Smoke + 쉐이크
- `status.poison`/`status.frostbite` 등: Looping 오라
- `combatStart`/`victory`/`defeat`: Texts(_BANG_ 등) 또는 풀스크린
- 쉐이크: impact(약)·death(중)·defeat(강). 히트스탑: 강타 cue 일부.

큐 태그 배선: 캐릭터 4종 스킬(impact.slash/impact.arcane/impact.blade/impact.toxin 등), 몬스터 14종 스킬(impact.goblin/impact.frost/impact.lunar/impact.solar/impact.fire 등) — 프리셋 스킬의 `castCue`/`impactCue`(+필요 시 `deathCue`) 지정. 채우지 않은 스킬은 루트 큐로 자동 폴백.

## 9. 구현 순서 (플랜 참고)

1. 인프라: VfxTags + VfxCue + VfxLibrary + VfxService + ImpactFeedback/CameraShaker (병존, 미연결).
2. 전투 이관: 파이프라인 impact/heal + cast → VfxService, `VfxProfile`→`castCue`/`impactCue`(스킬/템플릿) + context `VfxProfile`→`VfxCue`.
3. 타일 VFX 흡수: attributeEntries 이전 + TileData가 VfxService.PlayTileEvent, TileVfxManager/Database 제거.
4. 신규 훅: tileImpact(타일 조준) + 상태(Looping) + 사망 + 포션 + 전투 시작/종료 + 레벨업.
5. 임팩트: 쉐이크/히트스탑 cue 배선.
6. 저작·배선: VfxLibrary Cue 작성(CFXR 선정) + 캐릭터/몬스터 스킬 큐 태그 지정 + 씬 VfxService/CameraShaker 배치 + fallback 이전.
7. 레거시 제거: VfxManager/CombatVfxProfile/TileVfxManager/TileVfxDatabase.

각 단계 게이트: AssetDatabase.Refresh → GetConsoleLogs(error) 0건.

## 10. 검증 (사용자 플레이)

- [ ] 캐릭터 4종 스킬 시전·적중 VFX가 스킬별 큐로 다름, 미지정 스킬은 루트로 폴백
- [ ] 몬스터 공격이 타일(빈 칸 포함)+유닛 둘 다 연출
- [ ] 상태이상 부여 시 지속 오라, 만료 시 사라짐 (Looping)
- [ ] 유닛 사망 시 파티클 + 쉐이크
- [ ] 포션 사용, 전투 시작/승리/패배 연출
- [ ] 강타 시 카메라 쉐이크/히트스탑 체감
- [ ] 기존 타일속성 VFX(지뢰/꿀/뼈 등) 회귀 없음
- [ ] 레거시 제거 후 컴파일·플레이 정상
