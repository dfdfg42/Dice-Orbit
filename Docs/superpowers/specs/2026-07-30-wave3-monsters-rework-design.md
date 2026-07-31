# Wave3 몬스터 재작업 설계 (눈사람·눈골렘·서리토템)

**날짜:** 2026-07-30
**범위:** 기존 Wave3 몬스터 3종을 새 버전 스펙으로 재작업. 재작업 배치의 3/3(마지막). Wave1·Wave2 완료.

> 관련: [[clean-design-preference]]. Wave1: [[2026-07-30-wave1-monsters-rework-design]], Wave2: [[2026-07-30-wave2-monsters-rework-design]].

## 1. 목표

눈 세트 3종을 새 스펙에 맞춘다. 이동불가(빙결) 중심 상호작용 강화, 눈골렘 조건부 AI, 서리토템 반응형 팀 방어. 유일한 신규 인프라는 **반응형 몬스터 패시브를 위한 파이프라인 확장 1곳**(서리 갑옷용).

## 2. 확정된 결정

- **파이프라인 확장(안전)**: `CombatPipeline.NotifyReactors`가 `CombatManager.ActiveMonsters`의 리액터도 수집. 기존 몬스터 패시브는 전부 owner 게이트라 방관 이벤트엔 no-op → 회귀 안전. *(사용자 확정)*
- **서리 갑옷 = 적중당 +10**(다대상 AoE면 여러 번 발동; 강하면 `armorAmount` 튜닝).
- **이동불가는 눈사람 패시브로 이동**(피격 적 다음 턴 이동불가). 진창눈은 순수 피해로 전환.
- **조건부 AI = 신규 `SnowGolemPattern`**(FlameGirlPattern류). 이동불가 적 유무로 눈강타/눈주먹 선택.
- **`FrostbiteDebuff`(동상 status)는 유지** — Wave4(양력/음력)가 재사용 중. 서리토템 패시브(FrostbitePassive)만 [서리 갑옷]으로 교체.
- 값: 진창눈 25/취소 20, 눈보라 25, 눈강타 ±3/25, 눈주먹 ±2/25, 이슬점 +3.

## 3. 몬스터별 정의 (현재 → 새 버전)

### 눈사람 (HP 60)
| 요소 | 현재 | 새 버전 |
|---|---|---|
| 패시브 [행복한 눈사람] `HappySnowmanPassive` | OnHit: 눈사람 피격 시 피해추적, 자기 공격 적중 시 아군 회복 | OnHit(source==owner, IsEffected): **피격 대상에 이동불가(FrozenDebuff, 2턴)**. 피해추적(Target==owner→SnowSet.AddDamageTaken)·TurnEnd 리셋 **유지**. 아군 회복 **제거** |
| 패턴1 [진창눈] `ThrowSnow` | 무작위 1명 이동불가 부여, 10↑ 피해 시 취소 | 무작위 1명 **25 피해**, `SnowSet.GetDamageTaken ≥ 20`이면 취소 (이동불가 제거) |
| 패턴2 [눈보라] `SnowStorm` | 무작위 2명 ±1, 20 | 무작위 2명 ±1, **25** |
| 패턴 | Sequential [눈보라, 진창눈] | **[진창눈 → 눈보라]** |

### 눈골렘 (HP 60)
| 요소 | 현재 | 새 버전 |
|---|---|---|
| 패시브 [눈감옥] `SnowGolemPassive` | 턴시작 무작위 ±1(3타일) 눈감옥, 턴종료 시 빙결 | **동일**(값 유지) |
| 패턴1 [눈강타] `SnowSmash` | 이동불가 적 우선 ±2, 20 | 이동불가 적 우선 **±3, 25** |
| 패턴2 [눈 방패→**눈 주먹**] `SnowShield`→`SnowFistSkill` | 전 아군 방어도 +10 | **무작위 1명 타일 ±2, 25 피해** |
| 패턴 | Sequential [눈방패, 눈강타] | **조건부 `SnowGolemPattern`**: 이동불가(Frozen) 적 있으면 눈강타, 없으면 눈주먹 |

### 서리토템 (HP 60)
| 요소 | 현재 | 새 버전 |
|---|---|---|
| 패시브 [동상→**서리 갑옷**] `FrostbitePassive`→`FrostArmorPassive` | 턴종료 시 미이동 적 +20% 받피(동상) | OnHit: **다른 아군 몬스터**(source가 owner 아닌 Monster)의 명중(IsEffected) 시 → 전 아군 방어도 +armorAmount |
| 패턴1 [서리꽃] `FrostFlower` | 무작위 2명 ±1, 20 | **동일** |
| 패턴2 [이슬점] `DewPoint` | 아군 전체 피해량 +2 영구 | 아군 전체 피해량 **+3** 영구 |
| 패턴 | Sequential [이슬점, 서리꽃] | **[서리꽃 → 이슬점]** |

