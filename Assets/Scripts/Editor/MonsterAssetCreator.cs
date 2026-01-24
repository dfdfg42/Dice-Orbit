using UnityEngine;
using UnityEditor;
using DiceOrbit.Data.MonsterAI;
using System.IO;

namespace DiceOrbit.EditorTools
{
    public class MonsterAssetCreator
    {
#if UNITY_EDITOR
        [MenuItem("Tools/Dice Orbit/Generate Default Monster Data")]
        public static void CreateDefaultData()
        {
            string basePath = "Assets/Resources/MonsterData";
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            // 1. Create Skills
            MonsterSkill basicAttack = CreateAsset<MonsterSkill>(basePath, "Skill_BasicAttack");
            basicAttack.SkillName = "Basic Attack";
            basicAttack.Type = Data.IntentType.Attack;
            basicAttack.TargetType = Data.TargetType.Single;
            basicAttack.BaseDamage = 5;
            basicAttack.DamageMultiplier = 1.0f;
            basicAttack.Description = "A simple scratch.";

            MonsterSkill heavySmash = CreateAsset<MonsterSkill>(basePath, "Skill_HeavySmash");
            heavySmash.SkillName = "Heavy Smash";
            heavySmash.Type = Data.IntentType.Attack;
            heavySmash.TargetType = Data.TargetType.Single;
            heavySmash.BaseDamage = 15;
            heavySmash.DamageMultiplier = 1.5f;
            heavySmash.Description = "A powerful heavy blow.";

            MonsterSkill fireball = CreateAsset<MonsterSkill>(basePath, "Skill_Fireball_Area");
            fireball.SkillName = "Fireball";
            fireball.Type = Data.IntentType.Attack;
            fireball.TargetType = Data.TargetType.Area;
            fireball.AreaRadius = 1; // Radius 1 = 3 tiles total
            fireball.BaseDamage = 8;
            fireball.DamageMultiplier = 0.8f;
            fireball.Description = "Explodes affecting adjacent tiles.";

            // 2. Create Pattern
            SequentialPattern pattern = CreateAsset<SequentialPattern>(basePath, "Pattern_BasicRotation");
            // Reflection or SerializedObject needed to set list if it's private, 
            // but we made it private with [SerializeField], so we can't access it directly without changing accessibility.
            // For now, let's assume the user will inspect it, or we change fields to public/internal.
            // Wait, for this script to work, the fields in SequentialPattern should be accessible.
            // Since I cannot easily change them now without another file edit, I will prompt the user to check them.
            // OR I can use SerializedObject to set them safely.
            
            SerializedObject so = new SerializedObject(pattern);
            SerializedProperty skillsProp = so.FindProperty("skills");
            skillsProp.ClearArray();
            skillsProp.InsertArrayElementAtIndex(0);
            skillsProp.GetArrayElementAtIndex(0).objectReferenceValue = basicAttack;
            skillsProp.InsertArrayElementAtIndex(1);
            skillsProp.GetArrayElementAtIndex(1).objectReferenceValue = basicAttack;
            skillsProp.InsertArrayElementAtIndex(2);
            skillsProp.GetArrayElementAtIndex(2).objectReferenceValue = fireball;
            skillsProp.InsertArrayElementAtIndex(3);
            skillsProp.GetArrayElementAtIndex(3).objectReferenceValue = heavySmash;
            so.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = pattern;
            
            Debug.Log("Created Default Monster Data in " + basePath);
        }

        private static T CreateAsset<T>(string path, string name) where T : ScriptableObject
        {
            string fullPath = $"{path}/{name}.asset";
            T asset = AssetDatabase.LoadAssetAtPath<T>(fullPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, fullPath);
            }
            return asset;
        }
#endif
    }
}
