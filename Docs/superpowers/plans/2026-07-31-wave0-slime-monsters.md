# Wave0 슬라임 세트 (파란·초록x2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wave0 슬라임 세트 3종(파란 슬라임 1 + 초록 슬라임 2)을 구현한다.

**Architecture:** 신규 인프라는 점액 타일 하나뿐. 점액 디버프는 기존 `WeakStatus`(쇠약), 박치기 취소는 `SnowSet`식 세트 트래커(`SlimeSet`), 나머지는 기존 타일/스킬/패턴 재사용. 일반 클래스 변경 없음.

**Tech Stack:** Unity 6000.3.8f1, C#, `[SerializeReference]` 다형성, TileAttribute/StatusEffect. 유닛 테스트 없음.

**스펙:** `Docs/superpowers/specs/2026-07-31-wave0-slime-monsters-design.md`

---

## 검증 / 커밋 / 브랜치

- 자동 테스트 없음 → 코드 태스크는 **컴파일 에러 0**(MCP Refresh + GetConsoleLogs Error + 새 타입 참조 RunCommand), 마지막 **플레이 검증**.
- 커밋: 한국어 `feat:` + `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- 브랜치: `feature/wave0-slime-20260731`(생성됨)에서 진행.

**확인된 코드 사실:**
- `TileAttribute`(`.../Tile/TileAttribute.cs`): ctor `(TileAttributeType, value, duration, isStackable=false)`, 가상 `OnTraverse/OnEndTurn(Character)`, `Owner`(TileData). `TileAttributeType` enum도 이 파일(마지막 값 `Flame`).
- 타일 예시 `HoneyPawTile`: OnTraverse → 효과 부여 + `Owner?.RemoveAttribute(this)`. `RandMineTile`: OnTraverse/OnEndTurn → 피해 + 제거.
- `WeakStatus(int value, int duration)`(`DiceOrbit.Systems.Effects`): 쇠약(소유자 가하는 피해 -Value%).
- `SnowSet`(`Wave3/SnowMan/SnowMan.cs`): `Dictionary<Monster,int> DamageTakenThisRound` + `AddDamageTaken/GetDamageTaken/ResetDamageTaken`. `MineFieldCleaner`(`Wave1/Goblin/Goblin.cs`): `EnsureWaveHook`→`CombatManager.OnCombatStart` 구독 → 타일 정리.
- `AttackContext.IsEffected`(실제 피해), `.IsSimulation`, `.OutputValue`, `.Target`, `.SourceUnit`. `CombatTrigger` OnHit/OnPostAction. 몹 TurnEnd = `OnPostAction && Phase==TurnEnd && SourceUnit==owner`.
- `MonsterSkill` 타깃: `TilesWithAttribute`(strategy=3) + `targetTileAttribute`(TileAttributeType) — `MineBombSkill`이 RandMine으로 사용. `AttackTiles`/`AttackUnits`(SkillData).
- 프리셋 생성/배선: Wave5 기법(`ScriptableObject.CreateInstance<MonsterPreset>` + `AssetDatabase.CreateAsset` + SerializedObject).

---

## File Structure

**신규**
- `Assets/Scripts/Data/MonsterPresets/Wave0/Shared/SlimeTile.cs` — `SlimeTile : TileAttribute`.
- `.../Wave0/Shared/SlimeSet.cs` — 피격 추적 + 웨이브 점액 정리.
- `.../Wave0/BlueSlime/BlueSlime.cs` — `PlantSlimePassive`, `BodySlamSkill`, `SlimeSpraySkill`.
- `.../Wave0/GreenSlime/GreenSlime.cs` — `CorrosiveSlimeSkill`.
- 프리셋 에셋 2개.

**수정**: `.../Tile/TileAttribute.cs`(`Slime` enum).
**변경 없음**: `CharacterStats`/`Character`/`Monster`.

---

## Task 1: 점액 타일 시스템 (enum + SlimeTile + SlimeSet)

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/TileAttribute.cs`
- Create: `Assets/Scripts/Data/MonsterPresets/Wave0/Shared/SlimeTile.cs`
- Create: `Assets/Scripts/Data/MonsterPresets/Wave0/Shared/SlimeSet.cs`

- [ ] **Step 1: `TileAttributeType.Slime` 추가**

`TileAttribute.cs` enum의 `Flame,` 다음에:

