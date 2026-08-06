using UnityEngine;

namespace LoogaSoft.Tools.Runtime
{
    [CreateAssetMenu(fileName = "New Cross Reference", menuName = "LoogaSoft/Tools/Cross Reference")]
    public class CrossReference : ScriptableObject
    {
        public Object reference;
    }
}
