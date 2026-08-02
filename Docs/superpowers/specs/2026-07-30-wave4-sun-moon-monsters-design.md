# Wave4 태양/달 세트 몬스터 설계 (Set A)

**날짜:** 2026-07-30
**브랜치:** feature/event-outcomes-refactor-20260729
**범위:** Wave4 "태양/달" 세트 4종 (태양의 기사·사제, 달의 기사·사제). Wave5 불꽃 세트는 별도 스펙.

> 전체 몬스터 스펙 중 Set A. 몬스터 시스템 분석은 대화 기록 참조. 관련: [[run_structure_system]], [[dice_deck_system]]는 무관.

## 1. 목표

턴 홀짝 리듬으로 상호작용하는 4종 세트 몬스터. **태양 유닛은 홀수턴, 달 유닛은 짝수턴**에 약해지고(자신 받는 피해 +30%), 같은 턴에 사제가 자기 진영을 영구 강화한다. 전부 HP 70.

## 2. 확정된 결정

- **세트 식별 = `MonsterFaction` enum** (`None/Sun/Moon/Flame`) 필드를 `MonsterPreset`에 추가. 런타임 `Monster.Faction` 노출. 가호/흑점/만월이 `CombatManager.ActiveMonsters` 중 같은 faction 대상. (Wave5 불꽃도 재사용)
- **패턴 순서는 기존 패턴 재사용**: 순차 반복은 `SequentialPattern` + 스킬 중복 등록, 50/50은 `RandomPattern`. 조건부 AI 신규 인프라 **불필요**(Wave5 몫).
- **리듬 디버프(`양력`/`음력`)는 각 기사 패시브**, **가호는 각 사제 패시브** (스펙 구조 그대로). 즉 +30% 자기 디버프는 기사에게만.
- **가호 "첫턴 제외"** = 태양은 홀수턴 중 turn≥3(3,5,7…), 달은 짝수턴 전부(2,4,6…).
- 값: 모든 피해 30, HP 70, 가호 +3 영구, 리듬 +30%, 흑점 방어도 10, 만월 회복 10.

## 3. 신규 코드 (파라미터화 — 태양/달 공용)

| 요소 | 파일(신규) | 구현 |
|---|---|---|
| `MonsterFaction` enum + 필드 | `Units/Monster/MonsterPreset.cs`(enum + `[SerializeField] MonsterFaction faction`), `Monster.cs`(`Faction` 프로퍼티, InitializeFromPreset에서 세팅) | `enum MonsterFaction { None, Sun, Moon, Flame }` |
| 리듬 약화 패시브 | `Data/MonsterPresets/Wave4/Shared/TurnParityWeaknessPassive.cs` | `PassiveAbility` 파생. `[SerializeField] bool triggerOnOddTurn; int percent=30`. `OnTurnEvent(TurnStart)`에서 `CombatManager.TurnCount % 2` 홀짝이 맞으면 **자신에게** "받는 피해 +N%"(1턴) 상태 부여. (`FrostbiteDebuff` 패턴 — 대상=자신) |
| 가호 패시브 | `.../Wave4/Shared/FactionBlessingPassive.cs` | `PassiveAbility` 파생. `bool triggerOnOddTurn; int amount=3; bool skipFirstTurn`. TurnStart에 홀짝·첫턴 조건 맞으면 `ActiveMonsters` 중 `owner.Faction`과 같은 전원에 `BuffAttackStatus(amount, 영구=-1, stackable=true)` |
| 세트 지원 스킬 | `.../Wave4/Shared/FactionSupportSkill.cs` (SkillData 파생) | `enum Kind { Armor, Heal }; int amount; MonsterFaction 대상(=시전자 faction)`. 공격 대신: Armor→같은 faction `Stats.TempArmor += amount`; Heal→`HealContext`로 회복. |
| 홀/짝 타일 공격 | 기존 `GetCustomTiles()` override 패턴 (SolraKnight에 예시) | 플레어=홀수 타일, 월광=짝수 타일, 피해 30 |

## 4. 데이터/기존 재사용 (신규 코드 없음)

- **AoE**: `천공검`=`MonsterSkill{ RandomCharacter, Tiles, count 2, range 1, dmg 30 }`; `일식/초승달/월식`=`{ RandomCharacter, Tiles, count 1, range 2, dmg 30 }` (`AttackIntent.ExpandTilesRange`)
- **패턴 선택**:
  - 태양기사 `SequentialPattern [천공검, 천공검, 플레어]`
  - 달기사 `SequentialPattern [월광, 초승달, 초승달]`
  - 태양사제 `RandomPattern [흑점, 일식]`, 달사제 `RandomPattern [만월, 월식]`
