# VFX 시스템 재설계 — 통합 큐 라이브러리

- 날짜: 2026-07-31
- 브랜치: (신규 feature 브랜치 예정)
- 상태: 승인됨 (구두 — 리졸버 A안 "테마 토큰" 확정)

## 1. 목적 / 범위

파티클·카메라 연출을 **단일 서비스 + 단일 라이브러리**로 통합하고, 현재 비어 있는 연출 훅을 채운다.

**포함 (사용자 선택: 전투 코어 + 런 레이어 + 임팩트 계열)**
- 전투: 몬스터/캐릭터 스킬 시전·적중·힐, 타일 조준 공격(타일+유닛), 상태이상 부여/만료, 사망
- 런: 포션 사용, 유물 발동, 전투 시작/승리/패배, 레벨업 타일
- 임팩트: 카메라 쉐이크 + 히트스탑 + 화면 플래시

**제외 (이번 아님)**
- 이동 트레일/대시 파티클 (호핑 트윈은 현행 유지)
- 주사위 소비 순간 연출
- 사운드 (엔트리에 슬롯만 예약, 재생 미구현)
- 오브젝트 풀링 (스폰 경로 단일화만 — 풀링은 후속 시 이 한 곳만 수정)

## 2. 현행 조사 요약 (재설계 근거)

- 파티클 스폰 2계통: `VfxManager`(전투, `CombatVfxProfile` SO 5종) + `TileVfxManager`(타일, `TileVfxDatabase` SO). 텍스트는 별도 `CombatNotifier`.
- 재생 책임: 시전(cast)=실행부 직접, 적중/힐(hit/heal)=`CombatPipeline.ApplyAction` 한 곳 (`CombatPipeline.cs:174-180`).
- **부재 지점**: 몬스터 프로필 14종 전부 미배선 / 타일 조준 공격은 타일→유닛 변환 후 유닛에만(빈 타일 0) / 상태이상 부여·만료 순간 연출 없음(EffectApplied·Expired 빈 스텁) / 사망 파티클·페이드 없음 / 포션·유물·전투시작종료·레벨업 연출 없음 / 카메라 쉐이크·히트스탑·플래시 전무.
- 자산: CFXR 팩 ~244 프리팹 (Impacts/Fire/Explosions/Electric/Texts/Liquids/Misc/Screen Distortion). 자체 VFX 프리팹은 없음. DOTween 등 트윈 라이브러리 미사용 — 전부 손코딩 코루틴.

## 3. 목표 아키텍처

### 3.1 VfxCue (의미 이벤트 어휘, enum)
"무엇이 일어났는가"만 표현. 호출부는 프리팹을 모른다.
```
SkillCast, AttackImpact, Heal, TileImpact,
StatusApplied, StatusExpired, Death,
PotionUse, ArtifactTrigger,
CombatStart, Victory, Defeat, LevelUp
```

### 3.2 VfxTheme (표현 테마, enum)
스킬/몬스터가 프로필 SO 대신 **테마 토큰 하나** 선언. 시작 세트 (enum은 증가 가능, 끝에 추가로 직렬화 보존):
```
None,          // 폴백 기본
Slash,         // 전사
Arcane,        // 마법사
Blade,         // 도적
Toxin,         // 연금술사
Goblin, Bone,  // Wave1
Beast,         // Wave2 곰
Frost,         // Wave3 눈/서리
Lunar, Solar,  // Wave4 달/태양
Fire,          // Wave5 불꽃
```

### 3.3 VfxLibrary (단일 SO — CombatVfxProfile 5종 + TileVfxDatabase 통합)
```
[Serializable] class ShakePreset { float amplitude = 0f; float duration = 0f; }  // amplitude 0 = 쉐이크 없음

[Serializable] class CueEntry {
    VfxCue cue;
    VfxTheme theme = None;
    GameObject prefab;
    Vector3 offset;
    float lifetime = 2f;
    ShakePreset shake;         // 카메라 쉐이크
    float hitStop = 0f;        // 히트스탑 시간(초, realtime), 0 = 없음
    // AudioClip sound;        // (예약 — 이번 미사용)
}

[Serializable] class TileAttributeEntry { ... }  // TileVfxDatabase에서 그대로 이전 (attributeType, trigger, prefab, offset, lifetime)

List<CueEntry> cueEntries;
List<TileAttributeEntry> attributeEntries;
```
해소: `(cue, theme)` 정확 매칭 → 없으면 `(cue, None)` 폴백 → 없으면 null(무재생). 딕셔너리 캐시 (`OnValidate`에서 무효화), TileVfxDatabase 캐시 패턴 그대로.

에셋 경로: `Assets/Resources/Skill/VFX/VfxLibrary.asset` (기존 VFX 폴더).

