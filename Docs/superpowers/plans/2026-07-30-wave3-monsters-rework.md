# Wave3 몬스터 재작업 (눈사람·눈골렘·서리토템) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 기존 Wave3 몬스터 3종(눈사람·눈골렘·서리토템)을 새 버전 스펙으로 재작업한다(재작업 배치 마지막).

**Architecture:** 이동불가(빙결) 중심 상호작용. 유일한 신규 인프라는 `CombatPipeline.NotifyReactors`가 방관 몬스터 패시브도 수집하도록 하는 작고 안전한 확장(서리 갑옷용). 나머지는 기존 `SnowSet`(피해추적)·`FrozenDebuff`·조건부 AI 관례 재사용. 일반 클래스에 새 필드 없음.

**Tech Stack:** Unity 6000.3.8f1, C#, `[SerializeReference]` 다형성, CombatPipeline/StatusEffect. 유닛 테스트 없음.

**스펙:** `Docs/superpowers/specs/2026-07-30-wave3-monsters-rework-design.md`

---

## 검증 / 커밋 / 브랜치

- 자동 테스트 없음 → 코드 태스크는 **컴파일 에러 0**(MCP Refresh + GetConsoleLogs Error + 새 타입 참조 RunCommand), 마지막 **플레이 검증**.
- 커밋: 한국어 `feat:`/`refactor:` + `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- 브랜치: 현재 브랜치(`feature/ui-node-sprites-20260730`)에서 계속.

**확인된 코드 사실:**
- `CombatPipeline.NotifyReactors`: reactors 수집 = A.Source B.Target C.Party(캐릭터) D.Artifacts E.Tiles. **몬스터는 Source/Target일 때만** 수집됨. `CollectReactors(Unit, list)`는 `unit.CollectReactors(list)` 호출(Monster도 Unit이라 가능). `Core.PartyManager`/`Core.Run.ArtifactManager`처럼 `Core.CombatManager.Instance.ActiveMonsters`(List<Monster>) 접근 가능. `Distinct()`로 중복 제거됨.
- `AttackContext.IsEffected`(실제 피해 적중 시 true, ApplyAction에서 세팅), `.IsSimulation`, `.OutputValue`(float), `.Target`(Unit), `.SourceUnit`(Unit).
- `SnowMan.cs` `SnowSet`(static): `AddDamageTaken/GetDamageTaken/ResetDamageTaken(Monster)`, `ExpandLR(center,range)`, `OtherAliveMonsters(owner)`. `ThrowSnow`(prisonDuration/cancelDamageThreshold), `SnowStorm`(damage), `HappySnowmanPassive`(OnHit 피해추적+회복, TurnEnd 리셋).
- `SnowGolem.cs`: `SnowSmash`(damage/range, GetCustomTiles 빙결 우선), `SnowShield`(armorAmount), `SnowGolemPassive`(눈감옥 유지), `SnowGolemDeath`. Asset AIPattern=Sequential [SnowShield, SnowSmash].
- `FrostTotem.cs`: `FrostFlower`(damage20), `DewPoint`(damageBuff2), `FrostbitePassive`(동상), `FrostbiteDebuff`(status, **Wave4 재사용 중 — 유지**). Asset AIPattern=Sequential [DewPoint, FrostFlower].
- `FrozenDebuff(0, duration)`(이동불가) `DiceOrbit.Systems.Effects`, `EffectType.Frozen`. `MonsterAI`(ns DiceOrbit.Data.MonsterAI): `owner`, `availableSkills`, abstract `GetNextSkill()`.

---

## File Structure

**수정(코드)**
- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Pipeline/CombatPipeline.cs` — 몬스터 리액터 수집 추가.
- `Assets/Scripts/Data/MonsterPresets/Wave3/SnowMan/SnowMan.cs` — `HappySnowmanPassive`(이동불가), `ThrowSnow`(진창눈 25+취소20), `SnowStorm`(25).
- `Assets/Scripts/Data/MonsterPresets/Wave3/SnowGolem/SnowGolem.cs` — `SnowSmash`(±3/25), `SnowShield`→`SnowFistSkill`, 신규 `SnowGolemPattern`.
- `Assets/Scripts/Data/MonsterPresets/Wave3/FrostTotem/FrostTotem.cs` — `FrostbitePassive`→`FrostArmorPassive`, `DewPoint`(+3). `FrostFlower`/`FrostbiteDebuff` 유지.

