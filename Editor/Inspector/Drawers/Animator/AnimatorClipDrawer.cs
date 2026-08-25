using System.Collections.Generic;
using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Inspector.Editor
{
    [CustomPropertyDrawer(typeof(AnimatorClipAttribute))]
    public class AnimatorClipDrawer : PropertyDrawerBase
    {
        protected override void OnGUI_Internal(Rect position, SerializedProperty property, GUIContent label)
        {
            var acAttribute = (AnimatorClipAttribute)attribute;
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "AnimatorClipAttribute can only be used with strings");
                EditorGUI.EndProperty();
                return;
            }
            
            var controller = AnimatorHelper.GetAnimatorController(property, acAttribute.animatorControllerName);
            
            if (controller == null)
            {
                EditorGUI.LabelField(position, label.text, "Animator Controller not found");
                return;
            }
            
            List<string> clipNames = LoogaInspectorQueryUtility.GetAnimationClipNames(controller.animationClips);
            clipNames.Insert(0, "None");
            
            var currentIndex = Mathf.Max(0, clipNames.IndexOf(property.stringValue));
            if (currentIndex < 0) currentIndex = 0;
            
            var newIndex = LoogaGUI.Popup(position, label.text, currentIndex, clipNames.ToArray());
            
            if (newIndex != currentIndex) 
                property.stringValue = clipNames[newIndex];
            
            EditorGUI.EndProperty();
        }

        protected override VisualElement CreatePropertyGUI_Internal(SerializedProperty property, string label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                return LoogaPropertyDrawerUi.CreateMessage(
                    "AnimatorClip is for string fields only.",
                    HelpBoxMessageType.Warning);
            }

            AnimatorClipAttribute clip = (AnimatorClipAttribute)attribute;
            AnimatorController controller = AnimatorHelper.GetAnimatorController(property, clip.animatorControllerName);
            if (controller == null)
                return LoogaPropertyDrawerUi.CreateMessage("Animator Controller not found.", HelpBoxMessageType.Warning);

            List<string> names = LoogaDrawerOptionCache.GetOrCreate(
                $"animator.clips:{controller.GetInstanceID()}",
                () =>
                {
                    List<string> values = LoogaInspectorQueryUtility.GetAnimationClipNames(controller.animationClips);
                    values.Insert(0, "None");
                    return values;
                });
            int selected = Mathf.Max(0, names.IndexOf(property.stringValue));
            return LoogaPropertyDrawerUi.CreatePopup(property, label, names, selected, (current, index) =>
            {
                current.stringValue = index == 0 ? string.Empty : names[index];
            });
        }
    }
}
