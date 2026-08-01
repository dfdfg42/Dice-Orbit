# Wave3 수정(Crystal) 몬스터 세트 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wave3 수정 세트(수정 핵·수정석·수정 파편) 3종과, 이 세트가 요구하는 신규 "기절(행동 불가)" 상태이상을 구현한다.

**Architecture:** 기존 `Frozen`/`BindDebuff` 이동차단 패턴을 미러해 기절(`StunDebuff` + `canAct()` + UI/로직 게이트 3곳)을 추가한다. 세트 공유 상태는 `SlimeSet` 패턴을 미러한 정적 `CrystalSet`(수정 중첩 카운터 + 수정 핵 탐색 + 웨이브 정리)에 둔다. 자수정 타일은 `SlimeTile` 패턴(단 영구 유지). 몬스터 스킬/조건부 AI는 슬라임·눈 세트와 동형.

**Tech Stack:** Unity 6000.3.8f1, C#, `[SerializeReference]` 다형성(SkillData/PassiveAbility/MonsterAI), MonsterPreset ScriptableObject, Unity MCP(SerializedObject 배선).

**검증 방식:** 이 프로젝트는 유닛 테스트 프레임워크를 쓰지 않는다. 각 태스크의 "검증"은 **컴파일 무오류 확인**이다 — Unity MCP에서 `Unity_RunCommand`로 `AssetDatabase.Refresh()` 실행 후 `Unity_GetConsoleLogs {logTypes:"Error"}`로 해당 파일 관련 컴파일 에러가 없는지 확인한다. (헤드리스 MSBuild 대안은 메모리 `verify_unity_compile` 참조.) 프리셋은 직렬화 결과를 Read로 확인한다.

**기준 브랜치:** `feature/wave0-slime-20260731` (현재 브랜치에서 계속). main 아님.

---

## 파일 구조

**수정(기존 공유 파일):**
- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/EffectData.cs` — `EffectType.Stunned` 추가.
- `Assets/Scripts/Core/Stage/BattleStage/Units/Character/CharacterStats.cs` — `StunDebuff` 필드 + `canAct()`.
- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/CommonStatuses.cs` — `StunDebuff : StatusEffect`.
- `Assets/Scripts/UI/CharacterActionUI.cs` — `OnMoveClicked`/`RefreshActionButtonsState`에 `canAct()` 게이트.
- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/SkillManager.cs` — `ConfirmSkillExecution`에 `canAct()` 게이트.
- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/TileAttribute.cs` — `TileAttributeType.Amethyst` + `GetDisplayName`.

**신규(세트 파일):**
- `Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/Shared/CrystalSet.cs`
- `Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/Shared/AmethystTile.cs`
- `Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/Shared/CrystallizePassive.cs`
- `Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/Shared/CrystalMinionPattern.cs`
- `Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/CrystalCore/CrystalCore.cs`
- `Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/CrystalStone/CrystalStone.cs`
- `Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/CrystalShard/CrystalShard.cs`

**신규(프리셋):**
- `.../CrystalCore/CrystalCore.asset`, `.../CrystalStone/CrystalStone.asset`, `.../CrystalShard/CrystalShard.asset`

---

## Task 1: 기절(Stun) 인프라

행동 불가 상태이상. `Frozen`/`BindDebuff`(이동차단)을 미러한다. 몬스터 없이 독립적으로 컴파일·성립.

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/EffectData.cs:30`
- Modify: `Assets/Scripts/Core/Stage/BattleStage/Units/Character/CharacterStats.cs:44`
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/CommonStatuses.cs`
- Modify: `Assets/Scripts/UI/CharacterActionUI.cs:226,599-600`
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/SkillManager.cs:85`

- [ ] **Step 1: `EffectType.Stunned` 추가**

`EffectData.cs`에서 enum 마지막 항목(`BoneMark,` 30행) 뒤에 추가:

```csharp
        BoneMark,           // 뼈무덤 발동 마커: 이번 라운드 해골병사가 뼈 방어도 획득함 (Wave1)
        Stunned,            // 기절: 다음 턴 행동 불가(이동+스킬) (Wave3 수정)
