using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using DiceOrbit.Data;
using DiceOrbit.Data.Skills;
using System.IO;

public class SkillAssetCreator
{
    [MenuItem("Tools/Dice Orbit/Generate Default Skills")]
    public static void GenerateSkills()
    {
        string path = "Assets/Resources/Skills/Generated";
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        CreateBasicAttack(path);
        CreateStrongBash(path);
        CreateHeal(path);
        CreateStrengthPassive(path);
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("Default Skills Generated at " + path);
    }

    private static void CreateBasicAttack(string path)
    {
        var skill = ScriptableObject.CreateInstance<CharacterSkill>();
        skill.SkillName = "Basic Attack";
        skill.Type = CharacterSkillType.Active;
        skill.TargetType = SkillTargetType.SingleEnemy;
        skill.Description = "Deals damage based on Dice Value.";
        
        // Level 1
        skill.Levels.Add(new SkillLevelData
        {
            Level = 1,
            Description = "100% Damage",
            DamageMultiplier = 1,
            BonusDamage = 0,
            Requirement = new DiceRequirement()
        });
        
        // Level 2
        skill.Levels.Add(new SkillLevelData
        {
            Level = 2,
            Description = "100% Damage + 2 Bonus",
            DamageMultiplier = 1,
            BonusDamage = 2,
            Requirement = new DiceRequirement()
        });

        CreateAsset(skill, path, "BasicAttack.asset");
    }

    private static void CreateStrongBash(string path)
    {
        var skill = ScriptableObject.CreateInstance<CharacterSkill>();
        skill.SkillName = "Strong Bash";
        skill.Type = CharacterSkillType.Active;
        skill.TargetType = SkillTargetType.SingleEnemy;
        skill.Description = "A heavy strike that deals massive damage.";

        skill.Levels.Add(new SkillLevelData
        {
            Level = 1,
            Description = "Hit with 150% Attack Power",
            DamageMultiplier = 1,
            BonusDamage = 5,
            Requirement = new DiceRequirement()
        });

        skill.Levels.Add(new SkillLevelData
        {
            Level = 2,
            Description = "Hit with 200% Attack Power",
            DamageMultiplier = 2,
            BonusDamage = 5,
            Requirement = new DiceRequirement()
        });

        CreateAsset(skill, path, "StrongBash.asset");
    }

    private static void CreateHeal(string path)
    {
        var skill = ScriptableObject.CreateInstance<CharacterSkill>();
        skill.SkillName = "Lesser Heal";
        skill.Type = CharacterSkillType.Active;
        skill.TargetType = SkillTargetType.Self;
        skill.Description = "Restores HP.";

        var effectsLv1 = new List<EffectData>();
        effectsLv1.Add(new EffectData(EffectType.Heal, 5));

        skill.Levels.Add(new SkillLevelData
        {
            Level = 1,
            Description = "Heal 5 HP",
            Effects = effectsLv1,
            BonusDamage = 0,
            Requirement = new DiceRequirement()
        });
        
        var effectsLv2 = new List<EffectData>();
        effectsLv2.Add(new EffectData(EffectType.Heal, 10));
        
        skill.Levels.Add(new SkillLevelData
        {
            Level = 2,
            Description = "Heal 10 HP",
            Effects = effectsLv2,
            BonusDamage = 0,
            Requirement = new DiceRequirement()
        });

        CreateAsset(skill, path, "LesserHeal.asset");
    }

    private static void CreateStrengthPassive(string path)
    {
        var skill = ScriptableObject.CreateInstance<CharacterSkill>();
        skill.SkillName = "Strength Training";
        skill.Type = CharacterSkillType.Passive;
        skill.TargetType = SkillTargetType.Self;
        skill.Description = "Permanently increases Attack.";

        var effects = new List<EffectData>();
        effects.Add(new EffectData(EffectType.BuffAttack, 2, 99));

        skill.Levels.Add(new SkillLevelData
        {
            Level = 1,
            Description = "+2 Attack",
            Effects = effects,
            Requirement = new DiceRequirement()
        });

        CreateAsset(skill, path, "StrengthPassive.asset");
    }

    private static void CreateAsset(ScriptableObject asset, string path, string fileName)
    {
        string fullPath = path + "/" + fileName;
        AssetDatabase.CreateAsset(asset, fullPath);
    }
}
