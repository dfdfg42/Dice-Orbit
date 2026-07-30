# Wave5 불꽃 세트 몬스터 (Set B) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wave5 "불꽃" 세트 4종(불꽃 소녀[보스]·요정·인형·오르골)과 신규 불꽃 타일 시스템을 구현한다.

**Architecture:** 불꽃 타일은 기존 `TileAttribute`(OnEndTurn 피해 / OnTraverse 소화) 위에 얹고, 통과-소화 캡·피해누적·보스 피해감소는 **기존 `StatusEffect` 시스템**으로 처리한다(일반 클래스에 새 필드 없음, 정적 전역 없음). 설치/개수는 기존 `TileData.AddAttribute`/`OrbitManager.Tiles` 직접 사용. 조건부 AI는 보스 1종만 신규 `FlameGirlPattern`, 나머지는 기존 `Sequential`/`Random` 재사용.

**Tech Stack:** Unity 6000.3.8f1, C#, `[SerializeReference]` 다형성(SkillData/PassiveAbility/MonsterAI/TileAttribute), New Input System, URP. 유닛 테스트 프레임워크 없음.

**스펙:** `Docs/superpowers/specs/2026-07-30-wave5-flame-monsters-design.md`
**설계 원칙 메모리:** 일반 클래스 필드/정적 전역 대신 기존 확장 시스템 재사용.

---

## 검증 방식 (이 프로젝트 특성)

자동화 테스트 프로젝트가 없다. 각 코드 태스크는 **컴파일 에러 0**으로 마무리하고, 마지막에 **플레이 검증**을 한다.

**컴파일 확인 (매 코드 태스크 끝):** Unity 에디터가 열려 있으면 MCP로 재컴파일 후 콘솔 확인.
1. `Unity_RunCommand`으로 `AssetDatabase.Refresh()` 실행 → 스크립트 재컴파일 트리거.
2. `Unity_GetConsoleLogs { logTypes: "Error" }` → 편집 파일 관련 컴파일 에러 0 확인.
3. (강한 확인) 새 타입을 참조하는 `Unity_RunCommand` 스니펫이 컴파일·실행되면 프로젝트 어셈블리 빌드 성공 확정.
- 헤드리스 대안: VS2022 MSBuild + Unity mono 4.7.1-api (`verify_unity_compile` 메모리).

