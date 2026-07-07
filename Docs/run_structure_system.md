# 런 구조 시스템 (노드맵 · 경제 · 유물 · 포션)

> 구현: 2026-07-07, 브랜치 `feature/run-structure-node-map-20260707`
> 기획 스펙: `Docs/superpowers/specs/2026-07-07-run-structure-node-map-design.md` (의도의 진실 소스)
> 이 문서는 **구현 구조**의 진실 소스.

## 1. 전체 흐름

```
메인메뉴 → Recruit(2명 찰 때까지 반복) → RunManager.StartRun() → Map
                                                                  │
   ┌──────────────────────────────────────────────────────────────┘
   ▼
 NodeMapUI에서 노드 클릭 → GameFlowManager.OnNodeSelected(id)
   ├─ ⚔️ Battle/💀 Elite/👑 Boss → Combat → WaveManager.StartEncounter(node.WaveIndex+1)
   │      └─ 클리어 → PartyManager.ReviveRetiredMembers() (점감 부활)
   │              ├─ Boss였으면 → Victory
   │              ├─ Elite였으면 → 유물 랜덤 드랍 (RelicManager.GrantRandom)
   │              └─ Reward(골드+모디파이어 3택1+포션 드랍) → (전투 1·2 후엔 Recruit) → Map
   ├─ 🏕️ Rest  → 파티 30%(+유물 보너스) 즉시 회복 → Map 유지
   ├─ 🛒 Shop  → GameState.Shop → ShopUI (교체·포션·유물 구매) → 떠나기 → Map
   └─ 🎲 Event → GameState.Event → EventUI (주사위 도박) → 확인 → Map
```

상단 HUD(RunHudUI: 골드·유물·포션)는 Combat/Map/Shop/Event/Reward 동안 상시 표시.

## 2. 파일 지도

### 데이터/시스템 (`Assets/Scripts/Core/Run/`)
| 파일 | 역할 |
|---|---|
| `MapNodeType.cs` | 노드 6종 enum (Battle/Elite/Shop/Rest/Event/Boss) |
| `MapGraph.cs` | MapNode(층/레인/타입/WaveIndex/개조예고/Next) + 그래프 |
| `ActDefinition.cs` | **막 = 에셋**(SO): 층 수·보장 규칙 + **층 구간별 몹 세트 풀**(BattleTiers/ElitePool/BossPool, 노드마다 랜덤 배정 — 풀 비면 구 WaveDatabase 폴백) |
| `MapGenerator.cs` | ActDefinition → MapGraph (비례 창 매핑 간선, 시드 지원) |
| `RunManager.cs` | 런 상태 단일 출처: 맵/현재 노드/이동/전투 카운터/소멸 캐릭터 |
| `RelicDefinition.cs` | 유물 에셋: 규칙형 효과(enum+수치) + 전투 반응 효과(인라인) |
| `RelicCombatEffect.cs` | 유물의 파이프라인 리액터 베이스 (구 Artifact 계승) |
| `RelicManager.cs` | 유물 보유/풀/드랍/상점 진열 + 효과 질의 창구 |
| `PotionDefinition.cs` | 포션 에셋: 효과 enum+수치, 전투 전용 플래그 |
| `PotionManager.cs` | 포션 3슬롯 인벤토리 + 효과 실행 |

### UI (`Assets/Scripts/UI/`) — 전부 에디터 소유 + 런타임 폴백
| 파일 | 화면 |
|---|---|
| `NodeMapUI.cs` | 노드맵 (가로 진행형, 펠트 배경) |
| `ShopUI.cs` | 상점 (캐릭터 교체 3단계 + 포션/유물 진열대) |
| `EventUI.cs` | 주사위 도박 이벤트 (이벤트 목록은 Inspector 편집) |
| `RunHudUI.cs` | 상단 HUD (골드/유물 칩/포션 슬롯) |

### 개조된 기존 파일
| 파일 | 변경 |
|---|---|
| `GameFlowManager.cs` | 노드 라우팅 상태머신 (Map/Shop/Event 상태 추가) |
| `WaveManager.cs` | 순차 진행 철거 → `StartEncounter(waveNumber)` 실행기만 |
| `Character.cs` | 사망 = 리타이어 모델 + `Revive()` |
| `CharacterStats.cs` | `RevivalStock` (부활 스톡) |
| `PartyManager.cs` | `ReviveRetiredMembers()` |
| `DiceManager.cs` | `RerollAvailableDice()` (재굴림 물약) |
| `CombatPipeline.cs` | 유물 리액터 수집 D단계 → RelicManager |
| `RewardUI.cs` | 골드 유물 보너스 + 유물/포션 드랍 안내 라인 |

## 3. 핵심 설계 결정

**① 진행의 권위는 RunManager 하나.** WaveManager는 "다음 전투가 뭔지" 모른다 —
받은 웨이브 번호로 스폰+전멸 감지만. 이벤트 시그니처(`OnWaveStart(int)` 등)는 유지되어
기존 구독자(패시브/배경/몬스터) 무수정.

**② 막 = 데이터.** 2막 추가 = ActDefinition 에셋 하나 (+보스 클리어 분기에 다음 막 처리).

**③ 간선 비례 창 매핑.** 층 n→m개 연결 시 노드 i는 `[i*m/n, ((i+1)*m-1)/n]` 범위와 연결
— 교차 없음 + 전 노드 진입/진출 보장이 공식 하나.

