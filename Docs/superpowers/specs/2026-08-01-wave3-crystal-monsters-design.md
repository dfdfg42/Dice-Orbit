# Wave3 수정(Crystal) 몬스터 세트 설계

**작성일:** 2026-08-01
**대상:** Wave3 수정 세트 3종 — 수정 핵 / 수정석 / 수정 파편
**전제:** 기존 확장 시스템(StatusEffect / TileAttribute / 세트 정적 트래커 / 조건부 MonsterAI) 재사용. 일반 클래스에 ad-hoc 필드나 정적 전역을 새로 만들지 않는다.

---

## 1. 개요

수정 세트는 "수정 중첩(stack)"을 모아 보스급 광역 스킬을 터뜨리는 자원-축적형 세트다.

- **수정 핵**이 세트의 중심. 웨이브 시작 시 **자수정 타일 4개**를 깔고, 매 턴 그 타일과 부하(수정석·파편)로부터 **수정 중첩**을 공급받는다.
- 중첩이 **15 이상**이면 수정 핵이 **[수정 폭풍]**(광역 20 피해 + 다음 턴 **기절**)을 발동한다.
- 부하 2종은 수정 핵에게 중첩을 공급하고, 방어도 부여·회복·광역 딜을 담당한다. **수정 핵이 죽으면** 부하는 지정된 대체 패턴만 수행한다.

이 세트는 이 프로젝트에 **없던 메커니즘 하나 — "기절(행동 불가)"** 를 신규 도입한다. 나머지는 검증된 기존 패턴의 재사용이다.

---

## 2. 공유 인프라

### 2-1. 기절(Stun) — 신규, `Frozen`/`BindDebuff` 패턴 미러

현재 프로젝트에는 캐릭터의 **행동을 막는** 수단이 없다. `Frozen`은 `CharacterStats.canMove() => BindDebuff == 0`로 **이동만** 차단한다. 기절은 이동+스킬 사용을 모두 막는 "그 캐릭터 다음 턴 스킵"이며, 기존 `BindDebuff` 카운터 패턴을 그대로 미러하여 구현한다.

- **`EffectType.Stunned`** 추가 (`EffectData.cs` enum) + `GetDisplayName`에 `"기절"` 케이스.
- **`CharacterStats`**: `BindDebuff`/`canMove()` 바로 옆에 병렬로 추가.
  ```csharp
  public int StunDebuff = 0;
  public bool canAct() => StunDebuff == 0;
  ```
- **`StunDebuff : StatusEffect`** (신규 파일 `StunDebuff.cs`) — `FrozenDebuff`를 미러.
  - 생성자: `base(EffectType.Stunned, 0, duration, false)`
  - 적용 시(EffectApplied 상당) `Owner.Stats.StunDebuff++`, 만료 시(EffectExpired 상당) `Owner.Stats.StunDebuff--`.
  - 지속은 "다음 플레이어 턴 1회 스킵"이 되도록 `FrozenDebuff`의 지속 컨벤션(눈 세트에서 다음-턴 차단에 쓰던 값)을 그대로 사용한다. 정확한 tick 타이밍은 구현 계획에서 `FrozenDebuff`와 대조해 확정한다.
- **게이트 3곳** (기존 dead-character 비활성 패턴 미러):
  1. `CharacterActionUI.RefreshActionButtonsState()` — 이동/스킬 버튼 활성 조건에 `&& currentCharacter.Stats.canAct()` 추가 → 기절 시 두 버튼 비활성.
  2. `CharacterActionUI.OnMoveClicked()` — 기존 `canMove()` 체크에 `canAct()` 병행 체크.
  3. `SkillManager.ConfirmSkillExecution()` — 소스 유효성 검증 직후 `if (!source.Stats.canAct()) { 주사위 반환; return; }`.

