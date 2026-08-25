using System.Collections.Generic;
using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace LoogaSoft.Inspector.Editor
{
    [CustomPropertyDrawer(typeof(GlobalShaderPropertyAttribute))]
    public sealed class GlobalShaderPropertyDrawer : PropertyDrawerBase
    {
        private static readonly Dictionary<LoogaShaderPropertyType, string[]> CachedPropertyNames = new();

        static GlobalShaderPropertyDrawer()
        {
            LoogaDrawerOptionCache.Invalidated += CachedPropertyNames.Clear;
        }

        protected override void OnGUI_Internal(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            GlobalShaderPropertyAttribute globalPropertyAttribute = (GlobalShaderPropertyAttribute)attribute;
            List<string> propertyNames = new(GetPropertyNames(globalPropertyAttribute.propertyType));
            if (propertyNames.Count == 0)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            if (!string.IsNullOrWhiteSpace(property.stringValue) && !propertyNames.Contains(property.stringValue))
                propertyNames.Insert(0, property.stringValue);

            int currentIndex = Mathf.Max(0, propertyNames.IndexOf(property.stringValue));
            int newIndex = LoogaGUI.Popup(position, label.text, currentIndex, propertyNames.ToArray());
            property.stringValue = propertyNames[Mathf.Clamp(newIndex, 0, propertyNames.Count - 1)];
        }

        protected override VisualElement CreatePropertyGUI_Internal(SerializedProperty property, string label)
        {
            if (property.propertyType != SerializedPropertyType.String)
                return LoogaPropertyDrawerUi.CreateDefaultField(property, label, fieldInfo?.FieldType);

            GlobalShaderPropertyAttribute globalProperty = (GlobalShaderPropertyAttribute)attribute;
            List<string> names = new(GetPropertyNames(globalProperty.propertyType));
            if (!string.IsNullOrWhiteSpace(property.stringValue) && !names.Contains(property.stringValue))
                names.Insert(0, property.stringValue);
            if (names.Count == 0)
                return LoogaPropertyDrawerUi.CreateDefaultField(property, label, fieldInfo?.FieldType);

            int selected = Mathf.Max(0, names.IndexOf(property.stringValue));
            return LoogaPropertyDrawerUi.CreatePopup(
                property,
                label,
                names,
                selected,
                (current, index) => current.stringValue = names[index]);
        }

        private static string[] GetPropertyNames(LoogaShaderPropertyType propertyType)
        {
            if (CachedPropertyNames.TryGetValue(propertyType, out string[] cachedNames))
                return cachedNames;

            HashSet<string> names = new();
            string[] shaderGuids = AssetDatabase.FindAssets("t:Shader");

            foreach (string shaderGuid in shaderGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(shaderGuid);
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null)
                    continue;

                AddPropertyNames(shader, propertyType, names);
            }

            string[] sortedNames = new List<string>(names).ToArray();
            System.Array.Sort(sortedNames);
            CachedPropertyNames[propertyType] = sortedNames;
            return sortedNames;
        }

        private static void AddPropertyNames(
            Shader shader,
            LoogaShaderPropertyType propertyType,
            HashSet<string> names)
        {
            int propertyCount = shader.GetPropertyCount();
            for (int i = 0; i < propertyCount; i++)
            {
                if (!MatchesType(shader, i, propertyType))
                    continue;

                string propertyName = shader.GetPropertyName(i);
                if (!string.IsNullOrWhiteSpace(propertyName))
                    names.Add(propertyName);
            }
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
