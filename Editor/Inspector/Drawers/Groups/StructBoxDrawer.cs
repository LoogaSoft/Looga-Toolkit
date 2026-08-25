using System.Collections.Generic;
using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Inspector.Editor
{
    [CustomPropertyDrawer(typeof(StructBoxAttribute))]
    public sealed class StructBoxDrawer : PropertyDrawerBase
    {
        private const float Padding = 8f;
        private const float HeaderHeight = 20f;
        private const float Spacing = 3f;

        protected override void OnGUI_Internal(Rect position, SerializedProperty property, GUIContent label)
        {
            StructBoxAttribute boxAttribute = (StructBoxAttribute)attribute;
            GUI.Box(position, GUIContent.none, LoogaEditorFoldouts.SmallBoxStyle);

            Rect headerRect = new(position.x + Padding, position.y + 3f, position.width - Padding * 2f, HeaderHeight);
            string title = string.IsNullOrWhiteSpace(boxAttribute.Title) ? label.text : boxAttribute.Title;
            EditorGUI.LabelField(headerRect, title, EditorStyles.boldLabel);

            Rect contentRect = new(
                position.x + Padding,
                headerRect.yMax + Spacing,
                position.width - Padding * 2f,
                position.height - HeaderHeight - Padding);

            if (property.propertyType != SerializedPropertyType.Generic || !property.hasVisibleChildren || property.isArray)
            {
                EditorGUI.PropertyField(contentRect, property, GUIContent.none, true);
                return;
            }

            List<SerializedProperty> children = InlineRowEditorUtility.GetVisibleChildren(property);
            for (int i = 0; i < children.Count; i++)
            {
                float height = EditorGUI.GetPropertyHeight(children[i], true);
                Rect childRect = new(contentRect.x, contentRect.y, contentRect.width, height);
                EditorGUI.PropertyField(childRect, children[i], true);
                contentRect.y += height + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        protected override float GetPropertyHeight_Internal(SerializedProperty property, GUIContent label)
        {
            float height = HeaderHeight + Padding + Spacing;
            if (property.propertyType != SerializedPropertyType.Generic || !property.hasVisibleChildren || property.isArray)
                return height + EditorGUI.GetPropertyHeight(property, true);

            List<SerializedProperty> children = InlineRowEditorUtility.GetVisibleChildren(property);
            for (int i = 0; i < children.Count; i++)
                height += EditorGUI.GetPropertyHeight(children[i], true) + EditorGUIUtility.standardVerticalSpacing;

            return height;
        }

        protected override VisualElement CreatePropertyGUI_Internal(SerializedProperty property, string label)
        {
            StructBoxAttribute boxAttribute = (StructBoxAttribute)attribute;
            VisualElement box = LoogaUiToolkitStyle.CreateCard();
            string title = string.IsNullOrWhiteSpace(boxAttribute.Title) ? label : boxAttribute.Title;
            Label heading = new(title);
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.marginBottom = Spacing;
            box.Add(heading);

            if (property.propertyType != SerializedPropertyType.Generic
                || !property.hasVisibleChildren
                || property.isArray)
            {
                box.Add(LoogaPropertyDrawerUi.CreateSerializedField(property, string.Empty, fieldInfo?.FieldType));
                return box;
            }

            foreach (SerializedProperty child in LoogaPropertyDrawerUi.EnumerateVisibleChildren(property))
            {
                PropertyField field = new(child.Copy());
                field.Bind(property.serializedObject);
                box.Add(field);
            }

            return box;
        }
    }
}
