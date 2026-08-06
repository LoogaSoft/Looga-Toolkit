using System;
using LoogaSoft.PrefabBrowser.Runtime;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.PrefabBrowser.Editor
{
    public class PrefabBrowserConfigWindow : EditorWindow
    {
        private PrefabBrowserConfig _settings;
        private Vector2 _scrollPos;

        [MenuItem("Window/LoogaSoft/Prefab Browser/Browser Config")]
        public static void ShowWindow()
        {
            GetWindow<PrefabBrowserConfigWindow>("Prefab Browser Config");
        }
        private void OnEnable()
        {
            _settings = PrefabBrowserConfig.GetOrCreateConfig();
        }

        private void OnGUI()
        {
            if (_settings == null)
            {
                EditorGUILayout.HelpBox("Config not found", MessageType.Error);
                return;
            }
            
            EditorGUILayout.LabelField("Category Manager", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            for (int i = 0; i < _settings.Categories.Count; i++)
            {
                DrawCategory(_settings.Categories[i], i);
                GUILayout.Space(5);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Add New Category", GUILayout.Height(30)))
            {
                Undo.RecordObject(_settings, "Add Category");
                _settings.Categories.Add(new BrowserCategory { Name = "New Category" } );
                EditorUtility.SetDirty(_settings);
            }
            
            EditorGUILayout.EndScrollView();
            
            if (GUI.changed)
                EditorUtility.SetDirty(_settings);
        }

        private void DrawCategory(BrowserCategory category, int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            
            category.IsExpanded = EditorGUILayout.Foldout(category.IsExpanded, category.Name, true);

            Color oldColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("X", GUILayout.Width(25f)))
            {
                Undo.RecordObject(_settings, "Remove Category");
                _settings.Categories.RemoveAt(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            
            GUI.backgroundColor = oldColor;
            
            EditorGUILayout.EndHorizontal();
            
            if (category.IsExpanded)
            {
                EditorGUI.indentLevel++;
                
                string newName = EditorGUILayout.TextField("Category Name", category.Name);
                if (newName != category.Name)
                {
                    Undo.RecordObject(_settings, "Rename Category");
                    category.Name = newName;
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Subcategories", EditorStyles.miniBoldLabel);

                for (int i = 0; i < category.SubCategories.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    category.SubCategories[i] = EditorGUILayout.TextField(category.SubCategories[i]);

                    if (GUILayout.Button("-", GUILayout.Width(25f)))
                    {
                        Undo.RecordObject(_settings, "Remove Subcategory");
                        category.SubCategories.RemoveAt(i);
                        break;
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }

                if (GUILayout.Button("Add Subcategory"))
                {
                    Undo.RecordObject(_settings, "Add Subcategory");
                    category.SubCategories.Add("New Subcategory");
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
        }
    }
}










