using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    internal static class HierarchyContextMenus
    {
        internal static void Show(GameObject[] targets)
        {
            GameObject[] editableTargets = GetEditableTargets(targets);
            if (editableTargets.Length == 0)
            {
                return;
            }

            GenericMenu menu = new();
            AddFavoriteItem(menu, editableTargets);
            menu.AddSeparator(string.Empty);

            bool hasDescendants = HasDescendants(editableTargets);
            AddAction(menu, "Move Children To Parent", hasDescendants, () => MoveChildrenToParent(editableTargets));
            AddAction(menu, "Select Descendants", hasDescendants, () => SelectDescendants(editableTargets));
            AddAction(menu, "Enable Descendants", hasDescendants, () => SetDescendantsActive(editableTargets, true));
            AddAction(menu, "Disable Descendants", hasDescendants, () => SetDescendantsActive(editableTargets, false));
            menu.AddSeparator(string.Empty);
            menu.AddItem(
                new GUIContent("Bulk Rename..."),
                false,
                () => HierarchyBulkRenameWindow.Open(GetEditableTargets(editableTargets)));
            menu.ShowAsContext();
        }

        private static void AddFavoriteItem(GenericMenu menu, GameObject[] targets)
        {
            bool allFavorites = true;
            for (int index = 0; index < targets.Length; index++)
            {
                if (!HierarchyFavoriteStore.instance.Contains(targets[index]))
                {
                    allFavorites = false;
                    break;
                }
            }

            string label = allFavorites ? "Remove From Favorites" : "Add To Favorites";
            menu.AddItem(new GUIContent(label), false, () => SetFavoriteState(targets, !allFavorites));
        }

        private static void AddAction(
            GenericMenu menu,
            string label,
            bool enabled,
            GenericMenu.MenuFunction action)
        {
            GUIContent content = new(label);
            if (enabled)
            {
                menu.AddItem(content, false, action);
            }
            else
            {
                menu.AddDisabledItem(content);
            }
        }

        private static void SetFavoriteState(GameObject[] targets, bool favorite)
        {
            GameObject[] editableTargets = GetEditableTargets(targets);
            for (int index = 0; index < editableTargets.Length; index++)
            {
                GameObject target = editableTargets[index];
                if (HierarchyFavoriteStore.instance.Contains(target) != favorite)
                {
                    HierarchyFavoriteStore.instance.Toggle(target);
                }
            }
        }

        private static void MoveChildrenToParent(GameObject[] targets)
        {
            List<Transform> roots = GetTopLevelSelection(targets);
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Move Children To Parent");

            for (int rootIndex = 0; rootIndex < roots.Count; rootIndex++)
            {
                Transform root = roots[rootIndex];
                Transform destination = root.parent;
                int insertionIndex = root.GetSiblingIndex() + 1;
                List<Transform> children = new(root.childCount);

                for (int childIndex = 0; childIndex < root.childCount; childIndex++)
                {
                    children.Add(root.GetChild(childIndex));
                }

                for (int childIndex = 0; childIndex < children.Count; childIndex++)
                {
                    Undo.SetTransformParent(children[childIndex], destination, "Move Child To Parent");
                    children[childIndex].SetSiblingIndex(insertionIndex + childIndex);
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        private static void SelectDescendants(GameObject[] targets)
        {
            List<GameObject> descendants = CollectDescendants(targets);
            Selection.objects = descendants.ToArray();
        }

        private static void SetDescendantsActive(GameObject[] targets, bool active)
        {
            List<GameObject> descendants = CollectDescendants(targets);
            Undo.RecordObjects(descendants.ToArray(), active ? "Enable Descendants" : "Disable Descendants");

            for (int index = 0; index < descendants.Count; index++)
            {
                descendants[index].SetActive(active);
            }
        }

        private static List<GameObject> CollectDescendants(GameObject[] targets)
        {
            List<GameObject> descendants = new();
            HashSet<int> seen = new();
            GameObject[] editableTargets = GetEditableTargets(targets);

            for (int index = 0; index < editableTargets.Length; index++)
            {
                CollectDescendants(editableTargets[index].transform, descendants, seen);
            }

            return descendants;
        }

        private static void CollectDescendants(
            Transform parent,
            List<GameObject> results,
            HashSet<int> seen)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (seen.Add(child.gameObject.GetInstanceID()))
                {
                    results.Add(child.gameObject);
                }

                CollectDescendants(child, results, seen);
            }
        }

        private static List<Transform> GetTopLevelSelection(GameObject[] targets)
        {
            GameObject[] editableTargets = GetEditableTargets(targets);
            HashSet<Transform> selected = new();
            List<Transform> roots = new();

            for (int index = 0; index < editableTargets.Length; index++)
            {
                selected.Add(editableTargets[index].transform);
            }

            foreach (Transform transform in selected)
            {
                Transform ancestor = transform.parent;
                bool hasSelectedAncestor = false;

                while (ancestor != null)
                {
                    if (selected.Contains(ancestor))
                    {
                        hasSelectedAncestor = true;
                        break;
                    }

                    ancestor = ancestor.parent;
                }

                if (!hasSelectedAncestor)
                {
                    roots.Add(transform);
                }
            }

            return roots;
        }

        private static bool HasDescendants(GameObject[] targets)
        {
            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] != null && targets[index].transform.childCount > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static GameObject[] GetEditableTargets(GameObject[] targets)
        {
            if (targets == null || targets.Length == 0)
            {
                return System.Array.Empty<GameObject>();
            }

            List<GameObject> editable = new(targets.Length);
            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] != null)
                {
                    editable.Add(targets[index]);
                }
            }

            return editable.ToArray();
        }
    }
}
