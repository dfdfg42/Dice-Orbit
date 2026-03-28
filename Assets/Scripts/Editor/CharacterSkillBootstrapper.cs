using System.Collections.Generic;
using System.IO;
using DiceOrbit.Core;
using DiceOrbit.Data;
using DiceOrbit.Data.Skills;
using DiceOrbit.Data.Skills.Effects;
using UnityEditor;
using UnityEngine;

namespace DiceOrbit.EditorTools
{
    public static class CharacterSkillBootstrapper
    {
        private const string SkillRoot = "Assets/Resources/Skill";
        private const string EffectDir = SkillRoot + "/Effects";
        private const string CharacterSkillDir = SkillRoot + "/CharacterSkills";
        private const string PassiveDir = SkillRoot + "/Passives";
        private const string CharacterPresetDir = "Assets/Scripts/Data/Character Preset";

        [MenuItem("Dice Orbit/Skills/Bootstrap Character Skills")]
        public static void Bootstrap()
        {
            EnsureDirectory(SkillRoot);
            EnsureDirectory(EffectDir);
            EnsureDirectory(CharacterSkillDir);
            EnsureDirectory(PassiveDir);

            var warriorEffect = GetOrCreateDiceDamageEffect("Warrior_Damage_Eff", 12);
            var rogueEffect = GetOrCreateDiceDamageEffect("Rogue_Damage_Eff", 20);
            var alchemistEffect = GetOrCreateDiceDamageEffect("Alchemist_Damage_Eff", 12);
            var mageEffect = GetOrCreateMageDamageEffect("Mage_Damage_Eff", 12, 0.05f);

            var warriorSkill = GetOrCreateCharacterSkill(
                "Warrior_Active_Skill",
                "대검",
                "주사위 눈금 4 이상 시 (눈금 x 12) 피해",
                SkillTargetType.SingleEnemy,
                BuildRequirement(1, null, DicePattern.High),
                warriorEffect
            );

            var rogueSkill = GetOrCreateCharacterSkill(
                "Rogue_Active_Skill",
                "기습",
                "주사위 눈금 2 이하 시 (눈금 x 20) 피해",
                SkillTargetType.SingleEnemy,
                BuildRequirement(1, 2, DicePattern.None),
                rogueEffect
            );

            var alchemistSkill = GetOrCreateCharacterSkill(
                "Alchemist_Active_Skill",
                "시약 투척",
                "주사위 눈금 홀수 시 (눈금 x 12) 피해",
                SkillTargetType.SingleEnemy,
                BuildRequirement(1, null, DicePattern.Odd),
                alchemistEffect
            );

            var mageSkill = GetOrCreateCharacterSkill(
                "Mage_Active_Skill",
                "에너지 볼",
                "주사위 눈금 4 이상 시 (눈금 x 12) + 집중 스택당 5% 추가 피해",
                SkillTargetType.SingleEnemy,
                BuildRequirement(1, null, DicePattern.High),
                mageEffect
            );

            // 패시브 자동 생성은 비활성화합니다. 패시브는 CharacterSkill(Type=Passive) 에셋으로 직접 작성합니다.
            //var warriorPassive = GetOrCreateBattleCryPassive("Warrior_BattleCry_Passive", 1.05f);
            //var roguePassive = GetOrCreatePositioningPassive("Rogue_Positioning_Passive", 1.05f, 5);
            //var alchemistPassive = GetOrCreateStableReactionPassive("Alchemist_StableReaction_Passive", 1.10f, 0.6f);
            //var magePassive = GetOrCreateFocusPassive("Mage_Focus_Passive", 1);

            ApplyPreset("Warrior", warriorSkill, 600);
            ApplyPreset("Rogue", rogueSkill, 600);
            ApplyPreset("Alchemist", alchemistSkill, 600);
            ApplyPreset("Mage", mageSkill, 600);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CharacterSkillBootstrapper] Character skill bootstrap completed.");
        }

        private static DiceRequirement BuildRequirement(int min, int? max, DicePattern pattern)
        {
            return new DiceRequirement
            {
                MinDiceValue = min,
                MaxDiceValue = max,
                Pattern = pattern
            };
        }