```csharp
        Flame,          // 불꽃: 턴 종료 시 V 피해 / 통과 시 소화 (Wave5)
        Slime,          // 점액: 통과/턴 종료 시 쇠약 부여 (Wave0)
```

`GetDisplayName()` switch의 `Flame` 케이스 다음에:

```csharp
                TileAttributeType.Flame => "불꽃",
                TileAttributeType.Slime => "점액",
```

- [ ] **Step 2: `SlimeSet.cs` 생성**

```csharp
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;
using UnityEngine;

namespace DiceOrbit.Data.MonsterPresets.Wave0.Shared
{
    /// <summary>
    /// 슬라임 세트 공유 상태.
    /// - 파란 슬라임이 이번 라운드 받은 누적 피해 ([박치기] 취소 판정).
    /// - 점액 타일은 몬스터 사망 후에도 유지되고 웨이브 종료(=다음 전투 시작) 시 정리.
    /// </summary>
    public static class SlimeSet
    {
        private static readonly Dictionary<Monster, int> DamageTakenThisRound = new();
        private static CombatManager hookedManager;

        public static void AddDamageTaken(Monster slime, int amount)
        {
            if (slime == null) return;
            DamageTakenThisRound.TryGetValue(slime, out int cur);
            DamageTakenThisRound[slime] = cur + Mathf.Max(0, amount);
        }

        public static int GetDamageTaken(Monster slime)
        {
            if (slime == null) return 0;
            DamageTakenThisRound.TryGetValue(slime, out int cur);
            return cur;
        }

        public static void ResetDamageTaken(Monster slime)
        {
            if (slime != null) DamageTakenThisRound[slime] = 0;
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
            DamageTakenThisRound.Clear();
            var orbit = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbit?.Tiles == null) return;
            foreach (var tile in orbit.Tiles)
                if (tile != null) tile.RemoveAttributeType(TileAttributeType.Slime);
        }
    }
}
```

- [ ] **Step 3: `SlimeTile.cs` 생성**

```csharp
using DiceOrbit.Core;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// [점액]으로 설치되는 점액 타일. 캐릭터가 통과하거나 그 위에서 턴을 종료하면
    /// 그 캐릭터에 쇠약(가하는 피해 -weakenPercent%, weakenDuration턴)을 부여하고 자기 자신을 제거한다.
    /// 타일당 1개, 영구(몬스터 사망 후 유지), 웨이브 종료 시 SlimeSet이 정리.
    /// </summary>
    public class SlimeTile : TileAttribute
    {
        private readonly int weakenPercent;
        private readonly int weakenDuration;

        public SlimeTile(int weakenPercent, int weakenDuration)
            : base(TileAttributeType.Slime, weakenPercent, -1, false)
        {
            this.weakenPercent = weakenPercent;
            this.weakenDuration = weakenDuration;
        }

        public override void OnTraverse(Core.Character character) => Activate(character);
        public override void OnEndTurn(Core.Character character) => Activate(character);

        private void Activate(Core.Character target)
        {
            if (target == null || !target.IsAlive || target.StatusEffects == null) return;
            target.StatusEffects.AddEffect(new WeakStatus(weakenPercent, weakenDuration));
            Owner?.RemoveAttribute(this);
        }

        public override string GetDescription()
            => $"통과·턴 종료 시 쇠약(가하는 피해 -{weakenPercent}%, {weakenDuration}턴) 부여 (발동 후 삭제)";
    }
}
```

- [ ] **Step 4: 컴파일 확인 + 커밋**

MCP Refresh → `Unity_GetConsoleLogs { logTypes:"Error" }` = 0.

```bash
git add "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/TileAttribute.cs" \
        "Assets/Scripts/Data/MonsterPresets/Wave0/Shared/SlimeTile.cs" \
        "Assets/Scripts/Data/MonsterPresets/Wave0/Shared/SlimeSet.cs"
git commit -m "feat: 점액 타일 시스템 - Slime enum, SlimeTile(쇠약), SlimeSet(피격추적+정리)"
```

---

## Task 2: 파란 슬라임 (BlueSlime.cs)

**Files:**
- Create: `Assets/Scripts/Data/MonsterPresets/Wave0/BlueSlime/BlueSlime.cs`

- [ ] **Step 1: 파일 생성**