```

- [ ] **Step 2: `CharacterStats`에 `StunDebuff` + `canAct()` 추가**

`CharacterStats.cs`의 `BindDebuff` 필드(29행) 뒤, 그리고 `canMove()`(44행) 옆에 추가:

```csharp
        public int BindDebuff  = 0;
        public int StunDebuff  = 0;
```

그리고 `canMove()` 아래:

```csharp
        public bool canMove() => BindDebuff == 0;
        public bool canAct()  => StunDebuff == 0;
```

- [ ] **Step 3: `StunDebuff : StatusEffect` 추가 (CommonStatuses.cs)**

`CommonStatuses.cs` 상단 using에 `using DiceOrbit.Core;`를 추가(현재 `Core.Pipeline`, `Data`, `UnityEngine`만 있음). 그리고 `namespace DiceOrbit.Systems.Effects` 안, `PoisonStatus` 뒤에 추가:

```csharp
    /// <summary>기절 — 다음 턴 행동 불가(이동+스킬). Frozen/BindDebuff 패턴 미러.
    /// 적용 시 CharacterStats.StunDebuff++, 만료 시 --. canAct()가 이를 읽어 게이트한다.</summary>
    public class StunDebuff : StatusEffect
    {
        public StunDebuff(int duration) : base(EffectType.Stunned, 0, duration)
        {
            IsStackable = false;
        }

        public override void EffectApplied()
        {
            if (Owner != null && Owner.Stats is CharacterStats c) c.StunDebuff++;
            Debug.Log($"[StunDebuff] {Owner?.name} 기절! (지속: {Duration}턴)");
        }

        public override void EffectExpired()
        {
            if (Owner != null && Owner.Stats is CharacterStats c) c.StunDebuff--;
            Debug.Log($"[StunDebuff] {Owner?.name} 기절 해제.");
        }
    }
```

> 참고: `using DiceOrbit.Core;`는 `CharacterStats` 타입 참조용. `EffectApplied`/`EffectExpired`는 `FrozenDebuff`(SnowPrisonTile.cs:59-78)가 쓰는 것과 동일한 오버라이드 지점이다.

- [ ] **Step 4: 이동 버튼 게이트 (`OnMoveClicked`)**

`CharacterActionUI.cs`의 `canMove()` 체크(226행)를 확장:

```csharp
            if (!currentCharacter.Stats.canMove() || !currentCharacter.Stats.canAct())
            {
                Debug.LogWarning("[CharacterActionUI] 이동 불가 상태(속박/기절 등)입니다.");
                ReturnDiceElement();
                return;
            }
```

- [ ] **Step 5: 버튼 활성 상태 게이트 (`RefreshActionButtonsState`)**

`CharacterActionUI.cs`의 599-600행을 교체:

```csharp
            bool canAct = currentCharacter.Stats.canAct();
            bool canMove = hasDice && playerTurn && canAct && currentCharacter.Stats.canMove() && combatManager.CanSpendMove(currentCharacter);
            bool canSkill = hasDice && playerTurn && canAct && canUseSelectedDiceForSkill && combatManager.CanSpendAction(currentCharacter);
```

- [ ] **Step 6: 스킬 실행 로직 게이트 (`ConfirmSkillExecution`)**

`SkillManager.cs`의 null 검증 블록(85-89행) 바로 뒤, `AssignDice` 호출(91행 주석) 전에 추가:

```csharp
            if (!source.Stats.canAct())
            {
                Debug.LogWarning("[SkillManager] 기절 상태 — 행동할 수 없습니다.");
                CharacterActionUI.Instance?.ReturnDiceElement();
                return;
            }

```

- [ ] **Step 7: 컴파일 검증**

Unity MCP: `Unity_RunCommand`로 `UnityEditor.AssetDatabase.Refresh();` 실행 → `Unity_GetConsoleLogs {logTypes:"Error"}`.
Expected: `EffectData.cs`/`CharacterStats.cs`/`CommonStatuses.cs`/`CharacterActionUI.cs`/`SkillManager.cs` 관련 컴파일 에러 0.

- [ ] **Step 8: 커밋**

```bash
git add Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/EffectData.cs \
        Assets/Scripts/Core/Stage/BattleStage/Units/Character/CharacterStats.cs \
        Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/CommonStatuses.cs \
        Assets/Scripts/UI/CharacterActionUI.cs \
        Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/SkillManager.cs