**커밋:** 한국어 `feat:`/`refactor:` 접두어, 마지막 줄에 `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.

**브랜치:** 현재 `feature/event-outcomes-refactor-20260729`에서 진행.

**핵심 확인된 사실 (구현 근거):**
- `TileAttribute`(`.../Tile/TileAttribute.cs`): ctor `(TileAttributeType type, int value, int duration, bool isStackable=false)`, `Owner`(TileData), 가상 `OnArrive/OnTraverse/OnEndTurn(Character)`. `TileAttributeType` enum도 이 파일.
- `TileData`: `AddAttribute`(같은 Type 있으면 무시=타일당 1개), `RemoveAttribute(TileAttribute)`, `RemoveAttributeType(TileAttributeType)`, `HasAttribute(type)`, `GetCharactersOnTile()`. 타일 피해 예시 = `RandMineTile`: `new AttackContext(null, target, "이름", Value)` + `CombatPipeline.Instance?.Process(ctx)`, 그 후 `Owner.RemoveAttribute(this)`.
- `StatusEffect`(`.../Combat/Effects/StatusEffect.cs`, ns `DiceOrbit.Systems.Effects`): ctor `(EffectType, value, duration, isStackable=false)`, `Owner`(Unit), 가상 `OnAttack(CombatTrigger,AttackContext)`/`OnTurnEvent(...)`. 기본 OnTurnEvent가 TurnStart(OnPostAction, SourceUnit==Owner)에 Duration 감소.
- `EffectType` enum: `.../Combat/EffectData.cs` (ns `DiceOrbit.Data`).
- `StatusEffectManager`: `AddEffect`(같은 Type이면 AddStack+RefreshDuration로 갱신), `HasEffect(type)`, `GetEffectValue(type)`(Value 반환), `RemoveEffect(type)`.
- `BuffAttackStatus(value,duration)` = OnCalculateOutput에서 `context.OutputValue += Value`(자신이 공격자일 때). `FrostbiteDebuff` = OnCalculateOutput에서 `context.Target==Owner`면 `OutputValue *= 1+Value/100f`.
- 턴 흐름(`CombatManager.cs`): `StartPlayerTurn`에서 `turnCount++`(턴1=홀수), 캐릭터 `OnStartTurn`. `StartMonsterTurn`에서 몹 `OnStartTurn`(몹 패시브 TurnStart 발동 지점). `ExecuteMonsterTurn`에서 `ExecuteIntent`. 몹 TurnEnd 이벤트 존재(`FrostbitePassive`가 `OnPostAction && Phase==TurnEnd && SourceUnit==owner` 사용).
- `MonsterAI`(ns `DiceOrbit.Data.MonsterAI`): `protected Monster owner`, `public List<MonsterSkill> availableSkills`, abstract `GetNextSkill()`, `Initialize(Monster)`.
- `Monster.Stats.HPRatio`(0~1), `Stats.TempArmor`, `Unit.Heal(int)`, `Monster.Faction`(Wave4에서 추가됨), `CombatManager.Instance.ActiveMonsters`(List<Monster>).
- 몹 패시브 TurnStart 트리거 = `CombatTrigger.OnPreAction && context.Phase == EventPhase.TurnStart && context.SourceUnit == owner` (Wave4 검증).

---

## File Structure

**신규**
- `Assets/Scripts/Data/MonsterPresets/Wave5/Shared/FireTile.cs` — `FireTile : TileAttribute` + 불꽃 상태효과 3종(소화 마커/피해 누적/보스 가호).
- `.../Wave5/Shared/FlameSetSkills.cs` — 공용 스킬 `FlameTileDamageSkill`(타일 피해), `PlaceFireSkill`(불꽃 설치).
- `.../Wave5/Shared/FlameSet.cs` — 보스 조회 순수 헬퍼 `FlameSet.FindBoss()`.
- `.../Wave5/Shared/FlameGirlPattern.cs` — 보스 조건부 AI.
- `.../Wave5/FlameGirl/FlameGirl.cs` — `FireballSkill`, `ConflagrationSkill`, `FlameStagePassive`, `IncinerationPassive`.
- `.../Wave5/FlameFairy/FlameFairy.cs` — `PlayingWithFirePassive`, `KindlingSkill`.
- `.../Wave5/FlameDoll/FlameDoll.cs` — `FlameGraceGuardPassive`, `FlameShieldSkill`.
- `.../Wave5/FlameMusicBox/FlameMusicBox.cs` — `FlameSongPassive`, `BurningHeatSkill`.
- 프리셋 에셋 4종(Phase 6에서 생성).

**수정**
- `.../Tile/TileAttribute.cs` — `TileAttributeType.Flame` 추가 + `GetDisplayName` case.
- `.../Combat/EffectData.cs` — `EffectType` 값 3개 추가(`FireExtinguishMark`, `FireDamageTaken`, `FireGuard`).

**변경 없음(원칙):** `CharacterStats`/`Character`/`Monster`/`MonsterStats`.

---

## Phase 1 — 불꽃 타일 시스템 (토대)

### Task 1: enum 값 추가 (Flame / 상태효과 3종)

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/TileAttribute.cs`
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/EffectData.cs`

- [ ] **Step 1: `TileAttributeType`에 Flame 추가**

`TileAttribute.cs`의 enum 마지막 항목(`Disharmony,`) 다음에 추가:

```csharp
        Harmony,        // 조화: 턴 종료 시 최대체력 V% 회복 (이벤트)
        Disharmony,     // 부조화: 턴 종료 시 최대체력 V% 피해 (이벤트)
        Flame,          // 불꽃: 턴 종료 시 V 피해 / 통과 시 소화 (Wave5)
```

그리고 `GetDisplayName()` switch의 `Disharmony` 케이스 다음에 추가:

```csharp
                TileAttributeType.Disharmony => "부조화",
                TileAttributeType.Flame => "불꽃",
```

- [ ] **Step 2: `EffectType`에 값 3개 추가**

`EffectData.cs`의 enum 마지막 항목(`Power,`) 다음에 추가:

```csharp
        Power,           // 파워: 가하는 피해 +V% (포션)
        FireExtinguishMark, // 불꽃 소화 마커: 이번 턴 이미 불을 껐음 (Wave5)
        FireDamageTaken,    // 불꽃 요정용: 이번 턴 받은 누적 피해 (Wave5)
        FireGuard,          // 불의 가호: 방어도 보유 시 받는 피해 감소 (Wave5)
```

- [ ] **Step 3: 컴파일 확인**

Unity 포커스/Refresh → `Unity_GetConsoleLogs { logTypes: "Error" }`. 기대: 에러 0.

- [ ] **Step 4: 커밋**

```bash
git add "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/TileAttribute.cs" \
        "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/EffectData.cs"
git commit -m "feat: 불꽃 타일/상태효과용 enum 값 추가 (Flame, Fire* EffectType)"
```

### Task 2: FireTile + 불꽃 상태효과 3종

**Files:**
- Create: `Assets/Scripts/Data/MonsterPresets/Wave5/Shared/FireTile.cs`

- [ ] **Step 1: 파일 생성**

```csharp
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// 불꽃 타일. 설치원 무관 동일 효과:
    /// - OnEndTurn: 그 위 캐릭터에 damage 피해(파이프라인 — 방어도/디버프 적용).
    /// - OnTraverse: 캐릭터가 이번 턴 아직 불을 안 껐으면(소화 마커 없음) 이 타일 삭제 + 마커 부여(1턴).
    /// 타일당 1개(AddAttribute가 Type 중복 무시), 영구(duration -1, 몬스터 사망해도 잔존).
    /// </summary>
    public class FireTile : TileAttribute
    {
        public FireTile(int damage = 35) : base(TileAttributeType.Flame, damage, -1) { }

        public override void OnEndTurn(Core.Character character)
        {
            if (character == null || !character.IsAlive) return;
            var ctx = new AttackContext(null, character, "불꽃", Value);
            CombatPipeline.Instance?.Process(ctx);
        }

        public override void OnTraverse(Core.Character character)
        {
            if (character == null || !character.IsAlive || character.StatusEffects == null) return;
            if (character.StatusEffects.HasEffect(EffectType.FireExtinguishMark)) return; // 이번 턴 이미 1개 소화함
            Owner?.RemoveAttribute(this);
            character.StatusEffects.AddEffect(new FireExtinguishMarkStatus());
        }

        public override string GetDescription() => $"턴 종료 시 {Value} 피해, 통과 시 소화(턴당 1개), 영구";
    }
}

