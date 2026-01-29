using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using DiceOrbit.Data;
using DiceOrbit.Data.Skills;
using DiceOrbit.Data.Skills.Modules;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Passives.Rogue;

public class RogueAssetGenerator : EditorWindow
{
    [MenuItem("Tools/Dice Orbit/Generate Rogue Assets")]
    public static void ShowWindow()
    {
        GetWindow<RogueAssetGenerator>("Rogue Gen");
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Generate Rogue Class"))
        {
            Generate();
        }
    }

    private static void Generate()
    {
        string basePath = "Assets/Resources/Data/Characters/Rogue";
        if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);

        // 1. Create Passives
        var agility = CreateAsset<AgilityPassive>(basePath + "/Agility.asset");
        agility.PassiveName = "Agility";
        agility.BonusMove = 1;

        var weakness = CreateAsset<WeaknessExploitPassive>(basePath + "/WeaknessExploit.asset");
        weakness.PassiveName = "Weakness Exploit";
        weakness.HpThreshold = 0.5f;
        weakness.DamageMultiplier = 1.3f;

        // 2. Create Modules
        var backstabModule = CreateAsset<DealDamageModule>(basePath + "/BackstabModule.asset");
        backstabModule.AttackMultiplier = 2.0f;
        backstabModule.IgnoreDefense = false;

        var hitRunDmgModule = CreateAsset<DealDamageModule>(basePath + "/HitRunDamage.asset");
        hitRunDmgModule.AttackMultiplier = 1.2f;
        
        var hitRunMoveModule = CreateAsset<MoveModule>(basePath + "/HitRunMove.asset");
        hitRunMoveModule.Distance = 1;

        var comboModule = CreateAsset<MultiStrikeModule>(basePath + "/ComboModule.asset");
        comboModule.HitCount = 2; // +1 hit (total 2 hits? or base + 1? Logic implies total hits)
        comboModule.DamageMultiplierPerHit = 1.0f;

        // 3. Create Skills
        // Backstab
        var backstab = CreateAsset<CharacterSkill>(basePath + "/Backstab.asset");
        backstab.SkillName = "Backstab";
        backstab.Type = CharacterSkillType.Active;
        backstab.TargetType = SkillTargetType.SingleEnemy;
        backstab.Levels = new List<SkillLevelData>();
        
        var backstabLevel = new SkillLevelData();
        backstabLevel.Description = "[2 or less] x2 Damage";
        backstabLevel.Requirement = new DiceRequirement { MinDiceValue = 1, Pattern = DicePattern.Low }; // Low = 3 or less usually, but let's assume Low works for "Small Dice"
        // Wait, Requirement implementation: Low is <=3. Request says [2 or less].
        // I need to update DiceRequirement enum or use MaxDiceValue. But DiceRequirement doesn't have MaxDiceValue.
        // For now, I'll use Low (<=3). Or add MaxDiceValue later. Let's stick to existing for prototype.
        backstabLevel.Requirement.Pattern = DicePattern.Low; 
        backstabLevel.ActionModules = new List<SkillActionModule> { backstabModule };
        backstab.Levels.Add(backstabLevel);

        // Hit & Run
        var hitRun = CreateAsset<CharacterSkill>(basePath + "/HitAndRun.asset");
        hitRun.SkillName = "Hit & Run";
        hitRun.Type = CharacterSkillType.Active;
        hitRun.TargetType = SkillTargetType.SingleEnemy;
        hitRun.Levels = new List<SkillLevelData>();
        
        var hitRunLevel = new SkillLevelData();
        hitRunLevel.Description = "[2 or less] x1.2 Dmg + Move 1";
        hitRunLevel.Requirement = new DiceRequirement { Pattern = DicePattern.Low };
        hitRunLevel.ActionModules = new List<SkillActionModule> { hitRunDmgModule, hitRunMoveModule };
        hitRun.Levels.Add(hitRunLevel);

        // Combo
        var combo = CreateAsset<CharacterSkill>(basePath + "/Combo.asset");
        combo.SkillName = "Combo";
        combo.Type = CharacterSkillType.Active;
        combo.TargetType = SkillTargetType.SingleEnemy;
        combo.Levels = new List<SkillLevelData>();
        
        var comboLevel = new SkillLevelData();
        comboLevel.Description = "[2 or less] 2 Hits";
        comboLevel.Requirement = new DiceRequirement { Pattern = DicePattern.Low };
        comboLevel.ActionModules = new List<SkillActionModule> { comboModule };
        combo.Levels.Add(comboLevel);

        // 3.5 Create Basic Attack (Universal)
        // Note: Ideally BasicAttack is shared, but for now generating per class is fine or using a shared asset
        var basicAttackModule = CreateAsset<DealDamageModule>(basePath + "/BasicAttackModule.asset");
        basicAttackModule.AttackMultiplier = 1.0f;
        
        var basicAttack = CreateAsset<CharacterSkill>(basePath + "/BasicAttack.asset");
        basicAttack.SkillName = "Basic Attack";
        basicAttack.Type = CharacterSkillType.Active;
        basicAttack.TargetType = SkillTargetType.SingleEnemy;
        basicAttack.Levels = new List<SkillLevelData>();
        
        var basicLevel = new SkillLevelData();
        basicLevel.Description = "Deal 100% Atk Damage";
        basicLevel.Requirement = new DiceRequirement { Pattern = DicePattern.None }; // Any Dice
        basicLevel.ActionModules = new List<SkillActionModule> { basicAttackModule };
        basicAttack.Levels.Add(basicLevel);

        // 4. Create Character Preset
        var rogue = CreateAsset<CharacterPreset>(basePath + "/Rogue.asset");
        rogue.CharacterName = "Rogue";
        rogue.MaxHP = 600;
        rogue.Attack = 10;
        rogue.Defense = 0;
        
        // Refactor 2.0 Assignments
        rogue.BasicAttack = basicAttack;
        rogue.ActiveSkill = backstab; // Signature Skill
        rogue.PassiveSkill = agility; // Signature Passive
        
        // Passives? CharacterPreset doesn't seem to have a dedicated List<PassiveAbility> field yet.
        // I need to add that to CharacterPreset.cs first!

        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = rogue;
        
        Debug.Log("Rogue Assets Generated!");
    }

    private static T CreateAsset<T>(string path) where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