git commit -m "feat: 기절(Stun) 상태이상 인프라 - StunDebuff+canAct() 게이트 3곳 (Wave3 수정)"
```

---

## Task 2: 자수정 타일 + CrystalSet 트래커

세트 공유 상태와 자수정 타일. `SlimeSet`/`SlimeTile` 패턴 미러.

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/TileAttribute.cs:24,116`
- Create: `Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/Shared/CrystalSet.cs`
- Create: `Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/Shared/AmethystTile.cs`

- [ ] **Step 1: `TileAttributeType.Amethyst` 추가**

`TileAttribute.cs`의 enum에서 `Slime,`(24행) 뒤에 추가:

```csharp
        Slime,          // 점액: 통과/턴 종료 시 쇠약 부여 (Wave0)
        Amethyst,       // 자수정: 통과/턴 종료 시 수정 핵에 수정 중첩 +1, 영구 (Wave3 수정)
```

그리고 `GetDisplayName`의 `Slime` 케이스(116행) 뒤에 추가:

```csharp
                TileAttributeType.Slime => "점액",
                TileAttributeType.Amethyst => "자수정",
```

- [ ] **Step 2: `CrystalSet` 생성**

`Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/Shared/CrystalSet.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;
using UnityEngine;

namespace DiceOrbit.Data.MonsterPresets.Wave3.Crystal.Shared
{
    /// <summary>
    /// 수정 세트 공유 상태.
    /// - 수정 중첩(stack): 수정 핵 인스턴스에 귀속. 자수정 타일/결정화 패시브가 공급, 수정 핵 패턴이 소비.
    /// - 자수정 타일은 수정 핵 사망 후에도(지연 제거 전까지) 유지되고, 웨이브 종료(=다음 전투 시작) 시 정리.
    /// SlimeSet 패턴 미러.
    /// </summary>
    public static class CrystalSet
    {
        private static readonly Dictionary<Monster, int> Stacks = new();
        private static CombatManager hookedManager;

        /// <summary>현재 살아있는 수정 핵(이름으로 식별). 없으면 null.</summary>
        public static Monster GetCore()
        {
            var cm = CombatManager.Instance;
            if (cm == null) return null;
            return cm.GetAliveMonsters().FirstOrDefault(m => m != null && m.Stats != null && m.Stats.MonsterName == "수정 핵");
        }

        public static void AddStack(Monster core, int n)
        {
            if (core == null) return;
            Stacks.TryGetValue(core, out int cur);
            Stacks[core] = cur + Mathf.Max(0, n);
        }

        public static int GetStacks(Monster core)
        {
            if (core == null) return 0;
            Stacks.TryGetValue(core, out int cur);
            return cur;
        }

        public static void ResetStacks(Monster core)
        {
            if (core != null) Stacks[core] = 0;
        }

        public static void EnsureWaveHook()
        {
            var cm = CombatManager.Instance;
            if (cm == null || hookedManager == cm) return;
            if (hookedManager != null) hookedManager.OnCombatStart -= OnCombatStart;
            cm.OnCombatStart += OnCombatStart;
            hookedManager = cm;
        }

        private static void OnCombatStart()
        {
            Stacks.Clear();
            var orbit = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbit?.Tiles == null) return;
            foreach (var tile in orbit.Tiles)
                if (tile != null) tile.RemoveAttributeType(TileAttributeType.Amethyst);
        }
    }
}
```

- [ ] **Step 3: `AmethystTile` 생성**

`Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/Shared/AmethystTile.cs`:

```csharp
using DiceOrbit.Core;
using DiceOrbit.Data.MonsterPresets.Wave3.Crystal.Shared;

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// [자수정] 타일. 캐릭터가 통과하거나 그 위에서 턴을 종료하면 살아있는 수정 핵에게 수정 중첩 1을 공급한다.
    /// SlimeTile과 달리 발동해도 사라지지 않고 영구 유지된다. 수정 핵 사망 시 지연 제거(다음 발동 때),
    /// 웨이브 종료 시 CrystalSet이 일괄 정리.
    /// </summary>
    public class AmethystTile : TileAttribute
    {
        public AmethystTile() : base(TileAttributeType.Amethyst, 0, -1, false) { }

        public override void OnTraverse(Character character) => Activate();
        public override void OnEndTurn(Character character) => Activate();

        private void Activate()
        {
            var core = CrystalSet.GetCore();
            if (core != null)
                CrystalSet.AddStack(core, 1);
            else
                Owner?.RemoveAttribute(this); // 수정 핵 사망 → 지연 제거
        }

        public override string GetDescription()
            => "통과·턴 종료 시 수정 핵에게 수정 중첩 +1 (영구)";
    }
}
```