namespace DiceOrbit.Systems.Effects
{
    /// <summary>불꽃 소화 마커: 존재 여부만 사용(전투 효과 없음). 지속 1턴 → 다음 턴 자동 소멸.</summary>
    public class FireExtinguishMarkStatus : StatusEffect
    {
        public FireExtinguishMarkStatus() : base(DiceOrbit.Data.EffectType.FireExtinguishMark, 0, 1) { }
    }

    /// <summary>
    /// 불꽃 요정용: 이번 턴 받은 누적 피해. 소유자가 피격될 때(OnCalculateOutput, Target==Owner) 누적,
    /// 소유자 턴 종료 시 0으로 리셋. 영구(-1)라 만료되지 않음.
    /// </summary>
    public class FireDamageTakenStatus : StatusEffect
    {
        public FireDamageTakenStatus() : base(DiceOrbit.Data.EffectType.FireDamageTaken, 0, -1) { }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (Owner == null) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.Target != Owner) return;
            Value += Mathf.Max(0, Mathf.RoundToInt(context.OutputValue));
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            // 소유자 턴 종료(실행 이후)에 이번 턴 누적을 0으로. Duration -1이라 base 감소 무영향.
            if (Owner != null && context.Phase == EventPhase.TurnEnd && context.SourceUnit == Owner)
                Value = 0;
        }
    }

    /// <summary>
    /// 불의 가호: 소유자(불꽃 소녀)가 방어도>0일 때 받는 피해 Value% 감소. 인형이 매턴 1턴짜리로 갱신.
    /// </summary>
    public class FlameGuardStatus : StatusEffect
    {
        public FlameGuardStatus(int percent, int duration) : base(DiceOrbit.Data.EffectType.FireGuard, percent, duration) { }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (Owner == null || Owner.Stats == null) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.Target != Owner) return;
            if (Owner.Stats.TempArmor <= 0) return; // 방어도 게이트
            context.OutputValue *= 1f - Value / 100f;
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

Refresh → `Unity_GetConsoleLogs { logTypes: "Error" }` = 0.

- [ ] **Step 3: 커밋**

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave5/Shared/FireTile.cs"
git commit -m "feat: 불꽃 타일(FireTile) + 소화마커/피해누적/가호 상태효과"
```

### Task 3: 공용 스킬 (타일 피해 / 불꽃 설치) + 보스 조회 헬퍼

**Files:**
- Create: `Assets/Scripts/Data/MonsterPresets/Wave5/Shared/FlameSetSkills.cs`
- Create: `Assets/Scripts/Data/MonsterPresets/Wave5/Shared/FlameSet.cs`

- [ ] **Step 1: `FlameSet.cs` (보스 조회 순수 헬퍼)**

```csharp
using System.Linq;
using DiceOrbit.Core;

namespace DiceOrbit.Data.MonsterPresets.Wave5.Shared
{
    /// <summary>불꽃 세트 공용 조회(순수 함수, 상태 없음).</summary>
    public static class FlameSet
    {
        public const string BossName = "불꽃 소녀";

        /// <summary>살아있는 불꽃 소녀(보스)를 이름으로 찾는다. 없으면 null.</summary>
        public static Monster FindBoss()
        {
            var monsters = CombatManager.Instance?.ActiveMonsters;
            if (monsters == null) return null;
            return monsters.FirstOrDefault(m => m != null && m.IsAlive && m.Stats != null && m.Stats.MonsterName == BossName);
        }
    }
}
```

- [ ] **Step 2: `FlameSetSkills.cs` (공용 스킬 2종)**

```csharp
using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.MonsterPresets.Wave5.Shared
{
    /// <summary>
    /// 타일 피해 공용 스킬 — 대상 타일(MonsterSkill 타깃팅으로 결정)에 damage 피해.
    /// 불똥별/화염 폭발(RandomCharacter+Tiles+range2), 불의 저주(RandomTiles+count6) 등에 재사용.
    /// </summary>
    [System.Serializable]
    public class FlameTileDamageSkill : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("스킬 표시 이름")]
        [SerializeField] private string skillLabel = "화염";
        [SerializeField] private int damage = 30;

        public override string SkillName => string.IsNullOrEmpty(skillLabel) ? "화염" : skillLabel;
        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>
    /// 불꽃 설치 공용 스킬 — 대상 타일(MonsterSkill 타깃팅)에 불꽃 타일 설치(피해 없음). [발화].
    /// </summary>
    [System.Serializable]
    public class PlaceFireSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private string skillLabel = "발화";
        [SerializeField] private int fireDamage = 35;

        public override string SkillName => string.IsNullOrEmpty(skillLabel) ? "발화" : skillLabel;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            if (targetTiles == null) return;
            foreach (var tile in targetTiles)
            {
                if (tile == null || tile.HasAttribute(TileAttributeType.Flame)) continue;
                tile.AddAttribute(new FireTile(fireDamage));
            }
        }
    }
}
```

- [ ] **Step 3: 컴파일 확인 + 커밋**

Refresh → 콘솔 에러 0.

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave5/Shared/FlameSet.cs" \
        "Assets/Scripts/Data/MonsterPresets/Wave5/Shared/FlameSetSkills.cs"
git commit -m "feat: 불꽃 공용 스킬(타일피해/설치) + 보스 조회 헬퍼"
```

