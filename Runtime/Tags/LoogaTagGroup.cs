using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace LoogaSoft.Tags.Runtime
{
    /// <summary>
    /// Stores the stable identifiers assigned to one GameObject.
    /// </summary>
    [System.Serializable]
    [MovedFrom(true, "LoogaSoft.PolyTags.Runtime", "LoogaSoft.PolyTags.Runtime", "PolyTagGroup")]
    public struct LoogaTagGroup
    {
        [FormerlySerializedAs("selectedTagGuids")]
        [SerializeField]
        private List<string> _selectedTagGuids;

        /// <summary>
        /// Gets the assigned tag identifiers.
        /// </summary>
        public readonly IReadOnlyList<string> SelectedTagGuids => _selectedTagGuids;

        /// <summary>
        /// Returns true when the group contains the specified tag identifier.
        /// </summary>
        public readonly bool HasTag(string guid)
        {
            return !string.IsNullOrEmpty(guid) && _selectedTagGuids?.Contains(guid) == true;
        }

        /// <summary>
        /// Returns true when the group contains every specified tag identifier.
        /// </summary>
        public readonly bool HasTags(params string[] guids)
        {
            if (guids == null)
            {
                return false;
            }

            for (int index = 0; index < guids.Length; index++)
            {
                if (!HasTag(guids[index]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Removes every assigned tag.
        /// </summary>
        public void ClearTags()
        {
            _selectedTagGuids?.Clear();
        }
    }
}