이 세 지점이면 UI(버튼 비활성) + 로직(액션 커밋 직전 차단) 양쪽이 막힌다. 게이트는 기절뿐 아니라 향후 어떤 스턴 계열 효과에도 재사용 가능하다.

### 2-2. `CrystalSet` 정적 트래커 — `SlimeSet`/`SnowSet` 패턴

세트 공유 상태(수정 중첩)와 웨이브 종료 정리를 담당한다.

- 파일: `Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/Shared/CrystalSet.cs`
- 상태: `private static readonly Dictionary<Monster,int> Stacks = new();` (수정 핵 인스턴스에 귀속)
- API:
  - `AddStack(Monster core, int n)` — `core`가 null이면 no-op.
  - `GetStacks(Monster core)` — 없으면 0.
  - `ResetStacks(Monster core)`.
  - `GetCore()` — `CombatManager.Instance.GetAliveMonsters().FirstOrDefault(m => m.Stats.MonsterName == "수정 핵")`. 부하와 자수정 타일이 "현재 살아있는 수정 핵"을 찾는 단일 경로.
  - `EnsureWaveHook()` — `SlimeSet`과 동일하게 `CombatManager.OnCombatStart`에 1회 구독. 콜백에서 `Stacks.Clear()` + 모든 타일의 자수정 속성 제거.

> **수정 핵 식별:** `Stats.MonsterName == "수정 핵"` 문자열로 찾는다. 프리셋의 `MonsterName`을 정확히 `"수정 핵"`으로 배선한다. (조건부 AI·부하 스킬이 이 문자열에 의존.)

### 2-3. `AmethystTile` 자수정 타일 — `SlimeTile` 패턴(단 영구 유지)

- `TileAttributeType.Amethyst` 추가(현재 마지막이 `Slime`=15 → **`Amethyst`=16**) + `GetDisplayName`에 `"자수정"`.
- 파일: `Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/Shared/AmethystTile.cs` (네임스페이스는 `SlimeTile`과 동일하게 `DiceOrbit.Data.Tile`).
- 생성자: `base(TileAttributeType.Amethyst, 0, -1, false)` — 지속 -1(영구).
- `OnTraverse(Character)` / `OnEndTurn(Character)` → `Activate`:
  - `var core = CrystalSet.GetCore();`
  - `core != null` → `CrystalSet.AddStack(core, 1)`. **자기 자신을 제거하지 않는다(영구 유지).**
  - `core == null`(수정 핵 사망) → `Owner?.RemoveAttribute(this)`로 자기 제거(지연 정리).
- 전면 정리는 `CrystalSet.EnsureWaveHook`의 웨이브 종료 콜백이 담당.

---

## 3. 몬스터 3종 (전부 HP 45)

폴더/네임스페이스: 기존 Wave3(눈 세트)와 충돌을 피하기 위해 **`Wave3/Crystal/` 하위**에 둔다.
- `...Wave3/Crystal/Shared/` — 공유 클래스
- `...Wave3/Crystal/CrystalCore/CrystalCore.cs`
- `...Wave3/Crystal/CrystalStone/CrystalStone.cs`
- `...Wave3/Crystal/CrystalShard/CrystalShard.cs`

### 3-1. 수정 핵 (`CrystalCore`)

**패시브 `SummonAmethystPassive` [자수정]**
- `Initialize`에서 `CrystalSet.EnsureWaveHook()` 호출.
- 웨이브 시작 시 무작위 4타일에 `AmethystTile` 설치(1회만 — 패시브 인스턴스의 private `bool placed` 가드). 설치는 오르빗이 준비된 시점(패시브 `Initialize` 또는 수정 핵 첫 턴 시작)에서 수행하며, 정확한 타이밍은 구현 계획에서 `PlantSlimePassive`와 대조해 확정한다.
- 자수정 타일은 이미 자수정이 없는 타일 중에서 무작위로 고른다(`PlantSlimePassive.PlantSlime` 동형).

