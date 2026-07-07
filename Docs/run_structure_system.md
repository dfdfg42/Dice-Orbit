# 런 구조 시스템 (노드맵)

> 구현: 2026-07-07, 브랜치 `feature/run-structure-node-map-20260707`
> 기획 스펙: `Docs/superpowers/specs/2026-07-07-run-structure-node-map-design.md` (의도의 진실 소스)
> 이 문서는 **구현 구조**의 진실 소스.

## 한 장 요약

```
메인메뉴 → Recruit(2명 찰 때까지 반복) → RunManager.StartRun() → Map
                                                                  │
   ┌──────────────────────────────────────────────────────────────┘
   ▼
 NodeMapUI에서 노드 클릭 → GameFlowManager.OnNodeSelected(id)
   ├─ Battle/Elite/Boss → Combat → WaveManager.StartEncounter(node.WaveIndex+1)
   │      └─ 클리어 → PartyManager.ReviveRetiredMembers() (점감 부활)
   │              ├─ Boss였으면 → Victory
   │              └─ 아니면 → Reward → (전투 1·2 클리어 후엔 Recruit) → Map
   ├─ Rest  → 파티 30% 즉시 회복 → Map 유지 (Rebuild)
   └─ Shop/Event → v1 스텁 (로그만) → Map 유지
```

## 파일 지도

| 파일 | 역할 |
|---|---|
| `Core/Run/MapNodeType.cs` | 노드 6종 enum |
| `Core/Run/MapGraph.cs` | MapNode(층/레인/타입/WaveIndex/개조예고/Next) + MapGraph |
| `Core/Run/ActDefinition.cs` | **막 = 에셋** (SO): 층 수, 보장 규칙, WaveDatabase, 웨이브 매핑 |
| `Core/Run/MapGenerator.cs` | ActDefinition → MapGraph. 비례 창 매핑 간선 |
| `Core/Run/RunManager.cs` | 런 상태 단일 출처 (씬 배치 + firstAct 지정 필수) |
| `Core/GameFlowManager.cs` | 상태머신 — 노드 타입별 라우팅 |
| `Core/Stage/BattleStage/.../WaveManager.cs` | **전투 1회 실행기** (StartEncounter). 순차 진행 API는 철거됨 |
| `UI/NodeMapUI.cs` | 가로 진행형 맵 화면 (에디터 소유 + 런타임 폴백) |

## 핵심 설계 결정

**① 진행의 권위는 RunManager 하나.** WaveManager는 "다음 전투가 뭔지" 모른다 —
`StartEncounter(waveNumber)`를 받아 스폰+전멸 감지만 한다. `OnWaveStart(int)` 등
이벤트 시그니처는 유지되어 패시브/배경/몬스터 구독자는 무수정.

**② 막 = 데이터.** 2막 추가 = ActDefinition 에셋 하나 + (GameFlow의 보스 클리어 분기에
"다음 막" 처리 추가 — 현재 v1은 단일 막 → Victory).

**③ 간선 비례 창 매핑.** 층 n개 → m개 연결 시 노드 i는 `[i*m/n, ((i+1)*m-1)/n]` 범위와 연결.
교차 없음 + 모든 노드 진입/진출 보장이 공식 하나로 나온다.

**④ 점감 부활 = 리타이어 모델.** 사망 시 파괴하지 않고 `gameObject.SetActive(false)`.
타일 점유/타게팅/주사위 배분이 전부 "활성 캐릭터 순회" 기반이라 특수 분기 없이 자연 제외된다.
부활 HP = `RevivalStock × 25%` (3→75, 2→50, 1→25), 스톡 0에서 사망 = `EndCombat(false)` 한 경로로 게임오버.
전멸도 게임오버 (부활은 승리한 전투 후에만).

## 씬 요구사항

- `RunManager` 컴포넌트 + `First Act` 지정 (`Assets/Scripts/Data/Run/Act.asset`)
- Act 에셋의 `Wave Database` 지정
- NodeMapUI는 씬에 없으면 런타임 폴백으로 생성됨 (스타일링하려면 배치 + [기본 레이아웃 생성])
- RunManager가 없는 씬은 디버그 폴백 (배치된 몬스터로 전투만)

## 확장 지점 (후속 계획)

- 상점 노드: `GameFlowManager.OnNodeSelected`의 Shop 분기 — 포션/교체(부활 스톡 리셋)/유물
- 이벤트 노드: Event 분기 — 주사위 도박 이벤트
- 주사위 개조: 보상 화면 별도 줄 (`MapNode.DiceModReward` 예고는 이미 맵에 표시됨)
- 다막: 보스 클리어 분기에서 다음 ActDefinition으로 `StartRun`
- 부활 스톡 회복: 유물/이벤트 전용 (희소성 원칙 — 스펙 §4)
