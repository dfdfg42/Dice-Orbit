# Wave2 몬스터 재작업 (아기곰·엄마곰) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 기존 Wave2 몬스터 2종(아기곰·엄마곰)을 새 버전 스펙으로 재작업한다.

**Architecture:** 꿀 상호작용 강화가 핵심. 신규 규칙은 기존 `BearPackTracker`(곰 세트 공유 트래커)를 확장하고, 기존 상태효과(`WeakStatus` 쇠약 / `FrozenDebuff` 이동불가)에 얹어 구현한다. 일반 클래스(Character/Monster)에는 새 필드를 넣지 않는다. 패시브는 `OnAttack` 훅(OnCalculateOutput=피해 보정, OnHit=명중 후 부여)을 사용한다.

**Tech Stack:** Unity 6000.3.8f1, C#, `[SerializeReference]` 다형성, StatusEffect/BearPackTracker. 유닛 테스트 없음.

**스펙:** `Docs/superpowers/specs/2026-07-30-wave2-monsters-rework-design.md`

---

## 검증 / 커밋 / 브랜치

- 자동 테스트 없음 → 코드 태스크는 **컴파일 에러 0**(MCP Refresh + GetConsoleLogs Error + 새 타입 참조 RunCommand), 마지막 **플레이 검증**.
- 커밋: 한국어 `feat:`/`refactor:` + `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- 브랜치: 현재 브랜치(`feature/ui-node-sprites-20260730`)에서 계속 (main 아님).

**확인된 코드 사실:**
- `BearPackTracker`(static, `Wave2/BearPackTracker.cs`): `HoneyEaten`, `HoneySteps`(Dictionary<Character,(int turn,int count)>), `HoneyTileCount()`, `RegisterHoneyStep`, `EnsureWaveHook`(OnCombatStart→Reset+ClearHoneyTiles). `using DiceOrbit.Core; DiceOrbit.Data.Tile;`.
- `HoneyPawTile.OnTraverse`: 회복 + RegisterHoneyEaten + RegisterHoneyStep(≥threshold면 FrozenDebuff) + 자기 제거.
- `WeakStatus(int value, int duration)`(`DiceOrbit.Systems.Effects`): OnCalculateOutput, `SourceUnit==Owner`면 `OutputValue *= 1 - Value/100f` (=소유자 딜 감소).
- `CombatTrigger` = { OnPreAction, OnCalculateOutput, OnHit, OnPostAction }. `context.IsSimulation`으로 프리뷰/시뮬 구분.
- `BabyBear.cs`: `HoneyLoverPassive`(OnAttack +HoneyEaten), `HoneyPawSkill`(healOnStep/bindThreshold/bindDuration), `BabyBearCharge`(damage20). Asset AIPattern=Sequential [BabyBearCharge, HoneyPawSkill].
- `MommyBear.cs`: `ProtectSkill`(자버프), `MommyBearTear`(damage20, asset range3), `HoneyFurPassive`(꿀5개↑ 받피-20%), `BearHelper.FindMonsterByName`. Asset AIPattern=Sequential [ProtectSkill, MommyBearTear].
- `SkillData.GetCustomTargets(MonsterSkill, Monster)` override로 Custom 타깃 가능(TargetStrategy=Custom). `AttackUnits(source, list, damage)`.

---

## File Structure

**수정(코드)**
- `Assets/Scripts/Data/MonsterPresets/Wave2/BearPackTracker.cs` — `HoneyTilesNear`, `LastBabyAttacker`+`SetLastBabyAttacker`, `GetHoneySteps` 추가 + `Reset` 갱신.
- `Assets/Scripts/Data/MonsterPresets/Wave2/BabyBear/BabyBear.cs` — `HoneyLoverPassive` 재작업(±2 꿀×3 + 최근 공격자 기록).
- `Assets/Scripts/Data/MonsterPresets/Wave2/MommyBear/MommyBear.cs` — `HoneyFurPassive`→꿀 묻은 발톱(쇠약); `ProtectSkill` 제거→`ProtectiveInstinctSkill`(보호 본능).

**수정(에셋, MCP)**
- `BabyBear.asset`(스킬 순서 [꿀묻히기, 돌진]), `MommyBear.asset`(패턴 [보호본능, 찢어, 찢어]).

**신규 파일**: 없음. **변경 없음**: `CharacterStats`/`Character`/`Monster`.

---

## Task 1: BearPackTracker 확장

**Files:**
- Modify: `Assets/Scripts/Data/MonsterPresets/Wave2/BearPackTracker.cs`

- [ ] **Step 1: 필드/헬퍼 추가**

`BearPackTracker` 클래스 안, `HoneyEaten` 프로퍼티 아래에 추가:

```csharp
        public static int HoneyEaten { get; private set; }

        /// <summary>아기곰을 가장 최근에 공격한 캐릭터 (엄마곰 [보호 본능]용). 웨이브 시작 시 null.</summary>
        public static Character LastBabyAttacker { get; private set; }

        public static void SetLastBabyAttacker(Character c) => LastBabyAttacker = c;
