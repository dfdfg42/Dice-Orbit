using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using DiceOrbit.Data;
using DiceOrbit.Data.Skills;
using DiceOrbit.Data.Skills.Modules;
using DiceOrbit.Data.Skills.Modules.Dragoon; // Using AoEStatusModule
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Passives.Meteorologist;

public class MeteorologistAssetGenerator : EditorWindow
{
    [MenuItem("Tools/Dice Orbit/Generate Meteorologist Assets")]
    public static void ShowWindow()
    {
        GetWindow<MeteorologistAssetGenerator>("Meteor Gen");
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Generate Meteorologist Class"))
        {
            Generate();
        }
    }

    private static void Generate()
    {
        string basePath = "Assets/Resources/Data/Characters/Meteorologist";
        if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);

        // 1. Create Passives
        var tailwind = CreateAsset<TailwindPassive>(basePath + "/Tailwind.asset");
        tailwind.PassiveName = "Tailwind";
        tailwind.DistanceThreshold = 5;
        tailwind.AttackBuffValue = 2;

        // 2. Create Modules
        // Fog: AoE Evasion Up
        var fogModule = CreateAsset<AoEStatusModule>(basePath + "/FogModule.asset");
        fogModule.EffectType = StatusEffectType.EvasionUp;
        fogModule.Value = 15; // +15%
        fogModule.Duration = 2;
        fogModule.IncludeSelf = true;

        // Harmony: Heal All (Simple implementation for now) -> Use AoEStatus with Regeneration? 
        // Or create AoEHealModule? Let's used ApplyStatus -> Regeneration for now.
        var harmonyModule = CreateAsset<AoEStatusModule>(basePath + "/HarmonyModule.asset");
        harmonyModule.EffectType = StatusEffectType.Regeneration;
        harmonyModule.Value = 5; // 5 HP per turn
        harmonyModule.Duration = 3;

        // 3. Create Skills
        // Fog
        var fog = CreateAsset<CharacterSkill>(basePath + "/Fog.asset");
        fog.SkillName = "Fog";
        fog.Type = CharacterSkillType.Active;
        fog.TargetType = SkillTargetType.AllAllies;
        fog.Levels = new List<SkillLevelData>();
        
        var fogLevel = new SkillLevelData();
        fogLevel.Description = "[2] All Allies Evasion +15% (2 turns)";
        fogLevel.Requirement = new DiceRequirement { ExactDiceValue = 2 };
        fogLevel.ActionModules = new List<SkillActionModule> { fogModule };
        fog.Levels.Add(fogLevel);

        // Harmony
        var harmony = CreateAsset<CharacterSkill>(basePath + "/Harmony.asset");
        harmony.SkillName = "Harmony";
        harmony.Type = CharacterSkillType.Active;
        harmony.TargetType = SkillTargetType.AllAllies;
        harmony.Levels = new List<SkillLevelData>();
        
        var harmonyLevel = new SkillLevelData();
        harmonyLevel.Description = "[1] Regeneration +5 (3 turns)";
        harmonyLevel.Requirement = new DiceRequirement { ExactDiceValue = 1 };
        harmonyLevel.ActionModules = new List<SkillActionModule> { harmonyModule };
        harmony.Levels.Add(harmonyLevel);

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
        var meteor = CreateAsset<CharacterPreset>(basePath + "/Meteorologist.asset");
        meteor.CharacterName = "Meteorologist";
        meteor.MaxHP = 400; // Low HP
        meteor.Attack = 5;
        meteor.Defense = 0;
        
        meteor.BasicAttack = basicAttack;
        meteor.ActiveSkill = fog; 
        meteor.PassiveSkill = tailwind;
        
        // meteor.AvailableSkills = new List<CharacterSkill> { fog, harmony };
        // meteor.NativePassives = new List<PassiveAbility> { tailwind };
        
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = meteor;
        
        Debug.Log("Meteorologist Assets Generated!");
    }

    private static T CreateAsset<T>(string path) where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