- [ ] **Step 4: 컴파일 검증**

Unity MCP: `AssetDatabase.Refresh()` → `GetConsoleLogs {logTypes:"Error"}`.
Expected: `Amethyst` enum·`CrystalSet`·`AmethystTile` 관련 에러 0. (`RemoveAttributeType`/`GetOrbitManager`/`GetAliveMonsters`/`OnCombatStart`는 SlimeSet에서 검증된 기존 API.)

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/TileAttribute.cs \
        Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/Shared/CrystalSet.cs \
        Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/Shared/AmethystTile.cs
git commit -m "feat: 자수정 타일 + CrystalSet 트래커 (수정 중첩/웨이브 정리) - Wave3 수정"
```

---

## Task 3: 공유 부하 클래스 (결정화 패시브 + 부하 조건부 패턴)

수정석·수정 파편이 공유하는 패시브 1종과 조건부 AI 1종.

**Files:**
- Create: `Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/Shared/CrystallizePassive.cs`
- Create: `Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/Shared/CrystalMinionPattern.cs`

- [ ] **Step 1: `CrystallizePassive` 생성**

`Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/Shared/CrystallizePassive.cs`:

```csharp
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;

namespace DiceOrbit.Data.MonsterPresets.Wave3.Crystal.Shared
{
    /// <summary>[결정화] 턴 종료 시 살아있는 수정 핵에게 수정 중첩 1 공급. 수정석·수정 파편 공용.</summary>
    [System.Serializable]
    public class CrystallizePassive : PassiveAbility
    {
        public CrystallizePassive()
        {
            passiveName = "결정화";
            description = "턴 종료 시 수정 핵의 수정 중첩 1 증가";
            priority = 10; isStackable = false;
        }

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            CrystalSet.EnsureWaveHook();
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnPostAction || context.Phase != EventPhase.TurnEnd || context.SourceUnit != owner) return;

            var core = CrystalSet.GetCore();
            if (core != null) CrystalSet.AddStack(core, 1);
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }
}
```

> 참고: `Initialize`/`OnTurnEvent`/`AllowSamePassive` 오버라이드 및 turn-end 판정(`OnPostAction`+`TurnEnd`+`SourceUnit==owner`)은 `PlantSlimePassive`(BlueSlime.cs)에서 검증된 형태.

- [ ] **Step 2: `CrystalMinionPattern` 생성**

`Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/Shared/CrystalMinionPattern.cs`:

```csharp
using UnityEngine;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Data.MonsterPresets.Wave3.Crystal.Shared
{
    /// <summary>
    /// 부하(수정석·수정 파편) 공용 조건부 AI. 평소 availableSkills[0]/[1] 50:50.
    /// 단, 살아있는 수정 핵이 없으면 coreAbsentSkillIndex 스킬만 사용.
    /// </summary>
    [System.Serializable]
    public class CrystalMinionPattern : MonsterAI
    {
        [Tooltip("수정 핵이 없을 때 사용할 스킬 인덱스 (수정석=1 수정창, 수정파편=0 수정비)")]
        [SerializeField] private int coreAbsentSkillIndex = 0;

        public override MonsterSkill GetNextSkill()
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;

            if (CrystalSet.GetCore() == null)
            {
                int idx = Mathf.Clamp(coreAbsentSkillIndex, 0, availableSkills.Count - 1);
                return availableSkills[idx];
            }

            return availableSkills[Random.Range(0, availableSkills.Count)];
        }
    }
}
```

> 참고: `MonsterAI` 상속 + `availableSkills` + `GetNextSkill()` 오버라이드는 `SnowGolemPattern`(SnowGolem.cs)에서 검증된 형태. `MonsterSkill`/`MonsterAI`는 `DiceOrbit.Data.Monsters` 네임스페이스. `CrystalSet`은 같은 `Shared` 네임스페이스라 추가 using 불필요.

- [ ] **Step 3: 컴파일 검증**

Unity MCP: `AssetDatabase.Refresh()` → `GetConsoleLogs {logTypes:"Error"}`.
Expected: `CrystallizePassive`·`CrystalMinionPattern` 관련 에러 0.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/Shared/CrystallizePassive.cs \
        Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/Shared/CrystalMinionPattern.cs
git commit -m "feat: 수정 부하 공유 클래스 - 결정화 패시브 + 조건부 부하 패턴 (Wave3 수정)"
```

