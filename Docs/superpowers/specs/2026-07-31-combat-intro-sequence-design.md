# 전투 시작 연출 (Combat Intro Sequence) — 설계 스펙

- 날짜: 2026-07-31
- 브랜치: feature/vfx-redesign-20260731 (VFX 시스템 위에 얹음)
- 상태: 승인됨 (구두 — "그대로 스펙 써줘")

## 1. 목적 / 범위

전투 진입 시 **타일 시계방향 순차 낙하 → 캐릭터 순차 팝인 → 몬스터 순차 소환(VFX)** 을 재생하는 연출 디렉터를 추가한다. 연출이 끝난 뒤에 기존 "플레이어 턴" 배너 + 주사위 굴림 + 입력 개방이 이어진다.

**포함**
- 타일/캐릭터/몬스터 3단계 순차 연출 코루틴
- 몬스터 소환 VFX (신규 `summon` 큐)
- 연출 동안 입력 차단 (기존 게이트 재사용)

**제외**
- 스킵 기능 (사용자 결정: 매 전투 재생, 스킵 없음)
- 전투 종료/승리 연출
- 사운드

## 2. 현행 흐름 (조사 결과)

- **타일**: `OrbitManager.Start` → `GenerateOrbit()`가 씬 로드 시 20개 전량 즉시 생성 (`OrbitManager.cs:54-92`). 인덱스 순서 = 각도 증가순(수학 좌표계 반시계). 타일 트랜스폼 `Position`을 캐릭터 배치가 참조. 등장 애니메이션 없음.
- **캐릭터**: 영입(Recruit) 단계에서 `CharacterSpawner.Spawn`으로 생성되어 런 내내 유지 (`CharacterSpawner.cs:28`). 전투 진입 시 이미 타일 위에 존재. 등장 애니메이션 없음. `PartyManager.Instance.Party`로 순회.
- **몬스터**: 전투 진입 시 `CombatManager.StartEncounter`에서 `EncounterSpawner.Spawn`이 전량 즉시 생성 (`CombatManager.cs:165`, `EncounterSpawner.cs:60-71`). 등장 애니메이션 없음. `activeMonsters` 리스트.
- **오케스트레이션**: `GameFlowManager.StartCombat` → `CombatManager.StartEncounter`(몬스터 스폰 → `BroadcastCombatStart` → `StartCombat`) → `StartCombat`(`OnCombatStart` + CombatStart VFX + `AnnounceAndStartPlayerTurn` 코루틴) → `StartPlayerTurn`(주사위 자동 굴림 + endTurn 버튼 활성 + 인텐트 표시 + `playerTurnActive=true`) (`CombatManager.cs:157-350`).
- **입력 게이트**: `playerTurnActive`가 false면 캐릭터 클릭/이동/스킬 전부 무효 (`Character.cs:469-482`, `CharacterActionUI.cs:219/540/589`, `CombatManager.IsCharacterTurnActionValid:445-450`). → 연출을 `StartPlayerTurn` 이전에 두면 별도 잠금 불필요.
- **재사용 패턴**: `DiceRollAnimator`의 EaseOutBack 팝인 + 스태거 딜레이 (`DiceRollAnimator.cs:160-200, 394-411`), `TurnAnnouncementUI`의 `yield return ui.ShowPlayerTurn()` 코루틴 체이닝 관례.
- **VfxService**: `VfxService.Play(tag, Vector3)` / `PlayOn(tag, Unit)` (`VfxService.cs:42-58`). `VfxTags`에 소환 전용 상수 없음 → 신설 필요.

## 3. 아키텍처

