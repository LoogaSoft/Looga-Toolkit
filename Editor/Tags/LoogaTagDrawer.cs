using System.Collections.Generic;
using System.Linq;
using LoogaSoft.Tags.Runtime;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Tags.Editor
{
    [CustomPropertyDrawer(typeof(LoogaTag))]
    public class LoogaTagDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            LoogaTagDatabase db = LoogaTagManager.ValidateDatabase();
            if (db == null) return;
            
            SerializedProperty nameProp = property.FindPropertyRelative("name");
            SerializedProperty guidProp = property.FindPropertyRelative("guid");
            
            List<string> tags = db.tags.Select(t => string.IsNullOrEmpty(t.guid) ? "Unnamed Tag" : t.name).ToList();
            tags.Insert(0, "None");
            
            int dbIndex = db.tags.FindIndex(t => t.guid == guidProp.stringValue);
            int popupIndex = (dbIndex != -1) ? dbIndex + 1 : 0;
            
            EditorGUI.BeginProperty(position, label, property);
            
            position = EditorGUI.PrefixLabel(position, label);

            int newPopupIndex = EditorGUI.Popup(position, popupIndex, tags.ToArray());

            if (newPopupIndex != popupIndex)
            {
                if (newPopupIndex == 0)
                {
                    nameProp.stringValue = string.Empty;
                    guidProp.stringValue = string.Empty;
                }
                else
                {
                    int selectedDbIndex = newPopupIndex - 1;
                    LoogaTag selectedTag = db.tags[selectedDbIndex];
                    
                    nameProp.stringValue = selectedTag.name;
                    guidProp.stringValue = selectedTag.guid;
                }
            }
            
            EditorGUI.EndProperty();
        }
    }
}
