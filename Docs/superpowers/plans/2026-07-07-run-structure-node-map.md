# 노드맵 런 구조 구현 계획

> 스펙: `Docs/superpowers/specs/2026-07-07-run-structure-node-map-design.md`
> 브랜치: `feature/run-structure-node-map-20260707` (battle-ui 브랜치에서 분기 — 레벨 철거가 전제)

**목표(MVP 슬라이스):** 맵 화면 ↔ 전투를 오가는 런 루프 골격.
전투/엘리트/휴식/보스 노드가 실동작, 상점/이벤트는 자리만(스텁). 포션·유물·주사위 개조는 후속 계획.

**현재 흐름 (개조 대상):**
```
Combat → WaveManager.StartNextWave(순차 1~8) → 클리어 → Reward → Recruit → Combat ...
```
**목표 흐름:**
```
Map(노드 선택) → [전투류] Combat(노드가 WaveDefinition 지정) → 클리어 → Reward
                → (노드 1·2였으면 Recruit) → Map 복귀
              → [휴식] 회복 → Map 복귀
              → [보스] Combat → 클리어 → Victory
```

---

## Task 1: 런 데이터 레이어 (순수 C#, 씬 무관)

**생성:**
- `Assets/Scripts/Core/Run/MapNodeType.cs` — enum: Battle, Elite, Shop, Rest, Event, Boss
- `Assets/Scripts/Core/Run/MapGraph.cs` — `MapNode` (id, floor, lane, type, waveIndex, diceModReward, visited), `MapGraph` (nodes, next[] 간선, 시작/보스 id). [Serializable]
- `Assets/Scripts/Core/Run/ActDefinition.cs` — ScriptableObject: 층 수, 층별 노드 수 범위, 타입 배치 규칙(엘리트 층, 상점/휴식/이벤트 보장), WaveDatabase 참조, 층→웨이브 티어 매핑
- `Assets/Scripts/Core/Run/MapGenerator.cs` — ActDefinition → MapGraph. 비교차 간선(비례 창 매핑), 보장 규칙 적용, 시드 지원

**검증:** 컴파일 + MapGenerator 단독 로그 테스트(ContextMenu)

## Task 2: RunManager (런 상태 싱글톤)

**생성:** `Assets/Scripts/Core/Run/RunManager.cs`
- 현재 Act/MapGraph/현재 노드/클리어한 전투 수 보관
- `StartRun(ActDefinition)` → 맵 생성, `GetSelectableNodes()` → 현재 노드의 next 중 미방문, `MoveToNode(id)` → 방문 처리 + 노드 타입 반환
- 노드 1·2 자동 모집 판단: `ShouldRecruitAfterBattle` (클리어한 전투 수 < 2)

## Task 3: GameState.Map + GameFlowManager 개편

**수정:** `GameState.cs` (+Map), `GameFlowManager.cs`
- Combat 진입을 "현재 노드의 웨이브" 기반으로: `WaveManager.StartWave(WaveDefinition)` 공개 오버로드 추가
- `OnWaveCleared` → Boss 노드면 Victory, 아니면 Reward
- `OnRewardComplete` → `ShouldRecruitAfterBattle`이면 Recruit, 아니면 Map
- `OnRecruitComplete` → Map (첫 시작 제외)
- 게임 시작: CharacterSelection(2명) → RunManager.StartRun → Map
- 폴백: RunManager/ActDefinition 없으면 기존 순차 웨이브 흐름 유지 (기존 씬 호환)

## Task 4: 맵 UI (NodeMapUI)

**생성:** `Assets/Scripts/UI/NodeMapUI.cs` — 에디터 소유 + 런타임 폴백 (기존 패턴)
- 펠트 배경(보상 화면 팔레트) 위 노드 아이콘 그래프, 간선 라인, 현재 위치 표시
- 선택 가능 노드만 버튼 활성 (골드 테두리 펄스), 클릭 → RunManager.MoveToNode → GameFlow 상태 전환
- 방문 노드 딤 처리, 주사위 개조 예고 아이콘
- Shop/Event 노드: "준비 중" 토스트 후 Map 유지 (스텁)

## Task 5: 휴식 노드

- MoveToNode(Rest) → 파티 전원 회복(비율 미정, 초안 30%) 연출 + Map 복귀
- v1은 확인 팝업 하나 (선택지 확장은 후속)

## Task 6: 점감 부활

**수정:** `CharacterStats`(RevivalStock=3), `Character`/`CombatManager` 사망 처리
- 전투 중 사망 → 리타이어(타일 제거, 주사위 인원 감소, 오브젝트 비활성)
- 전투 종료 시 리타이어 캐릭터 부활: 스톡 차감 + HP 75/50/25%
- 스톡 0에서 사망 → 즉시 GameOver
- 로스터/정보 패널에 부활 핍 표시

## Task 7: 통합 검증 + 문서

- 풀런 테스트: 시작 2명 → 노드1·2 전투+모집 → 분기 선택 → 휴식/엘리트 → 보스 → Victory
- `Docs/` 시스템 문서 갱신

**태스크당 커밋. Task 1+2 → 3 → 4가 크리티컬 패스, 5·6은 병렬 가능.**
