using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace LoogaSoft.Tags.Runtime
{
    [System.Serializable]
    [MovedFrom(true, "LoogaSoft.PolyTags.Runtime", "LoogaSoft.PolyTags.Runtime", "PolyTag")]
    public struct LoogaTag
    {
        public string name;

        [ColorUsage(false, false)]
        public Color color;

        public string guid;
    }
}
