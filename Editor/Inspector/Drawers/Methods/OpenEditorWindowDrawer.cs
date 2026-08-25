using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Inspector.Editor
{
    [CustomPropertyDrawer(typeof(OpenEditorWindowAttribute))]
    public sealed class OpenEditorWindowDrawer : PropertyDrawerBase
    {
        private const float ButtonWidth = 82f;
        private const float Gap = 4f;

        protected override UnityEngine.UIElements.VisualElement CreatePropertyGUI_Internal(
            SerializedProperty property,
            string label)
        {
            OpenEditorWindowAttribute openAttribute = (OpenEditorWindowAttribute)attribute;
            UnityEngine.UIElements.VisualElement field = LoogaPropertyDrawerUi.CreateSerializedField(
                property,
                label,
                fieldInfo?.FieldType);
            UnityEngine.UIElements.Button button = new(() =>
                EditorApplication.ExecuteMenuItem(openAttribute.MenuPath))
            {
                text = openAttribute.Label
            };
            button.style.width = ButtonWidth;
            button.SetEnabled(!string.IsNullOrWhiteSpace(openAttribute.MenuPath));
            return LoogaPropertyDrawerUi.CreateFieldWithButtons(field, button);
        }

        protected override void OnGUI_Internal(Rect position, SerializedProperty property, GUIContent label)
        {
            OpenEditorWindowAttribute openAttribute = (OpenEditorWindowAttribute)attribute;
            Rect fieldRect = position;
            fieldRect.width -= ButtonWidth + Gap;

            Rect buttonRect = position;
            buttonRect.x = fieldRect.xMax + Gap;
            buttonRect.width = ButtonWidth;

            EditorGUI.PropertyField(fieldRect, property, label, true);

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(openAttribute.MenuPath)))
            {
                if (GUI.Button(buttonRect, openAttribute.Label))
                    EditorApplication.ExecuteMenuItem(openAttribute.MenuPath);
            }
        }
    }
}
