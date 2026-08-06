using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoogaSoft.PrefabBrowser.Runtime
{
    [Serializable]
    public class PrefabData
    {
        public string guid;
        public string path;
        public bool isUI;
        public bool isBroken;
        public List<string> labels = new List<string>();
    }

    public class PrefabBrowserDatabase : ScriptableObject
    {
        public List<PrefabData> prefabs = new List<PrefabData>();

        private static PrefabBrowserDatabase _instance;

        public static PrefabBrowserDatabase GetOrCreateDatabase()
        {
            if (_instance == null)
                _instance = PrefabBrowserProjectStorage.GetOrCreate<PrefabBrowserDatabase>(nameof(PrefabBrowserDatabase));

            return _instance;
        }
    }
}