```

`Reset()`에 `LastBabyAttacker = null;` 추가:

```csharp
        public static void Reset()
        {
            HoneyEaten = 0;
            HoneySteps.Clear();
            LastBabyAttacker = null;
        }
```

클래스 끝(`ClearHoneyTiles` 위 아무 곳)에 헬퍼 2개 추가:

```csharp
        /// <summary>중심 타일 ±radius 안 꿀 타일 개수 (궤도 모듈로).</summary>
        public static int HoneyTilesNear(int centerTileIndex, int radius)
        {
            var orbit = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbit?.Tiles == null) return 0;
            int total = orbit.Tiles.Count;
            if (total == 0) return 0;

            int count = 0;
            for (int i = -radius; i <= radius; i++)
            {
                int idx = (centerTileIndex + i) % total;
                if (idx < 0) idx += total;
                var tile = orbit.GetTile(idx);
                if (tile != null && tile.HasAttribute(TileAttributeType.Honey)) count++;
            }
            return count;
        }

        /// <summary>해당 캐릭터가 그 턴에 밟은 꿀 수(읽기 전용).</summary>
        public static int GetHoneySteps(Character character, int turn)
        {
            if (character == null) return 0;
            if (HoneySteps.TryGetValue(character, out var entry) && entry.turn == turn) return entry.count;
            return 0;
        }
```

- [ ] **Step 2: 컴파일 확인**

MCP Refresh → `Unity_GetConsoleLogs { logTypes:"Error" }` = 0.

- [ ] **Step 3: 커밋**

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave2/BearPackTracker.cs"
git commit -m "feat: BearPackTracker 확장 - HoneyTilesNear/LastBabyAttacker/GetHoneySteps"
```

---

## Task 2: 아기곰 패시브 재작업

**Files:**
- Modify: `Assets/Scripts/Data/MonsterPresets/Wave2/BabyBear/BabyBear.cs`

- [ ] **Step 1: `HoneyLoverPassive` 교체**

`BabyBear.cs`의 `HoneyLoverPassive` 클래스 전체를 아래로 교체(다른 클래스 `HoneyPawSkill`/`BabyBearCharge`는 그대로):

```csharp
    /// <summary>
    /// [아기 곰은 꿀을 좋아해] 공격 시 피격 대상 주변 ±honeyRadius칸 꿀 타일 개수 × damagePerHoney 만큼 피해 증가.
    /// + 아기곰이 피격되면 그 공격자를 BearPackTracker.LastBabyAttacker로 기록(엄마곰 보호 본능용).
    /// </summary>
    [System.Serializable]
    public class HoneyLoverPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [Tooltip("피격 대상 주변 ±칸")]
        [SerializeField] private int honeyRadius = 2;
        [Tooltip("주변 꿀 1개당 추가 피해")]
        [SerializeField] private int damagePerHoney = 3;

        public HoneyLoverPassive()
        {
            passiveName = "아기 곰은 꿀을 좋아해";
            description = "공격 대상 주변 꿀 타일 개수 × 3 만큼 피해 증가";
            priority = 10;
            isStackable = false;
        }

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            BearPackTracker.EnsureWaveHook();
        }

        public override string GetDynamicDescription()
            => $"공격 대상 주변 ±{honeyRadius}칸 꿀 1개당 피해 +{damagePerHoney}";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null) return;

            // 아기곰 공격 → 피격 대상 주변 꿀 × N 추가 피해
            if (trigger == CombatTrigger.OnCalculateOutput && context.SourceUnit == owner
                && context.Target is Character victim && victim.CurrentTile != null)
            {
                int honey = BearPackTracker.HoneyTilesNear(victim.CurrentTile.TileIndex, honeyRadius);
                context.OutputValue += honey * damagePerHoney;
            }

            // 아기곰 피격 → 최근 공격자 기록 (실제 명중, 시뮬 제외)
            if (trigger == CombatTrigger.OnHit && context.Target == owner
                && !context.IsSimulation && context.SourceUnit is Character attacker)
            {
                BearPackTracker.SetLastBabyAttacker(attacker);
            }
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }
```

