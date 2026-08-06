using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace LoogaSoft.Tags.Runtime
{
    [MovedFrom(true, "LoogaSoft.PolyTags.Runtime", "LoogaSoft.PolyTags.Runtime", "PolyTagDatabase")]
    public class LoogaTagDatabase : ScriptableObject
    {
        public List<LoogaTag> tags = new();
    }
}
