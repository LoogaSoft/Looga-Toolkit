using System.Collections.Generic;
using LoogaSoft.Inspector.Editor;
using UnityEditor;
using LoogaSoft.Inspector.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(LayerAttribute))]
public class LayerDrawer : PropertyDrawerBase
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
            int currentLayerIndex = LayerMask.NameToLayer(property.stringValue);
            
            if (currentLayerIndex < 0) 
                currentLayerIndex = 0;
            
            int newLayerIndex = EditorGUI.LayerField(position, label, currentLayerIndex);

            property.stringValue = LayerMask.LayerToName(newLayerIndex);
        }
        else
        {
            EditorGUI.LabelField(position, label.text, "Use LayerAttribute with ints or strings only");
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
