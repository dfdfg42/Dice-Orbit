# Wave5 불꽃 세트 몬스터 설계 (Set B)

**날짜:** 2026-07-30
**브랜치:** feature/event-outcomes-refactor-20260729
**범위:** Wave5 "불꽃" 세트 4종 (불꽃 소녀[보스급]·요정·인형·오르골) + 신규 **불꽃 타일 시스템**.

> 전체 몬스터 스펙 중 Set B. Set A(Wave4 태양/달)는 [[2026-07-30-wave4-sun-moon-monsters-design]] 참조. 설계 원칙은 [[clean-design-preference]](일반 클래스 필드/정적 전역 대신 기존 확장 시스템 재사용).

## 1. 목표

불꽃 타일을 축으로 상호작용하는 4종 세트. 여러 몹이 **불꽃 타일**을 깔고, 보스는 그 개수에 따라 강해지며(방어도·범위, 10개↑에서 대화재), HP 50%에서 대량 설치. 요정/오르골은 지속 설치·강화, 인형은 보스 보호. 불꽃 타일은 캐릭터에게 턴종료 시 피해를 주고 통과로 소화된다.

## 2. 확정된 결정

- **세트 식별 = `MonsterFaction.Flame`** (이미 enum 존재). 보스(불꽃 소녀)는 **`MonsterName == "불꽃 소녀"`** 로 식별(신규 필드 없음). [불의 가호]/[불꽃 방패]/[타오르는 열기]가 보스를 참조.
- **불꽃 타일 = 신규 `TileAttribute`**. 설치원(발화/불장난/불의노래/소각) 무관 **모든 불꽃 타일 동일 효과**: 턴종료 시 그 위 캐릭터에 35 피해 / 캐릭터 통과 시 삭제 / 타일당 1개(중첩불가) / 영구(몬스터 사망해도 유지). *(사용자 확정)*
- **통과-소화 캡(캐릭터당 턴당 1개) = 기존 StatusEffect 시스템의 1턴 마커 상태**로 구현. **`CharacterStats`/`Character`에 새 필드 없음, 정적 헬퍼 없음.** *(사용자 확정 — [[clean-design-preference]])*
- **불짚이기 취소 = 이번 턴 누적 피해 기준**. 요정이 이번 턴 받은 누적 피해가 25 이상이면 취소. 추적도 **새 필드 없이 기존 상태효과에 얹음**(§5 요정). *(사용자 확정)*
- **설치/개수는 기존 API 직접 사용**: `tile.AddAttribute(new FireTile())`, `orbitManager.Tiles.Count(t => t.HasAttribute(Flame))`. 정적 `FireBoard` 래퍼 **안 만듦**.
- **조건부 AI는 보스 1종만** 신규 패턴(`FlameGirlPattern`). 요정=Random, 인형/오르골=Sequential(기존 재사용).
- **HP 임계/1회 발동·범위 티어 등 몹별 런타임 상태는 해당 패시브 인스턴스가 자체 보유**(예: 소각 발동 플래그) — 일반 클래스 오염 없음.

## 3. 불꽃 타일 시스템 (토대)

| 요소 | 구현 |
|---|---|
| `TileAttributeType.Flame` | enum 값 추가 (`TileAttribute.cs`) |
| `FireTile : TileAttribute` | `base(Flame, value=35, duration=-1, isStackable=false)`. **OnEndTurn(char)** → 그 캐릭터에 35 피해(`AttackContext`+`CombatPipeline` — 방어도/디버프 정상 적용). **OnTraverse(char)** → char에 소화 마커 상태가 없으면 → `Owner`(타일)에서 이 속성 제거 + char에 마커 부여 ; 있으면 아무것도 안 함. |
| 소화 마커 상태 | `EffectType.FireExtinguishMark`(신규 값) + 마커 `StatusEffect`(지속 1턴, 전투 효과 없음, 존재 여부만). `character.StatusEffects`에 부여, 턴 경계 자동 소멸. `HasEffect(EffectType)` 로 조회. |
| 타일 속성 제거 | 통과-소화 시 `TileData`에서 Flame 속성 제거(기존 제거 API 사용/필요 시 `RemoveAttribute(type)` 확인). UI 버블 갱신 포함. |
| 설치 | 각 스킬/패시브가 대상 타일들에 `tile.AddAttribute(new FireTile())` 직접(이미 Flame이면 skip). |
| 개수 | `GameManager.Instance.GetOrbitManager().Tiles.Count(t => t != null && t.HasAttribute(TileAttributeType.Flame))` — 인라인/작은 내부 유틸. |
| 영구성 | duration -1 이라 몬스터가 죽어도 타일에 잔존(자동). |

**주의:** 불꽃 타일은 **캐릭터(파티)만** 영향(몬스터는 궤도 타일에 안 섬). EventTiles 주석대로 타일 훅은 Character 전용.

## 4. 신규/재사용 코드 요약

