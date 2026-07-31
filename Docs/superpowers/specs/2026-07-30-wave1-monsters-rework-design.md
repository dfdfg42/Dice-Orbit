# Wave1 몬스터 재작업 설계 (고블린·해골병사)

**날짜:** 2026-07-30
**브랜치:** (신규 예정) feature/wave1-rework
**범위:** 기존 Wave1 몬스터 2종(고블린, 해골병사)을 새 버전 스펙으로 재작업. Wave2·Wave3는 별도 사이클.

> 기존 몬스터 재작업 배치의 1/3. 관련: [[clean-design-preference]](일반 클래스 필드/정적 대신 기존 확장 시스템 재사용).

## 1. 목표

기존 Wave1 두 몬스터를 새 스펙에 맞춘다. 대부분 **값/시점/순서 조정**이며, 유일한 신규 메커닉은 해골병사 [뼈 화살]의 **"이번 라운드 뼈무덤 발동 시 취소"** 하나 — 기존 상태효과 시스템의 마커로 구현한다.

## 2. 확정된 결정

- **재작업 중심**: 기존 파일(Goblin.cs / Skeleton.cs / BoneTile.cs / 프리셋)을 수정. 새 파일 최소화.
- **취소 메커닉 = 상태효과 마커**: `EffectType.BoneMark` + `BoneMarkStatus`. `BoneTile.Activate`가 해골병사에 방어도 부여 시 마커도 부여 → `BoneArrowSkill`이 `HasEffect(BoneMark)` 검사 → 있으면 취소. **새 필드/정적 없음.**
- **마커 리셋 = 해골병사 TurnEnd**(실행 이후). 진창눈(SnowSet)·불짚이기(FireDamageTaken)와 동일한 "이번 라운드" 창.
- **지뢰 설치 시점 = 고블린 턴 종료(TurnEnd)** (현재 TurnStart에서 변경).
- 값 확정: 지뢰폭발 15, 뼈타일 4·10·16, 뼈 방어도 5, 뼈화살 15.

## 3. 몬스터별 정의 (현재 → 새 버전)

### 고블린 (HP 30)
| 요소 | 현재 | 새 버전 |
|---|---|---|
| 패시브 [지뢰 설치] `PlantMinePassive` | **턴 시작**에 무작위 2타일 지뢰(20) | **턴 종료**에 무작위 2타일 지뢰(20). 통과·턴종료 발동 후 삭제·중첩·사망 유지·웨이브 종료 삭제 — 유지 |
| 패턴1 [몽둥이질] `GoblinClubSwing` | 무작위 1명 타일 ±2, 15 | 동일 |
| 패턴2 [지뢰 폭발] `MineBombSkill` | 지뢰 타일 + ±1, **10**, 발동 후 지뢰 삭제 | 지뢰 타일 + ±1, **15**, 발동 후 지뢰 삭제 |
| 패턴 | Sequential [몽둥이질 → 지뢰폭발] | 동일 |

### 해골병사 (HP 30)
| 요소 | 현재 | 새 버전 |
|---|---|---|
| 패시브 [뼈 무덤] `PlantBonePassive` | 전투 시작 시 **4·9·14·19** 타일에 뼈, 방어도 **10**, 통과·턴종료 시 해골병사에 부여·영구·사망 시 삭제 | 전투 시작 시 **4·10·16** 타일에 뼈, 방어도 **5**, 나머지 유지. + **해골병사 TurnEnd에 BoneMark 리셋** |
| 패턴1 [뼈 검] `SkeletonWhip` | 무작위 1명 타일 ±2, 15 | 동일(표시명 "뼈 검") |
| 패턴2 [뼈 화살] (신규 `BoneArrowSkill`) | (현재 [칼슘 충전] 자버프 — **폐기**) | 무작위 1명 **15 피해**, 단 **이번 라운드 뼈무덤 발동 시 취소** |
| 패턴 | Sequential [뼈검 → 칼슘충전] | **Random 50/50** [뼈검 / 뼈화살] |

## 4. 신규/재사용 코드

