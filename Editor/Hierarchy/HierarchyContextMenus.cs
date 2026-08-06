using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    internal static class HierarchyContextMenus
    {
        private const string Root = "GameObject/Looga Hierarchy/";

        private static readonly Color Blue = new(0.22f, 0.52f, 0.82f, 1f);
        private static readonly Color Green = new(0.28f, 0.66f, 0.46f, 1f);
        private static readonly Color Amber = new(0.92f, 0.64f, 0.20f, 1f);
        private static readonly Color Red = new(0.82f, 0.34f, 0.32f, 1f);
        private static readonly Color Purple = new(0.62f, 0.43f, 0.82f, 1f);
        private static readonly Color Gray = new(0.52f, 0.56f, 0.61f, 1f);

        [MenuItem(Root + "Toggle Favorite", false, 0)]
        private static void ToggleFavorite()
        {
            GameObject[] selection = GetEditableSelection();
            for (int index = 0; index < selection.Length; index++)
            {
                HierarchyFavoriteStore.instance.Toggle(selection[index]);
            }
        }

        [MenuItem(Root + "Toggle Favorite", true)]
        [MenuItem(Root + "Color/Blue", true)]
        [MenuItem(Root + "Color/Green", true)]
        [MenuItem(Root + "Color/Amber", true)]
        [MenuItem(Root + "Color/Red", true)]
        [MenuItem(Root + "Color/Purple", true)]
        [MenuItem(Root + "Color/Gray", true)]
        [MenuItem(Root + "Color/Custom...", true)]
        [MenuItem(Root + "Color/Default", true)]
        [MenuItem(Root + "Move Children To Parent", true)]
        [MenuItem(Root + "Select Descendants", true)]
        [MenuItem(Root + "Enable Descendants", true)]
        [MenuItem(Root + "Disable Descendants", true)]
        [MenuItem(Root + "Bulk Rename...", true)]
        private static bool ValidateSelection()
        {
            return GetEditableSelection().Length > 0;
        }

        [MenuItem(Root + "Color/Blue", false, 1)]
        private static void SetBlueObjectColor() => SetObjectColor(Blue);

        [MenuItem(Root + "Color/Green", false, 2)]
        private static void SetGreenObjectColor() => SetObjectColor(Green);

        [MenuItem(Root + "Color/Amber", false, 3)]
        private static void SetAmberObjectColor() => SetObjectColor(Amber);

        [MenuItem(Root + "Color/Red", false, 4)]
        private static void SetRedObjectColor() => SetObjectColor(Red);

        [MenuItem(Root + "Color/Purple", false, 5)]
        private static void SetPurpleObjectColor() => SetObjectColor(Purple);

        [MenuItem(Root + "Color/Gray", false, 6)]
        private static void SetGrayObjectColor() => SetObjectColor(Gray);

        [MenuItem(Root + "Color/Custom...", false, 7)]
        private static void SetCustomObjectColor()
        {
            HierarchyColorWindow.Open(GetEditableSelection());
        }

        [MenuItem(Root + "Color/Default", false, 8)]
        private static void ClearObjectColor()
        {
            ForEachSelected(HierarchyPresentationStore.instance.ClearLabelColor);
        }

        // The priority gap preserves a divider between presentation and hierarchy operations.
        [MenuItem(Root + "Move Children To Parent", false, 100)]
        private static void MoveChildrenToParent()
        {
            List<Transform> roots = GetTopLevelSelection();
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

        [MenuItem(Root + "Select Descendants", false, 101)]
        private static void SelectDescendants()
        {
            List<GameObject> descendants = new();
            GameObject[] selection = GetEditableSelection();

            for (int index = 0; index < selection.Length; index++)
            {
                CollectDescendants(selection[index].transform, descendants);
            }

            Selection.objects = descendants.ToArray();
        }

        [MenuItem(Root + "Enable Descendants", false, 102)]
        private static void EnableDescendants() => SetDescendantsActive(true);

        [MenuItem(Root + "Disable Descendants", false, 103)]
        private static void DisableDescendants() => SetDescendantsActive(false);

        [MenuItem(Root + "Bulk Rename...", false, 104)]
        private static void OpenBulkRename()
        {
            HierarchyBulkRenameWindow.Open(GetEditableSelection());
        }

        private static void SetObjectColor(Color color)
        {
            ForEachSelected(gameObject =>
                HierarchyPresentationStore.instance.SetLabelColor(gameObject, color));
        }

        private static void ForEachSelected(System.Action<GameObject> action)
        {
            GameObject[] selection = GetEditableSelection();
            for (int index = 0; index < selection.Length; index++)
            {
                action(selection[index]);
            }
        }

        private static void CollectDescendants(Transform parent, List<GameObject> results)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                results.Add(child.gameObject);
                CollectDescendants(child, results);
            }
        }

        private static List<Transform> GetTopLevelSelection()
        {
            GameObject[] selection = GetEditableSelection();
            HashSet<Transform> selected = new();
            List<Transform> roots = new();

            for (int index = 0; index < selection.Length; index++)
            {
                selected.Add(selection[index].transform);
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

        private static void SetDescendantsActive(bool active)
        {
            List<GameObject> descendants = new();
            GameObject[] selection = GetEditableSelection();

            for (int index = 0; index < selection.Length; index++)
            {
                CollectDescendants(selection[index].transform, descendants);
            }

            Undo.RecordObjects(descendants.ToArray(), active ? "Enable Descendants" : "Disable Descendants");
            for (int index = 0; index < descendants.Count; index++)
            {
                descendants[index].SetActive(active);
            }
        }

        private static GameObject[] GetEditableSelection()
        {
            GameObject[] selection = Selection.gameObjects;
            List<GameObject> editable = new(selection.Length);

            for (int index = 0; index < selection.Length; index++)
            {
                if (!HierarchySceneFavorites.IsSynthetic(selection[index]))
                {
                    editable.Add(selection[index]);
                }
            }

            return editable.ToArray();
        }
    }

    internal sealed class HierarchyColorWindow : EditorWindow
    {
        private const float Width = 280f;
        private const float Height = 88f;

        private int[] _targetIds = System.Array.Empty<int>();
        private Color _color = HierarchyPresentationStore.DefaultLabelColor;

        internal static void Open(GameObject[] targets)
        {
            if (targets == null || targets.Length == 0)
            {
                return;
            }

            HierarchyColorWindow window = CreateInstance<HierarchyColorWindow>();
            window.titleContent = new GUIContent("Object Color");
            window._targetIds = new int[targets.Length];

            for (int index = 0; index < targets.Length; index++)
            {
                window._targetIds[index] = targets[index].GetInstanceID();
            }

            if (HierarchyPresentationStore.instance.TryGet(targets[0], out HierarchyPresentation presentation) &&
                presentation.HasLabelColor)
            {
                window._color = presentation.LabelColor;
            }

            window.minSize = new Vector2(Width, Height);
            window.maxSize = window.minSize;
            window.ShowAuxWindow();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            _color = EditorGUILayout.ColorField("Color", _color);
            EditorGUILayout.Space(6f);

            using (new EditorGUI.DisabledScope(_targetIds.Length == 0))
            {
                if (!GUILayout.Button("Apply", GUILayout.Height(22f)))
                {
                    return;
                }
            }

            for (int index = 0; index < _targetIds.Length; index++)
            {
#pragma warning disable CS0618
                GameObject target = EditorUtility.InstanceIDToObject(_targetIds[index]) as GameObject;
#pragma warning restore CS0618
                if (target != null)
                {
                    HierarchyPresentationStore.instance.SetLabelColor(target, _color);
                }
            }

            Close();
        }
    }
}
