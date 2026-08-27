using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    internal static class HierarchyInteractionHandler
    {
        internal static void Handle(GameObject gameObject, Rect rowRect)
        {
            Event current = Event.current;
            if (current.type != EventType.MouseDown ||
                !current.alt ||
                !rowRect.Contains(current.mousePosition))
            {
                return;
            }

            GameObject[] targets = ResolveTargets(gameObject);
            if (current.button == 0)
            {
                Rect anchor = new(current.mousePosition, Vector2.zero);
                HierarchyPresentationPopup.Open(anchor, targets);
                current.Use();
            }
            else if (current.button == 1)
            {
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