---

## Task 4: 수정 핵 (CrystalCore.cs)

자수정 소환 패시브 + 수정 폭발/폭풍 스킬 + 조건부 코어 패턴.

**Files:**
- Create: `Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/CrystalCore/CrystalCore.cs`

- [ ] **Step 1: `CrystalCore.cs` 전체 생성**

```csharp
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Systems.Effects;
using DiceOrbit.Data.MonsterPresets.Wave3.Crystal.Shared;

namespace DiceOrbit.Data.MonsterPresets.Wave3.Crystal.CrystalCore
{
    /// <summary>[자수정] 웨이브 시작(수정 핵 첫 턴) 시 무작위 tileCount 타일에 자수정 설치. 1회만.</summary>
    [System.Serializable]
    public class SummonAmethystPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [SerializeField] private int tileCount = 4;
        private bool placed;

        public SummonAmethystPassive()
        {
            passiveName = "자수정";
            description = "웨이브 시작 시 무작위 4타일에 자수정 설치 (통과·턴 종료 시 수정 핵 중첩 +1)";
            priority = 10; isStackable = false;
        }

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            CrystalSet.EnsureWaveHook();
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null || placed) return;
            if (trigger != CombatTrigger.OnPreAction || context.Phase != EventPhase.TurnStart || context.SourceUnit != owner) return;

            PlantAmethyst();
            placed = true;
        }

        private void PlantAmethyst()
        {
            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit?.Tiles == null) return;
            var candidates = orbit.Tiles.Where(t => t != null && !t.HasAttribute(TileAttributeType.Amethyst)).ToList();
            int place = Mathf.Min(tileCount, candidates.Count);
            for (int i = 0; i < place; i++)
            {
                int r = Random.Range(0, candidates.Count);
                candidates[r].AddAttribute(new AmethystTile());
                candidates.RemoveAt(r);
            }
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    /// <summary>[수정 폭발] 무작위 자수정 타일 2개 + 좌우 각각 ±1칸에 damage 피해.
    /// (TilesWithAttribute=Amethyst + range 1 + count 2로 배선)</summary>
    [System.Serializable]
    public class CrystalBurstSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 20;

        public CrystalBurstSkill()
        {
            skillName = "수정 폭발";
            description = "무작위 자수정 타일 2개 + 좌우 각각 한 칸에 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>[수정 폭풍] 자수정 타일을 제외한 모든 타일의 적에게 damage 피해 + 기절 부여.
    /// (AllTargets + Characters로 배선; Execute에서 자수정 위 캐릭터 제외)</summary>
    [System.Serializable]
    public class CrystalStormSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 20;
        [Tooltip("기절 지속 턴 (SnowMan 빙결과 동일 컨벤션 = 2 → 다음 턴 스킵)")]
        [SerializeField] private int stunDuration = 2;

        public CrystalStormSkill()
        {
            skillName = "수정 폭풍";
            description = "자수정 타일을 제외한 모든 타일의 적에게 피해 + 다음 턴 기절";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            if (targetUnits == null) return;
            var victims = targetUnits
                .OfType<Character>()
                .Where(c => c.IsAlive && !(c.CurrentTile != null && c.CurrentTile.HasAttribute(TileAttributeType.Amethyst)))
                .Cast<Unit>()
                .ToList();

            AttackUnits(source, victims, damage);

            foreach (var u in victims)
                if (u is Character c && c.IsAlive && c.StatusEffects != null)
                    c.StatusEffects.AddEffect(new StunDebuff(stunDuration));
        }
    }

    /// <summary>수정 핵 조건부 AI: 수정 중첩 ≥15 → [1]수정 폭풍, 아니면 [0]수정 폭발.</summary>
    [System.Serializable]
    public class CrystalCorePattern : MonsterAI
    {
        [Tooltip("이 값 이상이면 수정 폭풍(index 1) 발동")]
        [SerializeField] private int stormThreshold = 15;

        public override MonsterSkill GetNextSkill()
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;
            int stacks = CrystalSet.GetStacks(owner as Monster);
            int idx = (stacks >= stormThreshold && availableSkills.Count > 1) ? 1 : 0;
            return availableSkills[idx];
        }
    }
}
```

