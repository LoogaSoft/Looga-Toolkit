using System.Collections.Generic;
using System.Linq;
using LoogaSoft.PrefabBrowser.Runtime;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.PrefabBrowser.Editor
{
    public class PrefabBrowserPostprocessor : AssetPostprocessor
    {
        [MenuItem("Window/LoogaSoft/Prefab Browser/Rebuild Database")]
        public static void RebuildDatabase()
        {
            var db = PrefabBrowserDatabase.GetOrCreateDatabase();
            
            // Clear the old data to ensure there are no ghost references
            db.prefabs.Clear(); 

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

            var db = PrefabBrowserDatabase.GetOrCreateDatabase();
            bool dbChanged = false;

            // 1. Handle Deletions
            foreach (string str in deletedAssets)
            {
                if (str.EndsWith(".prefab"))
                {
                    string guid = AssetDatabase.AssetPathToGUID(str);
                    db.prefabs.RemoveAll(p => p.guid == guid);
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
                    var existingData = db.prefabs.FirstOrDefault(p => p.guid == guid);
                    if (existingData != null)
                    {
                        existingData.path = movedAssets[i];
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
            return imported.Any(p => p.EndsWith(".prefab")) || 
                   deleted.Any(p => p.EndsWith(".prefab")) || 
                   moved.Any(p => p.EndsWith(".prefab"));
        }

        private static void ProcessPrefab(string path, PrefabBrowserDatabase db)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;

            var data = db.prefabs.FirstOrDefault(p => p.guid == guid);
            if (data == null)
            {
                data = new PrefabData { guid = guid };
                db.prefabs.Add(data);
            }

            data.path = path;
            
            // UI Check
            data.isUI = prefab.layer == 5 || prefab.GetComponentInChildren<RectTransform>(true) != null;

            // Broken/Missing Script Check
            data.isBroken = false;
            Component[] components = prefab.GetComponentsInChildren<Component>(true);
            for (int c = 0; c < components.Length; c++)
            {
                if (components[c] == null)
                {
                    data.isBroken = true;
                    break;
                }
            }

            // Cache Labels
            data.labels = new List<string>(AssetDatabase.GetLabels(prefab));
        }
    }
}