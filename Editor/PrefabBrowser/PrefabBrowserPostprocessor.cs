using System.Collections.Generic;
using LoogaSoft.PrefabBrowser.Runtime;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.PrefabBrowser.Editor
{
    public sealed class PrefabBrowserPostprocessor : AssetPostprocessor
    {
        [MenuItem("Window/LoogaSoft/Prefab Browser/Rebuild Database")]
        public static void RebuildDatabase()
        {
            PrefabBrowserDatabase db = PrefabBrowserDatabase.GetOrCreateDatabase();
            db.Prefabs.Clear();

            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            int count = 0;

            try
            {
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    ProcessPrefab(path, db);
                    count++;

                    if (count % 100 == 0)
                    {
                        EditorUtility.DisplayProgressBar("Rebuilding Prefab Database", 
                            $"Processing {count}/{guids.Length} prefabs...", 
                            (float)count / guids.Length);
                    }
                }

                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssetIfDirty(db);
                Debug.Log($"Successfully rebuilt Prefab Database with {count} prefabs.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
        
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (!HasPrefabChanges(importedAssets, deletedAssets, movedAssets))
            {
                return;
            }

            PrefabBrowserDatabase db = PrefabBrowserDatabase.GetOrCreateDatabase();
            bool dbChanged = false;

            foreach (string path in deletedAssets)
            {
                if (path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    dbChanged |= db.Prefabs.RemoveAll(prefab => prefab.Path == path) > 0;
                }
            }

            foreach (string path in importedAssets)
            {
                if (path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    dbChanged |= ProcessPrefab(path, db);
                }
            }

            for (int i = 0; i < movedAssets.Length; i++)
            {
                if (movedAssets[i].EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    string guid = AssetDatabase.AssetPathToGUID(movedAssets[i]);
                    PrefabData existingData = FindPrefab(db, guid);
                    if (existingData != null && existingData.Path != movedAssets[i])
                    {
                        existingData.Path = movedAssets[i];
                        dbChanged = true;
                    }
                }
            }

            if (dbChanged)
            {
                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssetIfDirty(db);
            }
        }

        private static bool HasPrefabChanges(string[] imported, string[] deleted, string[] moved)
        {
            return ContainsPrefab(imported) || ContainsPrefab(deleted) || ContainsPrefab(moved);
        }

        private static bool ProcessPrefab(string path, PrefabBrowserDatabase db)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                return false;
            }

            PrefabData data = FindPrefab(db, guid);
            bool added = data == null;
            if (data == null)
            {
                data = new PrefabData { Guid = guid };
                db.Prefabs.Add(data);
            }

            bool isUi = prefab.layer == 5;
            bool isBroken = false;
            Component[] components = prefab.GetComponentsInChildren<Component>(true);
            for (int c = 0; c < components.Length; c++)
            {
                if (components[c] == null)
                {
                    isBroken = true;
                    continue;
                }

                if (components[c] is RectTransform)
                {
                    isUi = true;
                }
            }

            string[] labels = AssetDatabase.GetLabels(prefab);
            bool changed = added ||
                           data.Path != path ||
                           data.IsUi != isUi ||
                           data.IsBroken != isBroken ||
                           !LabelsMatch(data.Labels, labels);
            if (!changed)
            {
                return false;
            }

            data.Path = path;
            data.IsUi = isUi;
            data.IsBroken = isBroken;
            data.Labels = new List<string>(labels);
            return true;
        }

        private static bool LabelsMatch(IReadOnlyList<string> cachedLabels, IReadOnlyList<string> currentLabels)
        {
            if (cachedLabels == null || cachedLabels.Count != currentLabels.Count)
            {
                return false;
            }

            for (int index = 0; index < cachedLabels.Count; index++)
            {
                if (cachedLabels[index] != currentLabels[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContainsPrefab(string[] paths)
        {
            for (int index = 0; index < paths.Length; index++)
            {
                if (paths[index].EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static PrefabData FindPrefab(PrefabBrowserDatabase database, string guid)
        {
            for (int index = 0; index < database.Prefabs.Count; index++)
            {
                PrefabData prefab = database.Prefabs[index];
                if (prefab.Guid == guid)
                {
                    return prefab;
                }
            }

            return null;
        }
    }
}
