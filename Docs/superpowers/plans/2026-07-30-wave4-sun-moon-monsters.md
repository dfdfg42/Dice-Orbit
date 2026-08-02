# Wave4 태양/달 세트 몬스터 (Set A) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wave4 "태양/달" 세트 4종(태양·달의 기사/사제)을 턴 홀짝 리듬으로 상호작용하도록 구현한다.

**Architecture:** 세트 식별은 `MonsterFaction` enum + `MonsterPreset.Faction` 필드로 하고, 리듬 약화(양력/음력)와 진영 가호를 태양/달 공용 파라미터화 패시브 2종으로, 흑점/만월을 공용 지원 스킬 1종으로 만든다. 순차/랜덤 행동은 기존 `SequentialPattern`/`RandomPattern`에 스킬을 중복 등록해 표현하고(조건부 AI 신규 인프라 없음), 상태효과는 기존 `FrostbiteDebuff`(받는 피해 +%)·`BuffAttackStatus`(영구 공격력)·`Stats.TempArmor`·`Unit.Heal`을 재사용한다.

**Tech Stack:** Unity 6000.3.8f1, C#, `[SerializeReference]` 다형성(SkillData/PassiveAbility/MonsterAI), New Input System, URP. 유닛 테스트 프레임워크 없음.

**스펙:** `Docs/superpowers/specs/2026-07-30-wave4-sun-moon-monsters-design.md`

---

## 검증 방식 (이 프로젝트 특성)

이 저장소에는 자동화 테스트 프로젝트가 없다. 따라서 각 코드 태스크는 **컴파일 에러 0** 확인으로 마무리하고, 마지막 태스크에서 **플레이 검증**을 한다. (TDD의 "실패하는 테스트 먼저"는 적용 불가 — dice deck 계획과 동일한 방침.)

**컴파일 확인 방법 (매 코드 태스크 끝):**
- Unity 에디터가 열려 있으면: 에디터에 포커스를 주면 스크립트가 자동 재컴파일된다. 이후 MCP `Unity_GetConsoleLogs { types: ["Error"] }` 로 에러를 확인한다. **편집한 파일을 가리키는 컴파일 에러가 없어야 한다.**
- 헤드리스 대안: VS2022 MSBuild + Unity mono `4.7.1-api` `FrameworkPathOverride` 로 솔루션 빌드 (메모리 `verify_unity_compile` 참조).

**커밋:** 커밋 메시지는 프로젝트 관례대로 한국어 `feat:`/`refactor:` 접두어를 쓰고, 마지막 줄에 다음을 붙인다:
`Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`

**브랜치:** 현재 `feature/event-outcomes-refactor-20260729`. 이 브랜치에서 진행한다 (main 아님).

---

## File Structure

**신규 파일**
- `Assets/Scripts/Core/Stage/BattleStage/Units/Monster/MonsterFaction.cs` — 세트 식별 enum (`None/Sun/Moon/Flame`).
- `Assets/Scripts/Data/MonsterPresets/Wave4/Shared/TurnParityWeaknessPassive.cs` — 양력/음력 공용 패시브(받는 피해 +%).
- `Assets/Scripts/Data/MonsterPresets/Wave4/Shared/FactionBlessingPassive.cs` — 태양/달 가호 공용 패시브(영구 공격력).
- `Assets/Scripts/Data/MonsterPresets/Wave4/Shared/FactionSupportSkill.cs` — 흑점(방어도)/만월(회복) 공용 지원 스킬.

**수정 파일**
- `Assets/Scripts/Core/Stage/BattleStage/Units/Monster/MonsterPreset.cs` — `Faction` 필드 추가.
- `Assets/Scripts/Core/Stage/BattleStage/Units/Monster/Monster.cs` — `Faction` 프로퍼티 + `InitializeFromPreset`에서 세팅.
- `Assets/Scripts/Data/MonsterPresets/Wave4/SolraKnight/SolraKnight.cs` — 스킬(천공검/플레어) 재작업, 구 패시브 삭제.
- `Assets/Scripts/Data/MonsterPresets/Wave4/LunaKnight/LunaKnight.cs` — 스킬(월광/초승달) 재작업, 구 패시브 삭제.
- `Assets/Scripts/Data/MonsterPresets/Wave4/SolraPriest/SolraPriest.cs` — 일식만 남기고 단순화, 구 지원스킬/패시브 삭제.
- `Assets/Scripts/Data/MonsterPresets/Wave4/LunaPriest/LunaPriest.cs` — 월식만 남기고 단순화, 구 지원스킬/패시브/버프 삭제.

**에디터 셋업 (코드 밖, Task 10~11)**
- 4개 `MonsterPreset` 에셋: Faction, AIPattern(스킬 리스트+타깃 설정), StartingPassives, 값(HP70/피해30/방어10/회복10/버프3).
- Wave4 `EncounterDefinition` + `ActDefinition` 티어 배정.

---

## Task 1: MonsterFaction enum + MonsterPreset 필드

**Files:**
- Create: `Assets/Scripts/Core/Stage/BattleStage/Units/Monster/MonsterFaction.cs`
- Modify: `Assets/Scripts/Core/Stage/BattleStage/Units/Monster/MonsterPreset.cs`