- [ ] **Step 2: 컴파일 검증**

Unity MCP: `AssetDatabase.Refresh()` → `GetConsoleLogs {logTypes:"Error"}`.
Expected: `CrystalCore.cs` 관련 에러 0. (`AttackTiles`/`AttackUnits`/`GetPreviewDamage`/`Execute`/`CurrentTile`/`HasAttribute`/`AddAttribute`는 슬라임·곰 세트에서 검증된 기존 API.)

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/CrystalCore/CrystalCore.cs
git commit -m "feat: 수정 핵 - 자수정 소환/수정 폭발/수정 폭풍(기절) + 조건부 코어 패턴 (Wave3 수정)"
```

---

## Task 5: 수정석 (CrystalStone.cs)

결정 방패(수정 핵 방어도) + 수정 창(광역 딜).

**Files:**
- Create: `Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/CrystalStone/CrystalStone.cs`

- [ ] **Step 1: `CrystalStone.cs` 전체 생성**

```csharp
using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.MonsterPresets.Wave3.Crystal.Shared;

namespace DiceOrbit.Data.MonsterPresets.Wave3.Crystal.CrystalStone
{
    /// <summary>[결정 방패] 살아있는 수정 핵에게 일시 방어도 armor 부여. (targetType=Self로 배선, Execute는 대상 무시)</summary>
    [System.Serializable]
    public class CrystalShieldSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int armor = 10;

        public CrystalShieldSkill()
        {
            skillName = "결정 방패";
            description = "수정 핵에게 일시 방어도 부여";
        }

        public override int GetPreviewDamage() => 0;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var core = CrystalSet.GetCore();
            if (core != null && core.Stats != null) core.Stats.TempArmor += armor;
        }
    }

    /// <summary>[수정 창] 무작위 대상 1명이 속한 타일 + 좌우 각각 ±2칸에 damage 피해.
    /// (RandomCharacter + Tiles + range 2로 배선; CorrosiveSlimeSkill과 동형)</summary>
    [System.Serializable]
    public class CrystalSpearSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;

        public CrystalSpearSkill()
        {
            skillName = "수정 창";
            description = "무작위 대상 1명이 속한 타일 + 좌우 각각 두 칸에 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }
}
```

- [ ] **Step 2: 컴파일 검증**

Unity MCP: `AssetDatabase.Refresh()` → `GetConsoleLogs {logTypes:"Error"}`.
Expected: `CrystalStone.cs` 관련 에러 0. (`Stats.TempArmor`는 Explore로 확인된 기존 API; BoneTile이 `Stats.TempArmor += Value` 사용.)

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/CrystalStone/CrystalStone.cs
git commit -m "feat: 수정석 - 결정 방패(수정 핵 방어도) + 수정 창 (Wave3 수정)"
```

---

## Task 6: 수정 파편 (CrystalShard.cs)

수정 비(광역 딜) + 결정 재생(수정 핵 회복).

**Files:**
- Create: `Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/CrystalShard/CrystalShard.cs`

- [ ] **Step 1: `CrystalShard.cs` 전체 생성**

```csharp
using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.MonsterPresets.Wave3.Crystal.Shared;

namespace DiceOrbit.Data.MonsterPresets.Wave3.Crystal.CrystalShard
{
    /// <summary>[수정 비] 무작위 타일 count개에 있는 적에게 damage 피해. (RandomTiles + Tiles + count 6으로 배선)</summary>
    [System.Serializable]
    public class CrystalRainSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;

        public CrystalRainSkill()
        {
            skillName = "수정 비";
            description = "무작위 타일 6개에 있는 적에게 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>[결정 재생] 살아있는 수정 핵의 체력 healAmount 회복. (targetType=None으로 배선, Execute는 대상 무시)</summary>
    [System.Serializable]
    public class CrystalRegenSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int healAmount = 10;

        public CrystalRegenSkill()
        {
            skillName = "결정 재생";
            description = "수정 핵의 체력 회복";
        }

        public override int GetPreviewDamage() => 0;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var core = CrystalSet.GetCore();
            if (core != null && core.Stats != null) core.Stats.Heal(healAmount);
        }
    }
}
```

