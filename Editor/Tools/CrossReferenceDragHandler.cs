using LoogaSoft.Tools.Runtime;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Tools.Editor
{
    [InitializeOnLoad]
    internal static class CrossReferenceDragHandler
    {
        static CrossReferenceDragHandler()
        {
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
        }

        private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
        {
            Event currentEvent = Event.current;
            if (currentEvent == null)
            {
                return;
            }

            if (currentEvent.type == EventType.DragUpdated)
            {
                UnwrapCrossReference();
            }
        }
        private static void UnwrapCrossReference()
        {
            Object[] references = DragAndDrop.objectReferences;
            if (references == null || references.Length == 0)
            {
                return;
            }

            bool needsUnwrap = false;
            for (int index = 0; index < references.Length; index++)
            {
                if (references[index] is CrossReference { Reference: not null })
                {
                    needsUnwrap = true;
                    break;
                }
            }

            if (!needsUnwrap)
            {
                return;
            }

            Object[] unwrappedReferences = new Object[references.Length];
            for (int index = 0; index < references.Length; index++)
            {
                if (references[index] is CrossReference { Reference: not null } crossReference)
                {
                    unwrappedReferences[index] = crossReference.Reference;
                }
                else
                {
                    unwrappedReferences[index] = references[index];
                }
            }

            DragAndDrop.objectReferences = unwrappedReferences;
        }
    }
}