**수정(에셋, MCP)**: `SnowMan.asset`, `SnowGolem.asset`, `FrostTotem.asset`.
**신규 파일**: 없음. **변경 없음**: `CharacterStats`/`Character`/`Monster`.

---

## Task 1: 파이프라인 — 방관 몬스터 리액터 수집

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Pipeline/CombatPipeline.cs`

- [ ] **Step 1: 섹션 F 추가**

`NotifyReactors`에서 파티 수집(C) 블록 다음, 유물(D) 블록 앞에 삽입:

```csharp
            // C. Party 전체에서 Reactor 수집
                if (Core.PartyManager.Instance != null)
                {
                    foreach (var ally in Core.PartyManager.Instance.Party)
                    {
                        if (ally != null && ally.Passives is ICombatReactor allyReactor)
                        {
                            CollectReactors(ally, reactors);
                        }
                    }
            }

            // F. 활성 몬스터 전체에서 Reactor 수집 (반응형 몬스터 패시브 — 서리 갑옷 등)
            if (Core.CombatManager.Instance != null && Core.CombatManager.Instance.ActiveMonsters != null)
            {
                foreach (var m in Core.CombatManager.Instance.ActiveMonsters)
                    if (m != null) CollectReactors(m, reactors);
            }

            // D. 유물에서 Reactor 수집 (ArtifactManager — 보유 런타임 인스턴스 자체가 ICombatReactor)
