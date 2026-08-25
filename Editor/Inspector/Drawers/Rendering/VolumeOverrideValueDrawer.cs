using System;
using System.Collections.Generic;
using System.Reflection;
using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace LoogaSoft.Inspector.Editor
{
    [CustomPropertyDrawer(typeof(VolumeOverrideValueAttribute))]
    public sealed class VolumeOverrideValueDrawer : PropertyDrawerBase
    {
        private const BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private const BindingFlags ParameterFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const float LineSpacing = 2f;
        private const float IndentWidth = 16f;

        protected override void OnGUI_Internal(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!TryGetValueType(out Type valueType))
            {
                EditorGUI.PropertyField(position, property, label, includeChildren: true);
                return;
            }

            SerializedProperty profileProperty = property.FindPropertyRelative("_volumeProfile");
            SerializedProperty componentTypeProperty = property.FindPropertyRelative("_componentTypeName");
            SerializedProperty parameterNameProperty = property.FindPropertyRelative("_parameterName");
            if (profileProperty == null || componentTypeProperty == null || parameterNameProperty == null)
            {
                EditorGUI.PropertyField(position, property, label, includeChildren: true);
                return;
            }

            VolumeOverrideValueAttribute volumeAttribute = (VolumeOverrideValueAttribute)attribute;
            VolumeProfile profile = ResolveVolumeProfile(property, volumeAttribute.volumeProfileMember);
            profileProperty.objectReferenceValue = profile;

            Rect componentRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect parameterRect = new(
                position.x + IndentWidth,
                componentRect.yMax + LineSpacing,
                position.width - IndentWidth,
                EditorGUIUtility.singleLineHeight);

            using (new EditorGUI.DisabledScope(profile == null))
            {
                List<ComponentOption> componentOptions = BuildComponentOptions(profile, valueType);
                DrawComponentDropdown(componentRect, label, componentTypeProperty, parameterNameProperty, componentOptions, valueType);

                List<ParameterOption> parameterOptions = BuildParameterOptions(profile, componentTypeProperty.stringValue, valueType);
                DrawParameterDropdown(parameterRect, parameterNameProperty, parameterOptions);
            }
        }

        protected override float GetPropertyHeight_Internal(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2f + LineSpacing;
        }

        protected override UnityEngine.UIElements.VisualElement CreatePropertyGUI_Internal(
            SerializedProperty property,
            string label)
        {
            if (!TryGetValueType(out Type valueType))
                return LoogaPropertyDrawerUi.CreateSerializedField(property, label, fieldInfo?.FieldType);

            SerializedObject owner = property.serializedObject;
            string propertyPath = property.propertyPath;
            UnityEngine.UIElements.VisualElement root = new();
            PopupField<string> componentField = new(label, new List<string> { "None" }, 0);
            PopupField<string> parameterField = new("Value", new List<string> { "None" }, 0);
            parameterField.style.marginLeft = IndentWidth;
            root.Add(componentField);
            root.Add(parameterField);
            bool refreshing = false;
            List<ComponentOption> componentOptions = new();
            List<ParameterOption> parameterOptions = new();

            void Refresh(SerializedProperty current)
            {
                SerializedProperty profileProperty = current?.FindPropertyRelative("_volumeProfile");
                SerializedProperty componentProperty = current?.FindPropertyRelative("_componentTypeName");
                SerializedProperty parameterProperty = current?.FindPropertyRelative("_parameterName");
                if (profileProperty == null || componentProperty == null || parameterProperty == null)
                    return;

                VolumeOverrideValueAttribute volumeAttribute = (VolumeOverrideValueAttribute)attribute;
                VolumeProfile profile = ResolveVolumeProfile(current, volumeAttribute.volumeProfileMember);
                if (profileProperty.objectReferenceValue != profile)
                {
                    profileProperty.objectReferenceValue = profile;
                    owner.ApplyModifiedPropertiesWithoutUndo();
                }

                refreshing = true;
                componentOptions = BuildComponentOptions(profile, valueType);
                componentField.choices = componentOptions.Count == 0
                    ? new List<string> { $"No {ObjectNames.NicifyVariableName(valueType.Name)} Overrides" }
                    : new List<string>(ToLabels(componentOptions));
                int componentIndex = componentOptions.Count == 0
                    ? 0
                    : Mathf.Clamp(FindComponentIndex(componentOptions, componentProperty.stringValue), 0, componentOptions.Count - 1);
                componentField.SetValueWithoutNotify(componentField.choices[componentIndex]);

                parameterOptions = BuildParameterOptions(profile, componentProperty.stringValue, valueType);
                parameterField.choices = parameterOptions.Count == 0
                    ? new List<string> { "No Compatible Values" }
                    : new List<string>(ToLabels(parameterOptions));
                int parameterIndex = parameterOptions.Count == 0
                    ? 0
                    : Mathf.Clamp(FindParameterIndex(parameterOptions, parameterProperty.stringValue), 0, parameterOptions.Count - 1);
                parameterField.SetValueWithoutNotify(parameterField.choices[parameterIndex]);
                root.SetEnabled(profile != null);
                refreshing = false;
            }

            componentField.RegisterValueChangedCallback(evt =>
            {
                if (refreshing || componentOptions.Count == 0)
                    return;

                int index = componentField.choices.IndexOf(evt.newValue);
                if (index < 0 || index >= componentOptions.Count)
                    return;

                ComponentOption selected = componentOptions[index];
                LoogaPropertyDrawerUi.Commit(owner, propertyPath, current =>
                {
                    current.FindPropertyRelative("_componentTypeName").stringValue = selected.TypeName;
                    current.FindPropertyRelative("_parameterName").stringValue =
                        FirstParameterName(selected.Component, valueType);
                });
            });
            parameterField.RegisterValueChangedCallback(evt =>
            {
                if (refreshing || parameterOptions.Count == 0)
                    return;

                int index = parameterField.choices.IndexOf(evt.newValue);
                if (index < 0 || index >= parameterOptions.Count)
                    return;

                LoogaPropertyDrawerUi.Commit(owner, propertyPath, current =>
                    current.FindPropertyRelative("_parameterName").stringValue = parameterOptions[index].Name);
            });

            Refresh(property);
            LoogaPropertyDrawerUi.Track(root, property, Refresh);
            return root;
        }

        private void DrawComponentDropdown(
            Rect rect,
            GUIContent label,
            SerializedProperty componentTypeProperty,
            SerializedProperty parameterNameProperty,
            List<ComponentOption> options,
            Type valueType)
        {
            if (options.Count == 0)
            {
                LoogaGUI.Popup(rect, label.text, 0, new[] { $"No {ObjectNames.NicifyVariableName(valueType.Name)} Overrides" });
                componentTypeProperty.stringValue = string.Empty;
                parameterNameProperty.stringValue = string.Empty;
                return;
            }

            int currentIndex = FindComponentIndex(options, componentTypeProperty.stringValue);
            int newIndex = LoogaGUI.Popup(rect, label.text, currentIndex, ToLabels(options));
            newIndex = Mathf.Clamp(newIndex, 0, options.Count - 1);

            if (newIndex != currentIndex)
            {
                componentTypeProperty.stringValue = options[newIndex].TypeName;
                parameterNameProperty.stringValue = FirstParameterName(options[newIndex].Component, valueType);
            }
            else if (string.IsNullOrWhiteSpace(componentTypeProperty.stringValue))
            {
                componentTypeProperty.stringValue = options[newIndex].TypeName;
            }
        }

        private static void DrawParameterDropdown(
            Rect rect,
            SerializedProperty parameterNameProperty,
            List<ParameterOption> options)
        {
            if (options.Count == 0)
            {
                LoogaGUI.Popup(rect, "Value", 0, new[] { "No Compatible Values" });
                parameterNameProperty.stringValue = string.Empty;
                return;
            }

            int currentIndex = FindParameterIndex(options, parameterNameProperty.stringValue);
            int newIndex = LoogaGUI.Popup(rect, "Value", currentIndex, ToLabels(options));
            parameterNameProperty.stringValue = options[Mathf.Clamp(newIndex, 0, options.Count - 1)].Name;
        }

        private bool TryGetValueType(out Type valueType)
        {
            Type fieldType = fieldInfo.FieldType;
            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(VolumeValue<>))
            {
                valueType = fieldType.GetGenericArguments()[0];
                return true;
            }

            valueType = null;
            return false;
        }

        private static VolumeProfile ResolveVolumeProfile(SerializedProperty property, string memberName)
        {
            object target = PropertyUtils.GetTargetObjectWithProperty(property);
            object memberValue = GetMemberValue(target, memberName);
            return memberValue as VolumeProfile;
        }

        private static object GetMemberValue(object target, string memberName)
        {
            if (target == null || string.IsNullOrWhiteSpace(memberName))
                return null;

            Type type = target.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(memberName, MemberFlags);
                if (field != null)
                    return field.GetValue(target);

                PropertyInfo property = type.GetProperty(memberName, MemberFlags);
                if (property != null)
                    return property.GetValue(target, null);

                MethodInfo method = type.GetMethod(memberName, MemberFlags);
                if (method != null && method.GetParameters().Length == 0)
                    return method.Invoke(target, null);

                type = type.BaseType;
            }

            return null;
        }

        private static List<ComponentOption> BuildComponentOptions(VolumeProfile profile, Type valueType)
        {
            List<ComponentOption> options = new();
            if (profile == null)
                return options;

            for (int i = 0; i < profile.components.Count; i++)
            {
                VolumeComponent component = profile.components[i];
                if (component == null || !HasMatchingParameter(component.GetType(), valueType))
                    continue;

                Type componentType = component.GetType();
                string label = ObjectNames.NicifyVariableName(componentType.Name);
                string typeName = componentType.AssemblyQualifiedName ?? componentType.FullName;
                options.Add(new ComponentOption(label, typeName, component));
            }

            return options;
        }

        private static List<ParameterOption> BuildParameterOptions(VolumeProfile profile, string componentTypeName, Type valueType)
        {
            List<ParameterOption> options = new();
            VolumeComponent component = ResolveComponent(profile, componentTypeName);
            if (component == null)
                return options;

            FieldInfo[] fields = component.GetType().GetFields(ParameterFlags);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!IsVolumeParameterOfValueType(field.FieldType, valueType))
                    continue;

                string label = ObjectNames.NicifyVariableName(field.Name);
                options.Add(new ParameterOption(label, field.Name));
            }

            return options;
        }

        private static VolumeComponent ResolveComponent(VolumeProfile profile, string componentTypeName)
        {
            if (profile == null || string.IsNullOrWhiteSpace(componentTypeName))
                return null;

            for (int i = 0; i < profile.components.Count; i++)
            {
                VolumeComponent component = profile.components[i];
                Type componentType = component != null ? component.GetType() : null;
                if (componentType == null)
                    continue;

                if (componentType.AssemblyQualifiedName == componentTypeName || componentType.FullName == componentTypeName)
                    return component;
            }

            return null;
        }

        private static bool HasMatchingParameter(Type componentType, Type valueType)
        {
            FieldInfo[] fields = componentType.GetFields(ParameterFlags);
            for (int i = 0; i < fields.Length; i++)
            {
                if (IsVolumeParameterOfValueType(fields[i].FieldType, valueType))
                    return true;
            }

            return false;
        }

        private static string FirstParameterName(VolumeComponent component, Type valueType)
        {
            if (component == null)
                return string.Empty;

            FieldInfo[] fields = component.GetType().GetFields(ParameterFlags);
            for (int i = 0; i < fields.Length; i++)
            {
                if (IsVolumeParameterOfValueType(fields[i].FieldType, valueType))
                    return fields[i].Name;
            }

            return string.Empty;
        }

        private static bool IsVolumeParameterOfValueType(Type parameterType, Type valueType)
        {
            while (parameterType != null)
            {
                if (parameterType.IsGenericType
                    && parameterType.GetGenericTypeDefinition() == typeof(VolumeParameter<>)
                    && parameterType.GetGenericArguments()[0] == valueType)
                {
                    return true;
                }

                parameterType = parameterType.BaseType;
            }

            return false;
        }

        private static int FindComponentIndex(List<ComponentOption> options, string typeName)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].TypeName == typeName)
                    return i;
            }

            return 0;
        }

        private static int FindParameterIndex(List<ParameterOption> options, string name)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Name == name)
                    return i;
            }

            return 0;
        }

        private static string[] ToLabels(List<ComponentOption> options)
        {
            string[] labels = new string[options.Count];
            for (int i = 0; i < options.Count; i++)
                labels[i] = options[i].Label;

            return labels;
        }

        private static string[] ToLabels(List<ParameterOption> options)
        {
            string[] labels = new string[options.Count];
            for (int i = 0; i < options.Count; i++)
                labels[i] = options[i].Label;

            return labels;
        }

        private readonly struct ComponentOption
        {
            public readonly string Label;
            public readonly string TypeName;
            public readonly VolumeComponent Component;

            public ComponentOption(string label, string typeName, VolumeComponent component)
            {
                Label = label;
                TypeName = typeName;
                Component = component;
            }
        }

        private readonly struct ParameterOption
        {
            public readonly string Label;
            public readonly string Name;

            public ParameterOption(string label, string name)
            {
                Label = label;
                Name = name;
            }
        }
    }
}
