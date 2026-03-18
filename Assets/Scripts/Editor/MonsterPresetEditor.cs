using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Data.MonsterAI;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Skills;

[CustomEditor(typeof(MonsterPreset))]
public class MonsterPresetEditor : Editor
{
    private SerializedProperty startingPassivesProp;
    private SerializedProperty aiPatternProp;
    private SerializedProperty onDeathEffectsProp;

    private void OnEnable()
    {
        startingPassivesProp = serializedObject.FindProperty("StartingPassives");
        aiPatternProp = serializedObject.FindProperty("AIPattern");
        onDeathEffectsProp = serializedObject.FindProperty("OnDeathEffects");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(serializedObject, "StartingPassives", "AIPattern", "OnDeathEffects");

        EditorGUILayout.Space();
        DrawAIPatternSection();
        EditorGUILayout.Space();
        
        DrawStartingPassivesSection();
        EditorGUILayout.Space();

        DrawDeathEffectsSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawStartingPassivesSection()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Starting Passives", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+", GUILayout.Width(30)))
        {
            startingPassivesProp.arraySize++;
        }
        EditorGUILayout.EndHorizontal();

        int newSize = EditorGUILayout.IntField("Size", startingPassivesProp.arraySize);
        if (newSize != startingPassivesProp.arraySize)
        {
            startingPassivesProp.arraySize = newSize;
        }

        EditorGUI.indentLevel++;
        for (int i = 0; i < startingPassivesProp.arraySize; i++)
        {
            var element = startingPassivesProp.GetArrayElementAtIndex(i);
            DrawItemElement(element, i, startingPassivesProp, "Passive", typeof(PassiveAbility));
        }
        EditorGUI.indentLevel--;
    }

    private void DrawAIPatternSection()
    {
        EditorGUILayout.LabelField("AI Pattern", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.BeginHorizontal();

        var currentTypeName = aiPatternProp.managedReferenceFullTypename;
        var displayName = string.IsNullOrEmpty(currentTypeName) 
            ? "None (Select AI Type)" 
            : currentTypeName.Split(".").Last();

        EditorGUILayout.LabelField("Type:", displayName, EditorStyles.boldLabel);

        if (GUILayout.Button("Select AI Type", GUILayout.Width(120)))
        {
            ShowTypeMenu(aiPatternProp, typeof(MonsterAI), false);
        }

        if (GUILayout.Button("Clear", GUILayout.Width(60)))
        {
            aiPatternProp.managedReferenceValue = null;
        }
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(currentTypeName))
        {
            EditorGUILayout.Space(5);
            EditorGUI.indentLevel++;

            var skillsListProp = aiPatternProp.FindPropertyRelative("availableSkills");
            if (skillsListProp != null)
            {
                DrawSkillsList(skillsListProp);
            }
            else
            {
                DrawPropertyFields(aiPatternProp);
            }
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawSkillsList(SerializedProperty skillsListProp)
    {
        EditorGUILayout.LabelField("Available Skills", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        int newSize = EditorGUILayout.IntField("Size", skillsListProp.arraySize);
        if (newSize != skillsListProp.arraySize)
        {
            skillsListProp.arraySize = newSize;
        }
        if (GUILayout.Button("+", GUILayout.Width(30)))
        {
            skillsListProp.arraySize++;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.indentLevel++;
        for (int i = 0; i < skillsListProp.arraySize; i++)
        {
            var skillProp = skillsListProp.GetArrayElementAtIndex(i);
            DrawMonsterSkillElement(skillProp, i, skillsListProp);
        }
        EditorGUI.indentLevel--;
    }

    private void DrawMonsterSkillElement(SerializedProperty skillProp, int index, SerializedProperty listProp)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Skill {index}", EditorStyles.boldLabel, GUILayout.Width(60));

        if (GUILayout.Button("X", GUILayout.Width(25)))
        {
            listProp.DeleteArrayElementAtIndex(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.indentLevel++;
        var skillDataProp = skillProp.FindPropertyRelative("skillData");
        if (skillDataProp != null)
        {
            var currentTypeName = skillDataProp.managedReferenceFullTypename;
            var displayName = string.IsNullOrEmpty(currentTypeName) ? "None" : currentTypeName.Split(".").Last();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Skill Data:", EditorStyles.boldLabel, GUILayout.Width(80));
            
            if (GUILayout.Button(string.IsNullOrEmpty(currentTypeName) ? "Select Skill ¡å" : $"{displayName} ¡å", EditorStyles.popup))
            {
                ShowTypeMenu(skillDataProp, typeof(DiceOrbit.Data.SkillData), true);
            }

            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                skillDataProp.managedReferenceValue = null;
                skillDataProp.serializedObject.ApplyModifiedProperties();
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(currentTypeName))
            {
                EditorGUILayout.Space(2);
                DrawPropertyFields(skillDataProp);
            }
        }

        EditorGUILayout.Space(3);
        var child = skillProp.Copy();
        var endProperty = skillProp.GetEndProperty();
        child.NextVisible(true); 

        while (!SerializedProperty.EqualContents(child, endProperty))
        {
            if (child.name != "skillData") 
            {
                EditorGUILayout.PropertyField(child, true);
            }
            if (!child.NextVisible(false)) break;
        }

        var targetCountProp = skillProp.FindPropertyRelative("targetCount");
        if (targetCountProp != null && targetCountProp.intValue == 0)
        {
            targetCountProp.intValue = 1;
        }

        EditorGUI.indentLevel--;
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    private void DrawDeathEffectsSection()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Death Effects", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+", GUILayout.Width(30)))
        {
            onDeathEffectsProp.arraySize++;
        }
        EditorGUILayout.EndHorizontal();

        int newSize = EditorGUILayout.IntField("Size", onDeathEffectsProp.arraySize);
        if (newSize != onDeathEffectsProp.arraySize)
        {
            onDeathEffectsProp.arraySize = newSize;
        }

        EditorGUI.indentLevel++;
        for (int i = 0; i < onDeathEffectsProp.arraySize; i++)
        {
            var element = onDeathEffectsProp.GetArrayElementAtIndex(i);
            DrawItemElement(element, i, onDeathEffectsProp, "Effect", typeof(DeathEffect));
        }
        EditorGUI.indentLevel--;
    }

    private void DrawItemElement(SerializedProperty property, int index, SerializedProperty listProp, string label, Type baseType)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"{label} {index}", EditorStyles.boldLabel, GUILayout.Width(80));

        var currentTypeName = property.managedReferenceFullTypename;
        var displayName = string.IsNullOrEmpty(currentTypeName) ? "(Not Assigned)" : currentTypeName.Split(".").Last();

        if (GUILayout.Button(string.IsNullOrEmpty(currentTypeName) ? $"Select {label} ¡å" : $"{displayName} ¡å", EditorStyles.popup))
        {
            ShowTypeMenu(property, baseType, true);
        }

        if (GUILayout.Button("X", GUILayout.Width(25)))
        {
            listProp.DeleteArrayElementAtIndex(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(currentTypeName))
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Properties:", EditorStyles.miniLabel);
            EditorGUI.indentLevel++;
            DrawPropertyFields(property);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    private void DrawPropertyFields(SerializedProperty property)
    {
        var child = property.Copy();
        var endProperty = property.GetEndProperty();
        child.NextVisible(true); 
        
        while (!SerializedProperty.EqualContents(child, endProperty))
        {
            EditorGUILayout.PropertyField(child, true);
            if (!child.NextVisible(false)) break;
        }
    }

    private void ShowTypeMenu(SerializedProperty property, Type baseType, bool useNamespaceFolders)
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("None"), false, () => {
            property.managedReferenceValue = null;
            property.serializedObject.ApplyModifiedProperties();
        });
        menu.AddSeparator("");

        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
            .Where(t => !t.IsAbstract && t.IsSubclassOf(baseType))
            .OrderBy(t => t.FullName);

        foreach (var type in types)
        {
            string menuPath = type.Name;
            
            if (useNamespaceFolders)
            {
                if (!string.IsNullOrEmpty(type.Namespace))
                {
                    string ns = type.Namespace;
                    if (ns.Contains("DiceOrbit.Data.MonsterPresets"))
                    {
                        var relativePath = ns.Substring(ns.IndexOf("MonsterPresets") + "MonsterPresets.".Length);
                        menuPath = $"{relativePath.Replace(".", "/")}/{type.Name}";
                    }
                    else if (ns.Contains("DiceOrbit"))
                    {
                        menuPath = $"Common/{type.Name}";
                    }
                    else
                    {
                        menuPath = $"Other/{type.Name}";
                    }
                }
                else
                {
                    menuPath = $"Global/{type.Name}";
                }
            }

            var isSelected = property.managedReferenceFullTypename == $"{type.Assembly.GetName().Name} {type.FullName}";
            menu.AddItem(new GUIContent(menuPath), isSelected, () => {
                property.managedReferenceValue = Activator.CreateInstance(type);
                property.serializedObject.ApplyModifiedProperties();
            });
        }

        menu.ShowAsContext();
    }
}