- **상태효과**: 받는 피해 +% (`FrostbiteDebuff` 류, 대상=자신), 영구 스택 공격버프(`BuffAttackStatus(-1)`), 일시 방어도(`Stats.TempArmor`), 회복(`HealContext`) — 전부 기존

## 5. 몬스터별 정의

| 몹 | Faction | HP | 패시브 | 패턴(선택) |
|---|---|---|---|---|
| 태양의 기사 | Sun | 70 | **양력** = TurnParityWeakness(onOdd=true) | 순차 `천공검(무2+±1,30) → 천공검 → 플레어(홀타일,30)` |
| 태양의 사제 | Sun | 70 | **태양의 가호** = FactionBlessing(onOdd=true, skipFirst=true) | 랜덤50/50 `흑점(태양 방어도10) / 일식(무1+±2,30)` |
| 달의 기사 | Moon | 70 | **음력** = TurnParityWeakness(onOdd=false) | 순차 `월광(짝타일,30) → 초승달(무1+±2,30) → 초승달` |
| 달의 사제 | Moon | 70 | **달의 가호** = FactionBlessing(onOdd=false, skipFirst=false) | 랜덤50/50 `만월(달 회복10) / 월식(무1+±2,30)` |

기존 `SolraKnight`/`LunaKnight` 프리셋·스킬·패시브는 이 스펙으로 **재작업**(기존 tile-parity 패시브/스킬 교체). `SolraPriest`/`LunaPriest`는 스킬·패시브 **신규 작성**(프리셋 에셋은 존재).

## 6. 데이터 흐름 (매 턴)

```
플레이어 턴 시작 → CombatManager.TurnCount++
몬스터 턴:
  각 몬스터 패시브 OnTurnEvent(TurnStart):
    - 기사: 양력/음력 → TurnCount 홀짝 맞으면 자신에 "받는피해+30%"(1턴)
    - 사제: 가호 → 홀짝·첫턴 조건 맞으면 같은 faction 전원 +3 영구
  각 몬스터 AIPattern.GetNextSkill() → 스킬 실행 (AoE/타일/지원)
```

## 7. 이번 범위 밖

- **Wave5 불꽃 세트** (불꽃 타일·타일개수 버프·조건부 패턴3·HP<50%) — 별도 스펙/계획.
- 조건부 AI 패턴 인프라(HP/개수 기반) — Wave5에서.
- 인카운터/맵 배정 튜닝(ActDefinition 티어) — 에디터 셋업.

## 8. 영향 파일

**신규**
- `Units/Monster/MonsterPreset.cs`에 `MonsterFaction` enum+필드 (또는 별도 `MonsterFaction.cs`)
- `Data/MonsterPresets/Wave4/Shared/`: `TurnParityWeaknessPassive.cs`, `FactionBlessingPassive.cs`, `FactionSupportSkill.cs`
- `Wave4/SolraPriest/`, `Wave4/LunaPriest/`: 스킬(일식/월식은 데이터, 흑점/만월은 FactionSupportSkill 인스턴스) — 대부분 프리셋 인스펙터 배선

**수정**
- `Units/Monster/Monster.cs` — `Faction` 노출 + InitializeFromPreset 세팅
- `Wave4/SolraKnight/*`, `Wave4/LunaKnight/*` — 스킬·패시브 스펙 재작업 (플레어/월광 피해 30, 양력/음력 패시브로 교체)
- 홀/짝 타일 공격 스킬(플레어/월광) — 기존 override 재사용/조정

**에디터 셋업(코드 밖)**
- 각 프리셋에 faction, AIPattern(스킬 리스트), StartingPassives 배선. 스킬 값(피해30/방10/회복10/버프3) 설정.
- Wave4 EncounterDefinition에 4종 묶고 ActDefinition 티어에 배정.

## 9. 검증

- **컴파일**: 콘솔 에러 0.
- **플레이**: Wave4 전투에서 —
  - 홀수턴: 태양 기사·사제가 받는 피해 +30% 표시(디버프 아이콘), 태양 사제 가호로 태양 유닛 공격력 +3 누적(턴3부터)
  - 짝수턴: 달 유닛 +30% 받피, 달 사제 가호로 달 유닛 +3
  - 천공검=무작위2 타일±1 30피해 / 플레어=홀타일 30 / 월광=짝타일 30 / 일식·초승달·월식=무1±2 30
  - 흑점=태양 유닛 방어도10, 만월=달 유닛 회복10
  - 패턴 순서: 태양기사 천공검·천공검·플레어 반복, 달기사 월광·초승달·초승달 반복, 사제 50/50
- **회귀**: 기존 다른 몬스터/전투 정상, faction 필드 미설정(None) 몹은 세트 로직 무시.