### 3.1 CombatIntroDirector (신규, 단일 책임 코루틴)
전투 진입 연출 전담. `CombatManager`가 소유·호출.
```
public class CombatIntroDirector : MonoBehaviour
{
    // 튜닝값 (전부 [SerializeField])
    tileDropHeight = 6f; tileDropDuration = 0.22f; tileStagger = 0.03f; tileStartScale = 0.6f;
    charPopDuration = 0.25f; charStagger = 0.10f;
    monsterPopDuration = 0.25f; monsterStagger = 0.20f;
    bool clockwise = true;   // 화면상 시계방향이 되도록 순회 방향 (플레이로 맞춤)

    // 진입점 — 몬스터 스폰 결과를 받아 3단계 순차 연출 후 완료
    public IEnumerator Play(IReadOnlyList<Monster> spawnedMonsters);
}
```
- 싱글톤 + `EnsureInstance` (VfxService 패턴). 씬 없으면 자동 생성.
- `Play`는 `IEnumerator`라 `CombatManager`가 `yield return`으로 소비.

### 3.2 3단계 연출

**Phase 1 — 타일 시계방향 낙하** (~0.7s)
- 대상: `OrbitManager.Tiles` 전체. `clockwise`면 순회 순서를 뒤집어 화면상 시계방향으로.
- 각 타일: 원래 `localPosition`/`localScale` 캐시 → 시작값을 `원위치 + Vector3.up*tileDropHeight`, 스케일 `원스케일*tileStartScale`로 세팅 → `tileDropDuration` 동안 EaseOutQuad로 원위치·원스케일로 착지. 타일마다 `tileStagger` 지연.
- **위치 무결성**: 반드시 캐시한 원래 값으로 정확히 착지 (캐릭터 배치가 `tile.Position` 참조). 연출 중 캐릭터·몬스터는 숨김 상태라 충돌 없음.
- 연출 전 전 타일을 즉시 시작값(위·축소)으로 세팅해 "떨어지기 전" 상태를 만든 뒤 순차 착지.

**Phase 2 — 캐릭터 순차 팝인** (~0.5s)
- 대상: `PartyManager.Instance.Party`의 살아있는 캐릭터, 리스트 순.
- 각 캐릭터: 원래 `localScale` 캐시 → 스케일 0 → `charPopDuration` 동안 EaseOutBack로 원스케일. `charStagger` 지연.
- 위치는 건드리지 않음(이미 타일 위). 스케일만.

**Phase 3 — 몬스터 순차 소환** (~0.6s)
- 입력: `Play`가 받은 `spawnedMonsters` (이미 스폰됐으나 **숨김 상태**, §3.3).
- 각 몬스터: `VfxService.PlayOn(VfxTags.Summon, monster)` 재생 → 스케일 0→원스케일 EaseOutBack 팝인. `monsterStagger` 지연.

### 3.3 몬스터 스폰 — 숨긴 채 전량 스폰
- `EncounterSpawner.Spawn`은 현행대로 전량 즉시 생성(전멸 판정/인텐트 로직 안전 유지). 단 스폰 직후 각 몬스터 `localScale = 0`으로 숨김.
- 방법: `EncounterSpawner.Spawn`에 `bool startHidden = false` 파라미터 추가. `true`면 스폰 직후 스케일 0. Phase 3에서 디렉터가 원스케일로 팝인.
- 원스케일은 몬스터 생성 시점의 스케일을 디렉터가 캐시(또는 프리셋 기준 1). `Monster`에 원스케일 저장이 없으면 스폰 시 `Vector3.one` 가정 후 캐시.

### 3.4 오케스트레이션 (CombatManager.StartEncounter 코루틴화)
현행:
```
StartEncounter: DestroyActiveMonsters → EncounterSpawner.Spawn → RegisterMonster → BroadcastCombatStart → StartCombat
```
변경:
```
StartEncounter:
    DestroyActiveMonsters
    var spawned = EncounterSpawner.Spawn(encounter, startHidden:true)   // 숨긴 채 스폰
    foreach RegisterMonster(spawned)
    StartCoroutine(IntroThenStart(spawned))

IntroThenStart(spawned):
    yield return CombatIntroDirector.EnsureInstance().Play(spawned)     // 타일→캐릭터→몬스터
    BroadcastCombatStart()
    StartCombat()
```
- `StartCombat` 내 CombatStart VFX(`VfxTags.CombatStart`)는 현재 비활성(null)이라 연출과 충돌 없음.
- `GameFlowManager.StartCombat`은 `StartEncounter`를 호출만 하고 완료를 기다리지 않으므로(현행 동일) 코루틴화해도 호출부 변경 불필요.

