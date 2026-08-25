using System;
using System.Collections.Generic;
using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Inspector.Editor
{
    [CustomPropertyDrawer(typeof(SceneAttribute))]
    public class SceneDrawer : PropertyDrawerBase
    {
        protected override void OnGUI_Internal(Rect position, SerializedProperty property, GUIContent label)
        {
            List<string> scenesList = LoogaInspectorQueryUtility.GetSceneNames(EditorBuildSettings.scenes);
            scenesList.Insert(0, "None");
            string[] scenesArray = scenesList.ToArray();
            
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType == SerializedPropertyType.Integer)
            {
                property.intValue = LoogaGUI.Popup(position, label.text, property.intValue, scenesArray);
            }
            else if (property.propertyType == SerializedPropertyType.String)
            {
                var currentIndex = Mathf.Max(0, Array.IndexOf(scenesArray, property.stringValue));
                var newIndex = LoogaGUI.Popup(position, label.text, currentIndex, scenesArray);
                property.stringValue = scenesArray[newIndex];
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "Use SceneAttribute with ints or strings only");
            }
            
            EditorGUI.EndProperty();
        }

        protected override VisualElement CreatePropertyGUI_Internal(SerializedProperty property, string label)
        {
            List<string> scenes = LoogaDrawerOptionCache.GetOrCreate(
                "unity.scenes",
                () =>
                {
                    List<string> values = LoogaInspectorQueryUtility.GetSceneNames(EditorBuildSettings.scenes);
                    values.Insert(0, "None");
                    return values;
                });
            int selected = property.propertyType == SerializedPropertyType.String
                ? Mathf.Max(0, scenes.IndexOf(property.stringValue))
                : Mathf.Clamp(property.intValue, 0, scenes.Count - 1);
            return LoogaPropertyDrawerUi.CreatePopup(property, label, scenes, selected, (current, index) =>
            {
                if (current.propertyType == SerializedPropertyType.String)
                    current.stringValue = scenes[index];
                else
                    current.intValue = index;
            });
        }
    }
}
