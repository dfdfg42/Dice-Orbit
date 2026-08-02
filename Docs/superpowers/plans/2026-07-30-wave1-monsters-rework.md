# Wave1 몬스터 재작업 (고블린·해골병사) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 기존 Wave1 몬스터 2종(고블린·해골병사)을 새 버전 스펙으로 재작업한다.

**Architecture:** 대부분 기존 파일의 값/시점/순서 조정. 유일한 신규 메커닉(해골 [뼈 화살] "이번 라운드 뼈무덤 발동 시 취소")은 기존 StatusEffect 시스템의 마커(`BoneMarkStatus`)로 구현 — 일반 클래스에 새 필드/정적 없음. `BoneTile.Activate`가 마커를 부여하고, 해골 TurnEnd에 리셋한다.

**Tech Stack:** Unity 6000.3.8f1, C#, `[SerializeReference]` 다형성(SkillData/PassiveAbility/MonsterAI), StatusEffect 시스템. 유닛 테스트 없음.

**스펙:** `Docs/superpowers/specs/2026-07-30-wave1-monsters-rework-design.md`

---

## 검증 방식 / 커밋 / 브랜치

- 자동 테스트 없음 → 각 코드 태스크는 **컴파일 에러 0**(MCP `AssetDatabase.Refresh` + `Unity_GetConsoleLogs { logTypes:"Error" }`, 강한 확인은 새 타입 참조 RunCommand)으로 마무리, 마지막에 **플레이 검증**.
- 커밋: 한국어 `feat:`/`refactor:` + 마지막 줄 `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- **브랜치**: main에서 갈라진 `feature/wave1-rework`에서 진행(실행 시작 시 생성). main 직접 금지.

**확인된 현재 코드 사실:**
- `Goblin.cs`: `PlantMinePassive.OnTurnEvent`가 `TurnStart && OnPreAction && SourceUnit==owner`에서 `PlantMines()`(무작위 2타일 지뢰). `MineBombSkill.damage=10`. `mineDamage=30`(코드 기본) — 실값은 프리셋 직렬화.
- `Skeleton.cs`: `SkeletonWhip`(skillName="뼈 화살", ±2 타일 AoE 15) = **실제로는 새 [뼈 검]**. `CalciumChargeSkill`(자버프) = 폐기 대상. `PlantBonePassive`: `PlantBones()`가 `for(index=4; index<20; index+=5)` = 4·9·14·19, `armorAmount=10`(기본), OnTurnEvent 없음.
- `BoneTile.cs`(ns `DiceOrbit.Data.Tile`): `Activate()`가 `beneficiary.Stats.TempArmor += Value`. `beneficiary`=해골병사(Monster).
- `StatusEffect` ctor `(EffectType, value, duration, isStackable=false)`; `StatusEffectManager.HasEffect(type)`/`RemoveEffect(type)`/`AddEffect(effect)`. 몹 TurnEnd 이벤트 = `OnPostAction && Phase==TurnEnd && SourceUnit==owner`(FrostbitePassive 검증). `EffectType`는 `DiceOrbit.Data`(Skeleton.cs에서 `EffectType.BuffAttack` 사용 중 → 접근 가능).
- Skeleton.asset AIPattern = SequentialPattern [SkeletonWhip, CalciumChargeSkill]. Goblin.asset AIPattern = SequentialPattern [GoblinClubSwing, MineBombSkill].

---

## File Structure

**수정(코드)**
- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/EffectData.cs` — `EffectType.BoneMark` 값 추가.
- `Assets/Scripts/Data/MonsterPresets/Wave1/Skeleton/BoneTile.cs` — `Activate`가 수혜 몬스터에 `BoneMarkStatus` 부여.
- `Assets/Scripts/Data/MonsterPresets/Wave1/Skeleton/Skeleton.cs` — `SkeletonWhip` 표시명 "뼈 검"; `CalciumChargeSkill` 제거 → `BoneArrowSkill` 추가; `BoneMarkStatus` 추가; `PlantBonePassive` 인덱스 4·10·16·방어도 5·TurnEnd 리셋.
- `Assets/Scripts/Data/MonsterPresets/Wave1/Goblin/Goblin.cs` — `PlantMinePassive` 훅 TurnEnd·설명; `MineBombSkill.damage=15`.

