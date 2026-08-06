using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace LoogaSoft.Tags.Runtime
{
    /// <summary>
    /// Stores all tags defined by the current project.
    /// </summary>
    [MovedFrom(true, "LoogaSoft.PolyTags.Runtime", "LoogaSoft.PolyTags.Runtime", "PolyTagDatabase")]
    public sealed class LoogaTagDatabase : ScriptableObject
    {
        [FormerlySerializedAs("tags")]
        [SerializeField]
        private List<LoogaTag> _tags = new();

        /// <summary>
        /// Gets the mutable project tag collection.
        /// </summary>
        public List<LoogaTag> Tags => _tags;
    }
}