        private static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            var folder = Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureDirectory(parent);
            }

            AssetDatabase.CreateFolder(parent, folder);
        }

        private static DiceMultiplierDamageEffect GetOrCreateDiceDamageEffect(string fileName, int multiplier)
        {
            var assetPath = $"{EffectDir}/{fileName}.asset";
            var effect = AssetDatabase.LoadAssetAtPath<DiceMultiplierDamageEffect>(assetPath);
            if (effect == null)
            {
                effect = ScriptableObject.CreateInstance<DiceMultiplierDamageEffect>();
                AssetDatabase.CreateAsset(effect, assetPath);
            }

            effect.multiplier = multiplier;
            EditorUtility.SetDirty(effect);
            return effect;
        }

        private static MageStackDamageEffect GetOrCreateMageDamageEffect(string fileName, int baseMultiplier, float bonusPerStack)
        {
            var assetPath = $"{EffectDir}/{fileName}.asset";
            var effect = AssetDatabase.LoadAssetAtPath<MageStackDamageEffect>(assetPath);
            if (effect == null)
            {
                effect = ScriptableObject.CreateInstance<MageStackDamageEffect>();
                AssetDatabase.CreateAsset(effect, assetPath);
            }

            effect.baseMultiplier = baseMultiplier;
            effect.bonusDamageRatioPerStack = bonusPerStack;
            EditorUtility.SetDirty(effect);
            return effect;
        }

        private static CharacterSkill GetOrCreateCharacterSkill(
            string fileName,
            string displayName,
            string description,
            SkillTargetType targetType,
            DiceRequirement requirement,
            SkillEffectBase effect)
        {
            var assetPath = $"{CharacterSkillDir}/{fileName}.asset";
            var skill = AssetDatabase.LoadAssetAtPath<CharacterSkill>(assetPath);
            if (skill == null)
            {
                skill = ScriptableObject.CreateInstance<CharacterSkill>();
                AssetDatabase.CreateAsset(skill, assetPath);
            }

            skill.SkillName = displayName;
            skill.Description = description;
            skill.Type = CharacterSkillType.Active;
            skill.TargetType = targetType;
            skill.Requirement = requirement;

            skill.BaseData.SetSkillName(displayName);
            skill.BaseData.SetDescription(description);
            skill.BaseData.skillTargetType = targetType;
            skill.BaseData.Effects = new List<SkillEffectBase> { effect };
            skill.Levels = new List<SkillLevelData>();

            EditorUtility.SetDirty(skill);
            return skill;
        }

        private static void AssignSkillToPreset(string presetName, CharacterSkill skill)
        {
            var preset = LoadPreset(presetName);
            if (preset == null)
            {
                return;
            }

            if (preset.StartingSkills == null)
            {
                preset.StartingSkills = new List<CharacterSkill>();
            }

            if (!preset.StartingSkills.Contains(skill))
            {
                preset.StartingSkills.Add(skill);
                EditorUtility.SetDirty(preset);
            }
        }

        private static void ApplyPreset(string presetName, CharacterSkill skill, int maxHp)
        {
            var preset = LoadPreset(presetName);
            if (preset == null) return;

            AssignSkillToPreset(presetName, skill);

            preset.MaxHP = maxHp;
            EditorUtility.SetDirty(preset);
        }

        private static CharacterPreset LoadPreset(string presetName)
        {
            var path = $"{CharacterPresetDir}/{presetName}.asset";
            var preset = AssetDatabase.LoadAssetAtPath<CharacterPreset>(path);
            if (preset == null)
            {
                Debug.LogWarning($"[CharacterSkillBootstrapper] CharacterPreset not found: {path}");
            }
            return preset;
        }

        //private static BattleCryPassive GetOrCreateBattleCryPassive(string fileName, float multiplier)
        //{
        //    var assetPath = $"{PassiveDir}/{fileName}.asset";
        //    var passive = AssetDatabase.LoadAssetAtPath<BattleCryPassive>(assetPath);
        //    if (passive == null)
        //    {
        //        passive = ScriptableObject.CreateInstance<BattleCryPassive>();
        //        AssetDatabase.CreateAsset(passive, assetPath);
        //    }

        //    passive.PassiveName = "전투의 함성";
        //    passive.DamageMultiplier = multiplier;
        //    EditorUtility.SetDirty(passive);
        //    return passive;
        //}

        //private static StableReactionPassive GetOrCreateStableReactionPassive(string fileName, float multiplier, float thresholdRatio)
        //{
        //    var assetPath = $"{PassiveDir}/{fileName}.asset";
        //    var passive = AssetDatabase.LoadAssetAtPath<StableReactionPassive>(assetPath);
        //    if (passive == null)
        //    {
        //        passive = ScriptableObject.CreateInstance<StableReactionPassive>();
        //        AssetDatabase.CreateAsset(passive, assetPath);
        //    }

        //    passive.PassiveName = "안정 반응";
        //    passive.DamageMultiplier = multiplier;
        //    passive.HealthThresholdRatio = thresholdRatio;
        //    EditorUtility.SetDirty(passive);
        //    return passive;
        //}

        //private static PositioningPassive GetOrCreatePositioningPassive(string fileName, float multiplier, int distanceThreshold)
        //{
        //    var assetPath = $"{PassiveDir}/{fileName}.asset";
        //    var passive = AssetDatabase.LoadAssetAtPath<PositioningPassive>(assetPath);
        //    if (passive == null)
        //    {
        //        passive = ScriptableObject.CreateInstance<PositioningPassive>();
        //        AssetDatabase.CreateAsset(passive, assetPath);
        //    }

        //    passive.PassiveName = "위치 선정";
        //    passive.DamageMultiplier = multiplier;
        //    passive.ThresholdDistance = distanceThreshold;
        //    EditorUtility.SetDirty(passive);
        //    return passive;
        //}

        //private static FocusPassive GetOrCreateFocusPassive(string fileName, int stacksPerTurn)
        //{
        //    var assetPath = $"{PassiveDir}/{fileName}.asset";
        //    var passive = AssetDatabase.LoadAssetAtPath<FocusPassive>(assetPath);
        //    if (passive == null)
        //    {
        //        passive = ScriptableObject.CreateInstance<FocusPassive>();
        //        AssetDatabase.CreateAsset(passive, assetPath);
        //    }

        //    passive.PassiveName = "정신 집중";
        //    passive.stacksPerTurn = stacksPerTurn;
        //    EditorUtility.SetDirty(passive);
        //    return passive;
        //}
    }
}