- [ ] **Step 1: enum 파일 생성**

`MonsterFaction.cs`:

```csharp
namespace DiceOrbit.Data.Monsters
{
    /// <summary>
    /// 몬스터 세트(진영) 식별자. 세트 지원 로직(가호/흑점/만월 등)이
    /// 같은 진영끼리 대상을 고를 때 사용한다. (Wave5 불꽃도 재사용)
    /// </summary>
    public enum MonsterFaction
    {
        None,
        Sun,
        Moon,
        Flame
    }
}
```

- [ ] **Step 2: MonsterPreset에 Faction 필드 추가**

`MonsterPreset.cs` — `[Header("Stats")] public MonsterStats BaseStats;` 바로 아래(현재 14~15행)에 삽입:

```csharp
        [Header("Stats")]
        public MonsterStats BaseStats;

        [Header("Faction (세트)")]
        [Tooltip("세트 지원 로직(가호/흑점/만월)이 같은 진영끼리 대상으로 삼을 때 사용. 무소속이면 세트 로직 없음.")]
        public MonsterFaction Faction = MonsterFaction.None;
```

(`MonsterPreset`은 같은 `DiceOrbit.Data.Monsters` 네임스페이스라 using 불필요.)

- [ ] **Step 3: 컴파일 확인**

Unity 포커스 → `Unity_GetConsoleLogs { types: ["Error"] }`. 기대: 에러 0. Inspector에서 아무 MonsterPreset 에셋을 열면 "Faction" 드롭다운(None/Sun/Moon/Flame)이 보인다.

- [ ] **Step 4: 커밋**

```bash
git add "Assets/Scripts/Core/Stage/BattleStage/Units/Monster/MonsterFaction.cs" \
        "Assets/Scripts/Core/Stage/BattleStage/Units/Monster/MonsterPreset.cs"
git commit -m "feat: MonsterFaction enum + MonsterPreset.Faction 필드 (세트 식별)"
```

---

## Task 2: Monster.Faction 노출 + 프리셋 초기화 세팅

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/Units/Monster/Monster.cs`

- [ ] **Step 1: Faction 프로퍼티 추가**

`Monster.cs`의 `IdentityColor`/`HasIdentityColor` 선언(현재 50~51행) 바로 아래에 추가:

```csharp
        [System.NonSerialized] public Color IdentityColor = Color.white;
        [System.NonSerialized] public bool HasIdentityColor = false;

        // 세트(진영) — 프리셋에서 배정. 세트 지원 패시브/스킬이 같은 진영 대상 필터에 사용.
        public Data.Monsters.MonsterFaction Faction { get; private set; } = Data.Monsters.MonsterFaction.None;
```

- [ ] **Step 2: InitializeFromPreset에서 Faction 세팅**

`InitializeFromPreset`의 `preset = monsterPreset;`(현재 101행) 바로 아래에 추가:

```csharp
            preset = monsterPreset;
            Faction = monsterPreset.Faction;
```

- [ ] **Step 3: 컴파일 확인**

Unity 포커스 → `Unity_GetConsoleLogs { types: ["Error"] }`. 기대: 에러 0.

- [ ] **Step 4: 커밋**

```bash
git add "Assets/Scripts/Core/Stage/BattleStage/Units/Monster/Monster.cs"
git commit -m "feat: Monster.Faction 런타임 노출 + 프리셋에서 세팅"
```

---

## Task 3: TurnParityWeaknessPassive (양력/음력 공용)

**Files:**
- Create: `Assets/Scripts/Data/MonsterPresets/Wave4/Shared/TurnParityWeaknessPassive.cs`

- [ ] **Step 1: 파일 생성**

```csharp
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.MonsterPresets.Wave4.Shared
{
    /// <summary>
    /// 리듬 약화 패시브 (양력/음력 공용).
    /// 매 턴 시작, 현재 턴의 홀짝이 지정과 맞으면 "받는 피해 +percent%"를 자신에게 durationTurns턴 부여한다.
    /// 태양 유닛 = triggerOnOddTurn(홀수턴에 약화), 달 유닛 = triggerOnOddTurn 해제(짝수턴에 약화).
    /// 받는 피해 증가는 기존 FrostbiteDebuff(받는 피해 +Value%, 중첩 불가)를 재사용한다.
    /// </summary>
    [System.Serializable]
    public class TurnParityWeaknessPassive : PassiveAbility
    {
        [Header("Rhythm Settings")]
        [Tooltip("체크 시 홀수 턴에 약화, 해제 시 짝수 턴에 약화")]
        [SerializeField] private bool triggerOnOddTurn = true;
        [Tooltip("약화 시 받는 피해 증가 퍼센트")]
        [SerializeField] private int percent = 30;
        [Tooltip("약화 지속 턴 (플레이에서 다음 플레이어 턴을 못 덮으면 2로 올릴 것)")]
        [SerializeField] private int durationTurns = 1;

        public TurnParityWeaknessPassive()
        {
            passiveName = "양력";
            description = "지정된 홀짝 턴에 받는 피해가 증가한다";
            priority = 10;
            isStackable = false;
        }

        public override string GetDynamicDescription()
            => $"{(triggerOnOddTurn ? "홀수" : "짝수")} 턴에 받는 피해 +{percent}% ({durationTurns}턴)";

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnPreAction) return;
            if (context.Phase != EventPhase.TurnStart) return;
            if (context.SourceUnit != owner) return;

            var cm = CombatManager.Instance;
            if (cm == null) return;

            bool isOddTurn = (cm.TurnCount % 2) == 1;
            if (isOddTurn != triggerOnOddTurn) return;

            if (owner.StatusEffects == null) return;
            owner.StatusEffects.AddEffect(new FrostbiteDebuff(percent, durationTurns));
            Debug.Log($"[{PassiveName}] {owner.name} 받는 피해 +{percent}% ({durationTurns}턴) — turn {cm.TurnCount}");
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }
}
```

**주의:** `passiveName`은 생성자 기본값 "양력"이지만, 프리셋 인스펙터에서 인스턴스별로 "음력"으로 바꿔 쓸 수 있다(Task 10). `triggerOnOddTurn`은 태양=true, 달=false.

- [ ] **Step 2: 컴파일 확인**

Unity 포커스 → `Unity_GetConsoleLogs { types: ["Error"] }`. 기대: 에러 0.

- [ ] **Step 3: 커밋**

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave4/Shared/TurnParityWeaknessPassive.cs"
git commit -m "feat: 양력/음력 공용 TurnParityWeaknessPassive (홀짝 턴 받는 피해 +%)"
```