(`Character`는 `using DiceOrbit.Core;`로 이미 접근 가능. `AttackContext`/`CombatTrigger`는 `using DiceOrbit.Core.Pipeline;`로 접근 가능 — 둘 다 BabyBear.cs 상단에 이미 있음.)

- [ ] **Step 2: 컴파일 확인**

Refresh → 콘솔 에러 0.

- [ ] **Step 3: 커밋**

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave2/BabyBear/BabyBear.cs"
git commit -m "feat: 아기곰 패시브 재작업 - 피격 대상 ±2칸 꿀×3 + 최근 공격자 기록"
```

---

## Task 3: 엄마곰 재작업 (꿀 묻은 발톱 + 보호 본능)

**Files:**
- Modify: `Assets/Scripts/Data/MonsterPresets/Wave2/MommyBear/MommyBear.cs`

- [ ] **Step 1: MommyBear.cs 전체 교체**

```csharp
using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.MonsterPresets.Wave2.MommyBear
{
    /// <summary>
    /// [보호 본능] 아기곰을 가장 최근에 공격한 적에게 피해. 단 그 적이 이번 턴 꿀 cancelHoneySteps개 이상 밟았으면 취소.
    /// 최근 공격자 없음/사망 시 no-op. (BearPackTracker.LastBabyAttacker / GetHoneySteps 사용)
    /// </summary>
    [System.Serializable]
    public class ProtectiveInstinctSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 20;
        [Tooltip("대상이 이번 턴 이 개수 이상 꿀을 밟으면 취소")]
        [SerializeField] private int cancelHoneySteps = 2;

        public ProtectiveInstinctSkill()
        {
            skillName = "보호 본능";
            description = "아기곰을 가장 최근에 공격한 적에게 피해 (그 적이 이번 턴 꿀 2개 이상 밟으면 취소)";
        }

        public override int GetPreviewDamage() => damage;

        public override List<Unit> GetCustomTargets(MonsterSkill skill, Monster owner)
        {
            var attacker = BearPackTracker.LastBabyAttacker;
            return (attacker != null && attacker.IsAlive) ? new List<Unit> { attacker } : new List<Unit>();
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var attacker = BearPackTracker.LastBabyAttacker;
            if (attacker == null || !attacker.IsAlive)
            {
                Debug.Log("[보호 본능] 최근 아기곰 공격자 없음 — no-op");
                return;
            }

            int turn = CombatManager.Instance != null ? CombatManager.Instance.TurnCount : 0;
            if (BearPackTracker.GetHoneySteps(attacker, turn) >= cancelHoneySteps)
            {
                Debug.Log($"[보호 본능] 취소 — 대상이 이번 턴 꿀 {cancelHoneySteps}개 이상 밟음");
                return;
            }

            AttackUnits(source, new List<Unit> { attacker }, damage);
        }
    }

    /// <summary>[곰은 사람을 찢어] 무작위 대상 1명이 속한 타일 + 좌우 각각 3칸에 피해. (RandomCharacter + Tiles + range 3)</summary>
    [System.Serializable]
    public class MommyBearTear : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 20;

        public MommyBearTear()
        {
            skillName = "곰은 사람을 찢어";
            description = "무작위 대상 1명이 속한 타일 + 좌우 각각 3칸에 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>
    /// [꿀 묻은 발톱] 엄마곰 공격이 명중할 때, 피격 대상 주변 ±honeyRadius칸에 꿀 타일이 있으면
    /// 그 대상에게 쇠약(다음 턴 가하는 피해 -weakenPercent%, weakenDuration턴)을 부여한다.
    /// </summary>
    [System.Serializable]
    public class HoneyFurPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [Tooltip("피격 대상 주변 ±칸 (꿀 탐색)")]
        [SerializeField] private int honeyRadius = 2;
        [Tooltip("쇠약(다음 턴 가하는 피해 감소) 퍼센트")]
        [SerializeField] private int weakenPercent = 20;
        [Tooltip("쇠약 지속 턴 (다음 턴 커버)")]
        [SerializeField] private int weakenDuration = 2;

        public HoneyFurPassive()
        {
            passiveName = "꿀 묻은 발톱";
            description = "공격 범위에 꿀 타일이 있으면, 피격된 적은 다음 턴 피해량 20% 감소";
            priority = 10;
            isStackable = false;
        }

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            BearPackTracker.EnsureWaveHook();
        }

        public override string GetDynamicDescription()
            => $"공격 대상 주변 ±{honeyRadius}칸에 꿀이 있으면 피격 적 다음 턴 피해 -{weakenPercent}%";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnHit) return;      // 실제 명중 시
            if (context.IsSimulation) return;
            if (context.SourceUnit != owner) return;
            if (!(context.Target is Character victim) || victim.CurrentTile == null) return;

            if (BearPackTracker.HoneyTilesNear(victim.CurrentTile.TileIndex, honeyRadius) > 0)
            {
                victim.StatusEffects?.AddEffect(new WeakStatus(weakenPercent, weakenDuration));
            }
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    /// <summary>공용 헬퍼</summary>
    public static class BearHelper
    {
        public static Monster FindMonsterByName(string name)
        {
            var monsters = CombatManager.Instance?.ActiveMonsters;
            if (monsters == null) return null;
            foreach (var m in monsters)
                if (m != null && m.IsAlive && m.Stats != null && m.Stats.MonsterName == name)
                    return m;
            return null;
        }
    }
}
```

**삭제됨:** `ProtectSkill`(→ `ProtectiveInstinctSkill`로 대체). **변경:** `HoneyFurPassive`(발톱 로직), **유지:** `MommyBearTear`, `BearHelper`.

- [ ] **Step 2: 컴파일 확인**

Refresh → `Unity_GetConsoleLogs { logTypes:"Error" }` = 0. (MommyBear.asset이 아직 `ProtectSkill`을 참조해 "Missing type" 경고 가능 — Task 4에서 재배선하면 사라짐.)

- [ ] **Step 3: 커밋**

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave2/MommyBear/MommyBear.cs"
git commit -m "feat: 엄마곰 재작업 - 꿀 묻은 발톱(쇠약)/보호 본능(최근 공격자·취소)"
```

