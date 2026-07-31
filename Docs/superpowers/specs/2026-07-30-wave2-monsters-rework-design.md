# Wave2 몬스터 재작업 설계 (아기곰·엄마곰)

**날짜:** 2026-07-30
**범위:** 기존 Wave2 몬스터 2종(아기곰, 엄마곰)을 새 버전 스펙으로 재작업. Wave1 완료, Wave3 별도.

> 기존 몬스터 재작업 배치의 2/3. 관련: [[clean-design-preference]]. Wave1: [[2026-07-30-wave1-monsters-rework-design]].

## 1. 목표

곰 세트 2종을 새 스펙에 맞춘다. 핵심은 **꿀 타일 상호작용 강화** — 아기곰은 공격 지점 주변 꿀로 딜을 올리고, 엄마곰은 꿀 근처 피격 적을 약화(쇠약)시키며 아기곰을 공격한 적을 보복한다. 신규 규칙은 기존 `BearPackTracker`·상태효과 시스템에 얹는다(일반 클래스 새 필드 없음).

## 2. 확정된 결정

- **"공격 범위 안 꿀" = 피격 대상 타일 ±2칸 꿀 개수** (다대상 공격 시 대상별 계산). *(사용자 확정)*
- **보호본능 취소 = 그 적이 이번 턴 꿀 타일 2개 이상 밟음** — 기존 `BearPackTracker` 턴별 밟기 카운트 재사용. *(사용자 확정)*
- **BearPackTracker 확장**(기존 곰 세트 공유 트래커): `HoneyTilesNear`, `LastBabyAttacker`, `GetHoneySteps` 추가. 새 정적 전역 만들지 않고 기존 트래커에 추가.
- **쇠약(-피해량) = 기존 `WeakStatus`**, **이동불가 = 기존 `FrozenDebuff`** 재사용.
- 쇠약 지속 = 다음 플레이어 턴을 덮도록 **2턴**(플레이에서 튜닝).
- 최근 공격자 없거나 사망 시 보호본능 **no-op**.

## 3. 몬스터별 정의 (현재 → 새 버전)

### 아기곰 (HP 50)
| 요소 | 현재 | 새 버전 |
|---|---|---|
| 패시브 [아기곰은 꿀을 좋아해] `HoneyLoverPassive` | OnAttack: `+BearPackTracker.HoneyEaten`(먹은 꿀 총량) | OnAttack(OnCalculateOutput, source==owner): **피격 대상 ±2칸 꿀 개수 ×3** 만큼 `OutputValue +=`. **추가**: 아기곰 피격 시(Target==owner) `BearPackTracker.SetLastBabyAttacker(공격자)` |
| 패턴1 [꿀 묻히기] `HoneyPawSkill` | 무작위 5타일 꿀, 2회복, 2개 밟기 이동불가 | 동일(값 확정 5/2/2). "혈당 스파이크"=이동불가(FrozenDebuff). 발동 후 삭제·사망 유지·웨이브 종료 삭제 유지 |
| 패턴2 [돌진] `BabyBearCharge` | 무작위 1명 타일 ±2, 20 | 동일 |
| 패턴 | Sequential [돌진 → 꿀묻히기] | **[꿀 묻히기 → 돌진]** |

### 엄마곰 (HP 50)
| 요소 | 현재 | 새 버전 |
|---|---|---|
| 패시브 [꿀 묻은 털→**꿀 묻은 발톱**] `HoneyFurPassive` | 꿀 5개↑면 엄마곰 받는 피해 -20% | OnAttack(OnHit, source==owner): **피격 대상 ±2칸 꿀>0**이면 그 대상에 **쇠약(다음턴 피해량 -20%, 2턴)** 부여 |
| 패턴1 [보호→**보호 본능**] `ProtectSkill` | 본인+아기곰 방어도 +10 | **`BearPackTracker.LastBabyAttacker`에게 20 피해**, 단 그 적이 **이번 턴 꿀 2개↑ 밟았으면 취소**. 최근 공격자 없음/사망 시 no-op |
| 패턴2 [곰은 사람을 찢어] `MommyBearTear` | 무작위 1명 타일 ±3, 20 | 동일 |
| 패턴 | Sequential [보호 → 찢어] | **[보호본능 → 찢어 → 찢어]** |