**신규 (공용 — `Wave5/Shared/`)**
- `FireTile.cs` : `FireTile : TileAttribute` + 소화 마커 `StatusEffect`.
- `FlameGirlPattern.cs` : 보스 조건부 AI(`MonsterAI` 파생).
- 공용 스킬 (재사용): `FlameTileDamageSkill`(무작위 대상 타일 ±R에 D 피해 = FrostFlower류, 불똥별/화염폭발 공용), `PlaceFireSkill`(대상 타일에 불꽃 설치 = 발화).
- 보스/세트 지원 스킬: `FireballSkill`(±2, n≥7이면 ±3, 35), `ConflagrationSkill`(대화재), `FlameShieldSkill`(보스+자신 방어도), `FlameCurseSkill`(무작위 6타일 30), `BurningHeatSkill`(보스+무작위 아군 피해+3), `KindlingSkill`(불짚이기).
- 패시브: `FlameStagePassive`(타오르는 무대), `IncinerationPassive`(소각), `PlayingWithFirePassive`(불장난), `FlameGraceGuardPassive`(불의 가호)+`FlameGuardStatus`, `FlameSongPassive`(불의 노래).

**재사용 (신규 코드 없음)**
- `TileAttribute` 훅(OnTraverse/OnEndTurn), `TileData.AddAttribute/HasAttribute`, `OrbitManager.Tiles`.
- `StatusEffect`(마커·피해감소·피해누적), `BuffAttackStatus`(영구 +피해), `Stats.TempArmor`, `Unit.Heal`.
- `MonsterSkill` 타깃팅(RandomCharacter/Tiles/count/range/Custom), `AttackIntent.ExpandTilesRange`, `AttackTiles`.
- `SequentialPattern`/`RandomPattern`, `MonsterPreset`/`Monster.Faction`.
- `CombatManager`(ActiveMonsters/TurnCount), `Monster.HPRatio`(`Stats.HPRatio`).

## 5. 몬스터별 정의

### 불꽃 소녀 (보스, HP 100, Flame)
- 패시브 **[타오르는 무대]** (`FlameStagePassive`): 턴시작(자신). `n = 불꽃타일 개수`. `n≥4` → `Stats.TempArmor += n*3`. `n≥7` → 보스 피해 스킬 사거리 +1 활성(범위는 스킬이 n 읽어 확장 — 아래 화염구). (10+ 포함 n≥7 규칙. ★)
- 패시브 **[소각]** (`IncinerationPassive`): 턴시작(자신). `미발동 && Stats.HPRatio ≤ 0.5` → 무작위 8타일에 불꽃 설치, **자체 플래그 set(1회)**.
- 패턴1 **[발화]** (`PlaceFireSkill`): MonsterSkill RandomCharacter+Tiles+count1+range1 → 대상 타일들에 불꽃 설치(피해X).
- 패턴2 **[화염구]** (`FireballSkill`): 무작위 1명 타일 ±(2 + (n≥7 ? 1 : 0)) → **35 피해**. 동적 범위는 `GetCustomTiles`에서 n 읽어 계산(Custom 타깃).
- 패턴3 **[대화재]** (`ConflagrationSkill`): 모든 불꽃 타일 삭제 + **모든 타일에 35 피해**.
- AI **`FlameGirlPattern`**: `n≥10` → 대화재 ; else 랜덤(발화 50% / 화염구 50%).

### 불꽃 요정 (HP 80, Flame)
- 패시브 **[불장난]** (`PlayingWithFirePassive`): 턴시작(자신) → 무작위 2타일에 불꽃 설치.
- 패턴1 **[불똥별]** (`FlameTileDamageSkill`): 무작위 1명 타일 ±2 → 30 피해 (RandomCharacter+Tiles+count1+range2).
- 패턴2 **[불짚이기]** (`KindlingSkill`): **이번 턴 요정이 받은 누적 피해 ≥ 25면 취소(no-op)**, 아니면 무작위 1명에게 30 피해.
  - **피해 추적(새 필드 없이)**: 요정에 "이번 턴 받은 피해 누적" 상태효과(`OnAttack`에서 `Target==Owner`일 때 `Value += 피해`, 턴시작에 0으로 갱신/재부여)를 붙여 `KindlingSkill`이 그 Value를 조회. 상태 시스템 재사용, `Monster`/`Stats`에 필드 추가 없음. (★ 검토)
- AI **RandomPattern** [불똥별 50% / 불짚이기 50%].

### 불꽃 인형 (HP 80, Flame)
- 패시브 **[불의 가호]** (`FlameGraceGuardPassive`): 턴시작(자신). 보스(불꽃 소녀) 생존 시 보스에 **`FlameGuardStatus`(1턴 갱신)** 부여. 상태 자체가 게이트: `OnCalculateOutput && Target==보스 && 보스.Stats.TempArmor>0` → `OutputValue *= 0.8`(받는 피해 -20%). 인형이 죽으면 갱신이 끊겨 다음 턴 소멸.
- 패턴1 **[불꽃 방패]** (`FlameShieldSkill`): 보스 + 자신(인형)에 `Stats.TempArmor += 10`. (진영 전체 아님. ★)
- 패턴2 **[불의 저주]** (`FlameCurseSkill`): 무작위 6타일 → 30 피해.
- AI **SequentialPattern** [불꽃 방패 → 불의 저주] 반복.

