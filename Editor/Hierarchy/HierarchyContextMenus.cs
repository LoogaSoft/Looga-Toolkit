using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    internal static class HierarchyContextMenus
    {
        internal static void Show(GameObject[] targets, Vector2 screenPosition)
        {
            GameObject[] editableTargets = GetEditableTargets(targets);
            if (editableTargets.Length == 0)
            {
                return;
            }

            int[] targetIds = new int[editableTargets.Length];
            for (int index = 0; index < editableTargets.Length; index++)
            {
                targetIds[index] = editableTargets[index].GetInstanceID();
            }

            EditorApplication.delayCall += () =>
                HierarchyContextMenuWindow.Open(targetIds, screenPosition);
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

        private static bool AreAllFavorites(GameObject[] targets)
        {
            for (int index = 0; index < targets.Length; index++)
            {
                if (!HierarchyFavoriteStore.instance.Contains(targets[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private sealed class HierarchyContextMenuWindow : EditorWindow
        {
            private const float Width = 210f;
            private const float ItemHeight = 21f;
            private const float SeparatorHeight = 7f;
            private const float Padding = 4f;
            private const int ItemCount = 6;

            private static HierarchyContextMenuWindow _activeWindow;

            private int[] _targetIds = System.Array.Empty<int>();
            private GUIStyle _itemStyle;

            internal static void Open(int[] targetIds, Vector2 screenPosition)
            {
                if (targetIds == null || targetIds.Length == 0)
                {
                    return;
                }

                if (_activeWindow != null)
                {
                    _activeWindow.Close();
                }

                HierarchyContextMenuWindow window = CreateInstance<HierarchyContextMenuWindow>();
                window._targetIds = targetIds;
                window.hideFlags = HideFlags.HideAndDontSave;
                _activeWindow = window;

                float height = Padding * 2f + ItemCount * ItemHeight + SeparatorHeight * 2f;
                window.ShowAsDropDown(
                    new Rect(screenPosition.x, screenPosition.y, 1f, 1f),
                    new Vector2(Width, height));
                window.Focus();
            }

            private void OnGUI()
            {
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                {
                    Close();
                    Event.current.Use();
                    return;
                }

                GameObject[] targets = ResolveTargets();
                if (targets.Length == 0)
                {
                    Close();
                    return;
                }

                bool allFavorites = AreAllFavorites(targets);
                bool hasDescendants = HasDescendants(targets);

                GUILayout.Space(Padding);
                DrawItem(
                    allFavorites ? "Remove From Favorites" : "Add To Favorites",
                    true,
                    () => SetFavoriteState(targets, !allFavorites));
                DrawSeparator();
                DrawItem("Move Children To Parent", hasDescendants, () => MoveChildrenToParent(targets));
                DrawItem("Select Descendants", hasDescendants, () => SelectDescendants(targets));
                DrawItem("Enable Descendants", hasDescendants, () => SetDescendantsActive(targets, true));
                DrawItem("Disable Descendants", hasDescendants, () => SetDescendantsActive(targets, false));
                DrawSeparator();
                DrawItem("Bulk Rename...", true, () => HierarchyBulkRenameWindow.Open(targets));
            }

            private void OnDisable()
            {
                if (_activeWindow == this)
                {
                    _activeWindow = null;
                }
            }

            private void DrawItem(string label, bool enabled, System.Action action)
            {
                Rect itemRect = GUILayoutUtility.GetRect(Width - Padding * 2f, ItemHeight);
                bool hovered = enabled && itemRect.Contains(Event.current.mousePosition);
                if (hovered && Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(itemRect, new Color(0.24f, 0.49f, 0.82f, 0.72f));
                }

                using (new EditorGUI.DisabledScope(!enabled))
                {
                    if (!GUI.Button(itemRect, label, ItemStyle))
                    {
                        return;
                    }
                }

                Close();
                action();
                GUIUtility.ExitGUI();
            }

            private static void DrawSeparator()
            {
                Rect separatorRect = GUILayoutUtility.GetRect(1f, SeparatorHeight);
                if (Event.current.type == EventType.Repaint)
                {
                    float y = Mathf.Floor(separatorRect.center.y);
                    EditorGUI.DrawRect(
                        new Rect(separatorRect.x + 2f, y, separatorRect.width - 4f, 1f),
                        new Color(0f, 0f, 0f, 0.34f));
                }
            }

            private GameObject[] ResolveTargets()
            {
                List<GameObject> targets = new(_targetIds.Length);
                for (int index = 0; index < _targetIds.Length; index++)
                {
#pragma warning disable CS0618
                    GameObject target = EditorUtility.InstanceIDToObject(_targetIds[index]) as GameObject;
#pragma warning restore CS0618
                    if (target != null)
                    {
                        targets.Add(target);
                    }
                }

                return targets.ToArray();
            }

            private GUIStyle ItemStyle
            {
                get
                {
                    _itemStyle ??= new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        padding = new RectOffset(8, 4, 0, 0),
                        fixedHeight = ItemHeight
                    };
                    return _itemStyle;
                }
            }
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
