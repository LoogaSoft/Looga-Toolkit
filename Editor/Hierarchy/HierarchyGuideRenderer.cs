using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    /// <summary>
    /// Coordinates all lightweight row decorations in every Hierarchy window.
    /// </summary>
    [InitializeOnLoad]
    internal static class HierarchyGuideRenderer
    {
        // Unity 6 Hierarchy rows advance one tree level by 14 points.
        private const float IndentWidth = 14f;

        static HierarchyGuideRenderer()
        {
            EditorApplication.hierarchyWindowItemOnGUI -= DrawRow;
            EditorApplication.hierarchyWindowItemOnGUI += DrawRow;
        }

        private static void DrawRow(int instanceId, Rect rowRect)
        {
            HierarchyGuideSettings settings = HierarchyGuideSettings.instance;
            // The hierarchy callback still supplies an instance ID in Unity 6.3.
#pragma warning disable CS0618
            Object hierarchyObject = EditorUtility.InstanceIDToObject(instanceId);
#pragma warning restore CS0618
            if (hierarchyObject is not GameObject gameObject)
            {
                return;
            }

            if (HierarchySceneFavorites.Draw(gameObject, rowRect))
            {
                return;
            }

            if (settings.ShowPresentation && HierarchyPresentationRenderer.Draw(gameObject, rowRect))
            {
                return;
            }

            if (Event.current.type == EventType.Repaint)
            {
                if (settings.Enabled)
                {
                    DrawGuides(gameObject, rowRect, settings);
                }

            }

            // Status badges also receive mouse events, so they must participate outside Repaint.
            if (settings.ShowStatusBadges)
            {
                HierarchyStatusRenderer.Draw(gameObject, rowRect);
            }

            if (settings.ShowFavorites)
            {
                HierarchyFavoriteRenderer.Draw(gameObject, rowRect, settings.ShowStatusBadges);
            }
        }

        private static void DrawGuides(
            GameObject gameObject,
            Rect rowRect,
            HierarchyGuideSettings settings)
        {

            Transform item = gameObject.transform;
            if (item.parent == null)
            {
                return;
            }

            float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            float thickness = settings.Thickness / pixelsPerPoint;
            float currentGuideX = SnapToPixel(rowRect.x - IndentWidth, pixelsPerPoint);
            float centerY = SnapToPixel(rowRect.center.y, pixelsPerPoint);
            Color color = settings.ResolveColor();

            DrawAncestorContinuations(item, rowRect, currentGuideX, thickness, color, pixelsPerPoint);
            DrawParentConnector(item, rowRect, currentGuideX, centerY, thickness, color, pixelsPerPoint);
        }

        private static void DrawAncestorContinuations(
            Transform item,
            Rect rowRect,
            float currentGuideX,
            float thickness,
            Color color,
            float pixelsPerPoint)
        {
            Transform branch = item.parent;
            int levelsUp = 2;

            while (branch.parent != null)
            {
                if (HasFollowingSibling(branch))
                {
                    float guideX = currentGuideX - (IndentWidth * levelsUp);
                    DrawVertical(guideX, rowRect.yMin, rowRect.yMax, thickness, color, pixelsPerPoint);
                }

                branch = branch.parent;
                levelsUp++;
            }
        }

        private static void DrawParentConnector(
            Transform item,
            Rect rowRect,
            float currentGuideX,
            float centerY,
            float thickness,
            Color color,
            float pixelsPerPoint)
        {
            float parentGuideX = currentGuideX - IndentWidth;
            float top = rowRect.yMin;

            // The first child completes the connector from its parent's row center.
            if (item.GetSiblingIndex() == 0)
            {
                top -= rowRect.height * 0.5f;
            }

            float bottom = HasFollowingSibling(item) ? rowRect.yMax : centerY;
            DrawVertical(parentGuideX, top, bottom, thickness, color, pixelsPerPoint);
            DrawHorizontal(parentGuideX, currentGuideX, centerY, thickness, color, pixelsPerPoint);
        }

        private static bool HasFollowingSibling(Transform item)
        {
            return item.parent != null && item.GetSiblingIndex() < item.parent.childCount - 1;
        }

        private static void DrawVertical(
            float x,
            float yMin,
            float yMax,
            float thickness,
            Color color,
            float pixelsPerPoint)
        {
            float snappedX = SnapToPixel(x, pixelsPerPoint);
            float snappedYMin = SnapToPixel(yMin, pixelsPerPoint);
            float snappedYMax = SnapToPixel(yMax, pixelsPerPoint);
            EditorGUI.DrawRect(new Rect(snappedX, snappedYMin, thickness, snappedYMax - snappedYMin), color);
        }

        private static void DrawHorizontal(
            float xMin,
            float xMax,
            float y,
            float thickness,
            Color color,
            float pixelsPerPoint)
        {
            float snappedXMin = SnapToPixel(xMin, pixelsPerPoint);
            float snappedXMax = SnapToPixel(xMax, pixelsPerPoint);
            float snappedY = SnapToPixel(y, pixelsPerPoint);
            EditorGUI.DrawRect(new Rect(snappedXMin, snappedY, snappedXMax - snappedXMin, thickness), color);
        }

        private static float SnapToPixel(float value, float pixelsPerPoint)
        {
            return Mathf.Round(value * pixelsPerPoint) / pixelsPerPoint;
        }
    }
}
