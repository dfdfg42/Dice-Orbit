# 튜토리얼 (코어 전투) 설계 — 2026-08-02

## 목표

게임에 튜토리얼이 없어 진입장벽이 큼. 독특한 코어 전투(원형 궤도 + 주사위 배정 + 웨이브 + 패시브)를
새 플레이어가 이해하도록, **게임 시작 시 선택형 스크립트 튜토리얼 전투 1회**를 제공한다.
스코프는 **코어 전투 루프**로 한정한다.

## 범위 / 비범위

- **범위**: 첫 전투에서 — 주사위 굴림·배정, 이동/스킬(액티브) **턴 예산(이동 1·행동 1)**,
  캐릭터 **패시브 자동 발동**(전사=인접 아군, 도적=이동 거리), 몬스터 **의도·공격범위 타일** 읽기,
  턴 종료·몬스터 턴. **+ 전투 후 실제 런의 첫 캐릭터 선택(2명 고르기) 안내**.
- **비범위**: 노드맵/런 구조, 유물/포션/모디파이어, 상점/이벤트, 로컬라이제이션. (이후 별도 스코프)

## 진입 & 흐름

- 메인메뉴 `게임 시작`(StartGame) → `PlayerPrefs "tutorial_done"` **미완료 시** 확인 프롬프트:
  "튜토리얼을 하시겠어요? [예] [아니오]".
- **예** → `GameState.Tutorial` 진입 → 튜토리얼 전투(1부) → 승리 시 **실제 런 개시**(`StartGameFlow` → Recruit)
  → 첫 캐릭터 선택 화면에서 **"2명 고르기" 안내(2부)** → 선택 완료 → `tutorial_done=1` → 이후 Map(정상).
- **아니오** → 즉시 실제 런 (기존 StartGame 흐름, 안내 없음).
- 완료 후엔 자동 프롬프트 안 뜸. (선택) 메인메뉴 "튜토리얼 다시보기" 버튼으로 언제든 재생(플래그 무시).
- 오버레이 구석에 **"튜토리얼 스킵"** 최소 제공 → 즉시 실제 런.

## 격리 원칙 (중요)

튜토리얼은 실제 런 상태를 **절대 오염하지 않는다**:

- RunManager(맵/노드/시드), 세이브(RunSaveService), 영구 주사위 덱(DiceDeckManager 교체/부여분),
  골드/유물/포션을 변경하지 않는다.
- 데모용 **임시 파티(2)·몬스터(1)**만 스폰. 종료 시 전부 정리(파티/몬스터/오버레이) 후 실제 런 시작.
- 튜토리얼 전투는 노드 선택이 아니라 GameFlow가 **직접 개시**(고정 EncounterDefinition).

## 데모 셋업 (통제된 전투)

- **씬**: BattleScene 재사용(궤도/CombatManager/DiceUI 등 기존 시스템 그대로).
- **파티**: 전사 + 도적 2명, **인접 타일**에 배치(전사 패시브 조건 충족). 튜토리얼 전용 고정 참조.
- **몬스터**: 약체 1마리, **타일 기반 공격**(대상 타일이 색 범위로 표시)을 가진 프리셋.
  HP는 **1턴 생존·2턴 처치**되게 튜닝(몬스터 턴을 보여주기 위함).
- **주사위**: 튜토리얼 동안 굴림값을 스크립트가 통제(이동용 큰 값, 스킬 조건 만족값 보장).
  `DiceManager`에 "다음 굴림 고정값" 훅 추가.

## 구현 근거 (기존 코드 사실)

- 전사 `BattleCryPassive`: 좌우 인접 아군 1명당 공격 **+50%**(OnCalculateOutput). 범위=좌우 인접 타일
  (`IPassiveRangeProvider` → 브래킷 표시). → 2명 데모는 한쪽 인접 = **+50%** 시연.