### 3.4 VfxService (VfxManager + TileVfxManager 통합, 단일 싱글톤)
```
static void Play(VfxCue cue, VfxTheme theme, Vector3 at)
static void PlayAt(VfxCue cue, VfxTheme theme, Unit unit)       // unit.transform.position
static void PlayAt(VfxCue cue, VfxTheme theme, TileData tile)   // tile.Position
static void PlayTileEvent(TileData tile, TileVfxTrigger trigger)  // 기존 타일속성 VFX — 라이브러리 attributeEntries 조회
```
동작: 라이브러리 조회 → prefab 없으면 return → `Instantiate(prefab, at + offset)` → `lifetime` 뒤 Destroy → 엔트리 `shake.amplitude>0`면 `ImpactFeedback.Shake`, `hitStop>0`면 `ImpactFeedback.HitStop`. 전역 fallback 프리팹(현 defaultAttackHitVfx/defaultHealVfx)은 라이브러리 `(AttackImpact, None)`/`(Heal, None)` 엔트리로 대체.

### 3.5 ImpactFeedback + CameraShaker (신설, 임팩트 계열)
- `CameraShaker` (메인 카메라 부착): `Shake(float amplitude, float duration)` — 기준 localPosition 저장 후 매 프레임 감쇠 랜덤 오프셋, 종료 시 복원. 유닛 빌보드가 카메라를 참조하므로 카메라 자체가 아닌 **전용 쉐이크 피벗**(카메라 부모 or 렌더 오프셋)을 흔들어 빌보드 로직과 충돌 회피.
- `ImpactFeedback`: `Shake(amp, dur)` → CameraShaker 위임. `HitStop(float seconds)` — `Time.timeScale` 순간 딥 후 `WaitForSecondsRealtime`로 복원 (전투 코루틴이 scaled wait이라 히트스탑 동안 자연히 멈춤). 화면 플래시는 풀스크린 CFXR을 `Play(cue, ...)`로 카메라 앞에 스폰(별도 시스템 불필요).

### 3.6 재생 책임 규칙 (기존 패턴 계승)
- **파이프라인 자동**: `AttackImpact`, `Heal`. `CombatContext.VfxProfile`(SO 참조) → **`CombatContext.VfxTheme`(enum)** 로 교체. `ApplyAction`이 `VfxService.PlayAt(AttackImpact, ctx.VfxTheme, target)` / `PlayAt(Heal, ...)` 호출 (`CombatPipeline.cs:174-180` 교체). 공격은 기존대로 `IsEffected` 시에만.
- **실행부 명시**: `SkillCast`, `TileImpact`, `StatusApplied/Expired`, `Death`, `PotionUse`, `ArtifactTrigger`, `CombatStart/Victory/Defeat`, `LevelUp` — 각 훅에서 `VfxService.Play/PlayAt` 직접.

## 4. 훅 배선 (부재 지점 채우기)

| Cue | 호출 위치 | 비고 |
|---|---|---|
| SkillCast | `SkillData.ExecuteSkillWithIntent:85`, `CharacterActiveTemplate.Execute:111` | PlayCast → PlayAt(SkillCast, theme, source) |
| AttackImpact | `CombatPipeline.ApplyAction:175` | 파이프라인 자동, theme 경유 |
| Heal | `CombatPipeline.ApplyAction:180` | 파이프라인 자동 |
| **TileImpact** | `SkillData.AttackTiles` 시작부 + 캐릭터 타일 스킬 | **조준된 모든 타일(빈 칸 포함)** 순회 후 PlayAt(TileImpact, theme, tile). 그다음 기존 유닛 히트. |
| StatusApplied | `StatusEffect.EffectApplied()` 베이스 | 이번엔 테마 None(공용) 고정, owner 위치. 효과별 테마는 후속 |
| StatusExpired | `StatusEffect.EffectExpired()` 베이스 | 상동 |
| Death | `Unit.HandleDeath`(또는 Monster/Character 파생) despawn 직전 | PlayAt(Death, theme, unit) |
| PotionUse | `PotionManager.TryUse/TryUseOn/TryUseOnTile` 성공 분기 | 대상/타일 위치. 포션은 테마 None(공용) |
| ArtifactTrigger | 유물 발동부 (선택적, 유물이 직접 호출하는 헬퍼 제공) | 과도 배선 금지 — 눈에 띄는 유물만 |
| CombatStart | `CombatManager.StartCombat:209` (OnCombatStart 부근) | 화면 중앙/카메라 앞 |
| Victory/Defeat | `CombatManager.EndCombat` 결과 분기 | |
| LevelUp | `TileData.OnArrive`에서 index 0 판정 시 | 기존 타일 VFX와 별개 cue |

CombatNotifier(텍스트 버블)는 그대로 공존 — VFX 통합 대상 아님.

## 5. 마이그레이션 (레거시 정리)

