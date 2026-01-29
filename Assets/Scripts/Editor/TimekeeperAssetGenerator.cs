using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using DiceOrbit.Data;
using DiceOrbit.Data.Skills;
using DiceOrbit.Data.Skills.Modules;
using DiceOrbit.Data.Skills.Modules.Timekeeper;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Passives.Timekeeper;

public class TimekeeperAssetGenerator : EditorWindow
{
    [MenuItem("Tools/Dice Orbit/Generate Timekeeper Assets")]
    public static void ShowWindow()
    {
        GetWindow<TimekeeperAssetGenerator>("Timekeeper Gen");
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Generate Timekeeper Class"))
        {
            Generate();
        }
    }

    private static void Generate()
    {
        string basePath = "Assets/Resources/Data/Characters/Timekeeper";
        if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);

        // 1. Create Passives
        var chronos = CreateAsset<ChronosPassive>(basePath + "/Chronos.asset");
        chronos.PassiveName = "Chronos";
        chronos.Description = "Start turn with an extra 6-Dice";
        chronos.BonusDiceValue = 6;

        // 2. Create Modules
        // Time Rewind: Reroll All
        var rerollModule = CreateAsset<DiceManipulationModule>(basePath + "/RerollModule.asset");
        rerollModule.ManipulationType = DiceManipulationType.RerollAll;

        // Future Sight: Add 6 Dice
        var addDiceModule = CreateAsset<DiceManipulationModule>(basePath + "/AddDiceModule.asset");
        addDiceModule.ManipulationType = DiceManipulationType.AddDice;
        addDiceModule.AddCount = 1;
        addDiceModule.MinValue = 6;
        addDiceModule.MaxValue = 6;

        // 3. Create Skills
        // Time Rewind
        var rewind = CreateAsset<CharacterSkill>(basePath + "/TimeRewind.asset");
        rewind.SkillName = "Time Rewind";
        rewind.Type = CharacterSkillType.Active;
        rewind.TargetType = SkillTargetType.Self; // No target needed essentially
        rewind.Levels = new List<SkillLevelData>();
        
        var rewindLevel = new SkillLevelData();
        rewindLevel.Description = "[Any] Reroll all dice";
        rewindLevel.Requirement = new DiceRequirement { Pattern = DicePattern.None };
        rewindLevel.ActionModules = new List<SkillActionModule> { rerollModule };
        rewind.Levels.Add(rewindLevel);

        // Future Sight
        var future = CreateAsset<CharacterSkill>(basePath + "/FutureSight.asset");
        future.SkillName = "Future Sight";
        future.Type = CharacterSkillType.Active;
        future.TargetType = SkillTargetType.Self;
        future.Levels = new List<SkillLevelData>();
        
        var futureLevel = new SkillLevelData();
        futureLevel.Description = "[Any] Obtain a 6-Dice";
        futureLevel.Requirement = new DiceRequirement { Pattern = DicePattern.None };
        futureLevel.ActionModules = new List<SkillActionModule> { addDiceModule };
        future.Levels.Add(futureLevel);

        // 3.5 Create Basic Attack (Universal)
        var basicAttackModule = CreateAsset<DealDamageModule>(basePath + "/BasicAttackModule.asset");
        basicAttackModule.AttackMultiplier = 1.0f;
        
        var basicAttack = CreateAsset<CharacterSkill>(basePath + "/BasicAttack.asset");
        basicAttack.SkillName = "Basic Attack";
        basicAttack.Type = CharacterSkillType.Active;
        basicAttack.TargetType = SkillTargetType.SingleEnemy;
        basicAttack.Levels = new List<SkillLevelData>();
        
        var basicLevel = new SkillLevelData();
        basicLevel.Description = "Deal 100% Atk Damage";
        basicLevel.Requirement = new DiceRequirement { Pattern = DicePattern.None };
        basicLevel.ActionModules = new List<SkillActionModule> { basicAttackModule };
        basicAttack.Levels.Add(basicLevel);

        // 4. Create Character Preset
        var timekeeper = CreateAsset<CharacterPreset>(basePath + "/Timekeeper.asset");
        timekeeper.CharacterName = "Timekeeper";
        timekeeper.MaxHP = 500;
        timekeeper.Attack = 3; 
        timekeeper.Defense = 0;
        
        timekeeper.BasicAttack = basicAttack;
        timekeeper.ActiveSkill = rewind;
        timekeeper.PassiveSkill = chronos;
        
        // timekeeper.AvailableSkills = new List<CharacterSkill> { rewind, future };
        // timekeeper.NativePassives = new List<PassiveAbility> { chronos };
        
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = timekeeper;
        
        Debug.Log("Timekeeper Assets Generated!");
    }

    private static T CreateAsset<T>(string path) where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
