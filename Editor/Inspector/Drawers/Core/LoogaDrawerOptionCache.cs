using System;
using System.Collections.Generic;
using UnityEditor;

namespace LoogaSoft.Inspector.Editor
{
    [InitializeOnLoad]
    internal static class LoogaDrawerOptionCache
    {
        private static readonly Dictionary<string, object> Values = new();

        public static event Action Invalidated;

        static LoogaDrawerOptionCache()
        {
            EditorApplication.projectChanged += Invalidate;
            EditorBuildSettings.sceneListChanged += Invalidate;
            Undo.undoRedoPerformed += Invalidate;
        }

        public static T GetOrCreate<T>(string key, Func<T> factory)
        {
            if (Values.TryGetValue(key, out object value) && value is T cached)
                return cached;

            T created = factory();
            Values[key] = created;
            return created;
        }

        public static void Invalidate()
        {
            Values.Clear();
            Invalidated?.Invoke();
        }
    }
}
