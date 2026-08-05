# 눈 세트 리워크 + 공유 상태 리네이밍 설계

**작성일:** 2026-08-05
**대상:** (1) 범용 상태/타일 리네이밍, (2) Wave3 눈 세트(눈사람·눈골렘·서리토템) 리워크
**전제:** 기존 확장 시스템 재사용. 프리셋은 MCP(SerializedObject)로 재배선.

---

## Part 1 — 리네이밍 (범용 상태/타일)

눈-테마 이름이지만 여러 세트가 공유하는 3개를 범용 이름으로. 프리셋에 "이름"으로 참조되지 않아(상태는 런타임 생성, TileAttributeType은 int 직렬화 → 식별자만 변경) 코드 수정만으로 안전.

| 현재 | → 새 이름 | 의미 | 툴팁 |
|---|---|---|---|
| `FrozenDebuff` / `EffectType.Frozen` | `BindStatus` / `EffectType.Bound` | 이동 불가(BindDebuff) | 속박 |
| `FrostbiteDebuff` / `EffectType.Frostbite` | `VulnerableStatus` / `EffectType.Vulnerable` | 받는 피해 +% | 취약 |
| `TileAttributeType.SnowPrison` / `SnowPrisonTileAttribute` | `TileAttributeType.Bind` / `BindTileAttribute` | 속박 타일 | 속박 |

**사용처 교체:** 곰(HoneyPawTile), Wave4(TurnParityWeaknessPassive), 포션(NeutralizePotion), 눈 세트(SnowMan/SnowGolem). `EffectType.Frozen` 텍스트 참조도. 파일 `SnowPrisonTile.cs` → `BindTile.cs`(git mv). enum 순서(int) 유지. 툴팁: "Vulnerable"→"취약" 이미 존재, "Bound"→"속박" 추가, 죽은 "Frozen"/"Frostbite" 별칭 정리.

---

## Part 2 — 눈 세트 리워크

### 공유 신규 인프라
- **빙결 중첩** `FrostStackStatus` (`EffectType.FrostStack`, 툴팁 "빙결", 스택형). 대상 캐릭터 **턴 종료 시**: 중첩만큼 피해(파이프라인) → 중첩 −1 → 0이면 제거. 칩에 숫자.
- **빙결 타일** `FrostTile` (`TileAttributeType.Frost`). 그 위 턴 종료 시 `FrostStackStatus(5)` 부여 + 자기 삭제(SlimeTile 패턴). 중첩 가능, 몬스터 사망 후 유지, 웨이브 종료 정리.
- **최초 사망 감지**: `SnowSet`이 전투 시작 시 초기 몬스터 수 기록 → `HasAnyAllyDied()` = 현재 생존 < 초기.

### 몬스터 3종 (HP 40)
**눈사람** — Sequential[진창눈 → 눈보라]
- 패시브 `HappySnowmanPassive`: 피격 적에 `FrostStackStatus(+1)` (이동불가 제거). 진창눈 취소용 받은-피해 추적(`SnowDamageStatus`) 유지.
- [진창눈] `ThrowSnow`: 무작위 1명 20, 받은 피해 ≥10 취소(+의도선 제거 유지).
- [눈보라] `SnowStorm`: 무작위 2명 타일 ±1, 20.

**눈골렘** — 조건부
- [눈 방패] `SnowShieldSkill`(신규): 자신 제외 무작위 아군 일시 방어도 5.
- [눈 주먹] `SnowFistSkill`: 무작위 1명 타일 ±2, 20.
- AI: 눈방패 ONLY, 최초 아군 사망 시 눈주먹 ONLY.

**서리토템** — 조건부
- [빙결] `FrostPlantSkill`(신규): 무작위 4타일에 빙결 타일 설치.
- [서리 꽃] `FrostFlower`: 무작위 1명 타일 ±2, 20.
- AI: 빙결 ONLY, 최초 아군 사망 시 50/50[빙결·서리꽃].

### 제거 (눈 세트 전용 데드코드)
- 눈골렘: `SnowSmash`·`SnowGolemPassive`(눈감옥)·`SnowGolemDeath`·옛 `SnowGolemPattern`
- 서리토템: `DewPoint`(이슬점)·`FrostArmorPassive`(서리갑옷)
- 유지(공유): `BindStatus`(구 Frozen, 곰), `VulnerableStatus`(구 Frostbite, Wave4), `Bind` 타일/포션, `SnowDamageStatus`, `FrostFlower`

### 프리셋 재배선 (MCP)
- SnowMan.asset: Sequential[진창눈(dmg20), 눈보라(dmg20)] + HappySnowmanPassive.
- SnowGolem.asset: 조건부 패턴[눈방패, 눈주먹]. 패시브 없음.
- FrostTotem.asset: 조건부 패턴[빙결, 서리꽃]. 패시브 없음.

## 검증
- 리네이밍 후 컴파일(MCP). 눈 리워크 후 컴파일 + 프리셋 직렬화(Missing type). 콘솔 Error 0.
- 인카운터 배치·플레이(빙결 중첩 감소, 조건부 AI, 빙결 타일): 사용자.
