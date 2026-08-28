# Tutorial Combo Combat Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the obsolete one-turn tutorial with a deterministic three-turn combat lesson covering zones, automatic attacks, positional passives, and the warrior's full combo finisher.

**Architecture:** Add a small `TutorialCombatProgress` state object that translates real combo events into tutorial gates. Keep orchestration in `TutorialScenario`: it spawns four real monsters, scripts dice before each player turn, tunes only runtime monster stats, and builds the UI steps while leaving normal combat code unchanged.

**Tech Stack:** Unity C#, existing tutorial UI, `ComboSystem`, `DiceManager`, editor self-test command, Unity prefab YAML.

**Spec:** `Docs/superpowers/specs/2026-08-28-tutorial-combo-redesign-design.md`

## Global Constraints

- Use actual movement, automatic attacks, passive pipeline notifications, monster intents, and combo events; do not add tutorial-only attack buttons.
- Rogue hands-on content stops after combo stage 1.
- Warrior stage 3 must be directly activated and must defeat all four tutorial monsters.
- Runtime tutorial tuning must not mutate character or monster preset assets.
- Cleanup must remove every tutorial event subscription and dice/action lock.

---

### Task 1: Combo-event tutorial progress

**Files:**
- Create: `Assets/Scripts/Core/Tutorial/TutorialCombatProgress.cs`
- Modify: `Assets/Scripts/Editor/CombatRedesignSelfTests.cs`

**Interfaces:**
- Consumes: `ComboOutcome`, `Character` references supplied by `TutorialScenario`.
- Produces: `Observe(Character character, int stage, ComboOutcome outcome)`, `WarriorStage1Done`, `RogueStage1Done`, `WarriorStage2Done`, and `WarriorFinished`.

- [ ] **Step 1: Write the failing state-transition test**

Add a self-test which creates distinct warrior and rogue `GameObject` components, calls the wished-for `TutorialCombatProgress.Observe` API with wrong and correct events, and asserts that only `(warrior,1,Advanced)`, `(rogue,1,Advanced)`, `(warrior,2,Advanced)`, and `(warrior,0,Finished)` complete their matching gates.

- [ ] **Step 2: Run the editor self-test and verify RED**

Run `DiceOrbit.EditorTools.CombatRedesignSelfTests.RunAll` through the connected Unity editor. Expected: compile failure because `TutorialCombatProgress` does not exist.

- [ ] **Step 3: Implement the minimal state object**

```csharp
public sealed class TutorialCombatProgress
{
    private readonly Character warrior;
    private readonly Character rogue;

    public bool WarriorStage1Done { get; private set; }
    public bool RogueStage1Done { get; private set; }
    public bool WarriorStage2Done { get; private set; }
    public bool WarriorFinished { get; private set; }

    public TutorialCombatProgress(Character warrior, Character rogue)
    {
        this.warrior = warrior;
        this.rogue = rogue;
    }

    public void Observe(Character character, int stage, ComboOutcome outcome)
    {
        if (character == warrior && outcome == ComboOutcome.Advanced && stage == 1) WarriorStage1Done = true;
        if (character == rogue && outcome == ComboOutcome.Advanced && stage == 1) RogueStage1Done = true;
        if (character == warrior && outcome == ComboOutcome.Advanced && stage == 2) WarriorStage2Done = true;
        if (character == warrior && outcome == ComboOutcome.Finished && stage == 0) WarriorFinished = true;
    }
}
```

- [ ] **Step 4: Run the editor self-test and verify GREEN**

Run the same command. Expected: all state-transition assertions pass.

### Task 2: Four-zone deterministic demo setup

**Files:**
- Modify: `Assets/Scripts/Core/Tutorial/TutorialScenario.cs`
- Modify: `Assets/Resources/TutorialScenario.prefab`
- Modify: `Assets/Scripts/Editor/CombatRedesignSelfTests.cs`

**Interfaces:**
- Consumes: warrior and rogue presets, a blue slime preset for zone 0, a green slime preset for zones 1–3.
- Produces: `IReadOnlyList<Monster> DemoMonsters`, a four-entry encounter, and runtime monster survivability tuned to the warrior's stage-3 damage.

- [ ] **Step 1: Write the failing prefab wiring test**

Load `Assets/Resources/TutorialScenario.prefab` with `AssetDatabase`, get `TutorialScenario`, and assert the exposed validation properties report all four preset references configured. Assert the scenario's encounter preset builder returns exactly four entries in blue/green/green/green order.

- [ ] **Step 2: Run self-tests and verify RED**

