using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace LoogaSoft.Tags.Runtime
{
    /// <summary>
    /// Provides GameObject queries for Looga Tags.
    /// </summary>
    [MovedFrom(true, "LoogaSoft.PolyTags.Runtime", "LoogaSoft.PolyTags.Runtime", "PolyTagsExtensions")]
    public static class LoogaTagExtensions
    {
        /// <summary>
        /// Returns true when the GameObject has the specified Looga Tag identifier.
        /// </summary>
        public static bool HasTag(this GameObject gameObject, string tagGuid)
        {
            if (gameObject == null)
            {
                return false;
            }

            return gameObject.TryGetComponent(out LoogaTags loogaTags) &&
                   loogaTags.TagGroup.HasTag(tagGuid);
        }
    }
}
