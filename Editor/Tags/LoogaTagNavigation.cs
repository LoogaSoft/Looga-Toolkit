using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Tags.Editor
{
    public static class LoogaTagNavigation
    {
        private static Object[] _previousSelection;

        public static void SaveSelection() => _previousSelection = Selection.objects;
        public static bool HasHistory => _previousSelection != null && _previousSelection.Length > 0;

        public static void RestoreSelection()
        {
            if (HasHistory)
            {
                Object[] validObjects = LoogaTagQueryUtility.GetValidObjects(_previousSelection);

                if (validObjects.Length > 0)
                    Selection.objects = validObjects;
            }
            
            _previousSelection = null;
        }
    }
}
