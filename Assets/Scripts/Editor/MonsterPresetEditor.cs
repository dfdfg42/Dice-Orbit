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

        // 기본 필드들 그리기 (StartingPassives와 AIPattern 제외)
        DrawPropertiesExcluding(serializedObject, "StartingPassives", "AIPattern");

        EditorGUILayout.Space();

        // AI Pattern 섹션
        DrawAIPatternSection();

        EditorGUILayout.Space();

        // Starting Passives 헤더와 크기 조절
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Starting Passives", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+", GUILayout.Width(30)))
        {
            startingPassivesProp.arraySize++;
        }
        EditorGUILayout.EndHorizontal();

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
            DrawPassiveElement(element, i, startingPassivesProp);
        }

        EditorGUI.indentLevel--;

        EditorGUILayout.Space();

        // Death Effects 섹션
        DrawDeathEffectsSection();

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// 개별 PassiveAbility 렌더링 (드래그 앤 드롭 지원)
    /// </summary>
    private void DrawPassiveElement(SerializedProperty property, int index, SerializedProperty listProp)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Passive {index}", EditorStyles.boldLabel, GUILayout.Width(80));

        // 현재 타입 표시
        var currentTypeName = property.managedReferenceFullTypename;
        var displayName = string.IsNullOrEmpty(currentTypeName) 
            ? "(Not Assigned)" 
            : currentTypeName.Split('.').Last();

        EditorGUILayout.LabelField(displayName, EditorStyles.label);
        GUILayout.FlexibleSpace();

        // 삭제 버튼
        if (GUILayout.Button("X", GUILayout.Width(25)))
        {
            listProp.DeleteArrayElementAtIndex(index);
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

        // 백업 옵션: 드롭다운 메뉴 버튼
        if (GUILayout.Button("Or Select from List (Dropdown)", GUILayout.Height(25)))
        {
            ShowPassiveTypeMenu(property);
        }

        // 선택된 타입의 필드들 표시
        if (!string.IsNullOrEmpty(currentTypeName))
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Passive Properties:", EditorStyles.miniLabel);
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
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(DiceOrbit.Data.Passives.PassiveAbility)))
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

            // availableSkills 커스텀 렌더링
            var skillsListProp = aiPatternProp.FindPropertyRelative("availableSkills");
            if (skillsListProp != null)
            {
                DrawSkillsList(skillsListProp);
            }
            else
            {
                // fallback: 모든 필드 표시
                DrawPropertyFields(aiPatternProp);
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// MonsterSkill의 targetCount를 기본값 1로 설정
    /// </summary>
    private void EnsureSkillTargetCounts(SerializedProperty aiProp)
    {
        // availableSkills 리스트 찾기
        var skillsListProp = aiProp.FindPropertyRelative("availableSkills");
        if (skillsListProp == null || !skillsListProp.isArray) return;

        for (int i = 0; i < skillsListProp.arraySize; i++)
        {
            var skillProp = skillsListProp.GetArrayElementAtIndex(i);
            var targetCountProp = skillProp.FindPropertyRelative("targetCount");

            if (targetCountProp != null && targetCountProp.intValue == 0)
            {
                targetCountProp.intValue = 1;
            }
        }
    }

    /// <summary>
    /// availableSkills 리스트 렌더링 (SkillData 타입 선택 UI 포함)
    /// </summary>
    private void DrawSkillsList(SerializedProperty skillsListProp)
    {
        EditorGUILayout.LabelField("Available Skills", EditorStyles.boldLabel);

        // 리스트 크기 조절
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

        // 각 MonsterSkill 렌더링
        for (int i = 0; i < skillsListProp.arraySize; i++)
        {
            var skillProp = skillsListProp.GetArrayElementAtIndex(i);
            DrawMonsterSkillElement(skillProp, i, skillsListProp);
        }

        EditorGUI.indentLevel--;
    }

    /// <summary>
    /// 개별 MonsterSkill 렌더링
    /// </summary>
    private void DrawMonsterSkillElement(SerializedProperty skillProp, int index, SerializedProperty listProp)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Skill {index}", EditorStyles.boldLabel, GUILayout.Width(60));

        // 삭제 버튼
        if (GUILayout.Button("X", GUILayout.Width(25)))
        {
            listProp.DeleteArrayElementAtIndex(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.EndHorizontal();

        EditorGUI.indentLevel++;

        // SkillData 필드 (SerializeReference)
        var skillDataProp = skillProp.FindPropertyRelative("skillData");
        if (skillDataProp != null)
        {
            DrawSkillDataField(skillDataProp);
        }

        // 나머지 필드들 (targetStrategy, targetType 등)
        EditorGUILayout.Space(3);
        var child = skillProp.Copy();
        var endProperty = skillProp.GetEndProperty();
        child.NextVisible(true); // 자식으로 진입

        while (!SerializedProperty.EqualContents(child, endProperty))
        {
            if (child.name != "skillData") // skillData는 이미 렌더링했으므로 제외
            {
                EditorGUILayout.PropertyField(child, true);
            }
            if (!child.NextVisible(false)) break;
        }

        // targetCount 자동 초기화
        var targetCountProp = skillProp.FindPropertyRelative("targetCount");
        if (targetCountProp != null && targetCountProp.intValue == 0)
        {
            targetCountProp.intValue = 1;
        }

        EditorGUI.indentLevel--;

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    /// <summary>
    /// SkillData 필드 렌더링 (드래그 앤 드롭 지원)
    /// </summary>
    private void DrawSkillDataField(SerializedProperty skillDataProp)
    {
        var currentTypeName = skillDataProp.managedReferenceFullTypename;
        var displayName = string.IsNullOrEmpty(currentTypeName)
            ? "None"
            : currentTypeName.Split('.').Last();

        // 헤더 - 현재 스킬 타입과 Clear 버튼
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Skill Data:", EditorStyles.boldLabel, GUILayout.Width(80));
        
        if (!string.IsNullOrEmpty(currentTypeName))
        {
            EditorGUILayout.LabelField(displayName, EditorStyles.label);
        }
        else
        {
            EditorGUILayout.LabelField("(Not Assigned)", EditorStyles.miniLabel);
        }
        
        GUILayout.FlexibleSpace();
        
        if (GUILayout.Button("Clear", GUILayout.Width(60)))
        {
            skillDataProp.managedReferenceValue = null;
            skillDataProp.serializedObject.ApplyModifiedProperties();
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
        
        GUI.Box(dropArea, "📄 Drag .cs script here to assign SkillData\n(or use button below)", EditorStyles.helpBox);
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
                            scriptType.IsSubclassOf(typeof(DiceOrbit.Data.SkillData)))
                        {
                            skillDataProp.managedReferenceValue = Activator.CreateInstance(scriptType);
                            skillDataProp.serializedObject.ApplyModifiedProperties();
                            Debug.Log($"✓ SkillData assigned: {scriptType.Name}");
                            break;
                        }
                        else
                        {
                            Debug.LogWarning($"⚠️ '{monoScript.name}' is not a valid SkillData class!\n" +
                                           "Make sure it inherits from SkillData and is not abstract.");
                        }
                    }
                }
                evt.Use();
            }
        }

        // 백업 옵션: 드롭다운 메뉴 버튼
        if (GUILayout.Button("Or Select from List (Dropdown)", GUILayout.Height(25)))
        {
            ShowSkillDataTypeMenu(skillDataProp);
        }

        // 선택된 SkillData의 필드들 표시
        if (!string.IsNullOrEmpty(currentTypeName))
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Skill Properties:", EditorStyles.miniLabel);
            EditorGUI.indentLevel++;
            DrawPropertyFields(skillDataProp);
            EditorGUI.indentLevel--;
        }
    }

    /// <summary>
    /// SkillData 타입 선택 메뉴
    /// </summary>
    private void ShowSkillDataTypeMenu(SerializedProperty property)
    {
        var menu = new GenericMenu();

        menu.AddItem(new GUIContent("None"), false, () => {
            property.managedReferenceValue = null;
            property.serializedObject.ApplyModifiedProperties();
        });

        menu.AddSeparator("");

        // SkillData의 모든 서브클래스 찾기
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => {
                try { return assembly.GetTypes(); }
                catch { return Type.EmptyTypes; }
            })
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(DiceOrbit.Data.SkillData)))
            .OrderBy(t => t.Name);

        foreach (var type in types)
        {
            var typeName = type.Name;
            var isSelected = property.managedReferenceFullTypename == $"{type.Assembly.GetName().Name} {type.FullName}";

            menu.AddItem(new GUIContent(typeName), isSelected, () => {
                property.managedReferenceValue = Activator.CreateInstance(type);
                property.serializedObject.ApplyModifiedProperties();
            });
        }

        menu.ShowAsContext();
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

    /// <summary>
    /// Death Effects 섹션 렌더링
    /// </summary>
    private void DrawDeathEffectsSection()
    {
        // Death Effects 헤더와 크기 조절
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Death Effects", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+", GUILayout.Width(30)))
        {
            onDeathEffectsProp.arraySize++;
        }
        EditorGUILayout.EndHorizontal();

        // 리스트 크기 조절
        int newSize = EditorGUILayout.IntField("Size", onDeathEffectsProp.arraySize);
        if (newSize != onDeathEffectsProp.arraySize)
        {
            onDeathEffectsProp.arraySize = newSize;
        }

        EditorGUI.indentLevel++;

        // 각 요소 렌더링
        for (int i = 0; i < onDeathEffectsProp.arraySize; i++)
        {
            var element = onDeathEffectsProp.GetArrayElementAtIndex(i);
            DrawDeathEffectElement(element, i, onDeathEffectsProp);
        }

        EditorGUI.indentLevel--;
    }

    /// <summary>
    /// 개별 DeathEffect 렌더링 (드래그 앤 드롭 지원)
    /// </summary>
    private void DrawDeathEffectElement(SerializedProperty property, int index, SerializedProperty listProp)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Effect {index}", EditorStyles.boldLabel, GUILayout.Width(80));

        // 현재 타입 표시
        var currentTypeName = property.managedReferenceFullTypename;
        var displayName = string.IsNullOrEmpty(currentTypeName) 
            ? "(Not Assigned)" 
            : currentTypeName.Split('.').Last();

        EditorGUILayout.LabelField(displayName, EditorStyles.label);
        GUILayout.FlexibleSpace();

        // 삭제 버튼
        if (GUILayout.Button("X", GUILayout.Width(25)))
        {
            listProp.DeleteArrayElementAtIndex(index);
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
            GUI.backgroundColor = new Color(1f, 0.8f, 0.6f); // 연한 주황색 (Passive와 구분)
        }

        GUI.Box(dropArea, "💀 Drag .cs script here to assign DeathEffect\n(or use button below)", EditorStyles.helpBox);
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
                            scriptType.IsSubclassOf(typeof(DiceOrbit.Data.Monsters.DeathEffect)))
                        {
                            property.managedReferenceValue = Activator.CreateInstance(scriptType);
                            property.serializedObject.ApplyModifiedProperties();
                            Debug.Log($"✓ DeathEffect assigned: {scriptType.Name}");
                            break;
                        }
                        else
                        {
                            Debug.LogWarning($"⚠️ '{monoScript.name}' is not a valid DeathEffect class!\n" +
                                           "Make sure it inherits from DeathEffect and is not abstract.");
                        }
                    }
                }
                evt.Use();
            }
        }

        // 백업 옵션: 드롭다운 메뉴 버튼
        if (GUILayout.Button("Or Select from List (Dropdown)", GUILayout.Height(25)))
        {
            ShowDeathEffectTypeMenu(property);
        }

        // 선택된 타입의 필드들 표시
        if (!string.IsNullOrEmpty(currentTypeName))
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Effect Properties:", EditorStyles.miniLabel);
            EditorGUI.indentLevel++;
            DrawPropertyFields(property);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    /// <summary>
    /// DeathEffect 타입 선택 메뉴
    /// </summary>
    private void ShowDeathEffectTypeMenu(SerializedProperty property)
    {
        var menu = new GenericMenu();

        menu.AddItem(new GUIContent("None"), false, () => {
            property.managedReferenceValue = null;
            property.serializedObject.ApplyModifiedProperties();
        });

        menu.AddSeparator("");

        // DeathEffect의 모든 서브클래스 찾기
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => {
                try { return assembly.GetTypes(); }
                catch { return Type.EmptyTypes; }
            })
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(DiceOrbit.Data.Monsters.DeathEffect)))
            .OrderBy(t => t.Name);

        foreach (var type in types)
        {
            var typeName = type.Name;
            var isSelected = property.managedReferenceFullTypename == $"{type.Assembly.GetName().Name} {type.FullName}";

            menu.AddItem(new GUIContent(typeName), isSelected, () => {
                property.managedReferenceValue = Activator.CreateInstance(type);
                property.serializedObject.ApplyModifiedProperties();
            });
        }

        menu.ShowAsContext();
    }
}