### 불꽃 오르골 (HP 80, Flame)
- 패시브 **[불의 노래]** (`FlameSongPassive`): 턴시작(자신). 무작위 **기존** 불꽃 타일 하나를 골라 그 좌우 ±1(2타일)에 불꽃 설치. 불꽃 없으면 skip.
- 패턴1 **[화염 폭발]** (`FlameTileDamageSkill`): 무작위 1명 타일 ±2 → 30 피해.
- 패턴2 **[타오르는 열기]** (`BurningHeatSkill`): 보스 + 무작위 아군 1명에 `BuffAttackStatus(3, -1){IsStackable=true}` (피해량 +3 **영구**. ★).
- AI **SequentialPattern** [화염 폭발 → 타오르는 열기] 반복.

## 6. 데이터 흐름 (매 턴)

```
플레이어 턴: 캐릭터 이동 시 통과 타일 OnTraverse →
  불꽃 타일 & 소화마커 없음 → 불끄기(속성 제거) + 마커 부여(1턴)
캐릭터 턴종료: 서있는 불꽃 타일 OnEndTurn → 35 피해
몬스터 턴시작(각 몹 패시브):
  소녀: 타오르는 무대(개수→방어도/범위), 소각(HP50%↓ 1회 8설치)
  요정: 불장난(2설치)  오르골: 불의 노래(기존 불꽃 주변 2설치)  인형: 불의 가호(보스에 -20% 상태)
몬스터 행동(AIPattern.GetNextSkill):
  소녀: 불꽃≥10 대화재 / else 랜덤(발화·화염구)
  요정: 랜덤(불똥별·불짚이기[누적25↑ 취소])  인형: 방패→저주  오르골: 폭발→열기
```

## 7. ★ 확인 필요한 해석 (스펙 명시가 애매 — 제 판단)

1. **[불꽃 방패] "불꽃 소녀 및 자신"** = 보스 + 인형 본인만 방어도(진영 전체 아님).
2. **가호/버프 지속**: [타오르는 열기]·가호류 +피해 = **영구 누적**(Wave4 가호처럼). [불의 가호] -20%는 1턴 갱신(armor 게이트).
3. **[타오르는 무대] 티어**: 방어도 `n≥4`부터 `n×3`. 사거리 +1은 `n≥7`(10+ 포함, 이후 대화재가 정리).
4. **[불짚이기] 피해 추적 방식**: 상태효과 누적으로 구현(위). 다른 선호 있으면 조정.
5. **[불의 노래]** 기존 불꽃 없으면 그 턴 skip(무작위 설치로 대체 안 함).

## 8. 범위 밖

- Wave6+ 및 다른 세트.
- 인카운터/맵 배정 튜닝(ActDefinition 티어) — 에디터 셋업(사용자).
- 불꽃 타일 전용 VFX/아이콘 폴리시 — 기본 표시로 시작.
- 밸런스 수치 정밀 튜닝(플레이 후 프리셋에서).

## 9. 영향 파일

**신규**
- `Core/.../Tile/`(또는 `Wave5/Shared/`): `FireTile.cs`(FireTile + 소화 마커 StatusEffect)
- `Wave5/Shared/`: `FlameGirlPattern.cs`, 공용/세트 스킬·패시브(§4)
- `Wave5/{FlameGirl,FlameFairy,FlameDoll,FlameMusicBox}/`: 각 몹 스킬/패시브 + 프리셋 에셋

**수정**
- `TileAttribute.cs`(`TileAttributeType.Flame`), `EffectData.cs`(`EffectType.FireExtinguishMark` 등)
- (필요 시) `TileData.cs`에 속성 제거 API 확인/추가

**신규 필드 없음**: `CharacterStats`/`Character`/`Monster`/`MonsterStats` 변경 없음(상태효과·패시브 인스턴스 상태로 대체).

**에디터 셋업(코드 밖)**
- 프리셋 4종: Faction=Flame, HP(소녀100/나머지80), AIPattern(스킬+타깃), StartingPassives, 값.
- Wave5 EncounterDefinition(4종)·ActDefinition 배정.

## 10. 검증

- **컴파일**: 콘솔 에러 0.
- **플레이** (Wave5 전투):
  - 불꽃 타일: 캐릭터가 위에서 턴종료 시 35 피해 / 통과하면 삭제(같은 이동에서 1개만) / 타일당 1개.
  - 소녀: 불꽃 4+개 방어도(n×3), 7+개 화염구 ±3, 10+개 대화재(전 타일 삭제+35), HP50%↓ 시 소각 8개(1회).
  - 요정: 매턴 2설치, 불짚이기가 이번 턴 25↑ 피해 시 취소.
  - 인형: 보스 방어도 있을 때 보스 받는 피해 -20%, 방패 보스+자신 +10, 저주 6타일 30.
  - 오르골: 기존 불꽃 주변 2설치, 열기로 보스+아군 피해 +3.
  - 로그로 교차 확인(각 패시브/스킬 Debug.Log).
- **회귀**: 기존 몬스터/전투 정상, Faction=None/타 진영 몹 불꽃 로직 무시, 다른 타일 속성 정상.