---

## Task 4: FactionBlessingPassive (태양/달 가호 공용)

**Files:**
- Create: `Assets/Scripts/Data/MonsterPresets/Wave4/Shared/FactionBlessingPassive.cs`

- [ ] **Step 1: 파일 생성**

```csharp
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.MonsterPresets.Wave4.Shared
{
    /// <summary>
    /// 진영 가호 패시브 (태양의 가호 / 달의 가호 공용).
    /// 매 턴 시작, 홀짝·첫턴 조건이 맞으면 같은 진영 몬스터 전원에게 영구 공격력 +amount(중첩)를 부여한다.
    /// 태양 = triggerOnOddTurn + skipFirstTurn(턴 1 제외 → 3,5,7…), 달 = 해제 + 미제외(2,4,6…).
    /// 영구 공격버프는 기존 BuffAttackStatus(value, -1){IsStackable=true}(=DewPoint 패턴)를 재사용한다.
    /// </summary>
    [System.Serializable]
    public class FactionBlessingPassive : PassiveAbility
    {
        [Header("Blessing Settings")]
        [Tooltip("체크 시 홀수 턴에 발동, 해제 시 짝수 턴에 발동")]
        [SerializeField] private bool triggerOnOddTurn = true;
        [Tooltip("이번 발동으로 같은 진영에 더할 영구 공격력")]
        [SerializeField] private int amount = 3;
        [Tooltip("첫 번째 턴(턴 1)에는 발동하지 않음")]
        [SerializeField] private bool skipFirstTurn = false;

        public FactionBlessingPassive()
        {
            passiveName = "태양의 가호";
            description = "지정된 홀짝 턴마다 같은 진영 전체의 공격력을 영구히 올린다";
            priority = 10;
            isStackable = false;
        }

        public override string GetDynamicDescription()
            => $"{(triggerOnOddTurn ? "홀수" : "짝수")} 턴마다 같은 진영 공격력 +{amount} (영구)";

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnPreAction) return;
            if (context.Phase != EventPhase.TurnStart) return;
            if (context.SourceUnit != owner) return;

            var cm = CombatManager.Instance;
            if (cm == null) return;

            bool isOddTurn = (cm.TurnCount % 2) == 1;
            if (isOddTurn != triggerOnOddTurn) return;
            if (skipFirstTurn && cm.TurnCount <= 1) return;

            var myFaction = (owner as Monster)?.Faction ?? MonsterFaction.None;
            if (myFaction == MonsterFaction.None) return;

            var monsters = cm.ActiveMonsters;
            if (monsters == null) return;

            int count = 0;
            foreach (var m in monsters)
            {
                if (m == null || !m.IsAlive || m.StatusEffects == null) continue;
                if (m.Faction != myFaction) continue;
                m.StatusEffects.AddEffect(new BuffAttackStatus(amount, -1) { IsStackable = true });
                count++;
            }
            Debug.Log($"[{PassiveName}] {myFaction} {count}명 공격력 +{amount} (영구) — turn {cm.TurnCount}");
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }
}
```

**주의:** 같은 진영에 사제가 여러 명이면 각자 가호가 겹쳐 이중 스택이 된다. Set A는 진영당 사제 1명이라 문제없음(Wave5 확장 시 재검토). 가호는 자신(사제)도 같은 진영이므로 함께 강화된다.

- [ ] **Step 2: 컴파일 확인**

Unity 포커스 → `Unity_GetConsoleLogs { types: ["Error"] }`. 기대: 에러 0.

