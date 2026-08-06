using LoogaSoft.Tags.Runtime;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace LoogaSoft.Tags.Editor
{
    [CustomEditor(typeof(LoogaTagDatabase))]
    public class LoogaTagDatabaseEditor : UnityEditor.Editor
    {
        private ReorderableList _list;
        
        private readonly float _lineHeight = EditorGUIUtility.singleLineHeight;

        private void OnEnable()
        {
            _list = new ReorderableList(serializedObject, serializedObject.FindProperty("tags"), true, true, true, true);
            
            _list.drawElementCallback = (rect, index, _, _) =>
            {
                var element = _list.serializedProperty.GetArrayElementAtIndex(index);
                var nameProp = element.FindPropertyRelative("name");
                var colorProp = element.FindPropertyRelative("color");
                var guidProp = element.FindPropertyRelative("guid");

                if (string.IsNullOrEmpty(guidProp.stringValue))
                    guidProp.stringValue = System.Guid.NewGuid().ToString();

                float spacing = 5f;
                float halfWidth = (rect.width - spacing) / 2f;
                
                Rect nameRect = new Rect(rect.x, rect.y + 2f, halfWidth, _lineHeight);
                Rect colorRect = new Rect(rect.x + halfWidth + spacing, rect.y + 2f, halfWidth, _lineHeight);
                
                EditorGUI.PropertyField(nameRect, nameProp, GUIContent.none);
                EditorGUI.PropertyField(colorRect, colorProp, GUIContent.none);
            };
            
            _list.drawHeaderCallback = rect =>
            {
                float halfWidth = rect.width / 2f;
                
                EditorGUI.LabelField(new Rect(rect.x, rect.y, halfWidth, rect.height), "Looga Tags");
                EditorGUI.LabelField(new Rect(rect.x + halfWidth, rect.y, halfWidth, rect.height), "Color");
            };
            _list.onAddCallback = list =>
            {
                var index = list.serializedProperty.arraySize;
                list.serializedProperty.arraySize++;
                list.index = index;
                
                SerializedProperty element = list.serializedProperty.GetArrayElementAtIndex(index);
                
                element.FindPropertyRelative("name").stringValue = "New Tag";
                element.FindPropertyRelative("color").colorValue = Color.gray3;
                element.FindPropertyRelative("guid").stringValue = System.Guid.NewGuid().ToString();
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            SerializedProperty scriptProp = serializedObject.FindProperty("m_Script");
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(scriptProp);
            
            EditorGUILayout.Space();
            
            _list.DoLayoutList();
            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
                serializedObject.ApplyModifiedProperties();

            if (serializedObject.ApplyModifiedProperties() || GUILayout.Button("Force Save + Refresh"))
                AssetDatabase.SaveAssets();

            if (LoogaTagNavigation.HasHistory)
            {
                if (GUILayout.Button("<< Back to Previous Inspector"))
                    LoogaTagNavigation.RestoreSelection();
            }
        }
    }
}