```csharp
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Data.MonsterPresets.Wave0.Shared;

namespace DiceOrbit.Data.MonsterPresets.Wave0.BlueSlime
{
    /// <summary>
    /// [점액] 턴 종료 시 무작위 tileCount 타일에 점액 설치. + 파란 슬라임이 받은 피해를 누적([박치기] 취소용),
    /// 턴 종료 시 누적 초기화.
    /// </summary>
    [System.Serializable]
    public class PlantSlimePassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [SerializeField] private int tileCount = 3;
        [SerializeField] private int weakenPercent = 20;
        [SerializeField] private int weakenDuration = 2;

        public PlantSlimePassive()
        {
            passiveName = "점액";
            description = "턴 종료 시 무작위 3타일에 점액 설치(밟으면 쇠약)";
            priority = 10; isStackable = false;
        }

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            SlimeSet.EnsureWaveHook();
        }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null) return;
            // 파란 슬라임이 실제로 피해를 받으면 누적
            if (trigger == CombatTrigger.OnHit && !context.IsSimulation && context.IsEffected && context.Target == owner)
                SlimeSet.AddDamageTaken(owner as Monster, Mathf.RoundToInt(context.OutputValue));
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnPostAction || context.Phase != EventPhase.TurnEnd || context.SourceUnit != owner) return;

            PlantSlime();
            SlimeSet.ResetDamageTaken(owner as Monster);
        }

        private void PlantSlime()
        {
            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit == null) return;
            var candidates = orbit.Tiles.Where(t => t != null && !t.HasAttribute(TileAttributeType.Slime)).ToList();
            int place = Mathf.Min(tileCount, candidates.Count);
            for (int i = 0; i < place; i++)
            {
                int r = Random.Range(0, candidates.Count);
                candidates[r].AddAttribute(new SlimeTile(weakenPercent, weakenDuration));
                candidates.RemoveAt(r);
            }
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    /// <summary>[박치기] 무작위 대상 1명에게 damage. 이번 라운드 받은 누적 피해 ≥ cancelThreshold면 취소.</summary>
    [System.Serializable]
    public class BodySlamSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;
        [Tooltip("이 피해 이상 받으면 취소")]
        [SerializeField] private int cancelThreshold = 10;

        public BodySlamSkill()
        {
            skillName = "박치기";
            description = "무작위 대상 1명에게 피해 (이번 라운드 10 이상 받으면 취소)";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var slime = source as Monster;
            if (slime != null && SlimeSet.GetDamageTaken(slime) >= cancelThreshold)
            {
                Debug.Log($"[박치기] {source.name} 이번 라운드 피해 {SlimeSet.GetDamageTaken(slime)} ≥ {cancelThreshold} → 취소");
                return;
            }
            AttackUnits(source, targetUnits, damage);
        }
    }

    /// <summary>[점액 분사] 설치된 점액 타일 + 좌우 각각 1칸에 damage 피해. (TilesWithAttribute=Slime + range 1)</summary>
    [System.Serializable]
    public class SlimeSpraySkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;

        public SlimeSpraySkill()
        {
            skillName = "점액 분사";
            description = "설치된 점액 타일 + 좌우 각각 한 칸에 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인 + 커밋**

Refresh → 에러 0.

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave0/BlueSlime/BlueSlime.cs"
git commit -m "feat: 파란 슬라임 - 점액 패시브/박치기(취소)/점액 분사"
```

---

## Task 3: 초록 슬라임 (GreenSlime.cs)

**Files:**
- Create: `Assets/Scripts/Data/MonsterPresets/Wave0/GreenSlime/GreenSlime.cs`

- [ ] **Step 1: 파일 생성**

```csharp
using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.MonsterPresets.Wave0.GreenSlime
{
    /// <summary>[부식성 점액] 무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 damage 피해. (RandomCharacter + Tiles + range 2)</summary>
    [System.Serializable]
    public class CorrosiveSlimeSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;

        public CorrosiveSlimeSkill()
        {
            skillName = "부식성 점액";
            description = "무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인 + 커밋**

Refresh → 에러 0.

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave0/GreenSlime/GreenSlime.cs"
git commit -m "feat: 초록 슬라임 - 부식성 점액(타일 AoE)"
```

---

## Task 4: 프리셋 생성·배선 (MCP)

**Files:**
- Create(에셋): `Wave0/BlueSlime/BlueSlime.asset`, `Wave0/GreenSlime/GreenSlime.asset`

`Unity_RunCommand`(Wave5 기법): `ScriptableObject.CreateInstance<MonsterPreset>` + `AssetDatabase.CreateAsset` + 그래프 대입 + SerializedObject. enum: TargetSelectionStrategy RandomCharacter=0/TilesWithAttribute=3; TargetType Characters=0/Tiles=1; IntentType Attack=0; TileAttributeType.Slime=15(Flame 다음).