- [ ] **Step 3: 커밋**

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave4/Shared/FactionBlessingPassive.cs"
git commit -m "feat: 태양/달 가호 공용 FactionBlessingPassive (같은 진영 영구 공격력)"
```

---

## Task 5: FactionSupportSkill (흑점/만월 공용)

**Files:**
- Create: `Assets/Scripts/Data/MonsterPresets/Wave4/Shared/FactionSupportSkill.cs`

- [ ] **Step 1: 파일 생성**

```csharp
using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.MonsterPresets.Wave4.Shared
{
    /// <summary>
    /// 진영 지원 스킬 (흑점=방어도 / 만월=회복 공용). 공격 대신 같은 진영 몬스터 전원을 지원한다.
    /// MonsterSkill 설정 권장: TargetStrategy=Self, TargetType=Self, IntentType=Defend(흑점)/Buff(만월).
    /// </summary>
    [System.Serializable]
    public class FactionSupportSkill : SkillData
    {
        public enum SupportKind { Armor, Heal }

        [Header("Support Settings")]
        [Tooltip("스킬 표시 이름 (예: 흑점, 만월)")]
        [SerializeField] private string skillLabel = "지원";
        [Tooltip("Armor=같은 진영 일시 방어도, Heal=같은 진영 체력 회복")]
        [SerializeField] private SupportKind kind = SupportKind.Armor;
        [Tooltip("방어도/회복량")]
        [SerializeField] private int amount = 10;

        public override string SkillName => string.IsNullOrEmpty(skillLabel) ? "지원" : skillLabel;
        public override string Description => kind == SupportKind.Armor
            ? $"같은 진영 전체에 일시 방어도 +{amount}"
            : $"같은 진영 전체 체력 +{amount} 회복";

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var faction = (source as Monster)?.Faction ?? MonsterFaction.None;
            if (faction == MonsterFaction.None) return;

            var monsters = CombatManager.Instance?.ActiveMonsters;
            if (monsters == null) return;

            foreach (var m in monsters)
            {
                if (m == null || !m.IsAlive || m.Stats == null) continue;
                if (m.Faction != faction) continue;

                if (kind == SupportKind.Armor)
                    m.Stats.TempArmor += amount;
                else
                    m.Heal(amount);
            }
            Debug.Log($"[{SkillName}] {faction} 진영 {(kind == SupportKind.Armor ? $"방어도 +{amount}" : $"체력 +{amount}")}");
        }
    }
}
```

**주의:** `SkillData`는 상위 네임스페이스 `DiceOrbit.Data`에 있어 별도 using 없이 해석된다(기존 스킬 파일과 동일). `AttackTiles`/`AttackUnits`는 이 스킬에서 안 쓴다.

- [ ] **Step 2: 컴파일 확인**

Unity 포커스 → `Unity_GetConsoleLogs { types: ["Error"] }`. 기대: 에러 0.

- [ ] **Step 3: 커밋**

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave4/Shared/FactionSupportSkill.cs"
git commit -m "feat: 흑점/만월 공용 FactionSupportSkill (같은 진영 방어도/회복)"
```

---

## Task 6: 태양의 기사 스킬 재작업 + 구 패시브 삭제

**Files:**
- Modify: `Assets/Scripts/Data/MonsterPresets/Wave4/SolraKnight/SolraKnight.cs`

기존 파일에는 `SolraKnightSkill1`(천공검, 단일유닛 공격), `SolraKnightSkill2`(플레어, 홀수 타일), `SolraKnightPassive`(양력, 타일-위치 기반)이 있다. 천공검을 타일 AoE로, 플레어 피해를 30으로 바꾸고, 타일-위치 기반 구 패시브는 삭제한다(Task 3의 `TurnParityWeaknessPassive`로 대체 — Task 10에서 프리셋 배선).

- [ ] **Step 1: SolraKnight.cs 전체 교체**

```csharp
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Data.MonsterPresets.Wave4.SolraKnight
{
    /// <summary>
    /// 천공검 — 무작위 대상 2명이 속한 타일 + 좌우 1칸에 피해.
    /// 타깃/범위는 MonsterSkill 설정(RandomCharacter + Tiles + count 2 + range 1)이 담당.
    /// </summary>
    [System.Serializable]
    public class SolraKnightSkill1 : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("스킬 사용 시 입힐 피해량")]
        [SerializeField] private int damage = 30;

        public SolraKnightSkill1()
        {
            skillName = "천공검";
            description = $"무작위 대상 2명이 속한 타일 + 좌우 1칸에 {damage} 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>
    /// 플레어 — 모든 홀수 타일에 피해. (Custom 타깃팅: GetCustomTiles가 홀수 타일 전체 반환)
    /// </summary>
    [System.Serializable]
    public class SolraKnightSkill2 : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("스킬 사용 시 입힐 피해량")]
        [SerializeField] private int damage = 30;

        public SolraKnightSkill2()
        {
            skillName = "플레어";
            description = $"모든 홀수 타일에 {damage} 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override List<TileData> GetCustomTiles(MonsterSkill skill, Monster owner)
        {
            return GameManager.Instance.GetOrbitManager().Tiles
                .Where(tile => tile.TileIndex % 2 == 1)
                .ToList();
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }
}
```

