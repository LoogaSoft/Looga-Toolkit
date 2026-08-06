using System;
using UnityEngine;

namespace LoogaSoft.PrefabBrowser.Runtime
{
    /// <summary>
    /// Resolves mutable Prefab Browser data from the consuming project. Package
    /// contents are templates and code; project-specific configuration and the
    /// generated prefab index belong under Assets so package updates cannot replace them.
    /// </summary>
    internal static class PrefabBrowserProjectStorage
    {
        public const string ProjectDirectory = "Assets/Shared/Editor/Prefab Browser";

        public static T GetOrCreate<T>(string fileName) where T : ScriptableObject
        {
#if UNITY_EDITOR
            string preferredPath = $"{ProjectDirectory}/{fileName}.asset";
            T asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(preferredPath);
            if (asset != null)
                return asset;

            asset = FindExistingProjectAsset<T>();
            if (asset != null)
                return asset;

            EnsureProjectDirectory();
            asset = ScriptableObject.CreateInstance<T>();
            asset.name = fileName;
            UnityEditor.AssetDatabase.CreateAsset(asset, preferredPath);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(asset);
            return asset;
#else
            return null;
#endif
        }

#if UNITY_EDITOR
        private static T FindExistingProjectAsset<T>() where T : ScriptableObject
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets(
                $"t:{typeof(T).Name}",
                new[] { "Assets" });

            Array.Sort(guids, StringComparer.Ordinal);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                T asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                    return asset;
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
                if (!UnityEditor.AssetDatabase.IsValidFolder(child))
                    UnityEditor.AssetDatabase.CreateFolder(parent, segments[i]);

                parent = child;
            }
        }
#endif
    }
}