- 도적 `PositioningPassive`: 이번 턴 이동 1칸당 다음 공격 **+25%**(이동 누적 → 공격 시 소모, 턴 시작 리셋).
- 몬스터 `AttackIntent`(TargetType.Tiles): 대상 타일들을 색으로 강조(`MonsterAttackIntentManager`).
  그 타일 위 캐릭터가 피격.
- 조작: 캐릭터 선택 → `CharacterActionUI`(이동/스킬 버튼) → 주사위로 발동 → 이동은 `CanSpendMove`,
  스킬은 `CanSpendAction`(각 1회). 패시브는 UI 버튼 없이 리액터 체인에서 자동.

## 컴포넌트 (A-lean 프레임워크)

1. **`TutorialStep`** (직렬화 가능): `instruction`(안내문), `highlightTarget`(하이라이트 대상 식별),
   `advance`(진행조건), `gateInput`(대상 외 입력 차단 여부), 선택 `onEnter`/`onExit`.
   - 진행조건: `Confirm`, `DiceRolled`, `CharacterSelected(who)`, `MoveExecuted(who)`,
     `SkillExecuted(who)`, `EndTurn`, `MonsterActed`, `CombatWon`.
2. **`TutorialDirector`** (싱글톤 코루틴): 단계 리스트를 순회 — 오버레이 갱신 → (gateInput 시)
   대상만 클릭 통과 → advance 조건 충족까지 대기 → 다음. CombatManager/DiceManager 이벤트 구독으로
   조건 감지. **단계 리스트는 코드에 정의**(무거운 SO 미사용; 후속 데이터화 여지).
   - 방어: 어떤 단계가 예외로 죽어도 전투/게임이 멈추지 않게 격리 → 최악의 경우 오버레이 제거 후
     자유 플레이 폴백.
   - **상태 전환 지속**: 전투(Tutorial) → 실제 런(Recruit)는 BattleScene 내 상태 전환이라 Director가
     그대로 유지되어 step 13(캐릭터 선택 안내)을 이어서 재생. `RecruitComplete`는 GameFlow의
     `OnRecruitComplete`/CharacterSelectionUI 신호로 감지.
3. **`TutorialOverlayUI`** (전용 Canvas, 최상위 sortingOrder): 전체 딤 + **스포트라이트 컷아웃**
   (대상 RectTransform/월드 오브젝트의 화면 영역만 밝게 + 테두리) + 말풍선(안내문 + "다음/확인").
   gateInput 시 대상 밖 클릭 차단(풀스크린 Raycast 블로커 + 대상 홀). 월드 오브젝트(캐릭터/몬스터/타일)는
   화면좌표 변환해 스포트라이트.
4. **GameFlow 훅**: `GameState.Tutorial` 추가, StartGame 프롬프트 모달 추가.
5. **최소 침습 알림**: CombatManager에 이동/스킬/턴 완료 알림이 없으면 추가(있으면 재사용),
   DiceManager에 굴림 고정 훅.

## 단계 시퀀스 (13)

**1부 — 튜토리얼 전투** (고정 데모, `GameState.Tutorial`)

| # | 내용 | 하이라이트 | 진행조건 | 입력잠금 |
|---|---|---|---|---|
| 1 | 궤도·캐릭터(전사·도적)·몬스터 소개 | 전체 | Confirm | - |
| 2 | 몬스터 의도 + **바닥 색 타일=공격 범위** | 의도 아이콘 + 색 타일 | Confirm | - |
| 3 | 주사위 자동 굴림 설명 | 주사위 손패 | Confirm | - |
| 4 | 전사 선택 | 전사 | CharacterSelected(전사) | O |
| 5 | 예산: 이동 1 + 행동 1 (각 1회) | Move·Skill 버튼 | Confirm | - |
| 6 | 전사 패시브(인접 +50%) + 스킬 공격 | 인접 브래킷·Skill·주사위·몬스터 | SkillExecuted(전사) | O |
| 7 | 도적 선택 + **멀리 이동(1칸당 +25%)** | 도적·큰 주사위·Move | MoveExecuted(도적) | O |
| 8 | 도적 공격(이동 보너스) | Skill·몬스터 | SkillExecuted(도적) | O |
| 9 | 액티브 vs 패시브 정리(패시브=자동) | 액션 패널 | Confirm | - |
| 10 | 턴 종료 | End Turn 버튼 | EndTurn | O |
| 11 | 몬스터 턴 관전(색 타일 범위로 공격) | 몬스터·색 타일 | MonsterActed | - |
| 12 | 마무리·승리 → "이제 진짜 모험!" | 몬스터 | CombatWon | - |

