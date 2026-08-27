using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    internal static class HierarchyInteractionHandler
    {
        private static int _pendingAltRightClickId;

        internal static void Handle(GameObject gameObject, Rect rowRect)
        {
            Event current = Event.current;
            if (!rowRect.Contains(current.mousePosition))
            {
                return;
            }

            if (current.type == EventType.MouseDown)
            {
                if (current.button == 0 && current.alt)
                {
                    GameObject[] targets = ResolveTargets(gameObject);
                    Rect anchor = new(current.mousePosition, Vector2.zero);
                    HierarchyPresentationPopup.Open(anchor, targets);
                    current.Use();
                    return;
                }

                _pendingAltRightClickId = current.button == 1 && current.alt
                    ? gameObject.GetInstanceID()
                    : 0;
            }
            else if (current.type == EventType.ContextClick)
            {
                bool showLoogaMenu = current.alt ||
                    _pendingAltRightClickId == gameObject.GetInstanceID();
                _pendingAltRightClickId = 0;
                if (!showLoogaMenu)
                {
                    return;
                }

                GameObject[] targets = ResolveTargets(gameObject);
                HierarchyContextMenus.Show(targets);
                current.Use();
            }
        }

        private static GameObject[] ResolveTargets(GameObject clickedObject)
        {
            GameObject[] selection = Selection.gameObjects;
            for (int index = 0; index < selection.Length; index++)
            {
                if (selection[index] == clickedObject)
                {
                    return GetValidSelection(selection);
                }
            }

            Selection.activeGameObject = clickedObject;
            return new[] { clickedObject };
        }

        private static GameObject[] GetValidSelection(GameObject[] selection)
        {
            List<GameObject> valid = new(selection.Length);
            for (int index = 0; index < selection.Length; index++)
            {
                if (selection[index] != null)
                {
                    valid.Add(selection[index]);
                }
            }

            return valid.ToArray();
        }
    }
}
