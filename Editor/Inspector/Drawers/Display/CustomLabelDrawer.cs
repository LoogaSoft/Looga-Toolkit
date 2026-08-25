using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Inspector.Editor
{
    [CustomPropertyDrawer(typeof(CustomLabelAttribute))]
    public class CustomLabelDrawer : PropertyDrawerBase
    {
        protected override void OnGUI_Internal(Rect position, SerializedProperty property, GUIContent label)
        {
            CustomLabelAttribute attr = (CustomLabelAttribute)attribute;
            
            GUIContent newLabel = PropertyUtils.GetContent(attr.label);
            
            EditorGUI.PropertyField(position, property, newLabel);
        }

        protected override VisualElement CreatePropertyGUI_Internal(SerializedProperty property, string label)
        {
            CustomLabelAttribute customLabel = (CustomLabelAttribute)attribute;
            return LoogaPropertyDrawerUi.CreateDefaultField(property, customLabel.label, fieldInfo?.FieldType);
        }
    }
}
