using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LoogaSoft.PrefabBrowser.Editor
{
    public class AssetLabeler : EditorWindow
    {
        private string _searchPath = "Assets";
        private string _label = "";
        private LabelSearchProvider _labelSearchProvider;

        [MenuItem("Window/LoogaSoft/Prefab Browser/Asset Labeler")]
        public static void ShowWindow()
        {
            GetWindow(typeof(AssetLabeler));
        }
        private void OnEnable()
        {
            if (_labelSearchProvider == null)
            {
                _labelSearchProvider = CreateInstance<LabelSearchProvider>();
            }
            
            _labelSearchProvider.onLabelSelect = (label) =>
            {
                _label = label;
                Repaint();
            };
        }
        private void OnGUI()
        {
            GUILayout.Label("Asset Labeler", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            _searchPath = EditorGUILayout.TextField("Search Path", _searchPath);
            
            if (GUILayout.Button("...", GUILayout.Width(30)))
                BrowseFolder();
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button(string.IsNullOrEmpty(_label) ? "Select Label" : _label, EditorStyles.popup))
            {
                Vector2 mousePos = Event.current.mousePosition;
                Vector2 screenPos = GUIUtility.GUIToScreenPoint(mousePos);
                SearchWindow.Open(new SearchWindowContext(screenPos), _labelSearchProvider);
            }

            if (GUILayout.Button("Apply Labels", GUILayout.Height(40)))
            {
                ApplyLabels();
            } 
            if (GUILayout.Button("Remove Labels", GUILayout.Height(40)))
            {
                ApplyLabels(true);
            }
        }
        private void BrowseFolder()
        {
            string path = EditorUtility.OpenFolderPanel("Select Folder", "Assets", "");

            if (!string.IsNullOrEmpty(path))
            {
                if (path.StartsWith(Application.dataPath)) 
                    _searchPath = "Assets" + path.Substring(Application.dataPath.Length);
                else
                    EditorUtility.DisplayDialog("Error", "Please select a folder inside the Assets folder", "Ok");
            }
        }

        private void ApplyLabels() => ApplyLabels(false);
        private void ApplyLabels(bool removeLabels)
        {
            if (string.IsNullOrEmpty(_label))
            {
                EditorUtility.DisplayDialog("Error", "Please select a label", "Ok");
                return;
            }
            
            string[] guids = AssetDatabase.FindAssets("", new[] { _searchPath });
            int count = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);

                    if (asset != null)
                    {
                        string[] currentLabels = AssetDatabase.GetLabels(asset);
                        if (!currentLabels.Contains(_label) && !removeLabels)
                        {
                            List<string> newLabels = new List<string>(currentLabels);
                            newLabels.Add(_label);
                            AssetDatabase.SetLabels(asset, newLabels.ToArray());
                            count++;
                        }
                        else if (removeLabels && currentLabels.Contains(_label))
                        {
                            List<string> newLabels = new List<string>(currentLabels);
                            newLabels.Remove(_label);
                            AssetDatabase.SetLabels(asset, newLabels.ToArray());
                            count++;
                        }
                    }

                    if (count % 50 == 0)
                    {
                        string progressPrefix = removeLabels ? "Removing" : "Applying";
                        EditorUtility.DisplayProgressBar($"{progressPrefix} Labels", $"Applied {_label} label to {count} assets",
                            count / (float)guids.Length);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }
            
            string completedPrefix = removeLabels ? "Removed" : "Applied";
            Debug.Log($"{completedPrefix} {_label} label to {count} assets");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}