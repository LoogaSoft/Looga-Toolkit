using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    internal static class HierarchyPresentationRenderer
    {
        private const float IndentWidth = 14f;
        private const float HeaderOpacity = 0.46f;
        private const float DescendantOpacity = 0.18f;

        internal static bool Draw(GameObject gameObject, Rect rowRect)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return false;
            }

            HierarchyPresentationStore.instance.TryGet(gameObject, out HierarchyPresentation presentation);
            bool hasColor = TryResolveColor(gameObject, out Color color, out int levelsFromOwner);

            if (presentation != null &&
                presentation.HasIcon &&
                !HierarchyPresentationPreview.IsPreviewingIcon(gameObject))
            {
                SynchronizeNativeIcon(gameObject, presentation.IconName);
            }

            if (hasColor)
            {
                DrawColor(rowRect, color, levelsFromOwner);
            }

            return false;
        }

        private static bool TryResolveColor(
            GameObject gameObject,
            out Color color,
            out int levelsFromOwner)
        {
            Transform current = gameObject.transform;
            levelsFromOwner = 0;

            while (current != null)
            {
                GameObject currentObject = current.gameObject;
                if (HierarchyPresentationPreview.TryGetColor(
                        currentObject,
                        out bool previewHasColor,
                        out Color previewColor))
                {
                    if (previewHasColor)
                    {
                        color = previewColor;
                        return true;
                    }
                }
                else if (HierarchyPresentationStore.instance.TryGet(
                             currentObject,
                             out HierarchyPresentation presentation) &&
                         presentation.HasLabelColor)
                {
                    color = presentation.LabelColor;
                    return true;
                }

                current = current.parent;
                levelsFromOwner++;
            }

            color = default;
            levelsFromOwner = 0;
            return false;
        }

        private static void DrawColor(Rect rowRect, Color color, int levelsFromOwner)
        {
            float groupStartX = Mathf.Max(
                0f,
                rowRect.x - IndentWidth * (levelsFromOwner + 1));

            color.a *= levelsFromOwner == 0
                ? HeaderOpacity
                : DescendantOpacity;

            EditorGUI.DrawRect(
                new Rect(
                    groupStartX,
                    rowRect.y,
                    rowRect.xMax - groupStartX,
                    rowRect.height),
                color);
        }

        private static void SynchronizeNativeIcon(GameObject gameObject, string iconName)
        {
            Texture2D icon = HierarchyIconCatalog.GetTexture(iconName) as Texture2D;
            if (icon == null || EditorGUIUtility.GetIconForObject(gameObject) == icon)
            {
                return;
            }

            EditorGUIUtility.SetIconForObject(gameObject, icon);
            EditorApplication.DirtyHierarchyWindowSorting();
        }

    }

}