**수정(에셋, MCP)**
- `Goblin.asset`(MineBomb 15, 지뢰 20/2 확정), `Skeleton.asset`(뼈 방어도 5, AIPattern Random [뼈검/뼈화살]).

**신규 파일**: 없음.
**변경 없음(원칙)**: `CharacterStats`/`Character`/`Monster`/`MonsterStats`.

---

## Task 1: BoneMark enum + 마커 상태 + BoneTile 부여

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/EffectData.cs`
- Modify: `Assets/Scripts/Data/MonsterPresets/Wave1/Skeleton/BoneTile.cs`

- [ ] **Step 1: `EffectType.BoneMark` 추가**

`EffectData.cs` enum 마지막(`FireGuard,`) 다음에:

```csharp
        FireGuard,          // 불의 가호: 방어도 보유 시 받는 피해 감소 (Wave5)
        BoneMark,           // 뼈무덤 발동 마커: 이번 라운드 해골병사가 뼈 방어도 획득함 (Wave1)
```

- [ ] **Step 2: `BoneTile.Activate`가 마커 부여**

`BoneTile.cs` 상단 using에 `using DiceOrbit.Systems.Effects;` 추가하고, `Activate()`를 교체:

```csharp
using DiceOrbit.Core;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.Tile
{
    public class BoneTile : TileAttribute
    {
        // ... (ctor/SetBeneficiary/OnTraverse/OnEndTurn 그대로) ...

        private void Activate()
        {
            // 지정된 해골 병사에게만 방어도 부여 (살아있을 때) + 이번 라운드 발동 마커
            if (beneficiary != null && beneficiary.IsAlive)
            {
                beneficiary.Stats.TempArmor += Value;
                beneficiary.StatusEffects?.AddEffect(new BoneMarkStatus());
            }
        }

        // ... (GetDescription 그대로) ...
    }
}
```

(BoneMarkStatus는 Task 2 Step 2에서 정의 — 같은 어셈블리라 컴파일 순서 무관. Task 2까지 끝내고 컴파일한다.)

- [ ] **Step 3: 커밋 (Task 2 이후 함께 컴파일 확인)**

이 태스크는 Task 2와 함께 컴파일되므로, 커밋은 Task 2 끝에서 묶어서 한다. (여기선 파일 저장만.)

---

## Task 2: 해골병사 재작업 (뼈검/뼈화살/마커/뼈무덤)

**Files:**
- Modify: `Assets/Scripts/Data/MonsterPresets/Wave1/Skeleton/Skeleton.cs`

- [ ] **Step 1: Skeleton.cs 전체 교체**

```csharp
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using DiceOrbit.Systems.Effects;
using System.Collections.Generic;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Data.MonsterPresets.Wave1.Skeleton
{
    /// <summary>[뼈 검] 무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해. (RandomCharacter + Tiles + range 2)</summary>
    [System.Serializable]
    public class SkeletonWhip : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;

        public SkeletonWhip()
        {
            skillName = "뼈 검";
            description = "무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>[뼈 화살] 무작위 대상 1명에게 피해. 이번 라운드 뼈무덤 발동(BoneMark) 시 취소. (RandomCharacter + Characters)</summary>
    [System.Serializable]
    public class BoneArrowSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;

        public BoneArrowSkill()
        {
            skillName = "뼈 화살";
            description = "무작위 대상 1명에게 피해 (이번 라운드 뼈무덤 발동 시 취소)";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            if (source?.StatusEffects != null && source.StatusEffects.HasEffect(EffectType.BoneMark))
            {
                Debug.Log("[뼈 화살] 취소 — 이번 라운드 뼈무덤 발동");
                return;
            }
            AttackUnits(source, targetUnits, damage);
        }
    }

    /// <summary>
    /// [뼈 무덤] 웨이브 시작 시 4·10·16 타일에 뼈 타일. 통과/턴 종료 시 해골병사 방어도 +armorAmount(영구, 사망 시 삭제).
    /// 해골병사 턴 종료 시 BoneMark 마커를 리셋한다(이번 라운드 판정 종료).
    /// </summary>
    [System.Serializable]
    public class PlantBonePassive : PassiveAbility
    {
        [Header("Bone Settings")]
        [Tooltip("뼈 타일이 부여하는 일시 방어도")]
        [SerializeField] private int armorAmount = 5;

        private CombatManager hookedManager;

        public PlantBonePassive()
        {
            passiveName = "뼈 무덤";
            description = "웨이브 시작 시 4·10·16 타일에 뼈 타일 생성. 통과/턴 종료 시 해골 병사 방어도 +5";
            priority = 10;
            isStackable = false;
        }

        public override string Description => $"웨이브 시작 시 뼈 타일 생성. 통과/턴 종료 시 해골 병사 방어도 +{armorAmount}";

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            SubscribeCombatStart();
            if (CombatManager.Instance != null && CombatManager.Instance.InCombat)
                PlantBones();
        }

        private void SubscribeCombatStart()
        {
            var cm = CombatManager.Instance;
            if (cm == null) return;
            if (hookedManager == cm) return;
            if (hookedManager != null) hookedManager.OnCombatStart -= HandleCombatStart;
            cm.OnCombatStart += HandleCombatStart;
            hookedManager = cm;
        }

        private void HandleCombatStart()
        {
            if (owner == null || !owner.IsAlive) return;
            PlantBones();
        }

        private void PlantBones()
        {
            var orbitManager = GameManager.Instance?.GetOrbitManager();
            if (orbitManager == null) return;

            var skeleton = owner as Monster;
            foreach (int index in new[] { 4, 10, 16 })
            {
                var tile = orbitManager.GetTile(index);
                if (tile == null) continue;
                if (tile.HasAttribute(TileAttributeType.Bone)) continue;
                tile.AddAttribute(new BoneTile(TileAttributeType.Bone, armorAmount, -1, skeleton));
            }
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            // 해골병사 턴 종료 시 이번 라운드 뼈무덤 발동 마커 제거
            if (owner != null && trigger == CombatTrigger.OnPostAction
                && context.Phase == EventPhase.TurnEnd && context.SourceUnit == owner)
            {
                owner.StatusEffects?.RemoveEffect(EffectType.BoneMark);
            }
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    /// <summary>해골 병사 사망 시 모든 뼈 타일을 제거한다.</summary>
    [System.Serializable]
    public class SkelettonDeath : DeathEffect
    {
        public SkelettonDeath()
        {
            effectName = "Skeleton Death";
            description = "해골 병사가 죽을 때 발동하는 효과";
        }

        public override void Execute(Monster deadMonster)
        {
            Debug.Log($"[SkeletonDeath] {deadMonster.name} died. 뼈 타일 제거.");
            var tiles = GameManager.Instance?.GetOrbitManager()?.Tiles;
            if (tiles == null) return;
            foreach (var tile in tiles)
                if (tile != null) tile.RemoveAttributeType(TileAttributeType.Bone);
        }
    }
}

namespace DiceOrbit.Systems.Effects
{
    /// <summary>뼈무덤 발동 마커: 존재 여부만 사용(전투 효과 없음). 해골병사 TurnEnd에 RemoveEffect로 제거.</summary>
    public class BoneMarkStatus : StatusEffect
    {
        public BoneMarkStatus() : base(DiceOrbit.Data.EffectType.BoneMark, 0, -1) { }
    }
}
```

**삭제됨:** `CalciumChargeSkill`. **변경:** `SkeletonWhip` 표시명 뼈 검, `PlantBonePassive` 인덱스 4·10·16·방어도 5·TurnEnd 리셋. **추가:** `BoneArrowSkill`, `BoneMarkStatus`.

- [ ] **Step 2: 컴파일 확인**

MCP `AssetDatabase.Refresh()` → `Unity_GetConsoleLogs { logTypes:"Error" }` = 0. (Skeleton.asset이 아직 `CalciumChargeSkill`을 참조해 "Missing type" 경고 가능 — Task 4에서 재배선하면 사라짐. 경고이지 컴파일 에러 아님.)

- [ ] **Step 3: 커밋 (Task 1 + 2 묶음)**

```bash
git add "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/EffectData.cs" \
        "Assets/Scripts/Data/MonsterPresets/Wave1/Skeleton/BoneTile.cs" \
        "Assets/Scripts/Data/MonsterPresets/Wave1/Skeleton/Skeleton.cs"
git commit -m "feat: 해골병사 재작업 - 뼈검/뼈화살(BoneMark 취소)/뼈무덤 4·10·16·방어도5"
```

---

## Task 3: 고블린 재작업 (지뢰 턴종료 + 폭발 15)

**Files:**
- Modify: `Assets/Scripts/Data/MonsterPresets/Wave1/Goblin/Goblin.cs`

- [ ] **Step 1: MineBombSkill 피해 15**

`Goblin.cs`의 `MineBombSkill`에서:

```csharp
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;
```

(기존 `private int damage = 10;` → `15`. 코드 기본값. 실값은 Task 4에서 에셋에도 15로.)

- [ ] **Step 2: PlantMinePassive 발동 시점 = 턴 종료**

`PlantMinePassive`의 생성자 설명·`GetDynamicDescription`·`OnTurnEvent`를 교체:

```csharp
        [Tooltip("설치할 지뢰의 피해량")]
        [SerializeField] private int mineDamage = 20;

        [Tooltip("매 턴 설치할 지뢰 개수")]
        [SerializeField] private int minesPerTurn = 2;

        public PlantMinePassive()
        {
            passiveName = "지뢰 설치";
            description = "매 턴 종료 시 무작위 타일에 지뢰를 설치합니다. 지나가거나 턴 종료 시 피해, 발동 후 삭제";
            priority = 10;
            isStackable = false;
        }
```

`GetDynamicDescription`:

```csharp
        public override string GetDynamicDescription()
            => $"매 턴 종료 시 무작위 타일 {minesPerTurn}개에 {mineDamage} 피해 지뢰 설치 (중첩 가능)";
```

`OnTurnEvent`:

```csharp
        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (context.Phase == EventPhase.TurnEnd && context.SourceUnit == owner && trigger == CombatTrigger.OnPostAction)
            {
                PlantMines();
            }
        }