## 4. BearPackTracker 확장 (기존 트래커에 추가)

- `int HoneyTilesNear(int centerTileIndex, int radius)` — 중심 타일 ±radius 안 꿀 타일 개수(궤도 모듈로).
- `Character LastBabyAttacker` + `SetLastBabyAttacker(Character)` — 아기곰을 마지막으로 공격한 캐릭터. `Reset()`에서 null로.
- `int GetHoneySteps(Character, int turn)` — 해당 캐릭터가 그 턴에 밟은 꿀 수(읽기 전용; 기존 `HoneySteps` 딕셔너리 조회).

## 5. 신규/재사용 코드

**수정(코드)**
- `Wave2/BabyBear/BabyBear.cs`: `HoneyLoverPassive` 재작업(±2 꿀×3, 최근 공격자 기록).
- `Wave2/MommyBear/MommyBear.cs`: `HoneyFurPassive`→[꿀 묻은 발톱](쇠약 부여); `ProtectSkill`→[보호 본능](최근 공격자 피해/취소).
- `Wave2/BearPackTracker.cs`: `HoneyTilesNear`/`LastBabyAttacker`/`GetHoneySteps` 추가 + Reset 갱신.

**재사용(신규 없음)**: `HoneyPawTile`, `WeakStatus`(쇠약), `FrozenDebuff`(이동불가), `BearHelper.FindMonsterByName`, `AttackTiles/AttackUnits`, `SequentialPattern`, 패시브 OnAttack(OnCalculateOutput/OnHit).

**변경 없음(원칙)**: `CharacterStats`/`Character`/`Monster`/`MonsterStats`.

## 6. 데이터 흐름

```
플레이어 턴: 캐릭터가 아기곰 공격 → 아기곰 패시브 SetLastBabyAttacker(그 캐릭터)
           캐릭터가 꿀 타일 통과 → 회복 + HoneySteps++ (2개↑ 이동불가)
몬스터 턴:
  아기곰: 꿀묻히기(5타일) → 돌진(±2). 돌진 명중 시 대상 ±2 꿀×3 추가 피해
  엄마곰: 보호본능(최근 공격자, 이번턴 꿀2개면 취소/아니면 20) → 찢어 ±3 → 찢어.
          찢어/보호본능 명중 시 대상 ±2 꿀>0이면 대상에 쇠약(다음턴 -20%)
```

## 7. 범위 밖

- **Wave3**(눈사람·눈골렘·서리토템) — 별도 사이클.
- 인카운터/밸런스 튜닝 — 에디터/프리셋 필드.

## 8. 영향 파일

**수정 코드**: `BabyBear.cs`, `MommyBear.cs`, `BearPackTracker.cs`.
**에디터(MCP)**: `BabyBear.asset`(스킬 순서 [꿀묻히기, 돌진]·값), `MommyBear.asset`(패턴 [보호본능, 찢어, 찢어]).
**신규 파일**: 없음.

## 9. 검증

- **컴파일**: 콘솔 에러 0.
- **플레이**(Wave2 전투):
  - 아기곰: 꿀묻히기 5타일, 돌진이 꿀 밀집 지역 타격 시 피해 증가(±2 꿀×3), 아기곰 공격 후 그 적이 최근공격자로 기록.
  - 엄마곰: 찢어/보호본능 명중 대상 옆 꿀 있으면 다음턴 `쇠약` 아이콘, 보호본능이 아기곰 최근공격자 타격 — 그 적 이번턴 꿀 2개면 `[보호 본능] 취소` 로그.
  - 꿀 2개 밟으면 이동불가(혈당스파이크).
- **회귀**: 꿀 타일 웨이브 시작 정리, 다른 몬스터 정상.

## 10. ★ 확인 필요한 해석

1. 공격 범위 꿀 = **피격 대상 ±2칸**, 대상별 계산.
2. 보호본능 취소 = **이번 턴 꿀 2개↑ 밟기**(HoneySteps).
3. 쇠약 지속 **2턴**(다음 턴 커버), 최근 공격자 없음 → no-op.
4. `HoneyPawSkill` 값 5타일/회복2/바인드2 확정(현재와 동일 가정).