## 4. 파이프라인 변경

`CombatPipeline.NotifyReactors`(리액터 수집부)에 섹션 추가:

```
// F. 활성 몬스터 전체에서 Reactor 수집 (반응형 몬스터 패시브 — 서리 갑옷 등)
foreach (var m in CombatManager.Instance.ActiveMonsters) CollectReactors(m, reactors);
```

`Distinct()`가 Source/Target 중복 제거. 이후 정렬·실행은 기존 그대로.

## 5. 신규/재사용 코드

**수정(코드)**
- `Combat/Pipeline/CombatPipeline.cs`: 몬스터 리액터 수집 추가.
- `Wave3/SnowMan/SnowMan.cs`: `HappySnowmanPassive`(이동불가 부여), `ThrowSnow`(진창눈 25+취소20), `SnowStorm`(25).
- `Wave3/SnowGolem/SnowGolem.cs`: `SnowSmash`(±3/25), `SnowShield`→`SnowFistSkill`(눈주먹), 신규 `SnowGolemPattern`(조건부).
- `Wave3/FrostTotem/FrostTotem.cs`: `FrostbitePassive`→`FrostArmorPassive`(서리 갑옷), `DewPoint`(+3). `FrostFlower`/`FrostbiteDebuff` 유지.

**재사용(신규 없음)**: `SnowSet`(피해추적·ExpandLR·OtherAliveMonsters), `SnowPrisonTileAttribute`, `FrozenDebuff`(이동불가), `MonsterAI` 조건부 패턴 관례, `AttackTiles/AttackUnits`.

**변경 없음(원칙)**: `CharacterStats`/`Character`/`Monster`/`MonsterStats`.

## 6. 데이터 흐름

```
플레이어 턴: 눈감옥 타일 위 턴종료 → 빙결(이동불가). 눈사람 피격 → SnowSet.AddDamageTaken
몬스터 턴:
  눈사람: 진창눈(받은피해 20↑면 취소, 아니면 25) → 눈보라 25. 명중 대상마다 이동불가(패시브)
  눈골렘: 이동불가 적 있으면 눈강타(±3/25) else 눈주먹(±2/25). 턴시작 눈감옥 3타일
  서리토템: 서리꽃(2명 ±1/20) → 이슬점(전 아군 +3 영구)
  * 다른 아군 몬스터 공격 적중 시마다 서리토템 [서리 갑옷] → 전 아군 방어도 +10 (파이프라인이 서리토템 패시브도 디스패치)
눈사람 TurnEnd: SnowSet.ResetDamageTaken
```

## 7. 범위 밖

- 재작업 배치 완료(Wave1~3). 이후 신규 몬스터/밸런스는 별도.
- 인카운터/밸런스 튜닝 — 에디터/프리셋 필드.

## 8. 영향 파일

**수정 코드**: `CombatPipeline.cs`, `SnowMan.cs`, `SnowGolem.cs`, `FrostTotem.cs`.
**에디터(MCP)**: `SnowMan.asset`(순서 [진창눈,눈보라]), `SnowGolem.asset`(SnowGolemPattern [눈강타,눈주먹]), `FrostTotem.asset`(순서 [서리꽃,이슬점] + StartingPassives=FrostArmorPassive).
**신규 파일**: 없음.

## 9. 검증

- **컴파일**: 콘솔 에러 0. **회귀**: 기존 몬스터/캐릭터 패시브 정상(방관 디스패치 추가 후에도 owner 게이트로 무영향).
- **플레이**(Wave3 전투):
  - 눈사람: 진창눈 25(받은 피해 20↑ 라운드엔 `취소`), 눈보라 25, 눈사람 명중 대상 다음 턴 이동불가.
  - 눈골렘: 이동불가 적 있으면 눈강타(±3/25), 없으면 눈주먹(±2/25). 눈감옥 3타일.
  - 서리토템: 서리꽃·이슬점(+3). **다른 아군 몬스터 공격 적중 시 전 아군 방어도 증가 로그**.

## 10. ★ 확인 필요한 해석

1. 서리 갑옷 = **적중당 +10**(다대상 AoE면 여러 번). 과하면 armorAmount 하향.
2. 이동불가(눈사람 패시브) 지속 **2턴**(다음 턴 커버).
3. 눈골렘 조건부 = 살아있는 캐릭터 중 Frozen 상태가 하나라도 있으면 눈강타.
4. `FrostbiteDebuff`(동상 status) 유지 — 패시브 클래스만 `FrostbitePassive`→`FrostArmorPassive` 교체(StartingPassives 재배선).