**삭제됨:** `SolraKnightPassive`(구 양력, 타일-위치 기반), 주석 처리된 `SolraKnightDeath` 블록. `천공검`은 `AttackUnits`→`AttackTiles`로 바뀌고 피해 30, `플레어`는 20→30.

- [ ] **Step 2: 컴파일 확인**

Unity 포커스 → `Unity_GetConsoleLogs { types: ["Error"] }`. 기대: 에러 0.
(주의: SolraKnight 프리셋 에셋이 아직 구 `SolraKnightPassive`를 [SerializeReference]로 참조하면 "Missing type" 경고가 뜰 수 있다 — Task 10에서 재배선하면 사라진다. 경고이지 컴파일 에러 아님.)

- [ ] **Step 3: 커밋**

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave4/SolraKnight/SolraKnight.cs"
git commit -m "refactor: 태양의 기사 천공검=타일AoE·플레어=30, 구 위치기반 패시브 제거"
```

---

## Task 7: 달의 기사 스킬 재작업 + 구 패시브 삭제

**Files:**
- Modify: `Assets/Scripts/Data/MonsterPresets/Wave4/LunaKnight/LunaKnight.cs`

기존에는 `LunaKnightSkill1`(그믐달, 단일유닛), `LunaKnightSkill2`(월광, 짝수 타일), `LunaKnightPassive`(음력, 위치 기반)가 있다. `그믐달`을 스펙의 `초승달`(무작위 1명 타일 ±2)로 재작업, 월광 피해 30, 구 패시브 삭제.

- [ ] **Step 1: LunaKnight.cs 전체 교체**

```csharp
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Data.MonsterPresets.Wave4.LunaKnight
{
    /// <summary>
    /// 초승달 — 무작위 대상 1명이 속한 타일 + 좌우 2칸에 피해.
    /// 타깃/범위는 MonsterSkill 설정(RandomCharacter + Tiles + count 1 + range 2)이 담당.
    /// </summary>
    [System.Serializable]
    public class LunaKnightSkill1 : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("스킬 사용 시 입힐 피해량")]
        [SerializeField] private int damage = 30;

        public LunaKnightSkill1()
        {
            skillName = "초승달";
            description = $"무작위 대상 1명이 속한 타일 + 좌우 2칸에 {damage} 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>
    /// 월광 — 모든 짝수 타일에 피해. (Custom 타깃팅: GetCustomTiles가 짝수 타일 전체 반환)
    /// </summary>
    [System.Serializable]
    public class LunaKnightSkill2 : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("스킬 사용 시 입힐 피해량")]
        [SerializeField] private int damage = 30;

        public LunaKnightSkill2()
        {
            skillName = "월광";
            description = $"모든 짝수 타일에 {damage} 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override List<TileData> GetCustomTiles(MonsterSkill skill, Monster owner)
        {
            return GameManager.Instance.GetOrbitManager().Tiles
                .Where(tile => tile.TileIndex % 2 == 0)
                .ToList();
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }
}
```

**삭제됨:** `LunaKnightPassive`(구 음력, 위치 기반). `그믐달`→`초승달`(타일 AoE, 30), 월광 20→30.

- [ ] **Step 2: 컴파일 확인**

Unity 포커스 → `Unity_GetConsoleLogs { types: ["Error"] }`. 기대: 에러 0. ("Missing type" 경고는 Task 10에서 해소.)

- [ ] **Step 3: 커밋**

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave4/LunaKnight/LunaKnight.cs"
git commit -m "refactor: 달의 기사 초승달=타일AoE·월광=30, 구 위치기반 패시브 제거"
```

---

## Task 8: 태양의 사제 단순화 (일식만) + 구 지원스킬/패시브 삭제

**Files:**
- Modify: `Assets/Scripts/Data/MonsterPresets/Wave4/SolraPriest/SolraPriest.cs`

기존에는 `SolraPriestSkill1`(일식, 홀수타일 중심 ±2), `SolraPriestSkill2`(태양의 축복, 힐+데미지), `SolraPriestPassive`(태양의 가호, 적 수×방어도)가 있다. 스펙대로 일식은 `RandomCharacter + range 2` 타깃팅으로 단순화(구 홀수타일 중심 로직 제거), `태양의 축복`은 삭제(→ Task 5 `FactionSupportSkill{Armor}`로 대체), 구 패시브 삭제(→ Task 4 `FactionBlessingPassive`로 대체). 배선은 Task 10.

- [ ] **Step 1: SolraPriest.cs 전체 교체**

```csharp
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;

namespace DiceOrbit.Data.MonsterPresets.Wave4.SolraPriest
{
    /// <summary>
    /// 일식 — 무작위 대상 1명이 속한 타일 + 좌우 2칸에 피해.
    /// 타깃/범위는 MonsterSkill 설정(RandomCharacter + Tiles + count 1 + range 2)이 담당.
    /// (흑점 = 공용 FactionSupportSkill{Armor}, 태양의 가호 = 공용 FactionBlessingPassive — 프리셋에서 배선.)
    /// </summary>
    [System.Serializable]
    public class SolraPriestSkill1 : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("스킬 사용 시 입힐 피해량")]
        [SerializeField] private int damage = 30;

        public SolraPriestSkill1()
        {
            skillName = "일식";
            description = $"무작위 대상 1명이 속한 타일 + 좌우 2칸에 {damage} 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }
}
```