**신규**
- `EffectType.BoneMark`(EffectData.cs에 값 추가) + `BoneMarkStatus : StatusEffect`(마커, 전투 효과 없음) — Skeleton.cs 안 `DiceOrbit.Systems.Effects` 블록.
- `BoneArrowSkill : SkillData`(무작위 1명 피해 + 마커 있으면 취소) — Skeleton.cs.

**수정**
- `Goblin.cs`: `PlantMinePassive` 훅 TurnStart→TurnEnd(OnPostAction); `MineBombSkill` 피해 기본값 15.
- `Skeleton.cs`: `PlantBonePassive` 뼈 타일 인덱스 4·10·16·방어도 5·TurnEnd 마커 리셋; `CalciumChargeSkill` 제거→`BoneArrowSkill` 추가; `SkeletonWhip` 표시명 정리.
- `BoneTile.cs`: `Activate`에서 방어도 부여 후 수혜 몬스터에 `BoneMarkStatus` 부여.
- `EffectData.cs`: `BoneMark` enum 값.

**재사용 (신규 없음)**: `RandMineTile`(지뢰), `BoneTile`(뼈), `MineFieldCleaner`(웨이브 시작 지뢰 정리), `SkeletonDeath`(사망 시 뼈 삭제), `AttackTiles`/`AttackUnits`, `SequentialPattern`/`RandomPattern`, `StatusEffectManager.HasEffect/RemoveEffect`, 몹 TurnStart/TurnEnd 훅.

**변경 없음(원칙)**: `CharacterStats`/`Character`/`Monster`/`MonsterStats`.

## 5. 데이터 흐름 (해골병사 뼈 화살)

```
전투 시작: 뼈무덤 → 4·10·16 타일에 뼈(방어도5)
플레이어 턴: 캐릭터가 뼈 타일 통과/턴종료 → 해골병사 방어도 +5 + 해골병사에 BoneMark 부여
몬스터 턴(해골병사 행동): Random 50/50
  - 뼈 검: 무작위 1명 타일 ±2, 15
  - 뼈 화살: HasEffect(BoneMark)면 취소, 아니면 무작위 1명 15
해골병사 TurnEnd: BoneMark 리셋(RemoveEffect) → 다음 라운드 새로 판정
```

## 6. 범위 밖

- **Wave2**(아기곰·엄마곰), **Wave3**(눈사람·눈골렘·서리토템) — 각각 별도 스펙/계획.
- 인카운터/맵 배정 튜닝 — 에디터(사용자).
- 밸런스 정밀 튜닝 — 프리셋 필드.

## 7. 영향 파일

**신규 코드**: 없음(모두 기존 파일에 추가). 
**수정**: `Wave1/Goblin/Goblin.cs`, `Wave1/Skeleton/Skeleton.cs`, `Tile/BoneTile.cs`(경로 확인), `Combat/EffectData.cs`.
**에디터(코드 밖)**: `Goblin.asset`(지뢰폭발 15), `Skeleton.asset`(뼈 방어도 5, AIPattern Random+뼈검/뼈화살) — MCP 배선(시리얼라이즈 필드/패턴은 SerializedObject).

## 8. 검증

- **컴파일**: 콘솔 에러 0.
- **플레이**(Wave1 전투):
  - 고블린: 매 **턴 종료**에 지뢰 2개 설치, 지뢰폭발 15, 몽둥이질 ±2/15.
  - 해골병사: 전투 시작 4·10·16 뼈, 통과 시 방어도 +5. 뼈검 ±2/15. **뼈 화살**: 뼈 발동한 라운드엔 `[뼈 화살] 취소` 로그, 아니면 15 피해. 패턴 50/50.
- **회귀**: 기존 지뢰/뼈 타일·웨이브 정리·사망 삭제 정상. 다른 몬스터 영향 없음.

## 9. ★ 확인 필요한 해석

1. 지뢰 "매턴 종료 시" = **고블린 자신의 턴 종료**(플레이어 턴 종료 아님).
2. 뼈 화살 취소 = **이번 라운드(직전 해골 행동 이후) 뼈 발동 여부**, 마커로 판정, 해골 TurnEnd 리셋.
3. `SkeletonWhip`(뼈 검) 표시명만 정리, 로직/수치 동일.