```

- [ ] **Step 2: 컴파일 확인**

MCP Refresh → `Unity_GetConsoleLogs { logTypes:"Error" }` = 0.

- [ ] **Step 3: 커밋**

```bash
git add "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Pipeline/CombatPipeline.cs"
git commit -m "feat: 파이프라인이 방관 몬스터 리액터도 수집 (반응형 몬스터 패시브 지원)"
```

---

## Task 2: 눈사람 재작업 (SnowMan.cs 전체 교체)

**Files:**
- Modify: `Assets/Scripts/Data/MonsterPresets/Wave3/SnowMan/SnowMan.cs`

- [ ] **Step 1: 전체 교체**

```csharp
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using DiceOrbit.Systems.Effects;
using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Data.MonsterPresets.Wave3.SnowMan
{
    /// <summary>눈사람 세트 공유 헬퍼.</summary>
    public static class SnowSet
    {
        public static IEnumerable<Monster> OtherAliveMonsters(Monster owner)
        {
            var monsters = CombatManager.Instance?.ActiveMonsters;
            if (monsters == null) yield break;
            foreach (var m in monsters)
                if (m != null && m != owner && m.IsAlive) yield return m;
        }

        public static List<TileData> ExpandLR(TileData center, int range)
        {
            var result = new List<TileData>();
            if (center == null) return result;
            var set = new HashSet<TileData> { center };
            var t = center;
            for (int i = 0; i < range && t?.NextTile != null; i++) { t = t.NextTile; set.Add(t); }
            t = center;
            for (int i = 0; i < range && t?.PreviousTile != null; i++) { t = t.PreviousTile; set.Add(t); }
            result.AddRange(set);
            return result;
        }

        // 눈사람이 이번 라운드(직전 플레이어 턴) 동안 받은 피해 누적
        private static readonly Dictionary<Monster, int> DamageTakenThisRound = new();

        public static void AddDamageTaken(Monster snowman, int amount)
        {
            if (snowman == null) return;
            DamageTakenThisRound.TryGetValue(snowman, out int cur);
            DamageTakenThisRound[snowman] = cur + Mathf.Max(0, amount);
        }

        public static int GetDamageTaken(Monster snowman)
        {
            if (snowman == null) return 0;
            DamageTakenThisRound.TryGetValue(snowman, out int cur);
            return cur;
        }

        public static void ResetDamageTaken(Monster snowman)
        {
            if (snowman != null) DamageTakenThisRound[snowman] = 0;
        }
    }

    /// <summary>[진창눈] 무작위 대상 1명에게 damage 피해. 눈사람이 이번 라운드 cancelDamageThreshold 이상 받았으면 취소.</summary>
    [System.Serializable]
    public class ThrowSnow : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 25;
        [Tooltip("이 피해 이상 받으면 공격 취소")]
        [SerializeField] private int cancelDamageThreshold = 20;

        public ThrowSnow()
        {
            skillName = "진창눈";
            description = "무작위 대상 1명에게 피해. 이번 라운드 일정 피해 이상 받으면 취소";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var snowman = source as Monster;
            if (snowman != null && SnowSet.GetDamageTaken(snowman) >= cancelDamageThreshold)
            {
                Debug.Log($"[진창눈] {source.name} 피해 {SnowSet.GetDamageTaken(snowman)} ≥ {cancelDamageThreshold} → 취소");
                return;
            }
            AttackUnits(source, targetUnits, damage);
        }
    }

    /// <summary>[눈보라] 무작위 대상 2명이 속한 타일 + 좌우 각각 1칸에 피해. (RandomCharacter + Tiles + count 2 + range 1)</summary>
    [System.Serializable]
    public class SnowStorm : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 25;

        public SnowStorm()
        {
            skillName = "눈보라";
            description = "무작위 대상 2명이 속한 타일 + 좌우 각각 1칸에 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>
    /// [행복한 눈사람] 눈사람의 공격이 적중한 적은 다음 턴 이동 불가(빙결)가 된다.
    /// 또한 눈사람이 받은 피해를 누적해 [진창눈]의 취소 판정에 쓰고, 턴 종료 시 초기화한다.
    /// </summary>
    [System.Serializable]
    public class HappySnowmanPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [Tooltip("피격된 적 이동불가(빙결) 지속 턴")]
        [SerializeField] private int immobilizeDuration = 2;

        public HappySnowmanPassive()
        {
            passiveName = "행복한 눈사람";
            description = "눈사람에게 피격된 적은 다음 턴 이동 불가";
            priority = 10;
            isStackable = false;
        }

        public override string GetDynamicDescription()
            => $"눈사람에게 피격된 적 다음 턴 이동 불가 ({immobilizeDuration}턴)";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnHit || context.IsSimulation || !context.IsEffected) return;

            // 눈사람이 받은 피해 누적 (진창눈 취소 판정용)
            if (context.Target == owner)
                SnowSet.AddDamageTaken(owner as Monster, Mathf.RoundToInt(context.OutputValue));

            // 눈사람 공격이 적중한 적 → 다음 턴 이동 불가
            if (context.SourceUnit == owner && context.Target is Character victim && victim.IsAlive)
                victim.StatusEffects?.AddEffect(new FrozenDebuff(0, immobilizeDuration));
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (trigger == CombatTrigger.OnPostAction && context.Phase == EventPhase.TurnEnd && context.SourceUnit == owner)
                SnowSet.ResetDamageTaken(owner as Monster);
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }
}
```

**변경:** `ThrowSnow`→진창눈(피해+취소, 이동불가 제거), `SnowStorm` 25, `HappySnowmanPassive`(회복→이동불가 부여). `SnowSet` 유지.

- [ ] **Step 2: 컴파일 확인**

Refresh → 콘솔 에러 0.

- [ ] **Step 3: 커밋**

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave3/SnowMan/SnowMan.cs"
git commit -m "feat: 눈사람 재작업 - 피격 적 이동불가 패시브, 진창눈 25+취소20, 눈보라 25"
```

---

## Task 3: 눈골렘 재작업 (SnowGolem.cs)

**Files:**
- Modify: `Assets/Scripts/Data/MonsterPresets/Wave3/SnowGolem/SnowGolem.cs`

