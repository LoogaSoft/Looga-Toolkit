using System.Collections.Generic;
using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace LoogaSoft.Inspector.Editor
{
    [CustomPropertyDrawer(typeof(ShaderPropertyAttribute))]
    public sealed class ShaderPropertyDrawer : PropertyDrawerBase
    {
        protected override void OnGUI_Internal(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            ShaderPropertyAttribute shaderPropertyAttribute = (ShaderPropertyAttribute)attribute;
            Shader shader = LoogaShaderEditorUtility.ResolveShader(property, shaderPropertyAttribute.materialOrShaderMember);
            if (shader == null)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            List<string> propertyNames = GetPropertyNames(shader, shaderPropertyAttribute.propertyType);
            if (propertyNames.Count == 0)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            int currentIndex = Mathf.Max(0, propertyNames.IndexOf(property.stringValue));
            int newIndex = LoogaGUI.Popup(position, label.text, currentIndex, propertyNames.ToArray());
            property.stringValue = propertyNames[Mathf.Clamp(newIndex, 0, propertyNames.Count - 1)];
        }

        protected override VisualElement CreatePropertyGUI_Internal(SerializedProperty property, string label)
        {
            if (property.propertyType != SerializedPropertyType.String)
                return LoogaPropertyDrawerUi.CreateDefaultField(property, label, fieldInfo?.FieldType);

            ShaderPropertyAttribute shaderProperty = (ShaderPropertyAttribute)attribute;
            return LoogaPropertyDrawerUi.CreateTrackedPopup(
                property,
                label,
                current =>
                {
                    Shader shader = LoogaShaderEditorUtility.ResolveShader(current, shaderProperty.materialOrShaderMember);
                    return shader == null ? new List<string>() : GetPropertyNames(shader, shaderProperty.propertyType);
                },
                (current, names) => Mathf.Max(0, new List<string>(names).IndexOf(current.stringValue)),
                (current, names, index) => current.stringValue = names[index]);
        }

        private static List<string> GetPropertyNames(Shader shader, LoogaShaderPropertyType propertyType)
        {
            List<string> names = new();
            int propertyCount = shader.GetPropertyCount();

            for (int i = 0; i < propertyCount; i++)
            {
                if (!MatchesType(shader, i, propertyType))
                    continue;

                names.Add(shader.GetPropertyName(i));
            }

            return names;
        }

        private static bool MatchesType(Shader shader, int propertyIndex, LoogaShaderPropertyType propertyType)
        {
            if (propertyType == LoogaShaderPropertyType.Any)
                return true;

            ShaderPropertyType shaderPropertyType = shader.GetPropertyType(propertyIndex);
            return propertyType switch
            {
                LoogaShaderPropertyType.Color => shaderPropertyType == ShaderPropertyType.Color,
                LoogaShaderPropertyType.Vector => shaderPropertyType == ShaderPropertyType.Vector,
                LoogaShaderPropertyType.Float => shaderPropertyType == ShaderPropertyType.Float,
                LoogaShaderPropertyType.Range => shaderPropertyType == ShaderPropertyType.Range,
                LoogaShaderPropertyType.Texture => shaderPropertyType == ShaderPropertyType.Texture,
                _ => true
            };
        }
    }
}
