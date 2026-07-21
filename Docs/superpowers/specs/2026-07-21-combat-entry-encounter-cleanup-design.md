# 전투 진입 정리 설계 — WaveManager 폐지, Encounter 개념으로 통일

> 작성: 2026-07-21. 토론 확정본.
> 배경: 노드맵 개편(2026-07-07) 이후에도 전투 진입 경로에 구 "웨이브 순차 진행" 시대의
> 잔재가 남아 신/구 로직이 혼재한다. 이번에 "Wave" 개념을 코드·데이터에서 완전히 제거한다.

## 1. 현재 문제 (조사 결과, 2026-07-21)

1. **배경이 다른 소스에서 유도** — 몬스터는 `node.Encounter`(Act 티어 풀)에서 스폰되는데,
   배경은 `BackgroundManager`가 구 `1WaveDatabase`를 `층+1`로 인덱싱해 가져온다 (실질 버그 —
   구 DB는 4항목뿐이라 층 4+는 마지막 배경으로 클램프, 전투 내용과 무관).
2. **엘리트/보스/9~11층 일반 전투가 구 DB 폴백으로 동작** — `Act.asset`의 ElitePool/BossPool이
   비어 있고 유일한 티어가 0~8층만 커버해서, 조용히 구 로직으로 굴러간다.
3. **이중 장부 + 삼중 감지** — 몬스터 목록이 `WaveManager.spawnedMonsters`와
   `CombatManager.activeMonsters` 두 곳. 승리 감지가 WaveManager 사망 카운팅과
   `CombatManager.IsCombatFinished()` 양쪽에 있고, 패배 감지는 CombatManager에만 있는 비대칭.
4. **핑퐁 흐름** — `GameFlow → WaveManager.StartEncounter(스폰) → OnWaveStart →
   GameFlow.OnWaveStarted → CombatManager.StartCombat`. 불필요한 콜백 왕복.
5. **죽은 잔재** — `WaveDefinition.SpawnCount`(로그만), `TestWaveDB.asset`(참조 0),
   `CurrentWave`(구 의미 상실), 유물 시작 회복이 스포너에 낀 것.

## 2. 확정 결정 (토론)

| 쟁점 | 결정 |
|---|---|
| 전투 상태 권위 | **CombatManager 단일** (장부·승패 판정·수명주기). WaveManager 폐지 |
| WaveManager의 후신 | **`EncounterSpawner`** — 스폰만 하는 도구 컴포넌트 (B안) |
| 배경 소속 | **막 기본 + 몹 세트 오버라이드** (`ActDefinition.DefaultBackground` + `EncounterDefinition.BackgroundSprite`) |
| 구 DB 폴백 | **제거 + 데이터 이관** (구 DB 4웨이브를 Act 풀로 재배치 후 삭제. 풀 비면 시끄럽게 실패) |
| 데이터 개명 | `WaveDefinition` → **`EncounterDefinition`** ([Serializable] 평클래스 — 에셋 데이터 안 깨짐) |
| 오브젝트 풀링 | 안 함 (전투당 몬스터 한 자릿수 — YAGNI) |

## 3. 새 흐름

```
GameFlow (Combat 상태 진입, StartCombat())
  → CombatManager.StartEncounter(node.Encounter, node.Floor + 1)
      ① 이전 전투 몬스터 GameObject 파괴 + activeMonsters 초기화
         (파괴 책임 = 장부 권위인 CombatManager)
      ② EncounterSpawner.Spawn(encounter) → 반환 목록을 activeMonsters에 등록
      ③ 유물 시작 회복 적용 (ArtifactManager.BattleStartHeal)
      ④ CurrentEncounter/CurrentFloorNumber 설정 → 기존 StartCombat() 턴 시퀀스
승리:  IsCombatFinished() → EndCombat(true) → GameFlowManager.OnEncounterCleared()
패배:  IsCombatFinished() → EndCombat(false) → GameFlowManager.OnCombatDefeat()   (현행 유지)
```

- GameFlow의 `OnWaveStarted` 콜백 왕복 삭제 — 상태 진입에서 `StartEncounter`를 직접 호출.
- WaveManager의 사망 카운팅 / `CheckWaveClear` / `IsWaveActive` / `EndWave` 전부 삭제.
  승패 판정은 `IsCombatFinished()` 한 곳.
