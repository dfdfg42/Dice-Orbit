# 전투 개편 Phase 3a 구현 계획 — 위치 패시브 3종 (전사·도적·마법사)

> 스펙: `Docs/superpowers/specs/2026-08-21-auto-combat-redesign-design.md` §5
> 선행: `2026-08-21-auto-combat-phase1-2.md` (구역 + 자동 기본공격) — 완료

**목표:** 캐릭터 개성을 주사위 눈이 아니라 **어느 구역에 서 있는가**에서 나오게 한다.
Phase 1–2로 "어디에 서면 누구를 때리는가"가 생겼으니, 이제 "어디에 서면 무엇이 달라지는가"를 붙인다.

## 착수 전 발견 — 기존 패시브가 새 설계와 충돌한다

기존 4개 패시브를 읽어보니 하나가 이번 개편을 정면으로 거스른다:

- **도적 `PositioningPassive`** — 한 턴에 이동한 1칸당 공격 피해 +25%.
  주사위 눈이 곧 이동 거리이므로 이것은 **눈 → 딜 커플링을 뒷문으로 되살린다.**
  설계 토의 내내 걷어낸 바로 그 결합이라 반드시 교체해야 한다.
- **전사 `BattleCryPassive`** — 좌우 인접 아군당 +50% 피해. 타일 인접 기준이라 구역제와 층이 어긋나고,
  전사에게 주기로 한 방어적 정체성과도 다르다.
- **마법사 `FocusPassive`** — 안 맞은 턴에 집중 스택. 위치와 무관해 새 설계에서 할 일이 없다.
- **연금술사 `ReagentPrepPassive`** — 타일 기반이라 눈-딜 커플링이 없다. 이번엔 건드리지 않는다(Phase 3b).

## 이 계획의 범위

**포함:** 전사·도적·마법사 패시브 3종 교체 + 시그니처 모디파이어 2종 재조준 + 구역 API 확장.
**제외:** 연금술사 잔류물(ReagentTile 재작업 필요 — Phase 3b), 액티브 4종(Phase 4).

## Global Constraints

- **`git push` 절대 금지** (공모전 제출 ~2026-08-22). 커밋은 로컬만.
- **폴백 코드 금지** — 배선이 잘못되면 기본값을 지어내지 말고 `Debug.LogError`.
- **모디파이어 클래스명은 유지한다.** `ModifierRegistry`가 `GetType().Name`을 세이브 ID로 쓰므로
  이름을 바꾸면 저장된 런의 모디파이어가 복원되지 않는다. 표시명·설명·조건만 바꾼다.
- 기존 패시브 훅은 반드시 `override` — `public void`로 선언하면 DIM이 빈 채로 굳어 절대 호출되지 않는다.

## 설계 — 패시브 3종

| 클래스 | 새 패시브 | 효과 | 대체되는 것 |
|---|---|---|---|
| 전사 | **수호** `WarriorGuardPassive` | 같은 구역에 선 아군(자신 포함)이 받는 피해 −30% | BattleCryPassive |
| 도적 | **협공** `RogueFlankPassive` | 같은 구역에 다른 아군이 있으면 자기 공격 피해 +100% | PositioningPassive |
| 마법사 | **원거리** `MageRangedPassive` | 자기 구역에 몬스터가 없으면 인접 구역(사거리 1)의 몬스터를 대신 때린다 | FocusPassive |

**시너지 웹** — 전사와 도적이 같은 구역에 모이면 전사가 지켜주고 도적이 폭발한다.
마법사는 반대로 혼자 빠져 있어도 일한다: 중립지대에 숨은 채로도 옆 구역을 때리는 **유일한 캐릭터**다.
중립지대 결정(2026-08-21)이 마법사에게 존재 이유를 만들어 준 셈이라, 파티는 자연스럽게 뭉침 2 + 유격 1로 갈린다.

마법사를 "인접 구역 전부 타격"이 아니라 **하나만 고르게** 한 이유: 4마리 기준 3타겟이면
공격력 5로도 턴당 15가 나와 다른 캐릭터(4~6)를 압도한다. 사거리는 넓히되 표적 수는 1로 묶어
"안전한 자리에서 계속 일한다"는 정체성만 남긴다.

