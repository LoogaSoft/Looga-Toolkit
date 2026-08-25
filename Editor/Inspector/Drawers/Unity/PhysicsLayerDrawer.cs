using System.Collections.Generic;
using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Inspector.Editor
{
    [CustomPropertyDrawer(typeof(PhysicsLayerAttribute))]
    public sealed class PhysicsLayerDrawer : PropertyDrawerBase
    {
        protected override void OnGUI_Internal(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType == SerializedPropertyType.Integer)
            {
                property.intValue = EditorGUI.LayerField(position, label, property.intValue);
            }
            else if (property.propertyType == SerializedPropertyType.String)
            {
                int currentLayer = LayerMask.NameToLayer(property.stringValue);
                if (currentLayer < 0)
                    currentLayer = 0;

                int nextLayer = EditorGUI.LayerField(position, label, currentLayer);
                property.stringValue = LayerMask.LayerToName(nextLayer);
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "Use PhysicsLayerAttribute with ints or strings only");
            }

            EditorGUI.EndProperty();
        }

        protected override VisualElement CreatePropertyGUI_Internal(SerializedProperty property, string label)
        {
            List<string> names = new() { "None" };
            List<int> indices = new() { 0 };
            for (int i = 0; i < 32; i++)
            {
                string name = LayerMask.LayerToName(i);
                if (string.IsNullOrEmpty(name))
                    continue;
                names.Add(name);
                indices.Add(i);
            }

            int layer = property.propertyType == SerializedPropertyType.String
                ? Mathf.Max(0, LayerMask.NameToLayer(property.stringValue))
                : property.intValue;
            int selected = Mathf.Max(0, indices.IndexOf(layer));
            return LoogaPropertyDrawerUi.CreatePopup(property, label, names, selected, (current, index) =>
            {
                int selectedLayer = indices[index];
                if (current.propertyType == SerializedPropertyType.String)
                    current.stringValue = LayerMask.LayerToName(selectedLayer);
                else
                    current.intValue = selectedLayer;
            });
        }
    }
}