```

(`mineDamage` 기본값도 30→20으로 맞춤. 나머지 `PlantMines`/`Initialize`/`MineFieldCleaner`/`GoblinDeath`는 그대로.)

- [ ] **Step 3: 컴파일 확인**

Refresh → `Unity_GetConsoleLogs { logTypes:"Error" }` = 0.

- [ ] **Step 4: 커밋**

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave1/Goblin/Goblin.cs"
git commit -m "refactor: 고블린 지뢰 설치 턴종료로 변경 + 지뢰폭발 15"
```

---

## Task 4: 프리셋 배선 (MCP)

**Files:**
- Modify(에셋): `Goblin.asset`, `Skeleton.asset` (경로는 Step 1에서 확인)

`Unity_RunCommand` + `SerializedObject`로 배선(Wave4/5 기법). enum 정수: `TargetSelectionStrategy` RandomCharacter=0; `TargetType` Characters=0/Tiles=1; `IntentType` Attack=0.

- [ ] **Step 1: 에셋 경로 확인**

Glob `Assets/**/Wave1/Goblin/*.asset`, `Assets/**/Wave1/Skeleton/*.asset`.

- [ ] **Step 2: Goblin.asset — 값 확정**

`SerializedObject(goblin)`:
- `StartingPassives[0]`(PlantMinePassive): `mineDamage=20`, `minesPerTurn=2`.
- `AIPattern.availableSkills[1]`(MineBombSkill).skillData: `damage=15`.
- (AIPattern 유지: SequentialPattern [GoblinClubSwing, MineBombSkill].)
`ApplyModifiedPropertiesWithoutUndo()`.