**삭제됨:** `SolraPriestSkill2`(태양의 축복), `SolraPriestPassive`(구 태양의 가호), 일식의 홀수타일 `GetCustomTiles`/`range` 필드.

- [ ] **Step 2: 컴파일 확인**

Unity 포커스 → `Unity_GetConsoleLogs { types: ["Error"] }`. 기대: 에러 0. ("Missing type" 경고는 Task 10에서 해소.)

- [ ] **Step 3: 커밋**

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave4/SolraPriest/SolraPriest.cs"
git commit -m "refactor: 태양의 사제 일식만 남기고 단순화(공용 지원스킬/가호로 대체)"
```

---

## Task 9: 달의 사제 단순화 (월식만) + 구 지원스킬/패시브/버프 삭제

**Files:**
- Modify: `Assets/Scripts/Data/MonsterPresets/Wave4/LunaPriest/LunaPriest.cs`

기존에는 `LunaPriestSkill1`(월식, 짝수타일 ±2), `LunaPriestSkill2`(서늘한 달빛, 방어도+데미지), `LunaPriestPassive`(만월, 적 수×버프), 그리고 파일 하단 `DiceOrbit.Systems.Effects.LunaPriestBuff` 상태효과가 있다. 월식만 남기고 단순화, 나머지 삭제(만월 힐 = Task 5 `FactionSupportSkill{Heal}`, 달의 가호 = Task 4 `FactionBlessingPassive`).

- [ ] **Step 1: LunaPriestBuff 외부 참조 없음 확인**

Grep으로 `LunaPriestBuff` 사용처를 확인한다. `LunaPriest.cs` 안(패시브)에서만 쓰여야 한다.

Run: `Unity_Grep` 또는 리포지토리 Grep — 패턴 `LunaPriestBuff`.
Expected: `LunaPriest.cs` 외 히트 없음. (있으면 그 참조부터 정리 — 이 계획 범위 밖이면 STOP 후 보고.)

- [ ] **Step 2: LunaPriest.cs 전체 교체**

```csharp
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;

namespace DiceOrbit.Data.MonsterPresets.Wave4.LunaPriest
{
    /// <summary>
    /// 월식 — 무작위 대상 1명이 속한 타일 + 좌우 2칸에 피해.
    /// 타깃/범위는 MonsterSkill 설정(RandomCharacter + Tiles + count 1 + range 2)이 담당.
    /// (만월 = 공용 FactionSupportSkill{Heal}, 달의 가호 = 공용 FactionBlessingPassive — 프리셋에서 배선.)
    /// </summary>
    [System.Serializable]
    public class LunaPriestSkill1 : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("스킬 사용 시 입힐 피해량")]
        [SerializeField] private int damage = 30;

        public LunaPriestSkill1()
        {
            skillName = "월식";
            description = $"무작위 대상 1명이 속한 타일 + 좌우 2칸에 {damage} 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }
}
```

**삭제됨:** `LunaPriestSkill2`(서늘한 달빛), `LunaPriestPassive`(구 만월 패시브), `LunaPriestBuff` 상태효과 클래스와 그 `DiceOrbit.Systems.Effects` 네임스페이스 블록, 월식의 짝수타일 `GetCustomTiles`.

- [ ] **Step 3: 컴파일 확인**

Unity 포커스 → `Unity_GetConsoleLogs { types: ["Error"] }`. 기대: 에러 0. ("Missing type" 경고는 Task 10에서 해소.)

- [ ] **Step 4: 커밋**

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave4/LunaPriest/LunaPriest.cs"
git commit -m "refactor: 달의 사제 월식만 남기고 단순화(공용 지원스킬/가호로 대체)"
```

---

## Task 10: 프리셋 에셋 배선 (에디터)

**Files (수정 — 에셋):**
- 4개 `MonsterPreset` 에셋 (태양의 기사/사제, 달의 기사/사제).

코드가 아니라 Inspector 배선이다. `[SerializeReference]`(AIPattern·skillData·StartingPassives)는 MCP로 안정적으로 세팅하기 어려우므로 **Unity Inspector에서 직접** 배선한다(드롭다운으로 클래스 선택 후 값 입력). 값은 스펙 확정값이다.

- [ ] **Step 1: 프리셋 에셋 경로 찾기**

Run: Glob `Assets/**/Wave4/**/*.asset` (또는 Unity에서 `t:MonsterPreset` 검색).
Expected: 태양의 기사/사제, 달의 기사/사제 4개 `MonsterPreset` 에셋. 경로를 기록한다.

- [ ] **Step 2: 태양의 기사 프리셋 배선**

- `BaseStats`: `MonsterName = "태양의 기사"`, `MaxHP = 70`, `CurrentHP = 70`.
- `Faction = Sun`.
- `AIPattern = SequentialPattern`, `availableSkills`(순서대로 3개):
  1. `skillData = SolraKnightSkill1`(천공검, damage 30); `TargetStrategy = RandomCharacter`, `TargetType = Tiles`, `targetCount = 2`, `targetRange = 1`, `IntentType = Attack`.
  2. 1번과 동일한 천공검 MonsterSkill 한 개 더.
  3. `skillData = SolraKnightSkill2`(플레어, damage 30); `TargetStrategy = Custom`, `TargetType = Tiles`, `IntentType = Attack`.