**패턴1 `CrystalBurstSkill` [수정 폭발]**
- 무작위 자수정 타일 2개 + 각 좌우 ±1칸에 있는 적에게 **20** 피해.
- 배선: `targetStrategy=TilesWithAttribute(3)`, `targetTileAttribute=Amethyst(16)`, `targetType=Tiles(1)`, `targetCount=2`, `targetRange=1`. `Execute`는 `AttackTiles(source, targetTiles, damage)`.

**패턴2 `CrystalStormSkill` [수정 폭풍]**
- 자수정 타일을 **제외한** 모든 타일에 있는 적에게 **20** 피해 + 대상에게 **기절**(다음 턴 스킵) 부여.
- 배선: `targetStrategy=AllTargets(1)`, `targetType=Characters(0)`.
- `Execute`: `targetUnits`를 순회하며 대상의 현재 타일이 자수정 속성을 가지면 건너뛰고, 아니면 20 피해 후 `character.StatusEffects.AddEffect(new StunDebuff(duration))`.

**조건부 AI `CrystalCorePattern : MonsterAI`**
- `GetNextSkill`: `int stacks = CrystalSet.GetStacks(owner as Monster);`
  - `stacks >= 15` → `availableSkills[1]`(수정 폭풍)
  - 그 외(≤14) → `availableSkills[0]`(수정 폭발)
- (스펙 원문은 "15 이상 → 패턴2, 9 이하 → 패턴1". 10~14 구간은 패턴1(폭발)로 처리.)

### 3-2. 수정석 (`CrystalStone`)

**패시브 `CrystallizePassive` [결정화]** — 부하 2종 공유(`Wave3/Crystal/Shared/`)
- `OnTurnEvent`에서 `trigger==OnPostAction && Phase==TurnEnd && SourceUnit==owner`일 때:
  - `var core = CrystalSet.GetCore(); if (core != null) CrystalSet.AddStack(core, 1);`

**패턴1 `CrystalShieldSkill` [결정 방패]**
- 수정 핵에게 일시 방어도 **10** 부여.
- `Execute`: `var core = CrystalSet.GetCore(); if (core != null) core.Stats.TempArmor += 10;`
- 배선: `intentType=Defend(1)`, `targetType=Self(2)` 또는 `None(3)`(대상 선택 불필요; Execute가 직접 수정 핵을 찾음).

**패턴2 `CrystalSpearSkill` [수정 창]**
- 무작위 대상 1명이 속한 타일 + 좌우 ±2칸에 **15** 피해.
- 배선: `GreenSlime.CorrosiveSlimeSkill`과 동형 — `targetStrategy=RandomCharacter(0)`, `targetType=Tiles(1)`, `targetRange=2`, `targetCount=1`. `Execute`는 `AttackTiles`.

**조건부 AI `CrystalMinionPattern : MonsterAI`** — 부하 2종 공유, `[SerializeField] int coreAbsentSkillIndex`
- `GetNextSkill`:
  - `CrystalSet.GetCore() == null` → `availableSkills[coreAbsentSkillIndex]`
  - 아니면 `availableSkills[Random.Range(0,2)]` (50/50)
- 수정석: `coreAbsentSkillIndex = 1`(수정 창). → 수정 핵 없으면 수정 창만.

### 3-3. 수정 파편 (`CrystalShard`)

**패시브 `CrystallizePassive` [결정화]** — 수정석과 동일 클래스 재사용.

**패턴1 `CrystalRainSkill` [수정 비]**
- 무작위 타일 6개에 있는 적에게 **15** 피해.
- 배선: `targetStrategy=RandomTiles(2)`, `targetType=Tiles(1)`, `targetCount=6`, `targetRange=0`. `Execute`는 `AttackTiles`.

**패턴2 `CrystalRegenSkill` [결정 재생]**
- 수정 핵 체력 **10** 회복.
- `Execute`: `var core = CrystalSet.GetCore(); if (core != null) core.Stats.Heal(10);`
- 배선: `intentType=Buff(2)` 또는 `Special(3)`, `targetType=None(3)`.

