using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Inspector.Editor
{
    [CustomPropertyDrawer(typeof(SingleEnumFlagAttribute))]
    public class SingleEnumFlagDrawer : PropertyDrawerBase
    {
        protected override void OnGUI_Internal(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Enum)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            int selectedIndex = Mathf.Clamp(property.enumValueIndex, 0, property.enumDisplayNames.Length - 1);
            int nextIndex = LoogaGUI.Popup(position, label, selectedIndex, property.enumDisplayNames);
            if (nextIndex != selectedIndex)
                property.enumValueIndex = nextIndex;
        }

        protected override VisualElement CreatePropertyGUI_Internal(SerializedProperty property, string label)
        {
            if (property.propertyType != SerializedPropertyType.Enum)
                return LoogaPropertyDrawerUi.CreateDefaultField(property, label, fieldInfo?.FieldType);

            return LoogaPropertyDrawerUi.CreatePopup(
                property,
                label,
                property.enumDisplayNames,
                property.enumValueIndex,
                (current, index) => current.enumValueIndex = index);
        }
    }
}