---

## Phase 2 — 불꽃 소녀 (보스)

### Task 4: 보스 스킬·패시브 + 조건부 AI

**Files:**
- Create: `Assets/Scripts/Data/MonsterPresets/Wave5/FlameGirl/FlameGirl.cs`
- Create: `Assets/Scripts/Data/MonsterPresets/Wave5/Shared/FlameGirlPattern.cs`

- [ ] **Step 1: `FlameGirl.cs` (스킬 2 + 패시브 2)**

```csharp
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Data.MonsterPresets.Wave5.Shared;

namespace DiceOrbit.Data.MonsterPresets.Wave5.FlameGirl
{
    /// <summary>[화염구] 무작위 대상 1명 타일 ±(2 + (불꽃≥7 ? 1 : 0))에 damage 피해.</summary>
    [System.Serializable]
    public class FireballSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 35;
        [Tooltip("기본 좌우 범위")]
        [SerializeField] private int baseRange = 2;

        public FireballSkill() { skillName = "화염구"; description = "무작위 대상 1명 타일 좌우에 35 피해(불꽃 7개↑ 시 범위 +1)"; }

        public override int GetPreviewDamage() => damage;

        public override List<TileData> GetCustomTiles(MonsterSkill skill, Monster owner)
        {
            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit == null) return new List<TileData>();
            int flames = orbit.Tiles.Count(t => t != null && t.HasAttribute(TileAttributeType.Flame));
            int range = baseRange + (flames >= 7 ? 1 : 0);

            var alive = PartyManager.Instance?.GetAliveCharacters();
            if (alive == null || alive.Count == 0) return new List<TileData>();
            var center = alive[Random.Range(0, alive.Count)].CurrentTile;
            if (center == null) return new List<TileData>();

            int total = orbit.Tiles.Count;
            var result = new List<TileData>();
            for (int i = -range; i <= range; i++)
            {
                int idx = (center.TileIndex + i) % total;
                if (idx < 0) idx += total;
                result.Add(orbit.GetTile(idx));
            }
            return result.Distinct().ToList();
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
            => AttackTiles(source, targetTiles, damage);
    }

    /// <summary>[대화재] 모든 불꽃 타일 삭제 + 모든 타일에 damage 피해.</summary>
    [System.Serializable]
    public class ConflagrationSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 35;

        public ConflagrationSkill() { skillName = "대화재"; description = "모든 불꽃 타일 삭제 + 모든 타일에 35 피해"; }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit == null) return;
            foreach (var t in orbit.Tiles.ToList())
                t?.RemoveAttributeType(TileAttributeType.Flame);
            AttackTiles(source, orbit.Tiles, damage);
        }
    }

    /// <summary>[타오르는 무대] 턴시작: 불꽃 n개. n≥4 → 방어도 +n*3. (범위 +1은 n≥7일 때 화염구가 직접 계산.)</summary>
    [System.Serializable]
    public class FlameStagePassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [SerializeField] private int armorPerTile = 3;

        public FlameStagePassive()
        {
            passiveName = "타오르는 무대";
            description = "턴 시작 시 불꽃 4개↑면 개수×3 방어도, 7개↑면 화염구 범위 +1";
            priority = 10; isStackable = false;
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null || owner.Stats == null) return;
            if (trigger != CombatTrigger.OnPreAction || context.Phase != EventPhase.TurnStart || context.SourceUnit != owner) return;

            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit == null) return;
            int n = orbit.Tiles.Count(t => t != null && t.HasAttribute(TileAttributeType.Flame));
            if (n >= 4)
            {
                owner.Stats.TempArmor += n * armorPerTile;
                Debug.Log($"[타오르는 무대] 불꽃 {n}개 → 방어도 +{n * armorPerTile}");
            }
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    /// <summary>[소각] HP 50%↓ 최초 1회, 무작위 8타일에 불꽃 설치.</summary>
    [System.Serializable]
    public class IncinerationPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [SerializeField] private int tileCount = 8;
        [SerializeField] private int fireDamage = 35;

        [System.NonSerialized] private bool fired = false;

        public IncinerationPassive()
        {
            passiveName = "소각";
            description = "체력 50% 이하로 떨어지면 최초 1회 무작위 8타일에 불꽃 설치";
            priority = 10; isStackable = false;
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null || owner.Stats == null) return;
            if (trigger != CombatTrigger.OnPreAction || context.Phase != EventPhase.TurnStart || context.SourceUnit != owner) return;
            if (fired || owner.Stats.HPRatio > 0.5f) return;

            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit == null) return;
            var candidates = orbit.Tiles.Where(t => t != null && !t.HasAttribute(TileAttributeType.Flame)).ToList();
            int place = Mathf.Min(tileCount, candidates.Count);
            for (int i = 0; i < place; i++)
            {
                int r = Random.Range(0, candidates.Count);
                candidates[r].AddAttribute(new FireTile(fireDamage));
                candidates.RemoveAt(r);
            }
            fired = true;
            Debug.Log($"[소각] HP50%↓ → 불꽃 {place}개 설치");
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }
}
```