### 3.5 VFX — summon 큐 신설
- `VfxTags`에 `public const string Summon = "summon";` 추가 (enum 아님 — 상수 추가라 순서 무관).
- `VfxLibrary.asset`에 `summon` 큐 1개 배선: CFXR Portal 또는 Magic Poof 계열, offset y≈0.5, lifetime≈1.5, 쉐이크 없음.

## 4. 파일 구조

**신규**
- `Assets/Scripts/Visuals/Vfx/CombatIntroDirector.cs` — 3단계 연출 코루틴 (Visuals에 두어 VfxService와 인접)

**수정**
- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/CombatManager.cs` — `StartEncounter` 코루틴화 (`IntroThenStart`)
- `Assets/Scripts/Core/Stage/BattleStage/.../EncounterSpawner.cs` — `Spawn(encounter, bool startHidden = false)` 파라미터 추가
- `Assets/Scripts/Visuals/Vfx/VfxTags.cs` — `Summon` 상수
- `Assets/Resources/Skill/VFX/VfxLibrary.asset` — `summon` 큐 (에디터 RunCommand)

## 5. 엣지 케이스

- **몬스터 0마리 인카운터**: Phase 3 스킵(리스트 빈), 타일·캐릭터만 연출 후 진행.
- **파티 0명**: Phase 2 스킵. (실전 미발생이나 방어)
- **타일 미생성/OrbitManager 없음**: Phase 1 스킵, 경고 로그.
- **재진입(매 전투)**: 매번 원래 위치/스케일을 새로 캐시 후 연출 → 반복 안전. 이전 연출 코루틴이 남아있지 않도록 `Play` 시작 시 자기 코루틴 정리.
- **연출 중 씬 전환/파괴**: 디렉터 파괴 시 코루틴 중단 — 트랜스폼이 시작값(축소/위)에 멈출 수 있으나 다음 전투 진입이 다시 캐시·복원. 안전을 위해 `Play` 완료 시 반드시 원값으로 확정.

## 6. 타이밍 (기본값, 튜닝 가능)

| 단계 | 값 |
|---|---|
| 타일 낙하 | duration 0.22s, stagger 0.03s, 높이 6, 시작스케일 0.6 → 20타일 ≈ 0.6~0.7s |
| 캐릭터 팝인 | duration 0.25s, stagger 0.10s → 4명 ≈ 0.5s |
| 몬스터 소환 | duration 0.25s, stagger 0.20s → N마리 ≈ 0.4~0.8s |
| **총합** | **≈ 1.6~2.0s** |

전부 `Time.deltaTime` 기반(게임 시간). 히트스탑 등과 무관한 진입 연출이라 unscaled 불필요.

## 7. 검증 (사용자 플레이)

- [ ] 전투 진입 시 타일이 시계방향으로 하나씩 떨어져 자리에 안착
- [ ] 이어서 캐릭터가 순서대로 팝인
- [ ] 몬스터가 소환 VFX와 함께 한 마리씩 등장
- [ ] 연출 동안 주사위·클릭 입력 불가, 연출 종료 후 "플레이어 턴" 배너 + 주사위 굴림 + 입력 개방
- [ ] 매 전투 재생, 반복 시에도 타일/캐릭터 위치·스케일 정상 복원
- [ ] 몬스터 0/파티 특수 케이스에서 크래시 없음
- [ ] 시계방향 방향이 화면상 맞는지 (아니면 clockwise 토글)