- [ ] **Step 1: SnowSmash 값(±3/25) + SnowShield→SnowFistSkill + SnowGolemPattern**

`SnowSmash`의 필드 기본값 변경:

```csharp
        [Header("Skill Settings")]
        [SerializeField] private int damage = 25;
        [Tooltip("중심 타일 기준 좌우 확장 칸 수")]
        [SerializeField] private int range = 3;
```

`SnowShield` 클래스 전체를 아래 `SnowFistSkill`로 교체:

```csharp
    /// <summary>[눈 주먹] 무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해. (RandomCharacter + Tiles + range 2)</summary>
    [System.Serializable]
    public class SnowFistSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 25;

        public SnowFistSkill()
        {
            skillName = "눈 주먹";
            description = "무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }
```

파일 안(namespace `...SnowGolem` 내부, 아무 클래스 뒤)에 조건부 패턴 추가:

```csharp
    /// <summary>
    /// 눈골렘 AI. availableSkills 순서 = [0 눈강타, 1 눈주먹].
    /// 살아있는 적 중 이동불가(Frozen)가 하나라도 있으면 눈강타, 없으면 눈주먹.
    /// </summary>
    [System.Serializable]
    public class SnowGolemPattern : DiceOrbit.Data.MonsterAI.MonsterAI
    {
        public override MonsterSkill GetNextSkill()
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;

            var alive = PartyManager.Instance?.GetAliveCharacters();
            bool anyFrozen = false;
            if (alive != null)
                foreach (var c in alive)
                    if (c != null && c.StatusEffects != null &&
                        c.StatusEffects.HasEffect(DiceOrbit.Data.EffectType.Frozen)) { anyFrozen = true; break; }

            if (anyFrozen) return availableSkills[0];                          // 눈강타
            return availableSkills.Count >= 2 ? availableSkills[1] : availableSkills[0]; // 눈주먹
        }
    }
```

`SnowGolemPassive`(눈감옥), `SnowGolemDeath`, `SnowSmash`의 나머지 로직(GetCustomTiles 빙결 우선)은 그대로.

- [ ] **Step 2: 컴파일 확인**

Refresh → 콘솔 에러 0. (SnowGolem.asset이 아직 `SnowShield`를 참조해 "Missing type" 경고 가능 — Task 5에서 재배선.)

- [ ] **Step 3: 커밋**

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave3/SnowGolem/SnowGolem.cs"
git commit -m "feat: 눈골렘 재작업 - 눈강타 ±3/25, 눈주먹(눈방패 대체), 조건부 SnowGolemPattern"
```

---

## Task 4: 서리토템 재작업 (FrostTotem.cs)

**Files:**
- Modify: `Assets/Scripts/Data/MonsterPresets/Wave3/FrostTotem/FrostTotem.cs`

- [ ] **Step 1: DewPoint 피해 버프 +3**

`DewPoint`의 `damageBuff` 기본값을 2→3:

```csharp
        [Header("Skill Settings")]
        [Tooltip("아군 전체에 영구 부여할 공격력 증가량")]
        [SerializeField] private int damageBuff = 3;