- [ ] **Step 1: 파란 슬라임 프리셋**

- BaseStats: MonsterName="파란 슬라임", MaxHP=20, CurrentHP=20.
- 기본 스프라이트 3종(BasicIdle/Attack/Damaged) 할당(Wave5와 동일 경로).
- AIPattern = SequentialPattern, availableSkills:
  1. 박치기 `BodySlamSkill`: RandomCharacter(0)/Characters(0)/count1/range0/Attack(0)
  2. 점액 분사 `SlimeSpraySkill`: TilesWithAttribute(3)/Tiles(1)/count1/range1/Attack(0) + targetTileAttribute=Slime(15)
- StartingPassives: `PlantSlimePassive`

- [ ] **Step 2: 초록 슬라임 프리셋**

- BaseStats: MonsterName="초록 슬라임", MaxHP=20, CurrentHP=20. 스프라이트 할당.
- AIPattern = SequentialPattern, availableSkills:
  1. 부식성 점액 `CorrosiveSlimeSkill`: RandomCharacter(0)/Tiles(1)/count1/range2/Attack(0)
- StartingPassives: 없음(빈 리스트)

- [ ] **Step 3: 저장 + 검증**

`SaveAssets`+`Refresh`. 두 에셋 Read: 파란 [박치기, 점액분사(TilesWithAttribute/targetTileAttribute=15)]+점액 패시브, 초록 [부식성 점액], HP20, "Missing type" 없음.

- [ ] **Step 4: 커밋**

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave0"
git commit -m "feat: Wave0 슬라임 프리셋 2종 생성·배선 (파란/초록)"
```

---

## Task 5: 인카운터 + 플레이 검증

- [ ] **Step 1: 인카운터 배치 (사용자)**

`Act.asset` 등에 슬라임 인카운터 추가 — **파란 슬라임 1 + 초록 슬라임 2**(초록 프리셋을 MonsterPresets 리스트에 2번). 배치 위치는 게임 콘텐츠 결정이라 사용자 확인.

- [ ] **Step 2: 플레이(DX11 확인) → Wave0 전투**

- [ ] **Step 3: 스펙 §8 체크리스트**

- 파란: 매 턴종료 점액 3타일, 밟으면 `쇠약`(가하는 피해↓), 박치기(받은 피해 10↑ 라운드엔 `[박치기] 취소` 로그), 점액 분사(점액 주변 15).
- 초록x2: 부식성 점액 ±2/15.
- 회귀: 점액 웨이브 시작 정리, 다른 몬스터/타일 정상.

- [ ] **Step 4: 개발 브랜치 마무리**

Announce 후 `superpowers:finishing-a-development-branch`.

---

## Self-Review

**스펙 커버리지:**
- 점액 타일(쇠약/삭제/영구/정리) → Task 1 ✔
- 파란 [점액](3타일+피격추적)·[박치기](취소)·[점액분사] → Task 2 ✔
- 초록 [부식성 점액] → Task 3 ✔
- 프리셋(파란1/초록1, 초록 2배치) → Task 4/5 ✔
- 새 필드 0(SlimeSet 트래커·WeakStatus) ✔

**플레이스홀더 스캔:** 코드 스텝 완전. 에셋(Task 4)은 확정 수치·enum 정수 명시.

**타입 일관성:** `TileAttributeType.Slime`, `SlimeTile(weakenPercent,weakenDuration)`, `SlimeSet.AddDamageTaken/GetDamageTaken/ResetDamageTaken/EnsureWaveHook`, `PlantSlimePassive`(tileCount/weakenPercent/weakenDuration), `BodySlamSkill`(damage/cancelThreshold), `SlimeSpraySkill`(damage), `CorrosiveSlimeSkill`(damage), `WeakStatus`, `AttackTiles/AttackUnits`, `CombatTrigger.OnHit`, `context.IsEffected` — 일치.

**리스크(플레이 확정):**
1. 점액 분사 타깃(TilesWithAttribute=Slime+range1)이 targetTiles를 제대로 채우는지 — MineBombSkill과 동일 방식. 점액 없으면 빈 타격(무해).
2. 박치기 취소 임계·쇠약 지속 — 프리셋 필드로 튜닝.
3. 초록 슬라임 패시브 없음 → StartingPassives 빈 리스트로 확인.