**④ 점감 부활 = 리타이어 모델.** 사망 시 파괴하지 않고 `SetActive(false)`.
타일 점유/타게팅/주사위 배분이 "활성 캐릭터 순회" 기반이라 특수 분기 없이 자연 제외.
부활 HP = `RevivalStock × 25%` (3→75/2→50/1→25), 스톡 0에서 사망 = 영구사망 = 게임오버.
전멸도 게임오버 (부활은 승리한 전투 후에만).

**⑤ 수도꼭지 경제.** 모디파이어 = 전투 3택1 전용. 골드 = 전투산 → 상점(교체/포션/유물).
유물 = 엘리트 드랍 + 상점. "전투 없이 성장"은 구조적으로 불가.

**⑥ 유물 = 두 효과 유형의 통합** (구 Artifact 시스템 흡수, 2026-07):
- **규칙형(질의식)**: `RelicEffectType` enum + 수치. 소비처가 `RelicManager` 프로퍼티를 읽음
  (상점 할인/휴식 보너스/전투 골드/부활 HP/전투 시작 회복)
- **전투 반응형(리액터)**: `RelicCombatEffect` 서브클래스, CombatPipeline이 수집 (Priority 11)

## 4. 새 콘텐츠 만드는 법

### 새 유물 (규칙형)
1. `Create > DiceOrbit > Relic Definition` 에셋 생성
2. 이름/설명/아이콘 + `Effect Type` 선택 + `Value` 입력 (예: ShopDiscountPercent, 20)
3. 씬 `RelicManager`의 **Relic Pool**에 추가 → 엘리트 드랍/상점 진열 후보가 됨

### 새 유물 (전투 반응형)
1. `RelicCombatEffect` 상속 클래스 작성 — 훅(OnAttack/OnHeal/OnMove/OnTurnEvent)만 구현:
```csharp
[System.Serializable]
public class MyEffect : RelicCombatEffect
{
    public int power = 10;   // Inspector 노출
    public void OnAttack(CombatTrigger t, AttackContext c)
    {
        if (t != CombatTrigger.OnCalculateOutput) return;
        c.OutputValue += power;
    }
}
```
2. 컴파일 → 유물 에셋의 `Combat Effect` 드롭다운에 자동 등장 (`[SubclassPicker]` 드로어)
3. 규칙형 효과가 없으면 `Effect Type = None`

### 새 포션
`Create > DiceOrbit > Potion Definition` → 효과 enum/수치/가격/전투 전용 여부 →
씬 `PotionManager`의 **Potion Pool**에 추가. 새 효과 종류가 필요하면
`PotionEffectType` enum + `PotionManager.Execute` switch에 분기 추가.

### 새 이벤트
씬 `EventUI`의 **Gamble Events** 목록에 항목 추가 (제목/주사위 수/목표 합/보상/실패 피해).

### 2막
`Create > DiceOrbit > Act Definition` → 몬스터 웨이브 DB/층 구성 지정.
(현재 v1은 단일 막 → 보스 = 승리. 다막 전환은 GameFlow 보스 분기에서 확장)

## 5. 튜닝 포인트 (Inspector)

| 어디 | 뭐 |
|---|---|
| `Act.asset` | 층 수, 층당 노드 수, 엘리트 층, 상점/휴식/이벤트 개수, 개조 예고 수, **층 구간별 몹 세트 풀(BattleTiers)·엘리트/보스 풀** |
| `RunManager` | First Act, 시드(0=랜덤 — 고정하면 같은 맵 반복 테스트) |
| `GameFlowManager` | 시작 인원(2), 최대 인원(4), 자동 모집 전투 수(2), 휴식 회복률(30%) |
| `RewardUI` | 전투 골드(50), 포션 드랍 확률(20%) |
| `ShopUI` | 교체 기본가(60)/모디파이어당(30), 진열 수(포션2/유물2) |
| `EventUI` | 이벤트 목록 (내용 전부) |
| `RelicManager` | Relic Pool(획득 후보) / **Starting Relics(테스트용 즉시 보유)** |
| `PotionManager` | Potion Pool / Starting Potions |
| `CharacterStats` | RevivalStock (기본 3) |
| `RunHudUI` | 칩 크기, Chip Prefab(칩 모양 커스텀) |

⚠️ **Pool ≠ 보유**: Pool은 획득 "후보" 목록. HUD에 바로 띄우려면 Starting Relics/Potions에.
⚠️ Pool에 에셋을 하나라도 넣으면 기본 세트(런타임 생성)는 비활성화됨.

## 6. 씬 요구사항

- `RunManager` + First Act 지정 (필수 — 없으면 디버그 폴백)
- `RelicManager`/`PotionManager`: 커스텀 풀/시작 지급 쓰려면 배치 (없으면 런타임 생성 + 기본 세트)
- UI 4종(NodeMap/Shop/Event/RunHud): 스타일링하려면 배치 + [기본 레이아웃 생성], 없어도 폴백 동작
- ~~ArtifactManager / ArtifactPanelUI~~ — **철거됨**. 씬에 남은 오브젝트는 삭제할 것 (missing script)

## 7. 미구현 / 다음

- [ ] 주사위 개조 실지급 (맵 예고 배지만 있음 — 개조 데이터 구조부터)
- [ ] 다막 전환 (구조는 준비됨)
- [ ] 부활 스톡 회복 유물/이벤트 (희소성 원칙 하에)
- [ ] 포션 슬롯 확장 유물
- [ ] 이벤트 다양화 (도박 외 유형)
- [ ] main 머지 (battle-ui + run-structure 브랜치)