- [ ] **Step 3: Skeleton.asset — 방어도 + 패턴 교체**

코드로 그래프 재구성(Wave4/5처럼):
```
skeleton.AIPattern = new RandomPattern { availableSkills = new List<MonsterSkill> {
    Sk(new SkeletonWhip()),      // 0 뼈 검
    Sk(new BoneArrowSkill()),    // 1 뼈 화살
} };
```
`EditorUtility.SetDirty` + `SaveAssets`. 그 후 `SerializedObject(skeleton)`:
- `StartingPassives[0]`(PlantBonePassive): `armorAmount=5`.
- `AIPattern.availableSkills[0]`(뼈 검): RandomCharacter(0)/Tiles(1)/count1/range2/Attack(0), skillData.damage=15.
- `AIPattern.availableSkills[1]`(뼈 화살): RandomCharacter(0)/Characters(0)/count1/range0/Attack(0), skillData.damage=15.
`ApplyModifiedPropertiesWithoutUndo()` + `SaveAssets` + `Refresh`.

(네임스페이스: `RandomPattern`=`DiceOrbit.Data.MonsterAI.Patterns`; `SkeletonWhip`/`BoneArrowSkill`=`DiceOrbit.Data.MonsterPresets.Wave1.Skeleton`.)

- [ ] **Step 4: 검증**

