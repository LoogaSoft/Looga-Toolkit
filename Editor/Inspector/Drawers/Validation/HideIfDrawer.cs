using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Inspector.Editor 
{
    [CustomPropertyDrawer(typeof(HideIfAttribute))]
    public class HideIfDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (PropertyUtils.IsVisible(property))
                EditorGUI.PropertyField(position, property, label, true);
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement field = LoogaPropertyDrawerUi.CreateSerializedField(
                property,
                property.displayName,
                fieldInfo?.FieldType);
            VisualElement root = LoogaPropertyDrawerUi.CreateRoot(field, property.tooltip);
            LoogaPropertyDrawerUi.Track(root, property, current =>
                root.style.display = PropertyUtils.IsVisible(current)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None);
            root.style.display = PropertyUtils.IsVisible(property)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            return root;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (PropertyUtils.IsVisible(property)) return EditorGUIUtility.singleLineHeight;
                
            return -EditorGUIUtility.standardVerticalSpacing;
        }
    }
}
