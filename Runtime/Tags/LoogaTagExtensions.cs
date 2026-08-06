using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace LoogaSoft.Tags.Runtime
{
    [MovedFrom(true, "LoogaSoft.PolyTags.Runtime", "LoogaSoft.PolyTags.Runtime", "PolyTagsExtensions")]
    public static class LoogaTagExtensions
    {
        public static bool HasTag(this GameObject gameObject, string tagGuid)
        {
            if (gameObject == null)
                return false;

            return gameObject.TryGetComponent(out LoogaTags loogaTags) &&
                   loogaTags.tagGroup.HasTag(tagGuid);
        }
    }
}