- **설계 다듬기 (제시안에서 단순화)**: 새 이벤트를 만들지 않는다.
  - 시작 방송 = 기존 `CombatManager.OnCombatStart` 재사용. `StartEncounter`가 스폰 후
    `StartCombat()`을 부르므로 발화 시점이 구 `OnWaveStart`(스폰 후·턴 전)와 동일.
  - 클리어 통지 = `EndCombat(true)`에서 `GameFlowManager.OnEncounterCleared()` **직접 호출**
    — 패배 쪽 `OnCombatDefeat()` 직접 호출과 대칭. (구 `OnWaveClear` 구독자는 GameFlow뿐이었음)

## 4. 컴포넌트 상세

### CombatManager (확장)
```csharp
public EncounterDefinition CurrentEncounter { get; private set; }  // 배경 등 조회용
public int CurrentFloorNumber { get; private set; }                // 표시용 (층+1)

public void StartEncounter(EncounterDefinition encounter, int floorNumber)
{
    DestroyActiveMonsters();          // ① 이전 전투 잔여 파괴 + Clear
    CurrentEncounter = encounter;
    CurrentFloorNumber = floorNumber;
    var spawned = EncounterSpawner.Instance.Spawn(encounter);   // ②
    foreach (var m in spawned) RegisterMonster(m);
    ApplyBattleStartHeal();           // ③ ArtifactManager.BattleStartHeal (WaveManager에서 이사)
    StartCombat();                    // ④ 기존 턴 시퀀스 (OnCombatStart 발화)
}
```
`EndCombat(true)`의 `WaveManager.CheckWaveClear()` 호출 → `GameFlowManager.Instance.OnEncounterCleared()`로 교체.
몬스터 사망 시 목록 제거는 현행 `OnMonsterDefeated` 경로 그대로 (WaveManager의 중복 OnDeath 구독 삭제).

### EncounterSpawner (신규 — WaveManager 씬 자리 대체)
```csharp
public class EncounterSpawner : MonoBehaviour   // 싱글톤 (Instance/EnsureInstance 관례)
{
    [SerializeField] GameObject monsterPrefab;      // WaveManager에서 이식
    [SerializeField] Transform spawnRoot;
    [SerializeField] float fallbackSpawnRadius = 2.5f;

    /// 인스턴스화 + 프리셋 초기화 + 스폰 위치 배치 + 정체성 색 배정. 장부/이벤트/판정 없음.
    public List<Monster> Spawn(EncounterDefinition encounter);
}
```
스폰 위치 로직(스폰 포인트 셔플 → 소진 시 황금각 오프셋 → 폴백 원형 배치)은 WaveManager 것 그대로 승계.
`MonsterIdentityManager.Setup`도 여기서 호출 (스폰 연출의 일부).

### EncounterDefinition (구 WaveDefinition 개명·이동)
```csharp
// Assets/Scripts/Core/Run/EncounterDefinition.cs, namespace DiceOrbit.Core.Run
[System.Serializable]
public class EncounterDefinition
{
    public List<MonsterPreset> MonsterPresets;
    public Sprite BackgroundSprite;   // 오버라이드 — 비면 막 기본 배경
    // SpawnCount 삭제 (죽은 필드 — 에셋 YAML 잔존값은 Unity가 무시)
}
```
[Serializable] 평클래스는 필드명으로 직렬화되므로 클래스/네임스페이스 개명이 에셋 데이터를 깨지 않는다.
타입 참조처 일괄 개명: `ActDefinition`(티어/풀/Resolve), `MapGraph.MapNode.Encounter`,
`MapGenerator`, `GameFlowManager`, `CombatManager` 등 `WaveDefinition`을 쓰는 모든 곳.

### ActDefinition (수정)
- `public Sprite DefaultBackground;` 추가 (막 기본 배경 — Act 1 = 마녀의 실험실).
- `WaveDatabase` 폴백 필드와 `Resolve*`의 폴백 브랜치 삭제 — 풀 미매칭 시 `null` 반환
  (기존 GameFlow의 `Encounter == null` → 에러 로그 + 맵 복귀 가드가 안전망).
- 타입 참조 `WaveDefinition` → `EncounterDefinition`.