Expected: failure because the blue preset field, validation properties, and four-monster builder do not exist.

- [ ] **Step 3: Implement encounter creation and runtime tuning**

Add `blueDemoMonster`, retain `demoMonster` as green, build `[blue, green, green, green]`, collect all spawned monsters, compute `WarriorGreatswordActive.CalculateStageDamage(Warrior, 2)`, set each runtime `MaxHP` and `CurrentHP` to that value, and set `Invulnerable = true`. Add `ReleaseFinaleTargets()` to clear invulnerability immediately before the final guided move.

- [ ] **Step 4: Wire the blue slime asset in the prefab**

Assign `Assets/Scripts/Data/MonsterPresets/Wave0/BlueSlime/BlueSlime.asset` to `blueDemoMonster` while keeping the current green slime in `demoMonster`.

- [ ] **Step 5: Run self-tests and verify GREEN**

Expected: the prefab and encounter order assertions pass.

### Task 3: Three-turn guided tutorial

**Files:**
- Modify: `Assets/Scripts/Core/Tutorial/TutorialScenario.cs`

**Interfaces:**
- Consumes: `TutorialCombatProgress`, `ComboSystem.OnComboChanged`, `CombatManager.OnPlayerMoved`, `DiceManager.SetScriptedRoll`.
- Produces: a guided step list that gates on real combo results and scripts rolls `[4,2,5,3]`, `[4,1,5,6]`, and `[4,2,3,6]` before their respective player turns. The grouped-party step refreshes the blue slime's real body-slam intent so its fixed tile range uses the heroes' current positions.

- [ ] **Step 1: Replace stale movement-only flags with combo progress subscription**

Construct `TutorialCombatProgress` after the demo characters exist, subscribe `ComboSystem.OnComboChanged` to `Observe`, and keep movement flags only for character selection/movement diagnostics where a combo event is not the actual completion condition.

- [ ] **Step 2: Rewrite the first-turn steps**

Keep the intent, info panel, four-zone, danger-tile, dice, and autoattack explanations. Gate warrior eye-4 movement on `WarriorStage1Done`, gate rogue eye-2 movement on `RogueStage1Done`, and explain the real `균열 베기`, `쌍비수`, `협공 +30%`, and `방진 -10%` behavior.

- [ ] **Step 3: Add the second-turn steps**

Refresh the blue slime's body-slam intent after both heroes have grouped. Before the first end-turn action call `SetScriptedRoll(new[] { 4, 1, 5, 6 })`. Wait for the next player turn, guide warrior eye 4, and gate completion on `WarriorStage2Done`. Explain that `지진파` attacks the current and adjacent zones.

- [ ] **Step 4: Add the third-turn steps and finale**

Before the second end-turn action call `SetScriptedRoll(new[] { 4, 2, 3, 6 })`. Wait for the next player turn, release monster invulnerability on entering the final move step, guide warrior eye 4, and gate on `WarriorFinished` or the actual four-monster combat victory when combat teardown resets the combo first. Finish with the all-zone kill, party armor, and combo-break rules.

- [ ] **Step 5: Make cleanup symmetrical**

Unsubscribe both movement and combo handlers, clear `TutorialCombatProgress`, clear every runtime demo reference, reset dice/action locks, and ensure skip/cleanup cannot leak scripted rolls into the recruit run.

### Task 4: Verification

**Files:**
- Verify: `Assets/Scripts/Core/Tutorial/TutorialScenario.cs`
- Verify: `Assets/Scripts/Core/Tutorial/TutorialCombatProgress.cs`
- Verify: `Assets/Resources/TutorialScenario.prefab`
- Verify: `Assets/Scripts/Editor/CombatRedesignSelfTests.cs`

**Interfaces:**
- Consumes: completed implementation.
- Produces: fresh evidence that the code compiles, self-tests pass, and the tutorial completes through the actual third-stage event.

- [ ] **Step 1: Run all combat redesign self-tests**

Run `DiceOrbit.EditorTools.CombatRedesignSelfTests.RunAll` through Unity and require a `true` result with zero self-test errors.

- [ ] **Step 2: Compile the C# project**

Run `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` and require exit code 0.

- [ ] **Step 3: Inspect the final diff**

Run `git diff --check` and inspect `git diff -- Assets/Scripts/Core/Tutorial Assets/Resources/TutorialScenario.prefab Assets/Scripts/Editor/CombatRedesignSelfTests.cs Docs/superpowers`. Confirm no unrelated files changed and every design requirement maps to an implementation or verification step.
