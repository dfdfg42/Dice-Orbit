using UnityEngine;

namespace DiceOrbit.Core
{
    /// <summary>
    /// [SerializeReference] 필드에 서브클래스 선택 드롭다운을 붙인다.
    /// 사용: [SerializeReference, SubclassPicker] public RuntimeArtifact effect;
    /// 드로어: Assets/Scripts/Editor/SubclassPickerDrawer.cs
    /// </summary>
    public class SubclassPickerAttribute : PropertyAttribute { }
}
