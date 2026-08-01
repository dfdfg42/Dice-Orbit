# Wave0 슬라임 세트 몬스터 설계 (파란·초록x2)

**날짜:** 2026-07-31
**브랜치:** feature/wave0-slime-20260731
**범위:** Wave0 "슬라임" 세트 (파란 슬라임 1 + 초록 슬라임 2). Wave3 수정 세트는 별도 스펙.

> 신규 몬스터 2세트 중 Set A(슬라임). 관련: [[clean-design-preference]].

## 1. 목표

인트로 티어(HP 20)의 슬라임 세트. 파란 슬라임은 점액 타일을 깔아 밟은 캐릭터를 약화(쇠약)시키고 피격 시 박치기가 취소되며, 초록 슬라임 2마리는 단순 광역 딜러다. 신규 인프라는 **점액 타일 하나**뿐, 나머지는 기존 시스템 재사용.

## 2. 확정된 결정

- **점액 디버프 = 쇠약(`WeakStatus`, 가하는 피해 -20%), 지속 2턴.** *(사용자 확정)*
- **박치기 취소 = 이번 라운드 슬라임이 받은 누적 피해 ≥ 10** — `SnowSet` 식 세트 트래커(`SlimeSet`) 재사용 패턴. *(사용자 확정)*
- **점액 타일 = 신규 `TileAttribute`**: 통과/턴종료 시 그 캐릭터에 쇠약 부여 후 삭제, 중첩불가, 영구(몬스터 사망 유지), 웨이브 종료 시 정리.
- **초록 슬라임은 프리셋 1개**로 인카운터에 **2마리 배치**(동일 프리셋 재사용).
- 값: 모든 피해 15, HP 20, 점액 3타일/턴, 쇠약 20%/2턴, 박치기 취소 임계 10.

## 3. 몬스터별 정의

### 파란 슬라임 (HP 20, 세트 리더)
| 요소 | 구현 |
|---|---|
| 패시브 [점액] `PlantSlimePassive` | **턴 종료**(OnPostAction/TurnEnd/owner)에 무작위 3타일에 `SlimeTile` 설치. + 피격 추적: OnHit(Target==owner, IsEffected) → `SlimeSet.AddDamageTaken`. 턴 종료에 `SlimeSet.ResetDamageTaken`(설치와 동시). |
| 점액 타일 `SlimeTile` | OnTraverse/OnEndTurn(character) → 그 캐릭터에 `WeakStatus(20, 2)` 부여 후 `Owner.RemoveAttribute(this)`. 중첩불가(타일당 1개), duration -1, 웨이브 종료 시 `SlimeSet`가 정리. |
| 패턴1 [박치기] `BodySlamSkill` | 무작위 1명에게 15 피해. `SlimeSet.GetDamageTaken(slime) ≥ cancelThreshold(10)`이면 취소. (RandomCharacter + Characters) |
| 패턴2 [점액 분사] `SlimeSpraySkill` | 점액 타일 + 좌우 ±1에 15 피해. (TilesWithAttribute=Slime + range 1 → AttackTiles) |
| 패턴 | Sequential [박치기 → 점액 분사] |

### 초록 슬라임 ×2 (HP 20)
| 요소 | 구현 |
|---|---|
| 패시브 | 없음 |
| 패턴1 [부식성 점액] `CorrosiveSlimeSkill` | 무작위 1명이 속한 타일 + 좌우 ±2에 15 피해. (RandomCharacter + Tiles + range 2 → AttackTiles) |
| 패턴 | 패턴1 ONLY (Sequential, 스킬 1개) |

## 4. 신규/재사용 코드

**신규**
- `TileAttributeType.Slime`(TileAttribute.cs enum).
- `Wave0/Shared/SlimeTile.cs`: `SlimeTile : TileAttribute`.
- `Wave0/Shared/SlimeSet.cs`: `AddDamageTaken/GetDamageTaken/ResetDamageTaken`(Dictionary<Monster,int>) + `EnsureWaveHook`(OnCombatStart → 슬라임 타일 정리). SnowSet/MineFieldCleaner 패턴.
- `Wave0/BlueSlime/BlueSlime.cs`: `PlantSlimePassive`, `BodySlamSkill`, `SlimeSpraySkill`.
- `Wave0/GreenSlime/GreenSlime.cs`: `CorrosiveSlimeSkill`.

**재사용(신규 없음)**: `WeakStatus`(쇠약), `TileAttribute` 훅, `AttackTiles/AttackUnits`, `SequentialPattern`, 몹 OnHit/TurnEnd 훅.

**변경 없음(원칙)**: `CharacterStats`/`Character`/`Monster`.

## 5. 데이터 흐름

```
플레이어 턴: 캐릭터가 점액 타일 통과/턴종료 → 쇠약(가하는 피해 -20%, 2턴) + 타일 삭제
           캐릭터가 파란 슬라임 공격 → SlimeSet.AddDamageTaken
몬스터 턴:
  파란 슬라임: 박치기(이번 라운드 받은 피해 ≥10이면 취소, 아니면 15) → 점액 분사(점액 ±1, 15)
  파란 슬라임 턴종료: 무작위 3타일 점액 설치 + SlimeSet 리셋
  초록 슬라임 ×2: 부식성 점액(무작위 1명 ±2, 15)
```

## 6. 범위 밖

- **Wave3 수정 세트**(수정핵/수정석/수정파편) — 별도 스펙/계획.
- 인카운터/맵 배정 튜닝 — 에디터(사용자).

## 7. 영향 파일

**신규**: `Wave0/Shared/{SlimeTile,SlimeSet}.cs`, `Wave0/BlueSlime/BlueSlime.cs`, `Wave0/GreenSlime/GreenSlime.cs` + 프리셋 2개(파란·초록).
**수정**: `TileAttribute.cs`(Slime enum).
**에디터(MCP)**: 프리셋 2개 생성·배선(파란: Sequential [박치기,점액분사]+점액 패시브 / 초록: [부식성점액]), 인카운터에 파란1+초록2.

## 8. 검증

- **컴파일**: 콘솔 에러 0.
- **플레이**(Wave0 전투):
  - 파란: 매 턴종료 점액 3타일, 밟으면 쇠약(가하는 피해↓), 박치기(받은 피해 10↑ 라운드엔 `취소`), 점액 분사(점액 주변 15).
  - 초록x2: 부식성 점액 ±2/15.
- **회귀**: 점액 웨이브 시작 정리, 다른 몬스터/타일 정상.

## 9. ★ 확인 필요한 해석

1. 점액 디버프 = 쇠약(가하는 피해 -20%) 2턴.
2. 박치기 취소 = 이번 라운드 누적 피해 ≥10(SlimeSet).
3. 초록 슬라임 = 프리셋 1개, 인카운터에 2마리.