- [ ] **Step 2: `FlameGirlPattern.cs` (조건부 AI)**

```csharp
using System.Linq;
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.MonsterPresets.Wave5.Shared
{
    /// <summary>
    /// 불꽃 소녀 AI. availableSkills 순서 = [0 발화, 1 화염구, 2 대화재].
    /// 불꽃 타일 10개↑ → 대화재(2), 아니면 발화/화염구 랜덤(0 또는 1).
    /// </summary>
    [System.Serializable]
    public class FlameGirlPattern : DiceOrbit.Data.MonsterAI.MonsterAI
    {
        public override MonsterSkill GetNextSkill()
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;

            var orbit = GameManager.Instance?.GetOrbitManager();
            int n = orbit == null ? 0 : orbit.Tiles.Count(t => t != null && t.HasAttribute(TileAttributeType.Flame));

            if (n >= 10 && availableSkills.Count >= 3) return availableSkills[2]; // 대화재
            return availableSkills[Random.Range(0, Mathf.Min(2, availableSkills.Count))]; // 발화/화염구
        }
    }
}
```

- [ ] **Step 3: 컴파일 확인 + 커밋**

Refresh → 콘솔 에러 0. (참고: 화염 폭발/불똥별은 공용 `FlameTileDamageSkill` 재사용, 발화는 `PlaceFireSkill`.)

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave5/FlameGirl/FlameGirl.cs" \
        "Assets/Scripts/Data/MonsterPresets/Wave5/Shared/FlameGirlPattern.cs"
git commit -m "feat: 불꽃 소녀 화염구/대화재/타오르는무대/소각 + FlameGirlPattern"
```

---

## Phase 3 — 불꽃 요정

### Task 5: 요정 패시브 + 불짚이기

**Files:**
- Create: `Assets/Scripts/Data/MonsterPresets/Wave5/FlameFairy/FlameFairy.cs`

- [ ] **Step 1: 파일 생성**

```csharp
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.MonsterPresets.Wave5.FlameFairy
{
    /// <summary>[불장난] 턴시작: 무작위 2타일에 불꽃 설치. + 요정에 피해누적 상태 부여(불짚이기용).</summary>
    [System.Serializable]
    public class PlayingWithFirePassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [SerializeField] private int tileCount = 2;
        [SerializeField] private int fireDamage = 35;

        public PlayingWithFirePassive()
        {
            passiveName = "불장난";
            description = "턴 시작 시 무작위 2타일에 불꽃 설치";
            priority = 10; isStackable = false;
        }

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            // 불짚이기용 이번-턴-피해 누적 상태를 최초 1회 부여(영구, 자기-리셋). 이미 있으면 갱신(무해).
            if (Owner?.StatusEffects != null)
                Owner.StatusEffects.AddEffect(new FireDamageTakenStatus());
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnPreAction || context.Phase != EventPhase.TurnStart || context.SourceUnit != owner) return;

            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit == null) return;
            var candidates = orbit.Tiles.Where(t => t != null && !t.HasAttribute(TileAttributeType.Flame)).ToList();
            int place = Mathf.Min(tileCount, candidates.Count);
            for (int i = 0; i < place; i++)
            {
                int r = Random.Range(0, candidates.Count);
                candidates[r].AddAttribute(new FireTile(fireDamage));
                candidates.RemoveAt(r);
            }
            Debug.Log($"[불장난] 불꽃 {place}개 설치");
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    /// <summary>[불짚이기] 이번 턴 요정이 받은 누적 피해가 threshold↑면 취소, 아니면 무작위 1명에게 damage.</summary>
    [System.Serializable]
    public class KindlingSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 30;
        [Tooltip("이번 턴 받은 누적 피해가 이 값 이상이면 취소")]
        [SerializeField] private int cancelThreshold = 25;

        public KindlingSkill() { skillName = "불짚이기"; description = "무작위 1명 30 피해(이번 턴 25↑ 피해 시 취소)"; }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            int taken = source?.StatusEffects != null ? source.StatusEffects.GetEffectValue(EffectType.FireDamageTaken) : 0;
            if (taken >= cancelThreshold)
            {
                Debug.Log($"[불짚이기] 취소 — 이번 턴 누적 피해 {taken}");
                return;
            }
            AttackUnits(source, targetUnits, damage);
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인 + 커밋**