- [ ] **Step 2: 컴파일 검증**

Unity MCP: `AssetDatabase.Refresh()` → `GetConsoleLogs {logTypes:"Error"}`.
Expected: `CrystalShard.cs` 관련 에러 0. (`Stats.Heal(int)`은 Explore로 확인된 기존 API — UnitStats.Heal.)

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/CrystalShard/CrystalShard.cs
git commit -m "feat: 수정 파편 - 수정 비 + 결정 재생(수정 핵 회복) (Wave3 수정)"
```

---

## Task 7: 프리셋 3종 생성·배선 (MCP SerializedObject)

`.asset` 3종을 만들고 `[SerializeReference]` RefIds를 SerializedObject/SerializedProperty로 배선한다. **System.Reflection 금지** — SlimeSet 프리셋 배선과 동일 방식. `BlueSlime.asset`을 서식 레퍼런스로 삼는다.

**Files:**
- Create: `Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/CrystalCore/CrystalCore.asset`
- Create: `Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/CrystalStone/CrystalStone.asset`
- Create: `Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/CrystalShard/CrystalShard.asset`

배선 사양(각 MonsterSkill의 targeting 필드는 슬라임 배선과 동일 의미):

- **CrystalCore.asset** — `BaseStats.MaxHP=45, CurrentHP=45`, `MonsterName="수정 핵"`, `Faction=0`.
  - `AIPattern` = `CrystalCorePattern` (stormThreshold=15), `availableSkills`:
    - [0] `CrystalBurstSkill` — `targetStrategy=3`(TilesWithAttribute), `targetType=1`(Tiles), `targetTileAttribute=16`(Amethyst), `targetCount=2`, `targetRange=1`, `intentType=0`(Attack).
    - [1] `CrystalStormSkill` — `targetStrategy=1`(AllTargets), `targetType=0`(Characters), `targetCount=1`, `targetRange=0`, `intentType=3`(Special).
  - `StartingPassives` = [`SummonAmethystPassive` (tileCount=4)].
- **CrystalStone.asset** — `MaxHP=45, CurrentHP=45`, `MonsterName="수정석"`, `Faction=0`.
  - `AIPattern` = `CrystalMinionPattern` (coreAbsentSkillIndex=1), `availableSkills`:
    - [0] `CrystalShieldSkill` — `targetStrategy=4`(Self), `targetType=2`(Self), `targetCount=1`, `targetRange=0`, `intentType=1`(Defend).
    - [1] `CrystalSpearSkill` — `targetStrategy=0`(RandomCharacter), `targetType=1`(Tiles), `targetCount=1`, `targetRange=2`, `intentType=0`(Attack).
  - `StartingPassives` = [`CrystallizePassive`].
- **CrystalShard.asset** — `MaxHP=45, CurrentHP=45`, `MonsterName="수정 파편"`, `Faction=0`.
  - `AIPattern` = `CrystalMinionPattern` (coreAbsentSkillIndex=0), `availableSkills`:
    - [0] `CrystalRainSkill` — `targetStrategy=2`(RandomTiles), `targetType=1`(Tiles), `targetCount=6`, `targetRange=0`, `intentType=0`(Attack).
    - [1] `CrystalRegenSkill` — `targetStrategy=4`(Self), `targetType=3`(None), `targetCount=1`, `targetRange=0`, `intentType=2`(Buff).
  - `StartingPassives` = [`CrystallizePassive`].

- [ ] **Step 1: 3종 에셋 생성 + 스크립트 GUID 배선**

Unity MCP `Unity_ManageAsset`(create, MonsterPreset)로 3개 생성하거나, `BlueSlime.asset`을 복제해 만든 뒤 `Unity_RunCommand`(IRunCommand CommandScript, SerializedObject/SerializedProperty만 사용)로:
- `m_Script` GUID = MonsterPreset.
- `BaseStats.MaxHP/CurrentHP=45`, `BaseStats.MonsterName` 설정.
- `references.RefIds`에 각 타입(`type: {class, ns, asm: Assembly-CSharp}`) + `data` 필드 채우기. 위 사양대로 `availableSkills`/`StartingPassives`/`AIPattern` rid 연결.

> ns 값: 스킬/패시브는 `DiceOrbit.Data.MonsterPresets.Wave3.Crystal.CrystalCore`(핵) / `...CrystalStone`(석) / `...CrystalShard`(편); 공유 클래스(`CrystallizePassive`,`CrystalMinionPattern`)는 `DiceOrbit.Data.MonsterPresets.Wave3.Crystal.Shared`. `SequentialPattern`이 아니라 각 몬스터 전용 패턴 클래스를 `AIPattern` rid로 직접 연결.

- [ ] **Step 2: 직렬화 검증**

세 `.asset`을 Read로 열어 확인:
- `m_Script` guid가 MonsterPreset과 일치.
- `MaxHP/CurrentHP=45`, `MonsterName` 정확(특히 `"수정 핵"` — GetCore 매칭 의존).
- 각 RefId의 `type.class`/`ns`/`asm`이 실제 클래스와 일치하고, `availableSkills`/`StartingPassives`/`AIPattern`의 rid가 유효(`BlueSlime.asset` 구조와 대조).

- [ ] **Step 3: 타입 인스턴스화 검증**

Unity MCP: `AssetDatabase.Refresh()` → `GetConsoleLogs {logTypes:"Error"}`.
Expected: "Missing type"/직렬화 경고 0. (에셋 로드 시 모든 `[SerializeReference]` 타입이 해석돼야 함.)

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/CrystalCore/CrystalCore.asset \
        Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/CrystalStone/CrystalStone.asset \
        Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/CrystalShard/CrystalShard.asset \
        Assets/Scripts/Data/MonsterPresets/Wave3/Crystal/**/*.meta
git commit -m "feat: 수정 세트 프리셋 3종 배선 (수정 핵/수정석/수정 파편) - Wave3 수정"
```