---

## Task 4: 프리셋 배선 (MCP)

**Files:**
- Modify(에셋): `BabyBear.asset`, `MommyBear.asset` (경로 Step 1)

- [ ] **Step 1: 에셋 경로 + 현재 구조 확인**

Glob `Assets/**/Wave2/BabyBear/*.asset`, `Assets/**/Wave2/MommyBear/*.asset`. 두 에셋을 Read해 현재 availableSkills 순서·타깃·intentIcon guid 확보(아이콘 보존용).

- [ ] **Step 2: BabyBear.asset — 스킬 순서 스왑**

`Unity_RunCommand`: 로드 후 `preset.AIPattern.availableSkills`를 **[꿀묻히기(HoneyPawSkill), 돌진(BabyBearCharge)]** 순서로 재배치(기존 두 MonsterSkill 래퍼를 순서만 교환 — 아이콘/타깃/값 보존). `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets`.
확인값: 꿀묻히기 RandomTiles/Tiles/count5, 돌진 RandomCharacter/Tiles/range2/damage20.

- [ ] **Step 3: MommyBear.asset — 패턴 [보호본능, 찢어, 찢어]**

`Unity_RunCommand`(Wave4/5 기법): 
```
mommy.AIPattern = new SequentialPattern { availableSkills = new List<MonsterSkill> {
    Sk(new ProtectiveInstinctSkill()),  // 0 보호 본능
    Sk(new MommyBearTear()),            // 1 찢어
    Sk(new MommyBearTear()),            // 2 찢어
} };
```
`SetDirty` + `SaveAssets`. 그 후 `SerializedObject`로 타깃 세팅(enum: TargetSelectionStrategy Custom=5/RandomCharacter=0; TargetType Characters=0/Tiles=1; IntentType Attack=0):
- [0] 보호본능: Custom(5)/Characters(0)/count1/range0/Attack(0) — GetCustomTargets 사용
- [1] 찢어: RandomCharacter(0)/Tiles(1)/count1/range3/Attack(0)
- [2] 찢어: RandomCharacter(0)/Tiles(1)/count1/range3/Attack(0)
기존 intent 아이콘 보존: [0]에 옛 ProtectSkill 아이콘 guid, [1]·[2]에 옛 MommyBearTear 아이콘 guid를 `intentIcon.objectReferenceValue`로 설정(Step 1에서 확보).
`ApplyModifiedPropertiesWithoutUndo` + `SaveAssets` + `Refresh`.