Refresh → 에러 0. (불똥별 = 공용 `FlameTileDamageSkill`.)

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave5/FlameFairy/FlameFairy.cs"
git commit -m "feat: 불꽃 요정 불장난 + 불짚이기(누적피해 취소, 상태효과 추적)"
```

---

## Phase 4 — 불꽃 인형

### Task 6: 인형 패시브(가호) + 불꽃 방패

**Files:**
- Create: `Assets/Scripts/Data/MonsterPresets/Wave5/FlameDoll/FlameDoll.cs`

- [ ] **Step 1: 파일 생성**

```csharp
using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Systems.Effects;
using DiceOrbit.Data.MonsterPresets.Wave5.Shared;

namespace DiceOrbit.Data.MonsterPresets.Wave5.FlameDoll
{
    /// <summary>[불의 가호] 턴시작: 보스(불꽃 소녀)에 FlameGuardStatus(1턴) 부여. 인형 생존 시 매턴 갱신.</summary>
    [System.Serializable]
    public class FlameGraceGuardPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [Tooltip("보스 방어도 보유 시 받는 피해 감소 %")]
        [SerializeField] private int reductionPercent = 20;

        public FlameGraceGuardPassive()
        {
            passiveName = "불의 가호";
            description = "불꽃 소녀가 방어도를 가진 동안 받는 피해 20% 감소";
            priority = 10; isStackable = false;
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnPreAction || context.Phase != EventPhase.TurnStart || context.SourceUnit != owner) return;

            var boss = FlameSet.FindBoss();
            if (boss == null || !boss.IsAlive || boss.StatusEffects == null) return;
            boss.StatusEffects.AddEffect(new FlameGuardStatus(reductionPercent, 1));
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    /// <summary>[불꽃 방패] 불꽃 소녀 + 자신(인형)에 일시 방어도 amount 부여.</summary>
    [System.Serializable]
    public class FlameShieldSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int amount = 10;

        public FlameShieldSkill() { skillName = "불꽃 방패"; description = "불꽃 소녀와 자신에게 일시 방어도 +10"; }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            if (source?.Stats != null) source.Stats.TempArmor += amount; // 자신(인형)
            var boss = FlameSet.FindBoss();
            if (boss != null && boss.IsAlive && boss.Stats != null) boss.Stats.TempArmor += amount;
            Debug.Log($"[불꽃 방패] 보스+자신 방어도 +{amount}");
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인 + 커밋**

Refresh → 에러 0. (불의 저주 = 공용 `FlameTileDamageSkill`, MonsterSkill RandomTiles+count6.)

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave5/FlameDoll/FlameDoll.cs"
git commit -m "feat: 불꽃 인형 불의가호(보스 -20%) + 불꽃 방패"
```

---

## Phase 5 — 불꽃 오르골

### Task 7: 오르골 패시브(불의 노래) + 타오르는 열기

**Files:**
- Create: `Assets/Scripts/Data/MonsterPresets/Wave5/FlameMusicBox/FlameMusicBox.cs`

- [ ] **Step 1: 파일 생성**

```csharp
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Systems.Effects;
using DiceOrbit.Data.MonsterPresets.Wave5.Shared;

namespace DiceOrbit.Data.MonsterPresets.Wave5.FlameMusicBox
{
    /// <summary>[불의 노래] 턴시작: 무작위 기존 불꽃 타일 하나의 좌우 ±1(2타일)에 불꽃 설치. 불꽃 없으면 skip.</summary>
    [System.Serializable]
    public class FlameSongPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [SerializeField] private int fireDamage = 35;

        public FlameSongPassive()
        {
            passiveName = "불의 노래";
            description = "턴 시작 시 무작위 불꽃 타일 좌우 1칸에 불꽃 설치";
            priority = 10; isStackable = false;
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnPreAction || context.Phase != EventPhase.TurnStart || context.SourceUnit != owner) return;

            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit == null) return;
            var flames = orbit.Tiles.Where(t => t != null && t.HasAttribute(TileAttributeType.Flame)).ToList();
            if (flames.Count == 0) return; // 기존 불꽃 없으면 skip

            int total = orbit.Tiles.Count;
            var center = flames[Random.Range(0, flames.Count)];
            foreach (int off in new[] { -1, 1 })
            {
                int idx = (center.TileIndex + off) % total;
                if (idx < 0) idx += total;
                var tile = orbit.GetTile(idx);
                if (tile != null && !tile.HasAttribute(TileAttributeType.Flame))
                    tile.AddAttribute(new FireTile(fireDamage));
            }
            Debug.Log("[불의 노래] 불꽃 좌우 확산");
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    /// <summary>[타오르는 열기] 불꽃 소녀 + 무작위 아군 1명의 피해량 +amount 영구.</summary>
    [System.Serializable]
    public class BurningHeatSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int amount = 3;

        public BurningHeatSkill() { skillName = "타오르는 열기"; description = "불꽃 소녀와 무작위 아군의 피해량 +3 (영구)"; }

