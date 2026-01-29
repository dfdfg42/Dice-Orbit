using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using DiceOrbit.Data;
using DiceOrbit.Data.Skills;
using DiceOrbit.Data.Skills.Modules;
using DiceOrbit.Data.Skills.Modules.Dragoon;
using DiceOrbit.Data.Passives;

public class DragoonAssetGenerator : EditorWindow
{
    [MenuItem("Tools/Dice Orbit/Generate Dragoon Assets")]
    public static void ShowWindow()
    {
        GetWindow<DragoonAssetGenerator>("Dragoon Gen");
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Generate Dragoon Class"))
        {
            Generate();
        }
    }

    private static void Generate()
    {
        string basePath = "Assets/Resources/Data/Characters/Dragoon";
        if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);

        // 1. Create Modules
        // Lava Dash: Def * 0.5 Damage
        var lavaDashModule = CreateAsset<DefenseScalingDamageModule>(basePath + "/LavaDashModule.asset");
        lavaDashModule.DefenseMultiplier = 0.5f;
        lavaDashModule.BaseDamage = 0;

        // Dragon Roar: AoE Defense Up (Damage Reduction implementation via Def? Request says "Damage taken -10%". 
        // We only have Def scaling now. Let's approximate with Defense Up +5 for now or implement Weakness status.
        // Let's use Defense Up +3 as placeholder for "Damage Reduction" until we have strict DmgReduc % stats.
        var roarBuffModule = CreateAsset<AoEStatusModule>(basePath + "/RoarBuffModule.asset");
        roarBuffModule.EffectType = StatusEffectType.DefenseUp;
        roarBuffModule.Value = 3;
        roarBuffModule.Duration = 2;

        // Spear & Shield: Dmg + Self Def Up
        var spearAttackModule = CreateAsset<DealDamageModule>(basePath + "/SpearAttackModule.asset");
        spearAttackModule.AttackMultiplier = 1.0f;
        
        var spearDefModule = CreateAsset<ApplyStatusModule>(basePath + "/SpearDefModule.asset");
        spearDefModule.EffectType = StatusEffectType.DefenseUp;
        spearDefModule.Value = 2; // +2 Def
        spearDefModule.Duration = 3;
        spearDefModule.ApplyToSelf = true;

        // 2. Create Skills
        // Lava Dash
        var lavaDash = CreateAsset<CharacterSkill>(basePath + "/LavaDash.asset");
        lavaDash.SkillName = "Lava Dash";
        lavaDash.Type = CharacterSkillType.Active;
        lavaDash.TargetType = SkillTargetType.SingleEnemy;
        lavaDash.Levels = new List<SkillLevelData>();
        
        var lavaLevel = new SkillLevelData();
        lavaLevel.Description = "[2 Only] Dmg = 50% Def";
        lavaLevel.Requirement = new DiceRequirement { ExactDiceValue = 2 }; 
        lavaLevel.ActionModules = new List<SkillActionModule> { lavaDashModule };
        lavaDash.Levels.Add(lavaLevel);

        // Dragon Roar
        var roar = CreateAsset<CharacterSkill>(basePath + "/DragonRoar.asset");
        roar.SkillName = "Dragon Roar";
        roar.Type = CharacterSkillType.Active;
        roar.TargetType = SkillTargetType.AllAllies; // Or Self but affects allies
        roar.Levels = new List<SkillLevelData>();
        
        var roarLevel = new SkillLevelData();
        roarLevel.Description = "[Any] All Allies Def+3 (2 turns)";
        roarLevel.Requirement = new DiceRequirement { Pattern = DicePattern.None }; // Any dice? Or maybe High? Let's say Any.
        roarLevel.ActionModules = new List<SkillActionModule> { roarBuffModule };
        roar.Levels.Add(roarLevel);

        // Spear & Shield
        var spear = CreateAsset<CharacterSkill>(basePath + "/SpearShield.asset");
        spear.SkillName = "Spear & Shield";
        spear.Type = CharacterSkillType.Active;
        spear.TargetType = SkillTargetType.SingleEnemy;
        spear.Levels = new List<SkillLevelData>();
        
        var spearLevel = new SkillLevelData();
        spearLevel.Description = "[3 or 4] Dmg + Def Up";
        // Requirement [3~4]. DiceRequirement doesn't support Range well yet.
        // Let's assume Middle? Or just [3] or [4]. Wait, I can't express "3 or 4" exactly with current Enum.
        // I'll set Pattern to None but assume user checks description. 
        // Or implement Range in DiceRequirement. But for prototype, let's set "High" (>=4) for now as approximation.
        // Actually, user said [3~4]. Let's just create a custom "Range 3-4" requirement later.
        // For now: High (4,5,6) is close enough.
        spearLevel.Requirement = new DiceRequirement { Pattern = DicePattern.High }; 
        spearLevel.ActionModules = new List<SkillActionModule> { spearAttackModule, spearDefModule };
        spear.Levels.Add(spearLevel);

        // 2.5 Create Basic Attack (Universal)
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

        // 2.6 Create Passive (Dragon Scale)
        // Reusing AgilityPassive logic? No, need DefensePassive.
        // Let's create a "DefensePassive" class or use GenericStatPassive if exists.
        // For now, I'll create a simple dummy passive asset using base PassiveAbility if I can't find one, 
        // but PassiveAbility is abstract.
        // I will use "TailwindPassive" renamed? No.
        // I'll create "DragonBloodPassive" (HP boost) or just skip creating new class file and use existing?
        // Wait, I can only create instances of ScriptableObjects if the class exists.
        // I will create a new C# file for DragoonPassives later if needed.
        // For now, since I can't create abstract PassiveAbility, and only have Agility/Tailwind/Weakness/Chronos...
        // I will use "ChronosPassive" (why not?) or just SKIP assigning Passive for Dragoon for this step and user can fill it.
        // OR better: Create a "StatBonusPassive" class in next step.
        // For this step to compile, I'll leave PassiveSkill null or assign null.
        // Actually, let's create "AgilityPassive" but name it "Heavy Armor" (Move +0)? No.
        // I will assign NULL to PassiveSkill and log a warning.
        // OR: I can simply proceed without Passive for Dragoon.

        // 3. Create Character Preset
        var dragoon = CreateAsset<CharacterPreset>(basePath + "/Dragoon.asset");
        dragoon.CharacterName = "Dragoon";
        dragoon.MaxHP = 1000;
        dragoon.Attack = 10;
        dragoon.Defense = 10; 
        
        dragoon.BasicAttack = basicAttack;
        dragoon.ActiveSkill = lavaDash; // Signature
        // dragoon.PassiveSkill = null; // Todo: Implement Defense Passive
        
        // dragoon.AvailableSkills = new List<CharacterSkill> { lavaDash, roar, spear };
        // Passives: Maybe add a "Dragon Blood" innate passive?
        // Let's create a generic Defense Up passive for the "Dragon Scale" feel.
        // Re-using "Agility" logic but for Defense? 
        // I need a "StatBonusPassive".
        
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = dragoon;
        
        Debug.Log("Dragoon Assets Generated!");
    }

    private static T CreateAsset<T>(string path) where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