- `StartingPassives`(1개): `TurnParityWeaknessPassive` — `passiveName = "양력"`, `triggerOnOddTurn = ✔(true)`, `percent = 30`, `durationTurns = 1`.

- [ ] **Step 3: 태양의 사제 프리셋 배선**

- `BaseStats`: `MonsterName = "태양의 사제"`, `MaxHP = 70`, `CurrentHP = 70`.
- `Faction = Sun`.
- `AIPattern = RandomPattern`, `availableSkills`(2개, 50/50):
  1. `skillData = FactionSupportSkill`(흑점) — `skillLabel = "흑점"`, `kind = Armor`, `amount = 10`; `TargetStrategy = Self`, `TargetType = Self`, `IntentType = Defend`.
  2. `skillData = SolraPriestSkill1`(일식, damage 30); `TargetStrategy = RandomCharacter`, `TargetType = Tiles`, `targetCount = 1`, `targetRange = 2`, `IntentType = Attack`.
- `StartingPassives`(1개): `FactionBlessingPassive` — `passiveName = "태양의 가호"`, `triggerOnOddTurn = ✔(true)`, `amount = 3`, `skipFirstTurn = ✔(true)`.

- [ ] **Step 4: 달의 기사 프리셋 배선**

- `BaseStats`: `MonsterName = "달의 기사"`, `MaxHP = 70`, `CurrentHP = 70`.
- `Faction = Moon`.
- `AIPattern = SequentialPattern`, `availableSkills`(순서대로 3개):
  1. `skillData = LunaKnightSkill2`(월광, damage 30); `TargetStrategy = Custom`, `TargetType = Tiles`, `IntentType = Attack`.
  2. `skillData = LunaKnightSkill1`(초승달, damage 30); `TargetStrategy = RandomCharacter`, `TargetType = Tiles`, `targetCount = 1`, `targetRange = 2`, `IntentType = Attack`.
  3. 2번과 동일한 초승달 MonsterSkill 한 개 더.
- `StartingPassives`(1개): `TurnParityWeaknessPassive` — `passiveName = "음력"`, `triggerOnOddTurn = ☐(false)`, `percent = 30`, `durationTurns = 1`.

- [ ] **Step 5: 달의 사제 프리셋 배선**

- `BaseStats`: `MonsterName = "달의 사제"`, `MaxHP = 70`, `CurrentHP = 70`.
- `Faction = Moon`.
- `AIPattern = RandomPattern`, `availableSkills`(2개, 50/50):
  1. `skillData = FactionSupportSkill`(만월) — `skillLabel = "만월"`, `kind = Heal`, `amount = 10`; `TargetStrategy = Self`, `TargetType = Self`, `IntentType = Buff`.
  2. `skillData = LunaPriestSkill1`(월식, damage 30); `TargetStrategy = RandomCharacter`, `TargetType = Tiles`, `targetCount = 1`, `targetRange = 2`, `IntentType = Attack`.
- `StartingPassives`(1개): `FactionBlessingPassive` — `passiveName = "달의 가호"`, `triggerOnOddTurn = ☐(false)`, `amount = 3`, `skipFirstTurn = ☐(false)`.

- [ ] **Step 6: 저장 + 컴파일/에셋 확인**

Unity에서 `Ctrl+S`(프로젝트 저장). `Unity_GetConsoleLogs { types: ["Error"] }` — 에러 0, 그리고 4개 프리셋의 [SerializeReference] 슬롯에 "Missing type"이 남아있지 않은지 확인한다.

- [ ] **Step 7: 커밋**

```bash
git add "Assets/**/Wave4/**/*.asset"
git commit -m "feat: Wave4 태양/달 세트 프리셋 배선 (진영/스킬/패시브/값)"
```

---

## Task 11: 인카운터·액트 배정 + 플레이 검증

**Files:**
- Wave4 `EncounterDefinition` 에셋, `ActDefinition` 에셋 (경로는 Step 1에서 확인).

- [ ] **Step 1: Wave4 EncounterDefinition 확인/구성**

Run: Glob `Assets/**/*Encounter*.asset` 및 `Assets/**/*Act*.asset` (또는 Unity `t:EncounterDefinition`, `t:ActDefinition`).
- Wave4용 EncounterDefinition이 있으면 태양/달 4종 프리셋(또는 원하는 조합)을 몬스터 목록에 배정.
- 없으면 새 EncounterDefinition을 만들고 4종 프리셋을 등록.
- 해당 EncounterDefinition을 `ActDefinition`의 적절한 티어(Wave4/전투 노드)에 배정.

(정확한 필드명은 기존 다른 Wave의 EncounterDefinition 에셋을 그대로 참고해 동일 형식으로 채운다.)

- [ ] **Step 2: 플레이 진입 (DX11 주의)**

이 머신은 D3D12+Intel Arc에서 하드 크래시 이력이 있어 그래픽 API가 DX11로 고정돼 있어야 한다(메모리 `unity-d3d12-crash-dx11-fix`). Play 진입 후 즉시 크래시하면 그래픽 API를 먼저 확인한다.