- [ ] **Step 4: 검증**

두 에셋 Read: BabyBear availableSkills = [HoneyPawSkill, BabyBearCharge]; MommyBear AIPattern=SequentialPattern, availableSkills = [ProtectiveInstinctSkill(Custom), MommyBearTear(range3), MommyBearTear(range3)], "Missing type"(ProtectSkill) 없음.

- [ ] **Step 5: 커밋**

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave2/BabyBear"/*.asset \
        "Assets/Scripts/Data/MonsterPresets/Wave2/MommyBear"/*.asset
git commit -m "feat: Wave2 프리셋 배선 - 아기곰 [꿀묻히기,돌진], 엄마곰 [보호본능,찢어,찢어]"
```

---

## Task 5: 플레이 검증 + 마무리

- [ ] **Step 1: 플레이(DX11 확인) → Wave2 전투**

- [ ] **Step 2: 스펙 §9 체크리스트**

- 아기곰: 꿀묻히기 5타일 → 돌진. 돌진이 꿀 옆 대상 타격 시 피해 증가(±2 꿀×3). 아기곰 공격 후 그 적이 최근 공격자로 기록.
- 엄마곰: 찢어/보호본능 명중 대상 옆 꿀 있으면 다음 턴 `쇠약`. 보호본능이 아기곰 최근 공격자 타격 — 그 적 이번 턴 꿀 2개면 `[보호 본능] 취소` 로그, 최근 공격자 없으면 no-op 로그.
- 꿀 2개 밟으면 이동불가(혈당스파이크).
- 회귀: 꿀 웨이브 시작 정리 정상.

- [ ] **Step 3: 튜닝(프리셋 필드)**

쇠약이 다음 턴을 못 덮으면 `weakenDuration` 상향. 최근 공격자 추적이 안 되면 OnHit/IsSimulation 확인.

- [ ] **Step 4: 개발 브랜치 마무리**

Announce 후 `superpowers:finishing-a-development-branch`.

---

## Self-Review

**스펙 커버리지:**
- 아기곰 패시브 ±2 꿀×3 + 최근 공격자 기록 → Task 2 ✔ / 꿀묻히기·돌진 유지·순서 → Task 4 ✔
- 엄마곰 발톱(쇠약) → Task 3 ✔ / 보호본능(최근 공격자·이번턴 꿀2 취소·no-op) → Task 3 ✔ / 찢어·패턴 1→2→2 → Task 3+4 ✔
- BearPackTracker 확장(HoneyTilesNear/LastBabyAttacker/GetHoneySteps) → Task 1 ✔
- 새 필드 0(트래커 확장·상태효과) ✔

**플레이스홀더 스캔:** 코드 스텝 완전한 코드. 에셋(Task 4)은 확정 순서·enum 정수 + "현재 아이콘 guid 확보 후 보존" 명시.

**타입 일관성:** `BearPackTracker.HoneyTilesNear/LastBabyAttacker/SetLastBabyAttacker/GetHoneySteps`, `WeakStatus(percent,duration)`, `ProtectiveInstinctSkill`(damage/cancelHoneySteps/GetCustomTargets), `HoneyFurPassive`(honeyRadius/weakenPercent/weakenDuration), `HoneyLoverPassive`(honeyRadius/damagePerHoney), `CombatTrigger.OnCalculateOutput/OnHit`, `context.IsSimulation`, `AttackUnits/AttackTiles` — 일치 확인.

**리스크(플레이 확정):**
1. 쇠약 지속 2턴이 다음 플레이어 턴을 덮는지 — 안 되면 상향.
2. `OnHit`가 다대상 AoE 각 명중마다 발화 → 여러 적에 각각 쇠약 부여(의도대로). 시뮬 프리뷰엔 OnHit 미발화(IsSimulation 게이트로 이중 안전).
3. 최근 공격자 기록이 OnHit(실제 명중)에서만 → 아기곰이 한 번도 안 맞으면 보호본능 no-op(의도대로).