**시그니처 모디파이어 재조준** (클래스명 유지, 내용 교체)
- `RoguePositioningBoost` → 협공 계수 +10%p (표시명 「급소 감각」)
- `MageFocusBoost` → 사거리 +1 (표시명 「먼 시야」)
- `AlchemistExtraReagent` → 변경 없음 (연금 패시브를 유지하므로)

## 파일 구조

**신규**
| 파일 | 책임 |
|---|---|
| `.../Combat/Passive/IZoneReachProvider.cs` | 자동공격 사거리를 넓히는 패시브가 구현하는 인터페이스 |
| `Assets/Scripts/Data/Character Preset/Warrior/WarriorGuardPassive.cs` | 전사 수호 |
| `Assets/Scripts/Data/Character Preset/Rogue/RogueFlankPassive.cs` | 도적 협공 |
| `Assets/Scripts/Data/Character Preset/Mage/MageRangedPassive.cs` | 마법사 원거리 |

**수정**
| 파일 | 변경 |
|---|---|
| `.../Combat/Zone/CombatZoneManager.cs` | `GetTilesInZone`, `FindNearestOwner` 추가 |
| `.../Combat/AutoAttackSystem.cs` | 대상 수집이 사거리를 반영 |
| `Data/Modifiers/Rogue/RoguePositioningBoost.cs` | 협공 계수로 재조준 |
| `Data/Modifiers/Mage/MageFocusBoost.cs` | 사거리 보너스로 재조준 |
| `Data/Character Preset/{Warrior,Rogue,Mage}/*.asset` | StartingPassives 교체 |

**삭제**
`BattleCryPassive.cs` · `PositioningPassive.cs` · `FocusPassive.cs` (+ .meta)

## Task 목록

- [ ] **Task 1** — 구역 API 확장 (`GetTilesInZone`, `FindNearestOwner`) + `IZoneReachProvider` 정의
- [ ] **Task 2** — 패시브 3종 작성
- [ ] **Task 3** — `AutoAttackSystem`이 사거리를 반영해 대상 선정
- [ ] **Task 4** — 시그니처 모디파이어 2종 재조준
- [ ] **Task 5** — 옛 패시브 3개 삭제 (컴파일 복구)
- [ ] **Task 6** — 프리셋 3개의 StartingPassives 교체 + 표시명·설명 입력 (MCP `managedReferenceValue`)

각 Task 뒤에 MCP `typeof` 컴파일 게이트를 돌린다.

**순서 주의 (실행 중 확인됨).** 모디파이어를 새 패시브로 재조준하는 순간 옛 패시브가
사라진 속성명을 참조해 컴파일이 깨진다. 그 상태에서는 MCP 명령 자체가 컴파일되지 않아
프리셋을 만질 수 없으므로, **삭제를 먼저 하고 배선을 나중에** 해야 한다.
삭제 시점에 프리셋의 SerializeReference는 잠시 빈 참조가 되지만 곧바로 새 값으로 덮어쓴다.

교체로 새 인스턴스를 넣으면 `skillName`이 빈 문자열이라 발동 버블이 빈 채로 뜬다 —
표시명과 설명을 반드시 함께 입력할 것.

## 플레이 검증 (작업자)

1. **전사 수호** — 전사와 다른 캐릭터를 같은 구역에 두고 몬스터에게 맞아 본다.
   피해가 눈에 띄게 줄고 전사 머리 위에 패시브 버블이 뜬다. 다른 구역으로 떼어놓으면 감소가 사라진다.
2. **도적 협공** — 도적 혼자 있을 때와 아군과 같은 구역에 있을 때의 자동공격 피해를 비교한다. 2배여야 한다.
3. **마법사 원거리** — 마법사를 몬스터가 없는 중립지대에 세운다.
   다른 캐릭터는 아무도 못 때리지만 마법사만 옆 구역 몬스터를 때린다.
4. **눈-딜 분리 확인** — 주사위 1을 준 캐릭터와 6을 준 캐릭터의 기본공격 피해가 같다
   (이동 거리가 더 이상 피해에 영향을 주지 않는다).
5. 콘솔에 패시브 관련 에러·경고가 없다.
