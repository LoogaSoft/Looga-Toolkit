using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LoogaSoft.Hierarchy.Editor
{
    internal static class HierarchyObjectId
    {
        private const char LocatorSeparator = '|';

        private static readonly Dictionary<int, string> GlobalIdCache = new();
        private static readonly Dictionary<int, string> LocatorCache = new();

        static HierarchyObjectId()
        {
            EditorApplication.hierarchyChanged += ClearCache;
        }

        internal static string Get(GameObject gameObject)
        {
            int instanceId = gameObject.GetInstanceID();
            if (!GlobalIdCache.TryGetValue(instanceId, out string objectId))
            {
                objectId = GlobalObjectId.GetGlobalObjectIdSlow(gameObject).ToString();
                GlobalIdCache[instanceId] = objectId;
            }

            return objectId;
        }

        /// <summary>
        /// Returns a reload-safe locator for scene objects whose Unity local file ID has not
        /// stabilized yet. The global ID remains authoritative once the scene has been saved.
        /// </summary>
        internal static string GetLocator(GameObject gameObject)
        {
            int instanceId = gameObject.GetInstanceID();
            if (LocatorCache.TryGetValue(instanceId, out string locator))
            {
                return locator;
            }

            Scene scene = gameObject.scene;
            string sceneIdentity = GetSceneIdentity(scene);
            List<int> siblingPath = new();

            Transform current = gameObject.transform;
            while (current != null)
            {
                siblingPath.Add(current.GetSiblingIndex());
                current = current.parent;
            }

            siblingPath.Reverse();
            locator = $"{sceneIdentity}{LocatorSeparator}{string.Join("/", siblingPath)}";
            LocatorCache[instanceId] = locator;
            return locator;
        }

        internal static GameObject Resolve(string value)
        {
            if (string.IsNullOrEmpty(value) ||
                !GlobalObjectId.TryParse(value, out GlobalObjectId objectId))
            {
                return null;
            }

            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(objectId) as GameObject;
        }

        internal static GameObject ResolveLocator(string locator)
        {
            if (string.IsNullOrEmpty(locator))
            {
                return null;
            }

            int separatorIndex = locator.LastIndexOf(LocatorSeparator);
            if (separatorIndex <= 0 || separatorIndex >= locator.Length - 1)
            {
                return null;
            }

            Scene scene = ResolveScene(locator[..separatorIndex]);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            string[] pathSegments = locator[(separatorIndex + 1)..].Split('/');
            if (pathSegments.Length == 0 || !int.TryParse(pathSegments[0], out int rootIndex))
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            if (rootIndex < 0 || rootIndex >= roots.Length)
            {
                return null;
            }

            Transform current = roots[rootIndex].transform;
            for (int index = 1; index < pathSegments.Length; index++)
            {
                if (!int.TryParse(pathSegments[index], out int childIndex) ||
                    childIndex < 0 ||
                    childIndex >= current.childCount)
                {
                    return null;
                }

                current = current.GetChild(childIndex);
            }

            return current.gameObject;
        }

        private static string GetSceneIdentity(Scene scene)
        {
            if (!string.IsNullOrEmpty(scene.path))
            {
                string guid = AssetDatabase.AssetPathToGUID(scene.path);
                if (!string.IsNullOrEmpty(guid))
                {
                    return $"guid:{guid}";
                }

                return $"path:{scene.path}";
            }

            return $"unsaved:{scene.name}";
        }

        private static Scene ResolveScene(string identity)
        {
            string expectedPath = identity.StartsWith("guid:")
                ? AssetDatabase.GUIDToAssetPath(identity[5..])
                : identity.StartsWith("path:")
                    ? identity[5..]
                    : null;

            string expectedName = identity.StartsWith("unsaved:")
                ? identity[8..]
                : null;

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if ((!string.IsNullOrEmpty(expectedPath) && scene.path == expectedPath) ||
                    (expectedName != null && string.IsNullOrEmpty(scene.path) && scene.name == expectedName))
                {
                    return scene;
                }
            }

            return default;
        }

        private static void ClearCache()
        {
            GlobalIdCache.Clear();
            LocatorCache.Clear();
        }
    }
}
