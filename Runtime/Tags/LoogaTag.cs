using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace LoogaSoft.Tags.Runtime
{
    /// <summary>
    /// Defines one project tag and its editor presentation.
    /// </summary>
    [System.Serializable]
    [MovedFrom(true, "LoogaSoft.PolyTags.Runtime", "LoogaSoft.PolyTags.Runtime", "PolyTag")]
    public struct LoogaTag
    {
        [FormerlySerializedAs("name")]
        [SerializeField]
        private string _name;

        [FormerlySerializedAs("color")]
        [SerializeField]
        [ColorUsage(false, false)]
        private Color _color;

        [FormerlySerializedAs("guid")]
        [SerializeField]
        private string _guid;

        /// <summary>
        /// Gets or sets the designer-facing tag name.
        /// </summary>
        public string Name
        {
            readonly get => _name;
            set => _name = value;
        }

        /// <summary>
        /// Gets or sets the tag color used by editor controls.
        /// </summary>
        public Color Color
        {
            readonly get => _color;
            set => _color = value;
        }

        /// <summary>
        /// Gets or sets the stable tag identifier.
        /// </summary>
        public string Guid
        {
            readonly get => _guid;
            set => _guid = value;
        }
    }
}