Unity Play → Wave4 전투 노드까지 진행.

- [ ] **Step 3: 스펙 §9 체크리스트 검증**

전투에서 확인:
- **홀수턴**: 태양 기사·사제에 "받는 피해 +30%" 디버프 표시(FrostbiteDebuff 아이콘=동상으로 표기됨 — 표기만 임시). 태양 사제 가호로 태양 유닛 공격력이 **턴 3부터** +3씩 누적.
- **짝수턴**: 달 기사·사제에 받는 피해 +30%. 달 사제 가호로 달 유닛 공격력 +3 누적(턴 2부터).
- **피해량**: 천공검=무작위 2명 타일 ±1에 30 / 플레어=홀수 타일 전체 30 / 월광=짝수 타일 전체 30 / 일식·초승달·월식=무작위 1명 타일 ±2에 30.
- **지원**: 흑점=태양 진영 방어도 +10, 만월=달 진영 체력 +10.
- **패턴 순서**: 태양기사 천공검→천공검→플레어 반복, 달기사 월광→초승달→초승달 반복, 사제는 50/50.
- **회귀**: 기존 다른 몬스터/전투 정상. Faction=None 몹은 세트 로직 무시.

Console(`Unity_GetConsoleLogs`)에서 `[양력]`/`[음력]`/`[태양의 가호]`/`[달의 가호]`/`[흑점]`/`[만월]` 로그로 발동을 교차 확인한다.

- [ ] **Step 4: 홀짝/타이밍 튜닝 (필요 시)**

- 태양이 짝수턴에 약해지는 등 홀짝이 뒤집혀 보이면, 프리셋의 `triggerOnOddTurn` 체크를 반전(코드 수정 없음).
- 받는 피해 +30%가 다음 플레이어 턴을 못 덮으면(디버프가 몬스터 턴 종료 시 사라지면) `TurnParityWeaknessPassive.durationTurns`를 프리셋에서 2로 올린다.
- 조정 후 재검증.

- [ ] **Step 5: 최종 커밋**

```bash
git add "Assets/**/*Encounter*.asset" "Assets/**/*Act*.asset"
git commit -m "feat: Wave4 태양/달 세트 인카운터·액트 배정"
```

- [ ] **Step 6: 개발 브랜치 마무리**

Announce: "I'm using the finishing-a-development-branch skill to complete this work." 후 `superpowers:finishing-a-development-branch` 로 마무리(테스트/커밋 정리 → 푸시/PR 옵션 제시).

---

## Self-Review

**스펙 커버리지:**
- MonsterFaction enum + 필드 + Monster.Faction → Task 1, 2 ✔
- 양력/음력(리듬 약화, 자신 받는피해+30%) → Task 3 + Task 10 배선 ✔
- 가호(같은 진영 영구 +3, 태양 첫턴 제외) → Task 4 + Task 10 배선 ✔
- 흑점(방어도10)/만월(회복10) → Task 5 + Task 10 배선 ✔
- 천공검(무2±1,30)/플레어(홀타일,30) → Task 6 + Task 10 ✔
- 월광(짝타일,30)/초승달(무1±2,30) → Task 7 + Task 10 ✔
- 일식(무1±2,30) → Task 8 + Task 10 ✔
- 월식(무1±2,30) → Task 9 + Task 10 ✔
- 패턴(태양기사 순차 1·1·2, 달기사 순차, 사제 50/50) → Task 10 ✔
- HP 70 전원 → Task 10 (BaseStats) ✔
- 인카운터/액트 배정 → Task 11 ✔
- 플레이 검증(§9) → Task 11 ✔

**플레이스홀더 스캔:** 코드 스텝은 전부 완전한 코드. 에디터 스텝(Task 10~11)은 확정 수치 명시. "TBD"/"적절히" 없음.

**타입 일관성:** `MonsterFaction`(enum), `Monster.Faction`, `FactionSupportSkill.SupportKind`, `TurnParityWeaknessPassive.triggerOnOddTurn/percent/durationTurns`, `FactionBlessingPassive.triggerOnOddTurn/amount/skipFirstTurn`, `FrostbiteDebuff(percent,duration)`, `BuffAttackStatus(value,-1){IsStackable=true}`, `Stats.TempArmor`, `Unit.Heal(int)`, `CombatManager.TurnCount/ActiveMonsters`, `EventPhase.TurnStart`, `CombatTrigger.OnPreAction`, `SkillData.AttackTiles`, `SequentialPattern/RandomPattern.availableSkills` — 태스크 전반에서 시그니처 일치 확인됨.

**알려진 리스크(플레이에서 확정):**
1. `TurnCount` 홀짝이 태양=홀수와 맞는지 — Task 11 Step4에서 `triggerOnOddTurn` 반전으로 튜닝.
2. `durationTurns=1`이 다음 플레이어 턴을 덮는지 — 안 덮으면 2로 상향(프리셋 필드).
3. FrostbiteDebuff 표기가 "동상"으로 나옴 — 기능은 정확, 표기 라벨은 후속 폴리시.
