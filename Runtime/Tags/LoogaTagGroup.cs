using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

namespace LoogaSoft.Tags.Runtime
{
    [System.Serializable]
    [MovedFrom(true, "LoogaSoft.PolyTags.Runtime", "LoogaSoft.PolyTags.Runtime", "PolyTagGroup")]
    public struct LoogaTagGroup
    {
        public List<string> selectedTagGuids;

        public bool HasTag(string guid) => selectedTagGuids?.Contains(guid) == true;

        public bool HasTags(params string[] guids)
        {
            foreach (string guid in guids)
            {
                if (!HasTag(guid))
                    return false;
            }

            return true;
        }

        public void ClearTags() => selectedTagGuids?.Clear();
    }
}
