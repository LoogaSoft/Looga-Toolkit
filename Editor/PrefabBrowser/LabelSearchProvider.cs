using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace LoogaSoft.PrefabBrowser.Editor
{
    public class LabelSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        public Action<string> onLabelSelect;

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Labels"), 0)
            };

            string[] allLabels = GetAllLabels();

            foreach (string label in allLabels)
            {
                tree.Add(new SearchTreeEntry(new GUIContent(label))
                {
                    level = 1,
                    userData = label
                });
            }

            return tree;
        }

        public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            onLabelSelect?.Invoke(searchTreeEntry.userData as string);
            return true;
        }

        private string[] GetAllLabels()
        {
            Type assetDatabaseType = typeof(AssetDatabase);
            MethodInfo method =
                assetDatabaseType.GetMethod("GetAllLabels", BindingFlags.Static | BindingFlags.NonPublic);

            if (method != null)
            {
                Dictionary<string, float> labels = method.Invoke(null, null) as Dictionary<string, float>;
                return PrefabBrowserQueryUtility.ToStringArray(labels?.Keys);
            }

            Debug.LogError("Unable to get labels from AssetDatabase");
            return Array.Empty<string>();
        }
    }
}