두 에셋을 Read로 열어: Goblin MineBomb damage=15/지뢰 20·2; Skeleton armorAmount=5, AIPattern=RandomPattern, availableSkills [SkeletonWhip(RC/Tiles/1/2), BoneArrowSkill(RC/Characters/1/0)], "Missing type"(CalciumCharge) 없음. 확인.

- [ ] **Step 5: 커밋**

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave1/Goblin/Goblin.asset" \
        "Assets/Scripts/Data/MonsterPresets/Wave1/Skeleton/Skeleton.asset"
git commit -m "feat: Wave1 프리셋 배선 - 고블린 지뢰폭발15, 해골 방어도5·Random 뼈검/뼈화살"
```

---

## Task 5: 플레이 검증 + 마무리

- [ ] **Step 1: 플레이 진입 (DX11 확인)**

D3D12+Intel Arc 크래시 이력 → DX11 고정 확인(`unity-d3d12-crash-dx11-fix`). Play → Wave1 전투.

- [ ] **Step 2: 스펙 §8 체크리스트**

- 고블린: 매 **턴 종료**에 지뢰 2개 설치(로그/타일 버블), 지뢰폭발 15, 몽둥이질 ±2/15.
- 해골병사: 전투 시작 **4·10·16** 뼈, 통과 시 방어도 **+5**. 패턴 Random 50/50 — 뼈검 ±2/15 / 뼈화살 15. **뼈 타일 발동한 라운드**엔 `[뼈 화살] 취소` 로그, 발동 안 한 라운드엔 15 피해.
- 회귀: 지뢰 웨이브 시작 정리, 뼈 사망 시 삭제 정상.

- [ ] **Step 3: 튜닝(필요 시, 프리셋 필드)**

뼈 마커 리셋 타이밍이 어긋나 뼈화살이 항상/전혀 취소되면: BoneMark 리셋 시점(TurnEnd) 확인. 값은 프리셋 필드.

- [ ] **Step 4: 개발 브랜치 마무리**

Announce: "I'm using the finishing-a-development-branch skill to complete this work." 후 `superpowers:finishing-a-development-branch`.

---

## Self-Review

**스펙 커버리지:**
- 고블린 지뢰 턴종료 → Task 3 ✔ / 지뢰폭발 15 → Task 3+4 ✔ / 몽둥이질 유지 ✔
- 해골 뼈 무덤 4·10·16·방어도5 → Task 2+4 ✔ / 뼈검 ±2/15 → Task 2 ✔ / 뼈화살 신규+취소 → Task 2 ✔ / 패턴 Random → Task 4 ✔
- BoneMark 마커(BoneTile 부여·TurnEnd 리셋) → Task 1+2 ✔
- 새 필드 0(마커=상태효과) ✔

**플레이스홀더 스캔:** 코드 스텝 완전한 코드. 에셋(Task 4) 확정 수치·enum 정수 명시.

**타입 일관성:** `EffectType.BoneMark`, `BoneMarkStatus()`, `BoneArrowSkill`(damage), `SkeletonWhip`(뼈 검), `PlantBonePassive`(armorAmount, 4·10·16, TurnEnd RemoveEffect), `PlantMinePassive`(TurnEnd/OnPostAction, mineDamage 20), `MineBombSkill`(damage 15), `RandomPattern`, `HasEffect/RemoveEffect/AddEffect`, `AttackTiles/AttackUnits` — 태스크 전반 일치.

**리스크(플레이 확정):**
1. BoneMark 리셋이 해골 TurnEnd(OnPostAction/TurnEnd)에 실제 발화하는지 — 안 되면 뼈화살이 계속 취소됨 → 트리거 확인(FrostbitePassive와 동일 가정).
2. 지뢰 "턴 종료" = 고블린 자기 턴 종료. 매 라운드 2개 누적되므로 지뢰밭이 빠르게 참 — 밸런스는 minesPerTurn/피해로 튜닝.
