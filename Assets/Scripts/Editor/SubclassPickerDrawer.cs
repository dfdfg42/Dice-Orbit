using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// [SerializeReference, SubclassPicker] 필드용 드로어:
    /// 필드 라인 오른쪽에 파생 타입 드롭다운을 그리고, 선택 시 인스턴스를 생성해 꽂는다.
    /// 선택된 타입의 직렬화 필드는 그대로 펼쳐서 편집 가능.
    /// </summary>
    [CustomPropertyDrawer(typeof(SubclassPickerAttribute))]
    public class SubclassPickerDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUI.GetPropertyHeight(property, label, true);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            // 타입 드롭다운 (라벨 라인의 오른쪽 절반)
            var lineRect = new Rect(
                position.x + EditorGUIUtility.labelWidth + 2f,
                position.y,
                position.width - EditorGUIUtility.labelWidth - 2f,
                EditorGUIUtility.singleLineHeight);

            string currentName = property.managedReferenceValue == null
                ? "(없음)"
                : property.managedReferenceValue.GetType().Name;

            if (EditorGUI.DropdownButton(lineRect, new GUIContent(currentName), FocusType.Keyboard))
            {
                var menu = new GenericMenu();
                var prop = property.Copy();   // 클로저용 복사

                menu.AddItem(new GUIContent("(없음)"), property.managedReferenceValue == null, () =>
                {
                    prop.managedReferenceValue = null;
                    prop.serializedObject.ApplyModifiedProperties();
                });

                Type baseType = GetManagedReferenceFieldType(property);
                if (baseType != null)
                {
                    foreach (var type in TypeCache.GetTypesDerivedFrom(baseType)
                                 .Where(t => !t.IsAbstract && !t.IsGenericType && t.GetConstructor(Type.EmptyTypes) != null)
                                 .OrderBy(t => t.Name))
                    {
                        var captured = type;
                        bool selected = property.managedReferenceValue != null &&
                                        property.managedReferenceValue.GetType() == captured;
                        menu.AddItem(new GUIContent(captured.Name), selected, () =>
                        {
                            prop.managedReferenceValue = Activator.CreateInstance(captured);
                            prop.serializedObject.ApplyModifiedProperties();
                        });
                    }
                }
                menu.ShowAsContext();
            }

            // 본체 (foldout + 자식 필드)
            EditorGUI.PropertyField(position, property, label, true);
        }

        /// <summary>managedReferenceFieldTypename("어셈블리 타입명")에서 필드 선언 타입을 복원.</summary>
        private static Type GetManagedReferenceFieldType(SerializedProperty property)
        {
            string typename = property.managedReferenceFieldTypename;
            if (string.IsNullOrEmpty(typename)) return null;

            var parts = typename.Split(' ');
            if (parts.Length != 2) return null;

            return Type.GetType($"{parts[1]}, {parts[0]}");
        }
    }
}
