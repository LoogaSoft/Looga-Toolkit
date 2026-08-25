using System.Collections.Generic;
using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Inspector.Editor
{
    [CustomPropertyDrawer(typeof(TagAttribute))]
    public class TagDrawer : PropertyDrawerBase
    {
        protected override void OnGUI_Internal(Rect position, SerializedProperty property, GUIContent label)
        {
            List<string> tagsList = LoogaInspectorQueryUtility.ToStringList(InternalEditorUtility.tags);
            tagsList.Insert(0, "None");
            string[] tagsArray = tagsList.ToArray();
            
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType == SerializedPropertyType.String)
            {
                var currentIndex = Mathf.Max(0, tagsList.IndexOf(property.stringValue));
                var newIndex = LoogaGUI.Popup(position, label.text, currentIndex, tagsArray);
                property.stringValue = tagsList[newIndex];
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "Use TagAttribute with strings only");
            }
            
            EditorGUI.EndProperty();
        }

        protected override VisualElement CreatePropertyGUI_Internal(SerializedProperty property, string label)
        {
            if (property.propertyType != SerializedPropertyType.String)
                return LoogaPropertyDrawerUi.CreateMessage("Tag is for string fields only.", HelpBoxMessageType.Warning);

            List<string> tags = LoogaDrawerOptionCache.GetOrCreate(
                "unity.tags",
                () =>
                {
                    List<string> values = LoogaInspectorQueryUtility.ToStringList(InternalEditorUtility.tags);
                    values.Insert(0, "None");
                    return values;
                });
            int selected = Mathf.Max(0, tags.IndexOf(property.stringValue));
            return LoogaPropertyDrawerUi.CreatePopup(
                property,
                label,
                tags,
                selected,
                (current, index) => current.stringValue = tags[index]);
        }
    }
}
