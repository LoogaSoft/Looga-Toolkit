using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LoogaSoft.Tags.Runtime
{
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    [MovedFrom(true, "LoogaSoft.PolyTags.Runtime", "LoogaSoft.PolyTags.Runtime", "PolyTagManager")]
    public static class LoogaTagManager
    {
        private const string DatabaseResourcePath = "LoogaSoft/LoogaTagDatabase";

#if UNITY_EDITOR
        private const string DatabaseAssetPath = "Assets/Resources/LoogaSoft/LoogaTagDatabase.asset";

        // Keep this path only long enough to migrate databases created by the former tag system.
        private const string LegacyDatabaseAssetPath = "Assets/Resources/LoogaSoft/PolyTagDatabase.asset";
#endif

        static LoogaTagManager()
        {
            ValidateDatabase();
        }

        public static LoogaTagDatabase ValidateDatabase()
        {
            LoogaTagDatabase database = Resources.Load<LoogaTagDatabase>(DatabaseResourcePath);

#if UNITY_EDITOR
            if (database == null)
                database = MigrateLegacyDatabase();

            if (database == null)
                database = CreateDatabase();
#endif

            return database;
        }

#if UNITY_EDITOR
        private static LoogaTagDatabase MigrateLegacyDatabase()
        {
            LoogaTagDatabase database = AssetDatabase.LoadAssetAtPath<LoogaTagDatabase>(LegacyDatabaseAssetPath);
            if (database == null)
                return null;

            EnsureDatabaseFolder();

            string moveError = AssetDatabase.MoveAsset(LegacyDatabaseAssetPath, DatabaseAssetPath);
            if (!string.IsNullOrEmpty(moveError))
            {
                Debug.LogWarning($"Could not migrate the Looga Tags database: {moveError}");
                return database;
            }

            database.name = nameof(LoogaTagDatabase);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            return database;
        }

        private static LoogaTagDatabase CreateDatabase()
        {
            EnsureDatabaseFolder();

            LoogaTagDatabase database = ScriptableObject.CreateInstance<LoogaTagDatabase>();
            AssetDatabase.CreateAsset(database, DatabaseAssetPath);
            AssetDatabase.SaveAssets();
            return database;
        }

        private static void EnsureDatabaseFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            if (!AssetDatabase.IsValidFolder("Assets/Resources/LoogaSoft"))
                AssetDatabase.CreateFolder("Assets/Resources", "LoogaSoft");
        }
#endif
    }
}
