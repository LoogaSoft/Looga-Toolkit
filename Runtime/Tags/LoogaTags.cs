using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace LoogaSoft.Tags.Runtime
{
    /// <summary>
    /// Assigns project-defined Looga Tags to a GameObject.
    /// </summary>
    [AddComponentMenu("LoogaSoft/Looga Tags")]
    [ExecuteAlways]
    [MovedFrom(true, "LoogaSoft.PolyTags.Runtime", "LoogaSoft.PolyTags.Runtime", "PolyTagsObject")]
    public sealed class LoogaTags : MonoBehaviour
    {
        [FormerlySerializedAs("tagGroup")]
        [SerializeField]
        private LoogaTagGroup _tagGroup;

        /// <summary>
        /// Gets the tags assigned to this GameObject.
        /// </summary>
        public LoogaTagGroup TagGroup => _tagGroup;

        /// <summary>
        /// Removes all tags from this GameObject.
        /// </summary>
        public void ClearTags()
        {
            _tagGroup.ClearTags();
        }

        private void OnEnable()
        {
            hideFlags = HideFlags.HideInInspector;
        }

        private void OnValidate()
        {
            if ((hideFlags & HideFlags.HideInInspector) == 0)
            {
                hideFlags |= HideFlags.HideInInspector;
            }
        }
    }
}
