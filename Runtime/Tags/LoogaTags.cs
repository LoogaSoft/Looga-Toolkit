using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace LoogaSoft.Tags.Runtime
{
    [AddComponentMenu("LoogaSoft/Looga Tags")]
    [ExecuteAlways]
    [MovedFrom(true, "LoogaSoft.PolyTags.Runtime", "LoogaSoft.PolyTags.Runtime", "PolyTagsObject")]
    public class LoogaTags : MonoBehaviour
    {
        public LoogaTagGroup tagGroup;

        private void OnEnable()
        {
            hideFlags = HideFlags.HideInInspector;
        }

        private void OnValidate()
        {
            if ((hideFlags & HideFlags.HideInInspector) == 0)
                hideFlags |= HideFlags.HideInInspector;
        }
    }
}
