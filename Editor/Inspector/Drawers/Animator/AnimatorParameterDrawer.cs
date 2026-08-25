using System;
using System.Collections.Generic;
using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Inspector.Editor
{
    [CustomPropertyDrawer(typeof(AnimatorParameterAttribute))]
    public class AnimatorParameterDrawer : PropertyDrawerBase
    {
        protected override void OnGUI_Internal(Rect position, SerializedProperty property, GUIContent label)
        {
            var apAttribute = (AnimatorParameterAttribute)attribute;
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType != SerializedPropertyType.Integer &&
                property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "AnimatorParameterAttribute can only be used with ints or strings");
                EditorGUI.EndProperty();
                return;
            }
            
            var controller = AnimatorHelper.GetAnimatorController(property, apAttribute.animatorControllerName);

            if (controller == null)
            {
                EditorGUI.LabelField(position, label.text, "Animator Controller not found");
                EditorGUI.EndProperty();
                return;
            }
            
            bool filterByParameterType = apAttribute.filterByParameterType || IsTriggerProperty(property);
            AnimatorControllerParameterType parameterType = apAttribute.filterByParameterType
                ? apAttribute.parameterType
                : AnimatorControllerParameterType.Trigger;

            AnimatorControllerParameter[] parameterList = LoogaInspectorQueryUtility.FilterAnimatorParameters(
                controller.parameters,
                filterByParameterType,
                parameterType);

            List<string> paramNames = new(parameterList.Length + 1) { "None" };
            int[] paramHashesWithNone = new int[parameterList.Length + 1];

            for (int i = 0; i < parameterList.Length; i++)
            {
                AnimatorControllerParameter parameter = parameterList[i];
                paramNames.Add(parameter.name);
                paramHashesWithNone[i + 1] = parameter.nameHash;
            }

            var currentIndex = -1;
            
            if (property.propertyType == SerializedPropertyType.Integer)
                currentIndex = Array.IndexOf(paramHashesWithNone, property.intValue);
            else if (property.propertyType == SerializedPropertyType.String)
                currentIndex = paramNames.IndexOf(property.stringValue);
            
            if (currentIndex < 0) currentIndex = 0;
            
            var newIndex = LoogaGUI.Popup(position, label.text, currentIndex, paramNames.ToArray());

            if (newIndex >= 0 && newIndex != currentIndex)
            {
                if (property.propertyType == SerializedPropertyType.Integer)
                    property.intValue = paramHashesWithNone[newIndex];
                else if (property.propertyType == SerializedPropertyType.String)
                    property.stringValue = paramNames[newIndex];
            }
            
            EditorGUI.EndProperty();
        }

        protected override VisualElement CreatePropertyGUI_Internal(SerializedProperty property, string label)
        {
            if (property.propertyType != SerializedPropertyType.Integer &&
                property.propertyType != SerializedPropertyType.String)
            {
                return LoogaPropertyDrawerUi.CreateMessage(
                    "AnimatorParameter is for integer and string fields only.",
                    HelpBoxMessageType.Warning);
            }

            AnimatorParameterAttribute parameter = (AnimatorParameterAttribute)attribute;
            AnimatorController controller = AnimatorHelper.GetAnimatorController(property, parameter.animatorControllerName);
            if (controller == null)
                return LoogaPropertyDrawerUi.CreateMessage("Animator Controller not found.", HelpBoxMessageType.Warning);

            bool filter = parameter.filterByParameterType || IsTriggerProperty(property);
            AnimatorControllerParameterType type = parameter.filterByParameterType
                ? parameter.parameterType
                : AnimatorControllerParameterType.Trigger;
            string cacheKey = $"animator.parameters:{controller.GetInstanceID()}:{filter}:{type}";
            List<string> names = LoogaDrawerOptionCache.GetOrCreate(cacheKey, () =>
            {
                AnimatorControllerParameter[] values = LoogaInspectorQueryUtility.FilterAnimatorParameters(
                    controller.parameters,
                    filter,
                    type);
                List<string> result = new(values.Length + 1) { "None" };
                for (int i = 0; i < values.Length; i++)
                    result.Add(values[i].name);
                return result;
            });
            int selected = property.propertyType == SerializedPropertyType.String
                ? Mathf.Max(0, names.IndexOf(property.stringValue))
                : Mathf.Max(0, names.FindIndex(name => name != "None" && Animator.StringToHash(name) == property.intValue));
            return LoogaPropertyDrawerUi.CreatePopup(property, label, names, selected, (current, index) =>
            {
                string name = index == 0 ? string.Empty : names[index];
                if (current.propertyType == SerializedPropertyType.String)
                    current.stringValue = name;
                else
                    current.intValue = string.IsNullOrEmpty(name) ? 0 : Animator.StringToHash(name);
            });
        }

        private static bool IsTriggerProperty(SerializedProperty property)
        {
            return property != null
                && property.propertyType == SerializedPropertyType.String
                && property.name.IndexOf("trigger", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
