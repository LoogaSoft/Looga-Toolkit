using System;
using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Inspector.Editor
{
    [CustomPropertyDrawer(typeof(SliderlessRangeAttribute))]
    public class SliderlessRangeDrawer : PropertyDrawerBase
    {
        protected override void OnGUI_Internal(Rect position, SerializedProperty property, GUIContent label)
        {
            SliderlessRangeAttribute attr = (SliderlessRangeAttribute)attribute;

            label = EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();

            if (property.propertyType == SerializedPropertyType.Float)
            {
                if (fieldInfo.FieldType == typeof(double))
                {
                    double value = EditorGUI.DoubleField(position, label, property.doubleValue);
                    if (EditorGUI.EndChangeCheck())
                        property.doubleValue = Math.Clamp(value, attr.min, attr.max);
                }
                else
                {
                    float value = EditorGUI.FloatField(position, label, property.floatValue);
                    if (EditorGUI.EndChangeCheck())
                        property.floatValue = Mathf.Clamp(value, (float)attr.min, (float)attr.max);
                }
            }
            else if (property.propertyType == SerializedPropertyType.Integer)
            {
                if (fieldInfo.FieldType == typeof(long))
                {
                    long value = EditorGUI.LongField(position, label, property.longValue);
                    if (EditorGUI.EndChangeCheck())
                        property.longValue = Math.Clamp(value, (long)attr.min, (long)attr.max);
                }
                else
                {
                    int value = EditorGUI.IntField(position, label, property.intValue);
                    if (EditorGUI.EndChangeCheck())
                        property.intValue = Mathf.Clamp(value, (int)attr.min, (int)attr.max);
                }
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "SliderlessRange is for Floats and Integers only");
            }

            EditorGUI.EndProperty();
        }

        protected override VisualElement CreatePropertyGUI_Internal(SerializedProperty property, string label)
        {
            SliderlessRangeAttribute range = (SliderlessRangeAttribute)attribute;
            SerializedObject owner = property.serializedObject;
            string path = property.propertyPath;

            if (property.propertyType == SerializedPropertyType.Float && fieldInfo?.FieldType == typeof(double))
            {
                DoubleField field = new(label) { value = property.doubleValue };
                field.RegisterValueChangedCallback(evt => LoogaPropertyDrawerUi.Commit(
                    owner,
                    path,
                    current => current.doubleValue = Math.Clamp(evt.newValue, range.min, range.max)));
                return field;
            }

            if (property.propertyType == SerializedPropertyType.Float)
            {
                FloatField field = new(label) { value = property.floatValue };
                field.RegisterValueChangedCallback(evt => LoogaPropertyDrawerUi.Commit(
                    owner,
                    path,
                    current => current.floatValue = Mathf.Clamp(evt.newValue, (float)range.min, (float)range.max)));
                return field;
            }

            if (property.propertyType == SerializedPropertyType.Integer && fieldInfo?.FieldType == typeof(long))
            {
                LongField field = new(label) { value = property.longValue };
                field.RegisterValueChangedCallback(evt => LoogaPropertyDrawerUi.Commit(
                    owner,
                    path,
                    current => current.longValue = Math.Clamp(evt.newValue, (long)range.min, (long)range.max)));
                return field;
            }

            if (property.propertyType == SerializedPropertyType.Integer)
            {
                IntegerField field = new(label) { value = property.intValue };
                field.RegisterValueChangedCallback(evt => LoogaPropertyDrawerUi.Commit(
                    owner,
                    path,
                    current => current.intValue = Mathf.Clamp(evt.newValue, (int)range.min, (int)range.max)));
                return field;
            }

            return LoogaPropertyDrawerUi.CreateMessage(
                "SliderlessRange is for numeric fields only.",
                HelpBoxMessageType.Warning);
        }
    }
}