```

(설명 문자열 "+2"가 있으면 "+3"으로, 없으면 그대로.)

- [ ] **Step 2: FrostbitePassive → FrostArmorPassive 교체**

`FrostbitePassive` 클래스 전체(동상 패시브)를 아래로 교체(`FrostbiteDebuff` status 클래스는 **건드리지 않는다** — Wave4 재사용):

```csharp
    /// <summary>
    /// [서리 갑옷] 다른 아군 몬스터의 공격이 적중하면, 모든 아군 몬스터에게 일시 방어도를 부여한다.
    /// (파이프라인이 방관 몬스터 패시브도 디스패치하므로 owner가 당사자가 아니어도 발화)
    /// </summary>
    [System.Serializable]
    public class FrostArmorPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [Tooltip("다른 아군 공격 적중 시 전 아군에 부여할 방어도")]
        [SerializeField] private int armorAmount = 10;

        public FrostArmorPassive()
        {
            passiveName = "서리 갑옷";
            description = "다른 아군의 공격이 적중하면 모든 아군에게 일시 방어도 부여";
            priority = 10;
            isStackable = false;
        }

        public override string GetDynamicDescription()
            => $"다른 아군 공격 적중 시 모든 아군 방어도 +{armorAmount}";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnHit || context.IsSimulation || !context.IsEffected) return;
            if (!(context.SourceUnit is Monster attacker) || attacker == owner) return;  // 다른 아군 몬스터만

            var monsters = CombatManager.Instance?.ActiveMonsters;
            if (monsters == null) return;
            foreach (var m in monsters)
            {
                if (m == null || !m.IsAlive || m.Stats == null) continue;
                m.Stats.TempArmor += armorAmount;
            }
            Debug.Log($"[서리 갑옷] {attacker.name} 명중 → 전 아군 방어도 +{armorAmount}");
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }
```

**유지:** `FrostFlower`(서리꽃), `DewPoint`(이슬점, +3), `FrostbiteDebuff`(동상 status — Wave4 사용).

- [ ] **Step 3: 컴파일 확인**

Refresh → 콘솔 에러 0. (FrostTotem.asset이 아직 `FrostbitePassive`를 StartingPassives로 참조해 "Missing type" 경고 가능 — Task 5에서 재배선.)

- [ ] **Step 4: 커밋**

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave3/FrostTotem/FrostTotem.cs"
git commit -m "feat: 서리토템 재작업 - 서리 갑옷(반응형 팀 방어), 이슬점 +3"
```

---

## Task 5: 프리셋 배선 (MCP)

**Files:**
- Modify(에셋): `SnowMan.asset`, `SnowGolem.asset`, `FrostTotem.asset` (경로 Step 1)

`Unity_RunCommand` + SerializedObject(Wave1/2 기법). enum: TargetSelectionStrategy RandomCharacter=0/Custom=5; TargetType Characters=0/Tiles=1; IntentType Attack=0.

- [ ] **Step 1: 3개 에셋 경로·구조·아이콘 guid 확인**

Glob `Assets/**/Wave3/{SnowMan,SnowGolem,FrostTotem}/*.asset`. 각 Read해 availableSkills 순서·intentIcon guid·StartingPassives 확보.

- [ ] **Step 2: SnowMan.asset — 순서 [진창눈, 눈보라] + 값**

availableSkills를 **[ThrowSnow(진창눈), SnowStorm(눈보라)]** 순서로 재배치(기존 두 래퍼 순서 스왑, 아이콘 보존). SerializedObject로 SnowStorm.skillData.damage=25, ThrowSnow.skillData.damage=25/cancelDamageThreshold=20 확정. ThrowSnow 타깃 RandomCharacter(0)/Characters(0)/count1, SnowStorm RandomCharacter(0)/Tiles(1)/count2/range1.

- [ ] **Step 3: SnowGolem.asset — SnowGolemPattern [눈강타, 눈주먹]**

`golem.AIPattern = new SnowGolemPattern { availableSkills = [Sk(new SnowSmash()), Sk(new SnowFistSkill())] }` (코드), SetDirty+SaveAssets. SerializedObject:
- [0] 눈강타 SnowSmash: Custom(5)/Tiles(1)/count1/range0/Attack(0), skillData.damage=25/range=3
- [1] 눈주먹 SnowFistSkill: RandomCharacter(0)/Tiles(1)/count1/range2/Attack(0), skillData.damage=25
- 아이콘: 옛 SnowSmash 아이콘→[0], 옛 SnowShield 아이콘→[1] (Step 1 확보).

- [ ] **Step 4: FrostTotem.asset — 순서 [서리꽃, 이슬점] + StartingPassives=FrostArmorPassive**