        private void Buff(Monster m)
        {
            if (m == null || !m.IsAlive || m.StatusEffects == null) return;
            m.StatusEffects.AddEffect(new BuffAttackStatus(amount, -1) { IsStackable = true });
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            Buff(FlameSet.FindBoss());
            var monsters = CombatManager.Instance?.ActiveMonsters?.Where(m => m != null && m.IsAlive).ToList();
            if (monsters != null && monsters.Count > 0)
                Buff(monsters[Random.Range(0, monsters.Count)]);
            Debug.Log($"[타오르는 열기] 보스+무작위 아군 피해량 +{amount}");
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인 + 커밋**

Refresh → 에러 0. (화염 폭발 = 공용 `FlameTileDamageSkill`.)

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave5/FlameMusicBox/FlameMusicBox.cs"
git commit -m "feat: 불꽃 오르골 불의노래 + 타오르는 열기"
```

---

## Phase 6 — 프리셋 생성·배선 + 검증

### Task 8: 프리셋 4종 생성 + 배선 (MCP)

**Files:**
- Create (에셋): `Wave5/{FlameGirl,FlameFairy,FlameDoll,FlameMusicBox}/*.asset`

코드가 아니라 MCP `Unity_RunCommand`로 `MonsterPreset` 에셋을 만들고 `SerializedObject`로 배선한다(Wave4 Task 10과 동일 기법: public 필드는 직접 대입, private 직렬화 필드는 SerializedProperty). enum 정수값: `TargetSelectionStrategy` RandomCharacter=0/RandomTiles=2/Self=4/Custom=5, `TargetType` Characters=0/Tiles=1/Self=2, `IntentType` Attack=0/Defend=1/Buff=2. `MonsterFaction.Flame` = 3.

- [ ] **Step 1: 에셋 생성 + 스탯/진영**

`Unity_RunCommand`으로 4개 `MonsterPreset` 생성(`ScriptableObject.CreateInstance<MonsterPreset>()` + `AssetDatabase.CreateAsset(obj, path)`), 각 `BaseStats.MonsterName`/`MaxHP`/`CurrentHP` 세팅(SerializedObject) + `Faction = MonsterFaction.Flame`:
- `Wave5/FlameGirl/FlameGirl.asset` — "불꽃 소녀", HP 100
- `Wave5/FlameFairy/FlameFairy.asset` — "불꽃 요정", HP 80
- `Wave5/FlameDoll/FlameDoll.asset` — "불꽃 인형", HP 80
- `Wave5/FlameMusicBox/FlameMusicBox.asset` — "불꽃 오르골", HP 80

- [ ] **Step 2: AIPattern·스킬·패시브 배선 (SerializedObject)**

**불꽃 소녀** (`FlameGirlPattern`, availableSkills 순서 = [발화, 화염구, 대화재]):
- [0] 발화: `skillData=PlaceFireSkill`; RandomCharacter(0)/Tiles(1)/count1/range1/Attack(0)
- [1] 화염구: `skillData=FireballSkill`; Custom(5)/Tiles(1)/count1/range0/Attack(0)
- [2] 대화재: `skillData=ConflagrationSkill`; Self(4)/None 또는 Self(2)/count0/range0/Special. (타깃 무관 — Execute가 전 타일 처리)
- StartingPassives: `FlameStagePassive`, `IncinerationPassive`

**불꽃 요정** (`RandomPattern`, [불똥별, 불짚이기]):
- [0] 불똥별: `skillData=FlameTileDamageSkill{skillLabel="불똥별", damage=30}`; RandomCharacter(0)/Tiles(1)/count1/range2/Attack(0)
- [1] 불짚이기: `skillData=KindlingSkill`; RandomCharacter(0)/Characters(0)/count1/range0/Attack(0)
- StartingPassives: `PlayingWithFirePassive`

**불꽃 인형** (`SequentialPattern`, [불꽃 방패, 불의 저주]):
- [0] 불꽃 방패: `skillData=FlameShieldSkill`; Self(4)/Self(2)/count1/range0/Defend(1)
- [1] 불의 저주: `skillData=FlameTileDamageSkill{skillLabel="불의 저주", damage=30}`; RandomTiles(2)/Tiles(1)/count6/range0/Attack(0)
- StartingPassives: `FlameGraceGuardPassive`

**불꽃 오르골** (`SequentialPattern`, [화염 폭발, 타오르는 열기]):
- [0] 화염 폭발: `skillData=FlameTileDamageSkill{skillLabel="화염 폭발", damage=30}`; RandomCharacter(0)/Tiles(1)/count1/range2/Attack(0)
- [1] 타오르는 열기: `skillData=BurningHeatSkill`; Self(4)/Self(2)/count1/range0/Buff(2)
- StartingPassives: `FlameSongPassive`

- [ ] **Step 3: 저장 + 검증**

`AssetDatabase.SaveAssets()` + `Refresh()`. 4개 `.asset`을 Read로 열어 Faction=3, HP, availableSkills 타깃 정수, StartingPassives 타입/파라미터가 위와 일치하는지, "Missing type" 없는지 확인.

- [ ] **Step 4: 커밋**

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave5"
git commit -m "feat: Wave5 불꽃 세트 프리셋 4종 생성 + 배선"
```

### Task 9: 인카운터 배정 + 플레이 검증

**Files:**
- Modify(에셋): `Assets/Scripts/Data/Run/Act.asset` 또는 사용자 지정 액트.

- [ ] **Step 1: 인카운터 배치**

Wave5 4종을 하나의 `EncounterDefinition`(MonsterPresets 4개)으로 묶어 원하는 풀(BossPool/ElitePool/BattleTiers 중)에 추가. (배치 위치는 사용자 확인 — 게임 콘텐츠 결정.)

- [ ] **Step 2: 플레이 진입 (DX11 확인)**

이 머신은 D3D12+Intel Arc 크래시 이력 → 그래픽 API DX11 고정 확인(`unity-d3d12-crash-dx11-fix` 메모리). Play → Wave5 전투 진입.

- [ ] **Step 3: 스펙 §10 체크리스트 검증**

- 불꽃 타일: 캐릭터가 위에서 턴종료 시 35피해 / 통과하면 삭제(같은 이동/턴에 1개만, 마커 확인) / 타일당 1개.
- 소녀: 불꽃 4+개 방어도(n×3), 7+개 화염구 ±3, 10+개 대화재(전 타일 삭제+35), HP50%↓ 소각 8개(1회, `fired` 로그).
- 요정: 매턴 2설치, 불짚이기가 이번 턴 25↑ 피해 시 `[불짚이기] 취소` 로그.
- 인형: 보스 방어도 있을 때 보스 받는 피해 -20%, 방패 보스+자신 +10, 저주 6타일 30.
- 오르골: 기존 불꽃 좌우 확산, 열기로 보스+아군 피해 +3.
- Console 로그(`[타오르는 무대]`/`[소각]`/`[불장난]`/`[불의 노래]`/`[불꽃 방패]`/`[타오르는 열기]`)로 교차 확인.
- **회귀**: 기존 몬스터/전투 정상, 다른 타일 속성 정상.

- [ ] **Step 4: 튜닝 (필요 시, 코드 없이)**

- 소화 마커 "턴당 1개"가 게임 느낌과 안 맞으면 마커 지속 조정.
- 불꽃 피해/개수 티어/버프 수치는 프리셋 스킬·패시브 필드에서.

- [ ] **Step 5: 개발 브랜치 마무리**

Announce: "I'm using the finishing-a-development-branch skill to complete this work." 후 `superpowers:finishing-a-development-branch`.

---

## Self-Review

**스펙 커버리지:**
- 불꽃 타일(35피해/통과소화/타일당1/영구) → Task 2 (FireTile) ✔
- 통과-소화 캡 = 마커 상태 → Task 1(enum)+Task 2(FireExtinguishMarkStatus, OnTraverse) ✔
- 개수/설치 = 기존 API 직접 → Task 3(PlaceFireSkill)·각 패시브 ✔
- 소녀 타오르는무대/소각/발화/화염구/대화재/조건부AI → Task 4 ✔
- 요정 불장난/불똥별/불짚이기(누적취소) → Task 5 + 공용 skill ✔
- 인형 불의가호/불꽃방패/불의저주 → Task 6 + 공용 skill ✔
- 오르골 불의노래/화염폭발/타오르는열기 → Task 7 + 공용 skill ✔
- 보스 식별 → Task 3 (FlameSet.FindBoss) ✔
- 프리셋/HP/진영/패턴 배선 → Task 8 ✔
- 인카운터 배정·플레이 검증 → Task 9 ✔
- **신규 필드 0**(CharacterStats/Character/Monster 무변경): 소화캡=상태, 불짚이기=상태, 소각 플래그=패시브 인스턴스 필드 ✔

**플레이스홀더 스캔:** 코드 스텝 전부 완전한 코드. 배선(Task 8~9)은 확정 수치·enum 정수 명시.

**타입 일관성:** `FireTile(damage)`, `FireExtinguishMarkStatus()`, `FireDamageTakenStatus()`, `FlameGuardStatus(percent,duration)`, `FlameTileDamageSkill{skillLabel,damage}`, `PlaceFireSkill{fireDamage}`, `FlameSet.FindBoss()`, `FlameGirlPattern`(availableSkills[0 발화,1 화염구,2 대화재]), `EffectType.FireExtinguishMark/FireDamageTaken/FireGuard`, `TileAttributeType.Flame`, `GetEffectValue/HasEffect`, `owner.Stats.HPRatio/TempArmor` — 태스크 전반 일치 확인.

**알려진 리스크(플레이에서 확정):**
1. `FireDamageTakenStatus` 누적 시점(OnCalculateOutput OutputValue)이 방어도 적용 전 값 — "받은 피해" 정의가 다르면 조정(트리거/필드).
2. 몹 TurnEnd 이벤트 트리거가 `OnPostAction`가 아닐 수 있음 → 누적 리셋이 안 되면 트리거 확인(FrostbitePassive와 동일 가정).
3. 대화재 MonsterSkill 타깃 설정이 빈 targetTiles를 줘도 Execute가 전 타일 자체 처리하므로 무관 — 다만 IntentType/표시만 확인.
4. 소화 마커 지속 1턴의 만료 타이밍(캐릭터 TurnStart) — "턴당 1개"가 라운드 경계와 어긋나면 지속 조정.
