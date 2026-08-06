using System;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.PrefabBrowser.Runtime
{
    /// <summary>
    /// Stores mutable Prefab Browser data in the consuming project.
    /// </summary>
    internal static class PrefabBrowserProjectStorage
    {
        public const string ProjectDirectory = "Assets/Shared/Editor/Prefab Browser";

        public static T GetOrCreate<T>(string fileName) where T : ScriptableObject
        {
            string preferredPath = $"{ProjectDirectory}/{fileName}.asset";
            T asset = AssetDatabase.LoadAssetAtPath<T>(preferredPath);
            if (asset != null)
            {
                return asset;
            }

            asset = FindExistingProjectAsset<T>();
            if (asset != null)
            {
                return asset;
            }

            EnsureProjectDirectory();
            asset = ScriptableObject.CreateInstance<T>();
            asset.name = fileName;
            AssetDatabase.CreateAsset(asset, preferredPath);
            AssetDatabase.SaveAssetIfDirty(asset);
            return asset;
        }

        private static T FindExistingProjectAsset<T>() where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets(
                $"t:{typeof(T).Name}",
                new[] { "Assets" });

            Array.Sort(guids, StringComparer.Ordinal);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    return asset;
                }
            }

            return null;
        }

        private static void EnsureProjectDirectory()
        {
            string[] segments = ProjectDirectory.Split('/');
            string parent = segments[0];

            for (int i = 1; i < segments.Length; i++)
            {
                string child = $"{parent}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(child))
                {
                    AssetDatabase.CreateFolder(parent, segments[i]);
                }

                parent = child;
            }
        }
    }
}
