using LoogaSoft.Tags.Runtime;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Tags.Editor
{
    [InitializeOnLoad]
    internal static class LoogaTagsOverlay
    {
        private const float AddButtonHeight = 18f;
        private const float AddButtonIconSize = 10f;
        private const float AddButtonIconSpacing = 3f;
        private const string TagIconPath =
            "Packages/com.loogasoft.loogatoolkit/Editor/Inspector/Icons/Remix/price-tag-3-fill.png";

        private static Texture2D _tagIcon;
        private static UnityEditor.Editor _cachedEditor;
        
        static LoogaTagsOverlay()
        {
            UnityEditor.Editor.finishedDefaultHeaderGUI -= OnPostHeaderGUI;
            UnityEditor.Editor.finishedDefaultHeaderGUI += OnPostHeaderGUI;
        }

        private static void OnPostHeaderGUI(UnityEditor.Editor editor)
        {
            if (editor.target is GameObject)
            {
                LoogaTags tagComponent = null;
                
                if (editor.target is GameObject go)
                    go.TryGetComponent(out tagComponent);
                if (tagComponent == null)
                    DrawAddButton(editor.targets);
                else
                    DrawEmbeddedEditor(tagComponent, editor.targets);
            }
        }
        private static void DrawAddButton(Object[] targets)
        {
            if (_tagIcon == null)
                _tagIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(TagIconPath);

            GUILayout.Space(-EditorGUIUtility.standardVerticalSpacing * 2f);

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                imagePosition = ImagePosition.ImageLeft,
                alignment = TextAnchor.MiddleCenter
            };
            GUIContent content = new("Add Looga Tags", _tagIcon);
            Rect availableRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.Height(AddButtonHeight),
                GUILayout.ExpandWidth(true));

            Vector2 cachedIconSize = EditorGUIUtility.GetIconSize();
            EditorGUIUtility.SetIconSize(new Vector2(AddButtonIconSize, AddButtonIconSize));

            float preferredWidth = buttonStyle.CalcSize(new GUIContent(content.text)).x
                                   + AddButtonIconSize
                                   + AddButtonIconSpacing;
            Rect buttonRect = availableRect;
            buttonRect.width = Mathf.Min(Mathf.Ceil(preferredWidth), availableRect.width);
            buttonRect.x += (availableRect.width - buttonRect.width) * 0.5f;
            buttonRect.y += EditorGUIUtility.standardVerticalSpacing * 2f;

            if (GUI.Button(buttonRect, content, buttonStyle))
            {
                foreach (Object obj in targets)
                {
                    var go = obj as GameObject;
                    if (go == null) 
                        continue;
                    
                    if (!go.GetComponent<LoogaTags>())
                        Undo.AddComponent<LoogaTags>(go);
                }
            }
            
            EditorGUIUtility.SetIconSize(cachedIconSize);
        }

        private static void DrawEmbeddedEditor(LoogaTags tagComp, Object[] targets)
        {
            UnityEditor.Editor.CreateCachedEditor(tagComp, null, ref _cachedEditor);

            Rect rowRect = GUILayoutUtility.GetRect(GUIContent.none, GUI.skin.button, GUILayout.Height(24f));

            rowRect.y += 2f;
            rowRect.height -= 4f;

            float spacing = 4f;
            float halfWidth = (rowRect.width - spacing) / 2f;
            
            Rect clearRect = new Rect(rowRect.x, rowRect.y, halfWidth, rowRect.height);
            Rect removeRect = new Rect(rowRect.x + halfWidth + spacing, rowRect.y, halfWidth, rowRect.height);
            
            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(!HasAnyTags(targets)))
            {
                if (GUI.Button(clearRect, "Clear Tags"))
                {
                    foreach (Object obj in targets)
                    {
                        if (obj is not GameObject go ||
                            !go.TryGetComponent(out LoogaTags loogaTags) ||
                            loogaTags.TagGroup.SelectedTagGuids is not { Count: > 0 })
                        {
                            continue;
                        }

                        Undo.RecordObject(loogaTags, "Clear Looga Tags");
                        loogaTags.ClearTags();
                        EditorUtility.SetDirty(loogaTags);
                    }
                }
            }

            if (GUI.Button(removeRect, "Remove Component"))
            {
                foreach (Object obj in targets)
                {
                    var go = obj as GameObject;
                    if (go == null) 
                        continue;
                    LoogaTags loogaTags = go.GetComponent<LoogaTags>();
                    if (loogaTags != null)
                        Undo.DestroyObjectImmediate(loogaTags);
                }
                return;
            }

            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(2f);
            
            _cachedEditor.serializedObject.Update();
            
            SerializedProperty iterator = _cachedEditor.serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.name == "m_Script")
                {
                    continue;
                }

                if (iterator.name == "_tagGroup")
                {
                    EditorGUILayout.PropertyField(iterator, GUIContent.none, true);
                    break;
                }
            }
            
            _cachedEditor.serializedObject.ApplyModifiedProperties();
        }

        private static bool HasAnyTags(Object[] targets)
        {
            foreach (Object target in targets)
            {
                if (target is GameObject go &&
                    go.TryGetComponent(out LoogaTags tagsObject) &&
                    tagsObject.TagGroup.SelectedTagGuids is { Count: > 0 })
                {
                    return true;
                }
            }

            return false;
        }
    }
}