---

## Task 8: 인카운터 배치 + 플레이 검증 (사용자)

코드/프리셋은 완성. 실제 배치·플레이 검증은 사용자 몫(슬라임과 동일 흐름).

- [ ] Act 에셋에 수정 세트 인카운터로 3종 배치.
- [ ] 플레이로 확인: 자수정 4개 소환 → 중첩 축적 → 15에서 수정 폭풍 발동 → 피격 캐릭터가 **다음 턴 이동/스킬 모두 비활성(기절)** → 다음 턴 정상 복귀. 수정 핵 처치 시 부하가 대체 패턴만 사용. 결정 방패/재생이 수정 핵에 적용. 웨이브 종료 시 자수정 타일 정리.

---

## Self-Review 결과

- **스펙 커버리지:** 기절(§2-1)=Task1, CrystalSet(§2-2)=Task2, 자수정 타일(§2-3)=Task2, 수정 핵(§3-1)=Task4, 수정석(§3-2)=Task5, 수정 파편(§3-3)=Task6, 프리셋(§4)=Task7, 검증(§6)=각 Task Step + Task8. 누락 없음.
- **스펙과의 차이(의도적):** ① 스펙은 `StunDebuff.cs` 신규 파일을 제안했으나, 재사용 상태가 모인 `CommonStatuses.cs`에 두는 것이 일관적이라 그리로 변경. ② `EffectType`에는 `GetDisplayName`이 없고 툴팁은 `TooltipKeywordFormatter`(이미 "Stun"→"기절" 보유)가 처리하므로 별도 표시명 추가 불필요. 두 변경 모두 스펙 의도(재사용/최소 변경) 강화.
- **타입 일관성:** `canAct()`, `StunDebuff`(필드/클래스명 동일 — 필드는 CharacterStats, 클래스는 Effects 네임스페이스로 충돌 없음), `CrystalSet.GetCore/GetStacks/AddStack`, `AmethystTile()`, `CrystallizePassive`, `CrystalMinionPattern.coreAbsentSkillIndex`, `CrystalCorePattern.stormThreshold`, 스킬 클래스명 전부 태스크 간 일치.
- **플레이스홀더:** 없음(모든 코드 블록 완결).
