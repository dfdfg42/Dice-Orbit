# Combat Pipeline Authoring Guide

This document is a practical authoring reference for skill/passive/status/effect extension.
It reflects current code behavior.

## 1. Core model

- Action source: `SkillManager`, `Monster`, `EffectManager`, `Unit.OnStartTurn`
- Runtime payload: `CombatContext`
- Execution engine: `CombatPipeline.Process(context)`
- Reactive consumers: `PassiveManager` and `StatusEffectManager` (both implement `ICombatReactor`)

## 2. Data path (actual)

1. `CharacterSkill` (SO, level-based) -> `RuntimeSkill`
2. `RuntimeSkill.ToSkillData()` converts to runtime `SkillData`
3. `SkillManager.PrepareSkill()` validates dice requirement and target
4. `SkillManager.ExecuteSkill()` builds `CombatAction` + `CombatContext`
5. `CombatPipeline.Process()` runs `OnPreAction -> OnCalculateOutput -> ApplyAction -> OnHit -> OnPostAction`
6. `UnitStats.TakeDamage()` or `UnitStats.Heal()` applies final result

## 3. CombatContext contract

- `SourceUnit`: attacker/caster unit
- `Target`: receiver unit
- `Action`: action metadata (`Name`, `Type`, `BaseValue`, tags)
- `OutputValue`: mutable final value used at apply phase
- `IsCancelled`: action cancel flag

Current note:
- `CombatContext.AddEffectToTarget(...)` is only a stub log and not wired to `StatusEffectManager` yet.

## 4. Trigger map (current)

Implemented and fired:
- `OnPreAction`
- `OnCalculateOutput`
- `OnHit`
- `OnPostAction`

Defined but not fired by pipeline main path:
- `OnTurnStart`
- `OnTurnEnd`
- `OnActionSuccess`

Impact:
- Any passive/status logic that depends on `OnTurnStart` will not run unless you add explicit emit points.

## 5. Skill / Passive / Status / Effect boundaries

### Skill
- Authoring asset: `CharacterSkill` + `SkillLevelData`
- Runtime execution: `SkillManager`
- Optional behavior modules: `SkillActionModule`

### Passive
- Authoring asset: `PassiveAbility` (SO)
- Runtime host: `PassiveManager`
- Hook point: `OnReact(trigger, context)`

### Status
- Runtime container: `StatusEffectManager` with `StatusEffect`
- Hook point: `OnReact(trigger, context)`
- Duration/stack lifecycle is managed in manager side

### Effect
- Data payload: `EffectData` (type/value/duration)
- Execution registry: `EffectManager` + `IEffect` implementations
- Some effects currently mutate raw stats directly (outside pipeline modifiers)

## 6. Authoring checklist

When adding a new skill:
1. Define `CharacterSkill` level data (requirement/effects/modules)
2. Verify `RuntimeSkill.ToSkillData()` mapping covers all fields
3. If needed, implement a new `SkillActionModule`
4. Confirm target resolution in `SkillManager.ResolveTargets()`
5. Confirm the action type/tags expected by passive/status logic

When adding a new passive:
1. Inherit `PassiveAbility`
2. Restrict trigger conditions clearly
3. Avoid persistent runtime state on shared SO instance (clone/runtime wrapper if needed)
4. Register asset in character/monster preset

When adding a new status/effect:
1. Add `EffectType` if required
2. Implement `IEffect` and register in `EffectManager`
3. If duration logic is needed, add through `StatusEffectManager.AddEffect`
4. Verify trigger timing expectations (`OnTurnStart` currently not emitted by default path)

## 7. Known integration gaps (as-is)

- `OnTurnStart` trigger mismatch with current pipeline emit points.
- `IgnoreDefense` flag is set in action build path but defense ignore is not applied in `UnitStats.TakeDamage`.
- `AddEffectToTarget` helper in context is not connected to runtime effect manager.

## 8. Related files

- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Pipeline/CombatPipeline.cs`
- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Pipeline/CombatContext.cs`
- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/SkillManager.cs`
- `Assets/Scripts/Data/Passives/PassiveAbility.cs`
- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Passive/PassiveManager.cs`
- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/StatusEffectManager.cs`
- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/EffectData.cs`
- `Assets/Scripts/Core/Stage/BattleStage/Units/UnitStats.cs`