- `CombatVfxProfile.cs` + 5개 프로필 에셋 **제거**. `TileVfxDatabase` → VfxLibrary `attributeEntries`로 흡수 후 제거, `TileVfxManager` → VfxService로 흡수 후 제거.
- SkillData / CharacterActiveTemplate: `CombatVfxProfile vfxProfile` 필드 → `VfxTheme theme`. (직렬화: SO 참조가 사라지므로 프리셋에서 테마 재지정 필요 — 캐릭터 4종 + 몬스터 14종.)
- `CombatContext.VfxProfile` → `VfxTheme VfxTheme`.
- Goblin의 `VfxManager.PlayTile(vfxProfile, tile)` (지뢰 설치) → `VfxService.Play` 로 이관.
- 씬 VfxManager의 전역 fallback 프리팹 2종 → VfxLibrary `(AttackImpact/Heal, None)` 엔트리로 이전 후 컴포넌트 정리.
- `Scripts/Visuals/CombatVfxProfile.asset` (Resources 밖 미사용 잉여) 삭제.

## 6. 파일 구조

**신규**
- `Assets/Scripts/Visuals/Vfx/VfxCue.cs` (enum)
- `Assets/Scripts/Visuals/Vfx/VfxTheme.cs` (enum)
- `Assets/Scripts/Visuals/Vfx/VfxLibrary.cs` (SO)
- `Assets/Scripts/Visuals/Vfx/VfxService.cs` (싱글톤)
- `Assets/Scripts/Visuals/Vfx/ImpactFeedback.cs` + `CameraShaker.cs`
- `Assets/Resources/Skill/VFX/VfxLibrary.asset`

**수정** — CombatPipeline.cs, CombatContext.cs, SkillData.cs, CharacterActiveTemplate.cs, StatusEffect.cs, Unit/Monster/Character.cs(사망), PotionManager.cs, CombatManager.cs, TileData.cs, Goblin.cs

**제거** — VfxManager.cs, CombatVfxProfile.cs(+5 asset), TileVfxManager.cs, TileVfxDatabase.cs(+asset), Visuals/CombatVfxProfile.asset

## 7. 배선 데이터 (라이브러리 저작)

CFXR 프리팹 선정 (시작 매핑, 조정 가능):
- AttackImpact/None: Hit A (Red) · Slash: CFXR Slash (Blue) · Arcane: Magic Poof/전기 · Toxin: Liquids 계열 · Fire: Fire 계열 · Frost: 하늘색 Impact · Lunar/Solar: 색 변형 Impact
- Heal/None: 초록 Magical Source
- TileImpact: Impacts 소형 + 약한 쉐이크
- Death: Explosion Smoke 소형 + 쉐이크
- StatusApplied/Expired: Misc Flash 소형 (테마색)
- CombatStart/Victory/Defeat: Texts(_BANG_ 등) 또는 풀스크린
- 카메라 쉐이크: AttackImpact(약)·Death(중)·Defeat(강)에만. 히트스탑: 강타 cue 일부.

테마 배선: 캐릭터 4종(Slash/Arcane/Blade/Toxin), 몬스터 14종(Goblin/Bone/Beast/Frost/Lunar/Solar/Fire) — 프리셋 스킬의 theme enum 지정.

## 8. 구현 순서 (플랜 참고)

1. 인프라: enum 2종 + VfxLibrary + VfxService + ImpactFeedback/CameraShaker (기존 시스템과 병존, 미연결).
2. 전투 이관: 파이프라인 hit/heal + cast → VfxService, `VfxProfile`→`VfxTheme` (context/스킬/템플릿).
3. 타일 VFX 흡수: attributeEntries 이전 + TileData가 VfxService.PlayTileEvent 호출, TileVfxManager/Database 제거.
4. 신규 훅: TileImpact(타일 조준) + 상태 부여/만료 + 사망 + 포션 + 전투 시작/종료 + 레벨업.
5. 임팩트: 쉐이크/히트스탑 cue 배선.
6. 저작·배선: VfxLibrary 엔트리 작성(CFXR 선정) + 캐릭터/몬스터 테마 지정 + 씬 VfxService 배치 + fallback 이전.
7. 레거시 제거: VfxManager/CombatVfxProfile/TileVfxManager/TileVfxDatabase.

각 단계 게이트: AssetDatabase.Refresh → GetConsoleLogs(error) 0건.

## 9. 검증 (사용자 플레이)

- [ ] 캐릭터 4종 스킬 시전·적중 VFX가 테마별로 다름
- [ ] 몬스터 공격이 타일(빈 칸 포함)+유닛 둘 다 연출
- [ ] 상태이상 부여/만료 순간 연출
- [ ] 유닛 사망 시 파티클 + 쉐이크
- [ ] 포션 사용, 전투 시작/승리/패배 연출
- [ ] 강타 시 카메라 쉐이크/히트스탑 체감
- [ ] 기존 타일속성 VFX(지뢰/꿀/뼈 등) 회귀 없음
- [ ] 레거시 제거 후 컴파일·플레이 정상
