using UnityEngine;
using UnityEngine.Serialization;

namespace LoogaSoft.Tools.Runtime
{
    /// <summary>
    /// Stores an indirect reference to another Unity asset.
    /// </summary>
    [CreateAssetMenu(fileName = "New Cross Reference", menuName = "LoogaSoft/Tools/Cross Reference")]
    public sealed class CrossReference : ScriptableObject
    {
        [FormerlySerializedAs("reference")]
        [SerializeField]
        private Object _reference;

        /// <summary>
        /// Gets or sets the referenced asset.
        /// </summary>
        public Object Reference
        {
            get => _reference;
            set => _reference = value;
        }
    }
}