availableSkills를 **[FrostFlower(서리꽃), DewPoint(이슬점)]** 순서로. `frost.StartingPassives = new List<PassiveAbility>{ new FrostArmorPassive() }`(코드, FrostbitePassive 대체). SerializedObject: DewPoint.skillData.damageBuff=3; 서리꽃 RandomCharacter(0)/Characters(0)? — 현재 FrostFlower 타깃 유지(RandomCharacter+Characters+count2+range1 확인). 아이콘 보존.

- [ ] **Step 5: 검증**

3개 에셋 Read: SnowMan [진창눈,눈보라]·값; SnowGolem AIPattern=SnowGolemPattern·[SnowSmash(Custom), SnowFistSkill], SnowShield 없음; FrostTotem [FrostFlower,DewPoint]·StartingPassives=FrostArmorPassive, FrostbitePassive 없음.

- [ ] **Step 6: 커밋**

```bash
git add "Assets/Scripts/Data/MonsterPresets/Wave3"/*/*.asset
git commit -m "feat: Wave3 프리셋 배선 - 눈사람/눈골렘(조건부)/서리토템(서리갑옷) 재배선"
```

---

## Task 6: 플레이 검증 + 마무리

- [ ] **Step 1: 플레이(DX11 확인) → Wave3 전투**

- [ ] **Step 2: 스펙 §9 체크리스트**

- 눈사람: 진창눈 25(받은 피해 20↑ 라운드엔 `취소` 로그), 눈보라 25, 눈사람 명중 대상 다음 턴 이동불가.
- 눈골렘: 이동불가 적 있으면 눈강타(±3/25), 없으면 눈주먹(±2/25). 눈감옥 3타일.
- 서리토템: 다른 아군 몬스터 공격 적중 시 `[서리 갑옷]` 로그 + 전 아군 방어도 증가. 이슬점 +3.
- **회귀**: 기존 몬스터/캐릭터 패시브 정상(방관 디스패치 추가에도 owner 게이트로 무영향). Wave4 양력/음력(FrostbiteDebuff) 정상.

- [ ] **Step 3: 튜닝(프리셋 필드)**

서리 갑옷이 과하면(다대상 AoE 다중 발동) armorAmount 하향. 이동불가가 다음 턴 못 덮으면 immobilizeDuration 상향.

- [ ] **Step 4: 개발 브랜치 마무리**

Announce 후 `superpowers:finishing-a-development-branch`.

---

## Self-Review

**스펙 커버리지:**
- 파이프라인 몬스터 리액터 → Task 1 ✔
- 눈사람 이동불가 패시브·진창눈 25/취소20·눈보라 25·순서 → Task 2 + Task 5 ✔
- 눈골렘 눈강타 ±3/25·눈주먹·조건부 패턴 → Task 3 + Task 5 ✔
- 서리토템 서리 갑옷·이슬점 +3·순서 → Task 4 + Task 5 ✔
- FrostbiteDebuff 유지(Wave4) → Task 4 명시 ✔ / 새 필드 0 ✔

**플레이스홀더 스캔:** 코드 스텝 완전. 에셋(Task 5)은 순서·enum 정수·"아이콘 guid 확보 후 보존" 명시.

**타입 일관성:** `Core.CombatManager.ActiveMonsters`, `AttackContext.IsEffected/IsSimulation`, `FrozenDebuff(0,duration)`, `SnowSet.AddDamageTaken/GetDamageTaken/ResetDamageTaken`, `ThrowSnow`(damage/cancelDamageThreshold), `SnowStorm`(damage), `SnowSmash`(damage/range), `SnowFistSkill`(damage), `SnowGolemPattern`([0]눈강타/[1]눈주먹), `FrostArmorPassive`(armorAmount), `DewPoint`(damageBuff), `EffectType.Frozen`, `HasEffect` — 일치 확인.

**리스크(플레이 확정):**
1. 방관 몬스터 디스패치 추가 후 기존 패시브 회귀 — 전부 owner 게이트라 안전(확인됨).
2. 서리 갑옷 per-hit 다중 발동 — 강하면 armorAmount 튜닝.
3. 눈사람 이동불가가 눈보라(다대상)에도 적용 — 의도대로(피격 적 전부).
