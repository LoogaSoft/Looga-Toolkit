using LoogaSoft.Tags.Runtime;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Tags.Editor
{
    [CustomPropertyDrawer(typeof(LoogaTag))]
    public sealed class LoogaTagDrawer : PropertyDrawer
    {
        private static GUIContent[] _tagOptions;
        private static int _cachedDatabaseId;
        private static int _cachedContentHash;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            LoogaTagDatabase database = LoogaTagManager.ValidateDatabase();
            if (database == null)
            {
                EditorGUI.LabelField(position, label.text, "Tag database is unavailable.");
                return;
            }
            
            SerializedProperty nameProp = property.FindPropertyRelative("_name");
            SerializedProperty guidProp = property.FindPropertyRelative("_guid");
            RefreshOptions(database);
            
            int databaseIndex = database.Tags.FindIndex(tag => tag.Guid == guidProp.stringValue);
            int popupIndex = databaseIndex >= 0 ? databaseIndex + 1 : 0;
            
            EditorGUI.BeginProperty(position, label, property);
            
            position = EditorGUI.PrefixLabel(position, label);

            int newPopupIndex = EditorGUI.Popup(position, popupIndex, _tagOptions);

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
                    LoogaTag selectedTag = database.Tags[selectedDbIndex];
                    
                    nameProp.stringValue = selectedTag.Name;
                    guidProp.stringValue = selectedTag.Guid;
                }
            }
            
            EditorGUI.EndProperty();
        }

        private static void RefreshOptions(LoogaTagDatabase database)
        {
            int contentHash = database.Tags.Count;
            for (int index = 0; index < database.Tags.Count; index++)
            {
                LoogaTag tag = database.Tags[index];
                contentHash = (contentHash * 397) ^ (tag.Name?.GetHashCode() ?? 0);
                contentHash = (contentHash * 397) ^ (tag.Guid?.GetHashCode() ?? 0);
            }

            if (_tagOptions != null &&
                _cachedDatabaseId == database.GetInstanceID() &&
                _cachedContentHash == contentHash)
            {
                return;
            }

            _cachedDatabaseId = database.GetInstanceID();
            _cachedContentHash = contentHash;
            _tagOptions = new GUIContent[database.Tags.Count + 1];
            _tagOptions[0] = new GUIContent("None");

            for (int index = 0; index < database.Tags.Count; index++)
            {
                string tagName = database.Tags[index].Name;
                _tagOptions[index + 1] = new GUIContent(string.IsNullOrEmpty(tagName) ? "Unnamed Tag" : tagName);
            }
        }
    }
}
