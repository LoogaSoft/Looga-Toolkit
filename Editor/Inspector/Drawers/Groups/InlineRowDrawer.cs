using System.Collections.Generic;
using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Inspector.Editor
{
    [CustomPropertyDrawer(typeof(InlineRowAttribute))]
    public sealed class InlineRowDrawer : PropertyDrawerBase
    {
        protected override void OnGUI_Internal(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Generic || !property.hasVisibleChildren || property.isArray)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            Rect contentRect = EditorGUI.PrefixLabel(position, label);
            List<SerializedProperty> children = InlineRowEditorUtility.GetVisibleChildren(property);
            List<GUIContent> labels = new(children.Count);

            for (int i = 0; i < children.Count; i++)
                labels.Add(PropertyUtils.GetContent(children[i].displayName));

            InlineRowEditorUtility.DrawProperties(contentRect, children, labels);
        }

        protected override float GetPropertyHeight_Internal(SerializedProperty property, GUIContent label)
        {
            return InlineRowEditorUtility.SingleLineHeight;
        }

        protected override VisualElement CreatePropertyGUI_Internal(SerializedProperty property, string label)
        {
            if (property.propertyType != SerializedPropertyType.Generic
                || !property.hasVisibleChildren
                || property.isArray)
            {
                return LoogaPropertyDrawerUi.CreateSerializedField(property, label, fieldInfo?.FieldType);
            }

            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            Label rowLabel = new(label);
            rowLabel.style.minWidth = EditorGUIUtility.labelWidth;
            rowLabel.style.flexShrink = 0f;
            row.Add(rowLabel);

            foreach (SerializedProperty child in LoogaPropertyDrawerUi.EnumerateVisibleChildren(property))
            {
                PropertyField field = new(child.Copy(), child.displayName);
                field.style.flexGrow = 1f;
                field.style.flexBasis = 0f;
                field.Bind(property.serializedObject);
                row.Add(field);
            }

            return row;
        }
    }
}
