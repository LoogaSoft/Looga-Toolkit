#if LOOGA_INSPECTOR_ZLINQ_SUPPORT
using System;
using System.Collections.Generic;
using LoogaSoft.PrefabBrowser.Editor;
using LoogaSoft.PrefabBrowser.Runtime;
using LoogaSoft.Tags.Editor;
using UnityEditor;
using UnityEngine;
using ZLinq;
using Object = UnityEngine.Object;

namespace LoogaSoft.Toolkit.Editor
{
    [InitializeOnLoad]
    internal static class LoogaToolkitSearchZLinqProvider
    {
        static LoogaToolkitSearchZLinqProvider()
        {
            PrefabBrowserQueryUtility.PrefabObjectProvider = GetPrefabObjects;
            PrefabBrowserQueryUtility.StringArrayProvider = ToStringArray;
            LoogaTagQueryUtility.ValidObjectProvider = GetValidObjects;
        }

        private static GameObject[] GetPrefabObjects(List<PrefabData> prefabs)
        {
            return prefabs == null
                ? Array.Empty<GameObject>()
                : prefabs.AsValueEnumerable()
                    .Select(prefab => AssetDatabase.LoadAssetAtPath<GameObject>(prefab.Path))
                    .ToArray();
        }

        private static string[] ToStringArray(IEnumerable<string> values)
        {
            return values == null ? Array.Empty<string>() : values.AsValueEnumerable().ToArray();
        }

        private static Object[] GetValidObjects(Object[] objects)
        {
            return objects == null
                ? Array.Empty<Object>()
                : objects.AsValueEnumerable().Where(item => item != null).ToArray();
        }
    }
}
#endif
