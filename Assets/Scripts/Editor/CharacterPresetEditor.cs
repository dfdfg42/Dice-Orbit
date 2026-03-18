using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Data.Passives;

[CustomEditor(typeof(CharacterPreset))]
public class CharacterPresetEditor : Editor
{
    private SerializedProperty startingPassivesProp;

    private void OnEnable()
    {
        startingPassivesProp = serializedObject.FindProperty("StartingPassives");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(serializedObject, "StartingPassives");

        EditorGUILayout.Space();
        DrawStartingPassivesSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawStartingPassivesSection()
    {
        EditorGUILayout.LabelField("Starting Passives", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        int newSize = EditorGUILayout.IntField("Size", startingPassivesProp.arraySize);
        if (newSize != startingPassivesProp.arraySize)
        {
            startingPassivesProp.arraySize = Mathf.Max(0, newSize);
        }

        if (GUILayout.Button("+", GUILayout.Width(30)))
        {
            startingPassivesProp.arraySize++;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.indentLevel++;
        for (int i = 0; i < startingPassivesProp.arraySize; i++)
        {
            var element = startingPassivesProp.GetArrayElementAtIndex(i);
            DrawPassiveElement(element, i);
        }
        EditorGUI.indentLevel--;
    }

    private void DrawPassiveElement(SerializedProperty property, int index)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Passive {index}", EditorStyles.boldLabel, GUILayout.Width(80));

        var currentTypeName = property.managedReferenceFullTypename;
        var displayName = string.IsNullOrEmpty(currentTypeName)
            ? "(Not Assigned)"
            : currentTypeName.Split('.').Last();

        EditorGUILayout.LabelField(displayName, EditorStyles.label);
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("X", GUILayout.Width(25)))
        {
            startingPassivesProp.DeleteArrayElementAtIndex(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        EditorGUILayout.EndHorizontal();

        // 드래그 앤 드롭 영역
        Rect dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));

        // 드래그 중일 때 색상 변경
        Event evt = Event.current;
        bool isDragging = dropArea.Contains(evt.mousePosition) && 
                         (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform);

        Color originalColor = GUI.backgroundColor;
        if (isDragging)
        {
            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f); // 연한 초록색
        }

        GUI.Box(dropArea, "📄 Drag .cs script here to assign PassiveAbility\n(or use button below)", EditorStyles.helpBox);
        GUI.backgroundColor = originalColor;

        // 드래그 앤 드롭 처리
        if (dropArea.Contains(evt.mousePosition))
        {
            if (evt.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.Use();
            }
            else if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();

                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (obj is MonoScript monoScript)
                    {
                        var scriptType = monoScript.GetClass();
                        if (scriptType != null && 
                            !scriptType.IsAbstract && 
                            scriptType.IsSubclassOf(typeof(DiceOrbit.Data.Passives.PassiveAbility)))
                        {
                            property.managedReferenceValue = Activator.CreateInstance(scriptType);
                            property.serializedObject.ApplyModifiedProperties();
                            Debug.Log($"✓ PassiveAbility assigned: {scriptType.Name}");
                            break;
                        }
                        else
                        {
                            Debug.LogWarning($"⚠️ '{monoScript.name}' is not a valid PassiveAbility class!\n" +
                                           "Make sure it inherits from PassiveAbility and is not abstract.");
                        }
                    }
                }
                evt.Use();
            }
        }

        if (GUILayout.Button("Or Select from List (Dropdown)", GUILayout.Height(25)))
        {
            ShowPassiveTypeMenu(property);
        }

        if (!string.IsNullOrEmpty(currentTypeName))
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Passive Properties:", EditorStyles.miniLabel);
            DrawManagedReferenceChildren(property);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4);
    }

    private void DrawManagedReferenceChildren(SerializedProperty property)
    {
        EditorGUI.indentLevel++;
        var child = property.Copy();
        var end = property.GetEndProperty();
        bool enterChildren = true;
        while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
        {
            EditorGUILayout.PropertyField(child, true);
            enterChildren = false;
        }
        EditorGUI.indentLevel--;
    }

    private void ShowPassiveTypeMenu(SerializedProperty property)
    {
        var menu = new GenericMenu();

        menu.AddItem(new GUIContent("None"), false, () =>
        {
            property.managedReferenceValue = null;
            property.serializedObject.ApplyModifiedProperties();
        });
        menu.AddSeparator("");

        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return Type.EmptyTypes; }
            })
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(PassiveAbility)))
            .OrderBy(t => t.Name)
            .ToList();

        foreach (var type in types)
        {
            string fullTypeName = $"{type.Assembly.GetName().Name} {type.FullName}";
            bool selected = property.managedReferenceFullTypename == fullTypeName;
            menu.AddItem(new GUIContent(type.Name), selected, () =>
            {
                property.managedReferenceValue = Activator.CreateInstance(type);
                property.serializedObject.ApplyModifiedProperties();
            });
        }

        menu.ShowAsContext();
    }
}
