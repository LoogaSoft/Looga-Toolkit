using System.Collections.Generic;
using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Inspector.Editor
{
    [CustomPropertyDrawer(typeof(SortingLayerAttribute))]
    public class SortingLayerDrawer : PropertyDrawerBase
    {
        protected override void OnGUI_Internal(Rect position, SerializedProperty property, GUIContent label)
        {
            List<string> sortingLayers = LoogaInspectorQueryUtility.GetSortingLayerNames(SortingLayer.layers);
            sortingLayers.Insert(0, "None");
            string[] sortingLayerArray = sortingLayers.ToArray();
            
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType == SerializedPropertyType.Integer)
            {
                property.intValue = LoogaGUI.Popup(position, label.text, property.intValue, sortingLayerArray);
            }
            else if (property.propertyType == SerializedPropertyType.String)
            {
                var currentIndex = Mathf.Max(0, sortingLayers.IndexOf(property.stringValue));
                var newIndex = LoogaGUI.Popup(position, label.text, currentIndex, sortingLayerArray);
                property.stringValue = sortingLayers[newIndex];
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "Use SortingLayerAttribute with ints or strings only");
            }
            
            EditorGUI.EndProperty();
        }

        protected override VisualElement CreatePropertyGUI_Internal(SerializedProperty property, string label)
        {
            List<string> layers = LoogaDrawerOptionCache.GetOrCreate(
                "unity.sorting-layers",
                () =>
                {
                    List<string> values = LoogaInspectorQueryUtility.GetSortingLayerNames(SortingLayer.layers);
                    values.Insert(0, "None");
                    return values;
                });
            int selected = property.propertyType == SerializedPropertyType.String
                ? Mathf.Max(0, layers.IndexOf(property.stringValue))
                : Mathf.Clamp(property.intValue, 0, layers.Count - 1);
            return LoogaPropertyDrawerUi.CreatePopup(property, label, layers, selected, (current, index) =>
            {
                if (current.propertyType == SerializedPropertyType.String)
                    current.stringValue = layers[index];
                else
                    current.intValue = index;
            });
        }
    }
}
