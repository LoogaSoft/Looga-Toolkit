using LoogaSoft.Tools.Runtime;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Tools.Editor
{
    [InitializeOnLoad]
    public static class CrossReferenceDragHandler
    {
        static CrossReferenceDragHandler()
        {
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
        }

        private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
        {
            Event e = Event.current;
            if (e == null) return;

            if (e.type == EventType.DragUpdated)
                UnwrapCrossReference();
        }
        private static void UnwrapCrossReference()
        {
            var refs = DragAndDrop.objectReferences;
            if (refs == null || refs.Length == 0) return;

            bool needsUnwrap = false;
            foreach (var refObj in refs)
            {
                if (refObj is CrossReference cr && cr.reference != null)
                {
                    needsUnwrap = true;
                    break;
                }
            }
            
            if (!needsUnwrap) return;
            
            Object[] newRefs = new Object[refs.Length];
            for (int i = 0; i < refs.Length; i++)
            {
                if (refs[i] is CrossReference cr && cr.reference != null)
                    newRefs[i] = cr.reference;
                else
                    newRefs[i] = refs[i];
            }
            
            DragAndDrop.objectReferences = newRefs;
        }
    }
}