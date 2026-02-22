using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.MonsterAI;
using DiceOrbit.Data.MonsterAI.Patterns;

[CustomEditor(typeof(MonsterPreset))]
public class MonsterPresetEditor : Editor
{
    private SerializedProperty startingPassivesProp;
    private SerializedProperty aiPatternProp;

    private void OnEnable()
    {
        startingPassivesProp = serializedObject.FindProperty("StartingPassives");
        aiPatternProp = serializedObject.FindProperty("AIPattern");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 기본 필드들 그리기 (StartingPassives와 AIPattern 제외)
        DrawPropertiesExcluding(serializedObject, "StartingPassives", "AIPattern");

        EditorGUILayout.Space();

        // AI Pattern 섹션
        DrawAIPatternSection();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Starting Passives", EditorStyles.boldLabel);
        
        // 리스트 크기 조절
        int newSize = EditorGUILayout.IntField("Size", startingPassivesProp.arraySize);
        if (newSize != startingPassivesProp.arraySize)
        {
            startingPassivesProp.arraySize = newSize;
        }

        EditorGUI.indentLevel++;
        
        // 각 요소에 대해 타입 선택 UI 표시
        for (int i = 0; i < startingPassivesProp.arraySize; i++)
        {
            var element = startingPassivesProp.GetArrayElementAtIndex(i);
            DrawPassiveConfigElement(element, i);
        }
        
        EditorGUI.indentLevel--;

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawPassiveConfigElement(SerializedProperty property, int index)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Element {index}", EditorStyles.boldLabel);
        
        // 타입 선택 버튼
        var currentTypeName = property.managedReferenceFullTypename;
        var displayName = string.IsNullOrEmpty(currentTypeName) 
            ? "None (Select Type)" 
            : currentTypeName.Split('.').Last().Replace("PassiveConfig", "");
        
        if (GUILayout.Button(displayName, GUILayout.Width(200)))
        {
            ShowPassiveTypeMenu(property);
        }
        
        // 삭제 버튼
        if (GUILayout.Button("X", GUILayout.Width(25)))
        {
            property.managedReferenceValue = null;
        }
        
        EditorGUILayout.EndHorizontal();
        
        // 선택된 타입의 필드들 표시
        if (!string.IsNullOrEmpty(currentTypeName))
        {
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
        child.NextVisible(true); // 자식으로 진입
        
        while (!SerializedProperty.EqualContents(child, endProperty))
        {
            EditorGUILayout.PropertyField(child, true);
            if (!child.NextVisible(false)) break;
        }
    }

    private void ShowPassiveTypeMenu(SerializedProperty property)
    {
        var menu = new GenericMenu();
        
        menu.AddItem(new GUIContent("None"), false, () => {
            property.managedReferenceValue = null;
            property.serializedObject.ApplyModifiedProperties();
        });
        
        menu.AddSeparator("");
        
        // PassiveConfig의 모든 서브클래스 찾기
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => {
                try { return assembly.GetTypes(); }
                catch { return Type.EmptyTypes; }
            })
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(PassiveConfig)))
            .OrderBy(t => t.Name);
        
        foreach (var type in types)
        {
            var typeName = type.Name.Replace("PassiveConfig", "");
            var isSelected = property.managedReferenceFullTypename == $"{type.Assembly.GetName().Name} {type.FullName}";
            
            menu.AddItem(new GUIContent(typeName), isSelected, () => {
                property.managedReferenceValue = Activator.CreateInstance(type);
                property.serializedObject.ApplyModifiedProperties();
            });
        }

        menu.ShowAsContext();
    }

    private void DrawAIPatternSection()
    {
        EditorGUILayout.LabelField("AI Pattern", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.BeginHorizontal();

        // 현재 AI 타입 표시
        var currentTypeName = aiPatternProp.managedReferenceFullTypename;
        var displayName = string.IsNullOrEmpty(currentTypeName) 
            ? "None (Select AI Type)" 
            : currentTypeName.Split('.').Last();

        EditorGUILayout.LabelField("Type:", displayName, EditorStyles.boldLabel);

        // AI 타입 선택 버튼
        if (GUILayout.Button("Select AI Type", GUILayout.Width(120)))
        {
            ShowAITypeMenu(aiPatternProp);
        }

        // Clear 버튼
        if (GUILayout.Button("Clear", GUILayout.Width(60)))
        {
            aiPatternProp.managedReferenceValue = null;
        }

        EditorGUILayout.EndHorizontal();

        // 선택된 AI의 필드들 표시
        if (!string.IsNullOrEmpty(currentTypeName))
        {
            EditorGUILayout.Space(5);
            EditorGUI.indentLevel++;
            DrawPropertyFields(aiPatternProp);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void ShowAITypeMenu(SerializedProperty property)
    {
        var menu = new GenericMenu();

        menu.AddItem(new GUIContent("None"), false, () => {
            property.managedReferenceValue = null;
            property.serializedObject.ApplyModifiedProperties();
        });

        menu.AddSeparator("");

        // 수동으로 AI 타입 등록
        AddAITypeMenuItem<RandomPattern>(menu, property, "Random Pattern");
        AddAITypeMenuItem<SequentialPattern>(menu, property, "Sequential Pattern");

        // 여기에 새로운 AI 패턴 추가:
        // AddAITypeMenuItem<WeightedRandomPattern>(menu, property, "Weighted Random Pattern");
        // AddAITypeMenuItem<HPThresholdPattern>(menu, property, "HP Threshold Pattern");

        menu.ShowAsContext();
    }

    private void AddAITypeMenuItem<T>(GenericMenu menu, SerializedProperty property, string displayName) where T : MonsterAI, new()
    {
        var type = typeof(T);
        var fullTypeName = $"{type.Assembly.GetName().Name} {type.FullName}";
        var isSelected = property.managedReferenceFullTypename == fullTypeName;

        menu.AddItem(new GUIContent(displayName), isSelected, () => {
            property.managedReferenceValue = new T();
            property.serializedObject.ApplyModifiedProperties();
        });
    }
}
