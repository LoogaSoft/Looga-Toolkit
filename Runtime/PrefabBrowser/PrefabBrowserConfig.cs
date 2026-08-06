using System.Collections.Generic;
using UnityEngine;

namespace LoogaSoft.PrefabBrowser.Runtime
{
    public class PrefabBrowserConfig : ScriptableObject
    {
        public List<BrowserCategory> categories = new();

        private static PrefabBrowserConfig _instance;

        public static PrefabBrowserConfig GetOrCreateConfig()
        {
            if (_instance == null)
                _instance = PrefabBrowserProjectStorage.GetOrCreate<PrefabBrowserConfig>(nameof(PrefabBrowserConfig));

            return _instance;
        }
    }
}
