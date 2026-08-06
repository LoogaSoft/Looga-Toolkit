using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace LoogaSoft.PrefabBrowser.Runtime
{
    /// <summary>
    /// Defines one label-based Prefab Browser category.
    /// </summary>
    [System.Serializable]
    public sealed class BrowserCategory
    {
        [FormerlySerializedAs("name")]
        [SerializeField]
        private string _name;

        [FormerlySerializedAs("subCategories")]
        [SerializeField]
        private List<string> _subCategories = new();

        [FormerlySerializedAs("isExpanded")]
        [SerializeField]
        private bool _isExpanded = true;

        /// <summary>
        /// Gets or sets the category label and required prefab label.
        /// </summary>
        public string Name
        {
            get => _name;
            set => _name = value;
        }

        /// <summary>
        /// Gets the configured subcategory labels.
        /// </summary>
        public List<string> SubCategories => _subCategories;

        /// <summary>
        /// Gets or sets the editor foldout state.
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => _isExpanded = value;
        }
    }
}
