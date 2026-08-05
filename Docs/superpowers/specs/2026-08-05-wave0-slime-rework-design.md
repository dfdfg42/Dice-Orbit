# Wave0 슬라임 세트 리워크 설계

**작성일:** 2026-08-05
**대상:** Wave0 슬라임 세트 (파란 슬라임 ×1 + 초록 슬라임 ×2) — 기획 개정 반영 + 불필요 코드 제거
**전제:** 기존 확장 시스템(StatusEffect/TileAttribute/MonsterAI) 재사용. 세트 전용 정적 트래커 최소화.

---

## 1. 개요 (개정 기획)

| 몬스터 | HP | 패시브 | 패턴1 | 패턴2 | 패턴 조건 |
|---|---|---|---|---|---|
| 파란 슬라임 | 20 | 없음 | [박치기] 무작위 1명 타일 ±2에 15 | [점액 분사] 설치된 점액 타일 ±1에 15 | 패턴1→패턴2 반복 |
| 초록 슬라임 ×2 | 20 | 없음 | [점액] 무작위 1타일에 점액 설치 | [박치기] (파란과 동일 AoE) | 패턴1 ONLY, 단 파란 슬라임 사망 시 패턴2 ONLY |

**점액 타일 효과(개정):** 해당 타일에서 턴 종료·통과 시 그 캐릭터에게 **다음 턴 -1 이동 감소(둔화)** 부여. 발동 후 삭제, 타일 중첩 불가, 몬스터 사망 후에도 유지, 웨이브 종료 시 삭제.

---

## 2. 현재 대비 변경점 (diff)

**신규**
- `EffectType.Slowed`(둔화) + `SlowStatus : StatusEffect` — 적용 시 `CharacterStats.MoveDebuff += value`, 만료 시 `-= value`. `FrozenDebuff`(BindDebuff 토글) 패턴 미러. `OrbitManager.MoveRoutine`이 이미 `steps + MoveBuff - MoveDebuff`(0 클램프)로 반영 → 자동 동작.
- 공유 스킬 `SlimeBodySlamSkill`([박치기] AoE — 무작위 1명 타일 ±2, 15) · `SlimePlantSkill`([점액] 무작위 1타일 설치, `EnsureWaveHook` 호출).
- 공유 조건부 AI `SlimeGreenPattern` — `MonsterName=="파란 슬라임"` 생존 시 `availableSkills[0]`([점액]), 사망 시 `availableSkills[1]`([박치기]).

**변경**
- `SlimeTile`: 쇠약(`WeakStatus`) → **둔화(`SlowStatus`, -1 이동, 다음 턴)**. 나머지 동작 동일.
- `SlimeSet`: 피해 누적 추적(`DamageTakenThisRound`/Add/Get/Reset) **제거**. `IsBlueSlimeAlive()` 헬퍼 추가. `EnsureWaveHook` + 타일 정리(`OnCombatStart`)만 유지.
- 파란 슬라임 AI: `Sequential[박치기, 점액분사]` (순서 동일, 스킬 내용만 교체).

**제거 (불필요 코드)**
- `PlantSlimePassive` (파란 패시브 — 점액 배치가 초록 스킬로 이동, 피해추적 없어짐)
- `BodySlamSkill` (옛 단일대상+취소 박치기 → 공유 AoE로 대체)
- `CorrosiveSlimeSkill` (초록 옛 스킬 → 박치기로 대체) → `GreenSlime.cs` 파일 삭제(고유 클래스 없어짐)
- `SlimeSet`의 피해추적 API 3종

---

## 3. 파일 구조

**수정(기존):**
- `.../Combat/EffectData.cs` — `EffectType.Slowed` 추가.
- `.../Combat/Effects/CommonStatuses.cs` — `SlowStatus` 추가.
- `.../UI/TooltipKeywordFormatter.cs` — 별칭 `"Slowed" → "둔화"`.
- `.../Wave0/Shared/SlimeTile.cs` — 쇠약 → 둔화.
- `.../Wave0/Shared/SlimeSet.cs` — 피해추적 제거 + `IsBlueSlimeAlive()`.
- `.../Wave0/BlueSlime/BlueSlime.cs` — `SlimeSpraySkill`만 남기고 나머지 제거.

**신규:**
- `.../Wave0/Shared/SlimeSkills.cs` — `SlimeBodySlamSkill`, `SlimePlantSkill`, `SlimeGreenPattern`.

**삭제:**
- `.../Wave0/GreenSlime/GreenSlime.cs` (+ .meta) — 고유 클래스 없음.

**프리셋 재배선(YAML 직접 편집, MCP 부재 시):**
- `BlueSlime.asset` — StartingPassives 제거. availableSkills = [`SlimeBodySlamSkill`(RandomCharacter/Tiles/range2), `SlimeSpraySkill`(TilesWithAttribute=Slime/range1)]. Sequential.
- `GreenSlime.asset` — availableSkills = [`SlimePlantSkill`(Self/None), `SlimeBodySlamSkill`(RandomCharacter/Tiles/range2)]. AIPattern = `SlimeGreenPattern`. 패시브 없음.
- `초록 슬라임 ×2`는 Act 인카운터 배치(사용자 몫, 프리셋 1개를 2번).

---

## 4. 결정 / 애매점

- **둔화 이름**: 툴팁 "둔화"(`Slowed`). `SlushSnow`(진창눈) enum은 구현체 없는 사문이라 재사용 안 함(의미도 눈-전용).
- **둔화 지속**: "다음 턴" — `FrozenDebuff`의 다음-턴 컨벤션(값 2) 미러, 구현 시 확정.
- **점액 배치 1타일**: 초록 [점액] 스킬이 매 사용 시 무작위 1타일(이미 점액 아닌 타일)에 설치.
- **박치기 AoE 공유**: 파란·초록 동일 `SlimeBodySlamSkill` 재사용.
- **파란 생존 판정**: `CombatManager.GetAliveMonsters()`에서 `MonsterName=="파란 슬라임"` 존재 여부.

## 5. 검증

- 컴파일: Unity MCP 복구 시 `AssetDatabase.Refresh` + `GetConsoleLogs` Error + 타입 인스턴스화. (헤드리스 compile-refs 미설치.) 또는 사용자가 Unity에서 확인.
- 프리셋 직렬화: `.asset` Read로 확인 (crystal 프리셋 검증과 동일).
- 인카운터 배치·플레이(둔화 실제 적용, 조건부 AI, 점액 타일 지속): 사용자.
