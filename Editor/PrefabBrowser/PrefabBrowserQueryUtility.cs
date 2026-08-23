using System;
using System.Collections.Generic;
using LoogaSoft.PrefabBrowser.Runtime;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.PrefabBrowser.Editor
{
    /// <summary>
    /// Provides dependency-neutral collection operations for Prefab Browser editor workflows.
    /// </summary>
    public static class PrefabBrowserQueryUtility
    {
        public static Func<List<PrefabData>, GameObject[]> PrefabObjectProvider { private get; set; } =
            DefaultPrefabObjects;

        public static Func<IEnumerable<string>, string[]> StringArrayProvider { private get; set; } =
            DefaultStringArray;

        /// <summary>
        /// Loads the prefab assets represented by the supplied records.
        /// </summary>
        public static GameObject[] GetPrefabObjects(List<PrefabData> prefabs) => PrefabObjectProvider(prefabs);

        /// <summary>
        /// Materializes a sequence of strings without requiring a query-library dependency.
        /// </summary>
        public static string[] ToStringArray(IEnumerable<string> values) => StringArrayProvider(values);

        private static GameObject[] DefaultPrefabObjects(List<PrefabData> prefabs)
        {
            if (prefabs == null || prefabs.Count == 0)
                return Array.Empty<GameObject>();

            GameObject[] objects = new GameObject[prefabs.Count];
            for (int index = 0; index < prefabs.Count; index++)
                objects[index] = AssetDatabase.LoadAssetAtPath<GameObject>(prefabs[index].Path);

            return objects;
        }

        private static string[] DefaultStringArray(IEnumerable<string> values)
        {
            if (values == null)
                return Array.Empty<string>();

            if (values is ICollection<string> collection)
            {
                string[] result = new string[collection.Count];
                collection.CopyTo(result, 0);
                return result;
            }

            List<string> resultList = new();
            foreach (string value in values)
                resultList.Add(value);

            return resultList.ToArray();
        }
    }
}
