using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace LoogaSoft.PrefabBrowser.Runtime
{
    /// <summary>
    /// Stores project-owned Prefab Browser categories.
    /// </summary>
    public sealed class PrefabBrowserConfig : ScriptableObject
    {
        [FormerlySerializedAs("categories")]
        [SerializeField]
        private List<BrowserCategory> _categories = new();

        private static PrefabBrowserConfig _instance;

        /// <summary>
        /// Gets the configured browser categories.
        /// </summary>
        public List<BrowserCategory> Categories => _categories;

        /// <summary>
        /// Gets or creates the project-owned browser configuration.
        /// </summary>
        public static PrefabBrowserConfig GetOrCreateConfig()
        {
            if (_instance == null)
            {
                _instance = PrefabBrowserProjectStorage.GetOrCreate<PrefabBrowserConfig>(nameof(PrefabBrowserConfig));
            }

            return _instance;
        }
    }
}
