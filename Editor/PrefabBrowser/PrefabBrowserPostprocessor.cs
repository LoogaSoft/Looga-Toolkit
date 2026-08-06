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
            
            // Clear the old data to ensure there are no ghost references
            db.Prefabs.Clear();

            // Grab every single prefab in the project
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            int count = 0;

            try
            {
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    ProcessPrefab(path, db);
                    count++;

                    // Keep the editor responsive with a progress bar for massive projects
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
            // Early exit if no prefabs were touched to avoid loading the DB unnecessarily
            if (!HasPrefabChanges(importedAssets, deletedAssets, movedAssets))
                return;

            PrefabBrowserDatabase db = PrefabBrowserDatabase.GetOrCreateDatabase();
            bool dbChanged = false;

            // 1. Handle Deletions
            foreach (string str in deletedAssets)
            {
                if (str.EndsWith(".prefab"))
                {
                    string guid = AssetDatabase.AssetPathToGUID(str);
                    db.Prefabs.RemoveAll(prefab => prefab.Guid == guid);
                    dbChanged = true;
                }
            }

            // 2. Handle Imports (New and Modified Prefabs)
            foreach (string str in importedAssets)
            {
                if (str.EndsWith(".prefab"))
                {
                    ProcessPrefab(str, db);
                    dbChanged = true;
                }
            }

            // 3. Handle Moves / Renames
            for (int i = 0; i < movedAssets.Length; i++)
            {
                if (movedAssets[i].EndsWith(".prefab"))
                {
                    string guid = AssetDatabase.AssetPathToGUID(movedAssets[i]);
                    PrefabData existingData = FindPrefab(db, guid);
                    if (existingData != null)
                    {
                        existingData.Path = movedAssets[i];
                        dbChanged = true;
                    }
                }
            }

            // Save the database asset if we made any changes
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

        private static void ProcessPrefab(string path, PrefabBrowserDatabase db)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                return;
            }

            PrefabData data = FindPrefab(db, guid);
            if (data == null)
            {
                data = new PrefabData { Guid = guid };
                db.Prefabs.Add(data);
            }

            data.Path = path;
            
            // UI Check
            data.IsUi = prefab.layer == 5 || prefab.GetComponentInChildren<RectTransform>(true) != null;

            // Broken/Missing Script Check
            data.IsBroken = false;
            Component[] components = prefab.GetComponentsInChildren<Component>(true);
            for (int c = 0; c < components.Length; c++)
            {
                if (components[c] == null)
                {
                    data.IsBroken = true;
                    break;
                }
            }

            // Cache Labels
            data.Labels = new List<string>(AssetDatabase.GetLabels(prefab));
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
