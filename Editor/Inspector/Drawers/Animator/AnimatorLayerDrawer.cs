using System.Collections.Generic;
using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Inspector.Editor
{
    [CustomPropertyDrawer(typeof(AnimatorLayerAttribute))]
    public class AnimatorLayerDrawer : PropertyDrawerBase
    {
        protected override void OnGUI_Internal(Rect position, SerializedProperty property, GUIContent label)
        {
            var alAttribute = (AnimatorLayerAttribute)attribute;
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType != SerializedPropertyType.Integer &&
                property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "AnimatorLayerAttribute can only be used with ints or strings");
                EditorGUI.EndProperty();
                return;
            }
            
            var controller = AnimatorHelper.GetAnimatorController(property, alAttribute.animatorControllerName);
            
            if (controller == null)
            {
                EditorGUI.LabelField(position, label.text, "Animator Controller not found");
                return;
            }
            
            List<string> layerNames = LoogaInspectorQueryUtility.GetAnimatorLayerNames(controller.layers);
            layerNames.Insert(0, "None");

            int currentIndex = -1;

            if (property.propertyType == SerializedPropertyType.Integer)
                currentIndex = property.intValue + 1;
            else if (property.propertyType == SerializedPropertyType.String)
            {
                string currentLayerName = property.stringValue;
                currentIndex = string.IsNullOrEmpty(currentLayerName) ? 0 : layerNames.IndexOf(currentLayerName);
            }
            
            if (currentIndex < 0 || currentIndex >= layerNames.Count) 
                currentIndex = 0;
            
            var newIndex = LoogaGUI.Popup(position, label.text, currentIndex, layerNames.ToArray());
            
            if (newIndex != currentIndex) 
            {
                if (property.propertyType == SerializedPropertyType.Integer)
                    property.intValue = newIndex - 1;
                else if (property.propertyType == SerializedPropertyType.String)
                    property.stringValue = newIndex == 0 ? "" : layerNames[newIndex];
            }
            
            EditorGUI.EndProperty();
        }

        protected override VisualElement CreatePropertyGUI_Internal(SerializedProperty property, string label)
        {
            if (property.propertyType != SerializedPropertyType.Integer &&
                property.propertyType != SerializedPropertyType.String)
            {
                return LoogaPropertyDrawerUi.CreateMessage(
                    "AnimatorLayer is for integer and string fields only.",
                    HelpBoxMessageType.Warning);
            }

            AnimatorLayerAttribute layer = (AnimatorLayerAttribute)attribute;
            AnimatorController controller = AnimatorHelper.GetAnimatorController(property, layer.animatorControllerName);
            if (controller == null)
                return LoogaPropertyDrawerUi.CreateMessage("Animator Controller not found.", HelpBoxMessageType.Warning);

            List<string> names = LoogaDrawerOptionCache.GetOrCreate(
                $"animator.layers:{controller.GetInstanceID()}",
                () =>
                {
                    List<string> values = LoogaInspectorQueryUtility.GetAnimatorLayerNames(controller.layers);
                    values.Insert(0, "None");
                    return values;
                });
            int selected = property.propertyType == SerializedPropertyType.Integer
                ? Mathf.Clamp(property.intValue + 1, 0, names.Count - 1)
                : Mathf.Max(0, names.IndexOf(property.stringValue));
            return LoogaPropertyDrawerUi.CreatePopup(property, label, names, selected, (current, index) =>
            {
                if (current.propertyType == SerializedPropertyType.Integer)
                    current.intValue = index - 1;
                else
                    current.stringValue = index == 0 ? string.Empty : names[index];
            });
        }
    }
}