**조건부 AI `CrystalMinionPattern`** — 공유 클래스, `coreAbsentSkillIndex = 0`(수정 비). → 수정 핵 없으면 수정 비만.

---

## 4. 파일 목록

**수정(기존 파일):**
- `.../Tile/TileAttribute.cs` — `TileAttributeType.Amethyst`(16) enum + `GetDisplayName` "자수정".
- `.../Combat/EffectData.cs` — `EffectType.Stunned` + `GetDisplayName` "기절".
- `.../CharacterStats.cs` — `StunDebuff` 필드 + `canAct()`.
- `.../UI/CharacterActionUI.cs` — `RefreshActionButtonsState`, `OnMoveClicked`에 `canAct()` 게이트.
- `.../Combat/SkillManager.cs` — `ConfirmSkillExecution`에 `canAct()` 게이트.

**신규:**
- `.../CharacterStats` 인근 또는 Effects 폴더 `StunDebuff.cs` — `StunDebuff : StatusEffect`.
- `Wave3/Crystal/Shared/CrystalSet.cs`
- `Wave3/Crystal/Shared/AmethystTile.cs`
- `Wave3/Crystal/Shared/CrystallizePassive.cs`
- `Wave3/Crystal/Shared/CrystalMinionPattern.cs`
- `Wave3/Crystal/CrystalCore/CrystalCore.cs` (SummonAmethystPassive, CrystalBurstSkill, CrystalStormSkill, CrystalCorePattern)
- `Wave3/Crystal/CrystalStone/CrystalStone.cs` (CrystalShieldSkill, CrystalSpearSkill)
- `Wave3/Crystal/CrystalShard/CrystalShard.cs` (CrystalRainSkill, CrystalRegenSkill)

**프리셋(.asset) 3종** — MCP `SerializedObject`로 배선:
- `CrystalCore.asset` — HP45, MonsterName `"수정 핵"`, AIPattern `CrystalCorePattern`[0=Burst,1=Storm], StartingPassives[SummonAmethystPassive].
- `CrystalStone.asset` — HP45, MonsterName `"수정석"`, AIPattern `CrystalMinionPattern(coreAbsentSkillIndex=1)`[0=Shield,1=Spear], StartingPassives[CrystallizePassive].
- `CrystalShard.asset` — HP45, MonsterName `"수정 파편"`, AIPattern `CrystalMinionPattern(coreAbsentSkillIndex=0)`[0=Rain,1=Regen], StartingPassives[CrystallizePassive].

---

## 5. 결정 사항 / 애매점 처리

- **중첩 10~14 구간**: 수정 폭풍은 ≥15에서만. 그 미만은 전부 수정 폭발.
- **자수정 타일**: `SlimeTile`과 달리 발동해도 사라지지 않음(영구). 수정 핵 사망 시 지연 제거 + 웨이브 종료 시 일괄 정리.
- **기절 지속**: "다음 턴 1회 스킵". `FrozenDebuff`의 다음-턴 컨벤션을 미러하며 정확한 tick은 계획에서 확정.
- **수정 핵 식별**: `MonsterName == "수정 핵"` 문자열. 프리셋 배선 시 정확히 일치시킨다.
- **부하 패턴 확률**: `Random.Range(0,2)` 균등 50/50 (게임 런타임 코드 — MCP RunCommand 아님).

## 6. 검증

- 컴파일 체크(헤드리스 또는 MCP `GetConsoleLogs` Error) — `Amethyst`=16, `Stunned` enum, 신규 클래스 인스턴스화.
- 프리셋 3종 직렬화 확인(BlueSlime.asset 검증과 동일 방식).
- 인카운터 배치 + 플레이 검증은 사용자 몫(슬라임과 동일 흐름).