**2부 — 첫 캐릭터 선택** (실제 런 `GameState.Recruit`, Director가 상태 전환 넘어 계속)

| # | 내용 | 하이라이트 | 진행조건 | 입력잠금 |
|---|---|---|---|---|
| 13 | "여기서 파티에 넣을 **캐릭터 2명**을 고르세요" (각자 액티브·패시브 보유 — 1부 회상) | 캐릭터 선택 UI + 2/2 카운터 | RecruitComplete(2명 선택) | 경량(선택 UI만 허용) |

승리 → 실제 런 개시(`StartGameFlow` → Recruit) → 13단계 안내 → 2명 선택 완료 → `tutorial_done=1` → 데모 정리 → 이후 Map(정상).

## 데이터 흐름

```
StartGame → (프롬프트 "예") → GameFlow: GameState.Tutorial
  → 데모 셋업 스폰(전사+도적 인접, 약체 타일공격 몬스터, 통제 주사위)
  → CombatManager 전투 개시 → TutorialDirector.Play(steps 1~12) (전투 병행, 입력 게이팅)
  → CombatWon → 데모 전투 정리 → StartGameFlow (실제 런: Recruit)
  → Director가 Recruit 상태 감지 → step 13 "2명 고르기" 안내 → RecruitComplete
  → tutorial_done=1 → Director 종료 → 이후 Map (정상)
```
(Tutorial·Recruit 모두 BattleScene 내 상태 전환이라 씬 리로드 없음 → Director가 전환을 넘어 지속.)

## 엣지 / 에러 처리

- **각 안내 단계의 행동이 반드시 성공하도록 셋업을 통제**한다 — 주사위 값, 스킬 사거리/대상 도달,
  이동으로 도적이 몬스터를 타격 가능한 위치, 몬스터 공격범위에 캐릭터가 들어오는지 등.
- 게이팅 우회 불가(대상 외 입력 차단). 스킵 버튼은 항상 노출.
- 승리 판정은 기존 CombatManager 사용(빈 몬스터 공허참 방지 이미 반영됨).
- Director 예외에도 게임이 멈추지 않게 방어(오버레이 제거 후 자유 플레이 폴백).
- 이미 `tutorial_done`이면 프롬프트 생략.

## 테스트

- 에디트모드: Director 단계 진행 로직 확인(모의 이벤트로 advance 트리거).
- 플레이: 실제 전투에서 각 단계 하이라이트/게이팅/진행 수동 확인(스크린샷).
- **격리 검증**: 튜토리얼 후 RunManager/세이브/덱이 깨끗한지 확인.

## 신규 / 수정 파일 (예상)

- **신규**: `TutorialDirector.cs`, `TutorialStep.cs`, `TutorialOverlayUI.cs`, 튜토리얼 시작 프롬프트 UI,
  튜토리얼 데모 데이터(고정 EncounterDefinition/파티 참조).
- **수정**: `GameFlowManager`(Tutorial 상태 + 프롬프트), `MainMenuUI`(다시보기 버튼, 선택),
  `CombatManager`(이동/스킬/턴 알림 필요 시), `DiceManager`(굴림 고정 훅),
  `CharacterSelectionUI`(step 13 하이라이트 대상 + 2명 선택 완료 신호).

## 비범위 / 후속

- 전사 "양옆(+100%)" 전체 시연은 3명 필요 — 이번엔 **2명(한쪽 +50%)**. 후속 확장 여지.
- 단계 데이터의 SO화(디자이너 편집)·다국어는 후속.