### BackgroundManager (수정)
- `OnCombatStart` 구독으로 이사. 배경 결정:
  `CombatManager.CurrentEncounter?.BackgroundSprite ?? RunManager.CurrentAct?.DefaultBackground`.
- `RunManager`에 `CurrentAct`(ActDefinition) 프로퍼티가 없으면 노출 추가.
- 구 `GetWaveDefinition` 인덱싱 삭제.

### GameFlowManager (수정)
- `StartCombat()`: `WaveManager.StartEncounter` 호출 → `CombatManager.StartEncounter`로 교체.
  재진입 가드 `!WaveManager.IsWaveActive` → `!CombatManager.InCombat`으로 대체.
  RunManager 없는 디버그 폴백(배치된 몬스터로 시작)은 유지.
- `OnWaveStarted(int)` 삭제 (핑퐁 제거). `OnWaveCleared(int wave)` → `OnEncounterCleared()`
  (본문 동일: 부활 → 보스면 Victory → Reward 라우팅. 번호는 로그에만 쓰였음).

## 5. 이벤트 구독자 이사 (기계적, 7곳)

| 구독자 | 구 | 신 |
|---|---|---|
| GameFlowManager | OnWaveStart → OnWaveStarted | 삭제 (직접 호출 흐름) |
| GameFlowManager | OnWaveClear → OnWaveCleared(int) | EndCombat(true)가 OnEncounterCleared() 직접 호출 |
| FocusPassive (마법사) | OnWaveStart(int) 리셋 | CombatManager.OnCombatStart 구독 |
| ReagentPrepPassive (연금술사) | OnWaveStart(int) 리셋 | 〃 |
| Skeleton / Goblin / BearPackTracker (static 훅) | OnWaveStart(int) | 〃 (스폰 시 구독 → StartCombat 발화 순서 보장) |
| BackgroundManager | OnWaveStart(int) + GetWaveDefinition | OnCombatStart + CurrentEncounter/막 기본 |

주의: `OnCombatStart`는 전투 1회당 1번 발화 — 구 `OnWaveStart`와 의미 동일. 핸들러 시그니처만
`(int)` → `()` 수정.

## 6. 데이터 이관·삭제

**이관** (에디터 작업, Unity MCP 스크립트 가능):
1. `1WaveDatabase`의 웨이브 4개를: 마지막 → `BossPool`, 끝-1 → `ElitePool`,
   나머지 → `BattleTiers`에 분배 (기존 "asdf" 티어 세트 유지).
2. 티어 커버리지를 **전 일반 전투 층**(0 ~ FloorCount-2)으로 확장 — 9~11층 구멍 봉합.
3. 각 웨이브의 `BackgroundSprite`는: 공통이면 `Act.DefaultBackground`로 승격, 다르면 개별 유지.

**삭제** (이관 후):
- `WaveManager.cs` (씬 컴포넌트는 EncounterSpawner로 교체, monsterPrefab/spawnRoot 참조 이식)
- `WaveDatabase.cs` (클래스), `1WaveDatabase.asset`, `TestWaveDB.asset`
- `Data/Waves/` 폴더 (EncounterDefinition은 Core/Run으로 이동)

## 7. 스코프 밖 (이번에 안 건드림)

- `CombatManager.FindButtonByName`(GameObject.Find UI 스크래핑), `AttackMonster/AttackAllMonsters`
  정리 — 별도 부채.
- 몬스터 오브젝트 풀링 — YAGNI.
- 다막 전환 — 기존 미구현 항목 그대로.

## 8. 검증

- 컴파일: Unity MCP 콘솔 에러 0.
- 플레이 (에디터): ① 노드 클릭 → 전투 진입 → 몬스터 스폰 (일반/엘리트/보스 각 1회)
  ② 클리어 → 부활/보상 라우팅, 보스 → Victory ③ 파티 전멸 → GameOver
  ④ 배경: 막 기본 표시 + 오버라이드 세트에서 교체 ⑤ 마법사 집중/연금 시약 리셋 발동
  ⑥ 스켈레톤 뼈 설치/고블린 지뢰 정리 동작 ⑦ 9~11층 일반 전투 몹 세트 정상 배정
  ⑧ "Wave" 문자열이 게임플레이 코드에서 사라졌는지 grep 확인.
