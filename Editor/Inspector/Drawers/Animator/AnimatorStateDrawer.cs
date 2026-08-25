using System.Collections.Generic;
using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Inspector.Editor
{
    [CustomPropertyDrawer(typeof(AnimatorStateAttribute))]
    public class AnimatorStateDrawer : PropertyDrawerBase
    {
        protected override void OnGUI_Internal(Rect position, SerializedProperty property, GUIContent label)
        {
            var asAttribute = (AnimatorStateAttribute)attribute;
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType != SerializedPropertyType.String &&
                property.propertyType != SerializedPropertyType.Integer)
            {
                EditorGUI.LabelField(position, label.text, "AnimatorStateAttribute can only be used with strings or ints");
                EditorGUI.EndProperty();
                return;
            }
            
            var controller = AnimatorHelper.GetAnimatorController(property, asAttribute.animatorControllerName);

            if (controller == null)
            {
                EditorGUI.LabelField(position, label.text, "Animator Controller not found");
                return;
            }

            List<string> stateNames = new List<string> { "None" };
            foreach (var layer in controller.layers)
            {
                GetStatesRecursive(layer.stateMachine, stateNames);
            }

            var currentIndex = 0;
            
            if (property.propertyType == SerializedPropertyType.String)
                currentIndex = Mathf.Max(0, stateNames.IndexOf(property.stringValue));
            else if (property.propertyType == SerializedPropertyType.Integer)
            {
                var currentHash = property.intValue;

                foreach (var stateName in stateNames)
                {
                    if (Animator.StringToHash(stateName) == currentHash)
                    {
                        currentIndex = stateNames.IndexOf(stateName);
                        break;
                    }
                }
            }
            
            var newIndex = LoogaGUI.Popup(position, label.text, currentIndex, stateNames.ToArray());

            if (newIndex != currentIndex)
            {
                var selectedName = newIndex == 0 ? "" : stateNames[newIndex];
                
                if (property.propertyType == SerializedPropertyType.String)
                    property.stringValue = selectedName;
                else if (property.propertyType == SerializedPropertyType.Integer)
                    property.intValue = string.IsNullOrEmpty(selectedName) ? 0 : Animator.StringToHash(selectedName);
            }
            
            EditorGUI.EndProperty();
        }

        protected override VisualElement CreatePropertyGUI_Internal(SerializedProperty property, string label)
        {
            if (property.propertyType != SerializedPropertyType.String &&
                property.propertyType != SerializedPropertyType.Integer)
            {
                return LoogaPropertyDrawerUi.CreateMessage(
                    "AnimatorState is for string and integer fields only.",
                    HelpBoxMessageType.Warning);
            }

            AnimatorStateAttribute state = (AnimatorStateAttribute)attribute;
            AnimatorController controller = AnimatorHelper.GetAnimatorController(property, state.animatorControllerName);
            if (controller == null)
                return LoogaPropertyDrawerUi.CreateMessage("Animator Controller not found.", HelpBoxMessageType.Warning);

            List<string> names = LoogaDrawerOptionCache.GetOrCreate(
                $"animator.states:{controller.GetInstanceID()}",
                () =>
                {
                    List<string> values = new() { "None" };
                    foreach (AnimatorControllerLayer layer in controller.layers)
                        GetStatesRecursive(layer.stateMachine, values);
                    return values;
                });
            int selected = property.propertyType == SerializedPropertyType.String
                ? Mathf.Max(0, names.IndexOf(property.stringValue))
                : Mathf.Max(0, names.FindIndex(name =>
                    name != "None" && Animator.StringToHash(name) == property.intValue));

            return LoogaPropertyDrawerUi.CreatePopup(property, label, names, selected, (current, index) =>
            {
                string name = index == 0 ? string.Empty : names[index];
                if (current.propertyType == SerializedPropertyType.String)
                    current.stringValue = name;
                else
                    current.intValue = string.IsNullOrEmpty(name) ? 0 : Animator.StringToHash(name);
            });
        }

        private static void GetStatesRecursive(AnimatorStateMachine stateMachine, List<string> stateNames)
        {
            foreach (var state in stateMachine.states) 
                stateNames.Add(state.state.name);
            
            foreach (var subStateMachine in stateMachine.stateMachines)
                GetStatesRecursive(subStateMachine.stateMachine, stateNames);
        }
    }
}
