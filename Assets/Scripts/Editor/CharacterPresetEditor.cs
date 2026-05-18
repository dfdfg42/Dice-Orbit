using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Data.Skills;
using DiceOrbit.Data.Passives;

[CustomEditor(typeof(CharacterPreset))]
public class CharacterPresetEditor : Editor
{
    private SerializedProperty startingActivesProp;
    private SerializedProperty startingPassivesProp;

    private void OnEnable()
    {
        startingActivesProp  = serializedObject.FindProperty("StartingActives");
        startingPassivesProp = serializedObject.FindProperty("StartingPassives");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(serializedObject, "StartingActives", "StartingPassives");

        EditorGUILayout.Space();
        DrawSkillListSection(startingActivesProp,  "Starting Actives",  typeof(CharacterActiveSkill));
        EditorGUILayout.Space();
        DrawSkillListSection(startingPassivesProp, "Starting Passives", typeof(CharacterPassiveSkill));

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSkillListSection(SerializedProperty listProp, string label, Type baseType)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+", GUILayout.Width(30)))
        {
            listProp.arraySize++;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.indentLevel++;
        for (int i = 0; i < listProp.arraySize; i++)
        {
            var element = listProp.GetArrayElementAtIndex(i);
            DrawElement(element, i, listProp, baseType);
        }
        EditorGUI.indentLevel--;
    }

    private void DrawElement(SerializedProperty property, int index, SerializedProperty listProp, Type baseType)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.BeginHorizontal();

        var currentTypeName = property.managedReferenceFullTypename;
        var displayName = string.IsNullOrEmpty(currentTypeName) ? "(Not Assigned)" : currentTypeName.Split('.').Last();

        if (GUILayout.Button(string.IsNullOrEmpty(currentTypeName) ? "Select Type ▼" : $"{displayName} ▼", EditorStyles.popup))
        {
            ShowTypeMenu(property, baseType);
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
            EditorGUILayout.Space(3);
            EditorGUI.indentLevel++;
            DrawPropertyFields(property);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(3);
    }

    private void DrawPropertyFields(SerializedProperty property)
    {
        var child = property.Copy();
        var end   = property.GetEndProperty();
        child.NextVisible(true);

        while (!SerializedProperty.EqualContents(child, end))
        {
            EditorGUILayout.PropertyField(child, true);
            if (!child.NextVisible(false)) break;
        }
    }

    private void ShowTypeMenu(SerializedProperty property, Type baseType)
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("None"), false, () =>
        {
            property.managedReferenceValue = null;
            property.serializedObject.ApplyModifiedProperties();
        });
        menu.AddSeparator("");

        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
            .Where(t => !t.IsAbstract && t.IsSubclassOf(baseType))
            .OrderBy(t => t.Name);

        foreach (var type in types)
        {
            var captured    = type;
            var isSelected  = property.managedReferenceFullTypename == $"{type.Assembly.GetName().Name} {type.FullName}";
            menu.AddItem(new GUIContent(type.Name), isSelected, () =>
            {
                property.managedReferenceValue = Activator.CreateInstance(captured);
                property.serializedObject.ApplyModifiedProperties();
            });
        }

        menu.ShowAsContext();
    }
}
