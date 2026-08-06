using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace LoogaSoft.PrefabBrowser.Runtime
{
    /// <summary>
    /// Stores cached editor metadata for one prefab asset.
    /// </summary>
    [Serializable]
    public sealed class PrefabData
    {
        [FormerlySerializedAs("guid")]
        [SerializeField]
        private string _guid;

        [FormerlySerializedAs("path")]
        [SerializeField]
        private string _path;

        [FormerlySerializedAs("isUI")]
        [SerializeField]
        private bool _isUi;

        [FormerlySerializedAs("isBroken")]
        [SerializeField]
        private bool _isBroken;

        [FormerlySerializedAs("labels")]
        [SerializeField]
        private List<string> _labels = new();

        /// <summary>
        /// Gets or sets the stable Unity asset identifier.
        /// </summary>
        public string Guid { get => _guid; set => _guid = value; }

        /// <summary>
        /// Gets or sets the current asset path.
        /// </summary>
        public string Path { get => _path; set => _path = value; }

        /// <summary>
        /// Gets or sets whether the prefab contains UI content.
        /// </summary>
        public bool IsUi { get => _isUi; set => _isUi = value; }

        /// <summary>
        /// Gets or sets whether the prefab contains a missing component.
        /// </summary>
        public bool IsBroken { get => _isBroken; set => _isBroken = value; }

        /// <summary>
        /// Gets or sets the cached Unity asset labels.
        /// </summary>
        public List<string> Labels { get => _labels; set => _labels = value ?? new List<string>(); }
    }

    /// <summary>
    /// Stores the generated prefab index for the current project.
    /// </summary>
    public sealed class PrefabBrowserDatabase : ScriptableObject
    {
        [FormerlySerializedAs("prefabs")]
        [SerializeField]
        private List<PrefabData> _prefabs = new();

        private static PrefabBrowserDatabase _instance;

        /// <summary>
        /// Gets the indexed prefab records.
        /// </summary>
        public List<PrefabData> Prefabs => _prefabs;

        /// <summary>
        /// Gets or creates the project-owned prefab index.
        /// </summary>
        public static PrefabBrowserDatabase GetOrCreateDatabase()
        {
            if (_instance == null)
            {
                _instance = PrefabBrowserProjectStorage.GetOrCreate<PrefabBrowserDatabase>(nameof(PrefabBrowserDatabase));
            }

            return _instance;
        }
    }
}
