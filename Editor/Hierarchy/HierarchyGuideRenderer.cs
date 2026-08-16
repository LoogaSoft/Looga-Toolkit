using System.Collections.Generic;
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
        private const float ParentElbowLength = IndentWidth * 0.5f;

        private static readonly HashSet<int> SelectedInstanceIds = new();
        private static readonly List<BranchTarget> SelectedBranches = new();
        private static readonly HashSet<int> VisibleRowIds = new();
        private static readonly HashSet<int> PendingVisibleRowIds = new();
        private static readonly HashSet<int> VisibleParentIds = new();
        private static readonly HashSet<int> PendingVisibleParentIds = new();

        private static BranchTarget _hoveredBranch;
        private static BranchTarget _pendingHoveredBranch;
        private static Vector2 _lastHierarchyMousePosition = new(float.NaN, float.NaN);
        private static bool _hoverCommitScheduled;
        private static bool _visibleParentsCommitScheduled;

        static HierarchyGuideRenderer()
        {
            EditorApplication.hierarchyWindowItemOnGUI -= DrawRow;
            EditorApplication.hierarchyWindowItemOnGUI += DrawRow;
            Selection.selectionChanged -= CacheSelection;
            Selection.selectionChanged += CacheSelection;
            EditorApplication.hierarchyChanged -= CacheSelection;
            EditorApplication.hierarchyChanged += CacheSelection;
            CacheSelection();
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

            TrackHoveredRow(gameObject, rowRect, settings);
            TrackVisibleRow(gameObject);

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
            float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            float thickness = settings.Thickness / pixelsPerPoint;
            float currentGuideX = ResolveGuideX(rowRect.x);
            float centerY = SnapToPixel(rowRect.center.y, pixelsPerPoint);
            Color color = settings.ResolveColor();

            if (item.parent != null)
            {
                DrawAncestorContinuations(item, rowRect, thickness, color, pixelsPerPoint);
                DrawParentConnector(
                    item,
                    rowRect,
                    centerY,
                    thickness,
                    color,
                    pixelsPerPoint);
            }

            if (VisibleParentIds.Contains(gameObject.GetInstanceID()))
            {
                DrawVertical(currentGuideX, centerY, rowRect.yMax, thickness, color, pixelsPerPoint);
                DrawHorizontal(
                    currentGuideX,
                    currentGuideX + ParentElbowLength,
                    centerY,
                    thickness,
                    color,
                    pixelsPerPoint);
            }

            if (!settings.HighlightInteractiveBranches)
            {
                return;
            }

            int itemDepth = GetDepth(item);
            int hoveredId = _hoveredBranch.InstanceId;
            if (IsBranchVisible(_hoveredBranch) && !SelectedInstanceIds.Contains(hoveredId))
            {
                DrawInteractiveBranchSegment(
                    item,
                    itemDepth,
                    rowRect,
                    _hoveredBranch,
                    thickness + (1f / pixelsPerPoint),
                    settings.ResolveHoverColor(),
                    pixelsPerPoint);
            }

            for (int i = 0; i < SelectedBranches.Count; i++)
            {
                if (!IsBranchVisible(SelectedBranches[i]))
                {
                    continue;
                }

                DrawInteractiveBranchSegment(
                    item,
                    itemDepth,
                    rowRect,
                    SelectedBranches[i],
                    thickness + (1f / pixelsPerPoint),
                    settings.ResolveSelectedColor(),
                    pixelsPerPoint);
            }
        }

        private static void CacheSelection()
        {
            SelectedInstanceIds.Clear();
            SelectedBranches.Clear();
            GameObject[] selectedObjects = Selection.gameObjects;
            for (int i = 0; i < selectedObjects.Length; i++)
            {
                GameObject selectedObject = selectedObjects[i];
                SelectedInstanceIds.Add(selectedObject.GetInstanceID());
                SelectedBranches.Add(new BranchTarget(selectedObject.transform));
            }

            EditorApplication.RepaintHierarchyWindow();
        }

        private static void TrackHoveredRow(
            GameObject gameObject,
            Rect rowRect,
            HierarchyGuideSettings settings)
        {
            if (!settings.HighlightInteractiveBranches)
            {
                return;
            }

            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseLeaveWindow)
            {
                _pendingHoveredBranch = default;
                SetHoveredBranch(default);
                return;
            }

            if (currentEvent.type != EventType.MouseMove && currentEvent.type != EventType.Repaint)
            {
                return;
            }

            Vector2 mousePosition = currentEvent.mousePosition;
            if (float.IsNaN(_lastHierarchyMousePosition.x) ||
                (mousePosition - _lastHierarchyMousePosition).sqrMagnitude > 0.01f)
            {
                _lastHierarchyMousePosition = mousePosition;
                _pendingHoveredBranch = default;
                ScheduleHoverCommit();
            }

            if (rowRect.Contains(mousePosition))
            {
                BranchTarget hoveredBranch = new(gameObject.transform);
                _pendingHoveredBranch = hoveredBranch;
                SetHoveredBranch(hoveredBranch);
            }
        }

        private static void TrackVisibleRow(GameObject gameObject)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            PendingVisibleRowIds.Add(gameObject.GetInstanceID());
            Transform parent = gameObject.transform.parent;
            if (parent != null)
            {
                PendingVisibleParentIds.Add(parent.gameObject.GetInstanceID());
            }

            if (_visibleParentsCommitScheduled)
            {
                return;
            }

            _visibleParentsCommitScheduled = true;
            EditorApplication.delayCall += CommitVisibleParents;
        }

        private static void CommitVisibleParents()
        {
            _visibleParentsCommitScheduled = false;
            bool changed = !VisibleRowIds.SetEquals(PendingVisibleRowIds) ||
                           !VisibleParentIds.SetEquals(PendingVisibleParentIds);

            VisibleRowIds.Clear();
            VisibleRowIds.UnionWith(PendingVisibleRowIds);
            PendingVisibleRowIds.Clear();

            VisibleParentIds.Clear();
            VisibleParentIds.UnionWith(PendingVisibleParentIds);
            PendingVisibleParentIds.Clear();

            if (changed)
            {
                EditorApplication.RepaintHierarchyWindow();
            }
        }

        private static bool IsBranchVisible(BranchTarget branch)
        {
            return branch.IsValid && VisibleRowIds.Contains(branch.InstanceId);
        }

        private static void ScheduleHoverCommit()
        {
            if (_hoverCommitScheduled)
            {
                return;
            }

            _hoverCommitScheduled = true;
            EditorApplication.delayCall += CommitHoveredBranch;
        }

        private static void CommitHoveredBranch()
        {
            _hoverCommitScheduled = false;
            SetHoveredBranch(_pendingHoveredBranch);
        }

        private static void SetHoveredBranch(BranchTarget branch)
        {
            if (_hoveredBranch.InstanceId == branch.InstanceId)
            {
                return;
            }

            _hoveredBranch = branch;
            EditorApplication.RepaintHierarchyWindow();
        }

        private static void DrawInteractiveBranchSegment(
            Transform item,
            int itemDepth,
            Rect rowRect,
            BranchTarget target,
            float thickness,
            Color color,
            float pixelsPerPoint)
        {
            if (!target.IsValid)
            {
                return;
            }

            int levelsUp = itemDepth - target.Depth + 1;
            float parentGuideX = ResolveGuideX(rowRect.x, levelsUp);
            float centerY = rowRect.center.y;

            if (item == target.Parent)
            {
                DrawVertical(parentGuideX, centerY, rowRect.yMax, thickness, color, pixelsPerPoint);
                DrawHorizontal(
                    parentGuideX,
                    parentGuideX + ParentElbowLength,
                    centerY,
                    thickness,
                    color,
                    pixelsPerPoint);
                return;
            }

            if (item == target.Item)
            {
                float currentGuideX = ResolveObjectConnectorEnd(item, rowRect);
                DrawVertical(parentGuideX, rowRect.yMin, centerY, thickness, color, pixelsPerPoint);
                DrawHorizontal(parentGuideX, currentGuideX, centerY, thickness, color, pixelsPerPoint);
                return;
            }

            if (IsInPrecedingSiblingSubtree(item, target.Parent, target.SiblingIndex))
            {
                DrawVertical(parentGuideX, rowRect.yMin, rowRect.yMax, thickness, color, pixelsPerPoint);
            }
        }

        private static bool IsInPrecedingSiblingSubtree(
            Transform item,
            Transform targetParent,
            int targetSiblingIndex)
        {
            Transform branch = item;
            while (branch != null && branch.parent != targetParent)
            {
                branch = branch.parent;
            }

            return branch != null &&
                   branch.parent == targetParent &&
                   branch.GetSiblingIndex() < targetSiblingIndex;
        }

        private static int GetDepth(Transform item)
        {
            int depth = 0;
            while (item.parent != null)
            {
                depth++;
                item = item.parent;
            }

            return depth;
        }

        private static void DrawAncestorContinuations(
            Transform item,
            Rect rowRect,
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
                    float guideX = ResolveGuideX(rowRect.x, levelsUp);
                    DrawVertical(guideX, rowRect.yMin, rowRect.yMax, thickness, color, pixelsPerPoint);
                }

                branch = branch.parent;
                levelsUp++;
            }
        }

        private static void DrawParentConnector(
            Transform item,
            Rect rowRect,
            float centerY,
            float thickness,
            Color color,
            float pixelsPerPoint)
        {
            float parentGuideX = ResolveGuideX(rowRect.x, 1);
            float top = rowRect.yMin;

            float bottom = HasFollowingSibling(item) ? rowRect.yMax : centerY;
            DrawVertical(parentGuideX, top, bottom, thickness, color, pixelsPerPoint);
            DrawHorizontal(
                parentGuideX,
                ResolveObjectConnectorEnd(item, rowRect),
                centerY,
                thickness,
                color,
                pixelsPerPoint);
        }

        private static float ResolveObjectConnectorEnd(Transform item, Rect rowRect)
        {
            // Unity reserves a foldout column for every row. Leaf objects have no triangle, so use
            // half of that empty column while keeping branch rows clear of their foldout control.
            float reservedFoldoutSpace = item.childCount > 0
                ? IndentWidth
                : ParentElbowLength;
            return rowRect.x - reservedFoldoutSpace;
        }

        private static float ResolveGuideX(float rowX, int levelsUp = 0)
        {
            return rowX - IndentWidth - ParentElbowLength - IndentWidth * levelsUp;
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

        private readonly struct BranchTarget
        {
            public BranchTarget(Transform item)
            {
                Item = item;
                Parent = item != null ? item.parent : null;
                SiblingIndex = item != null ? item.GetSiblingIndex() : -1;
                Depth = item != null ? GetDepth(item) : 0;
                InstanceId = item != null ? item.gameObject.GetInstanceID() : 0;
            }

            public Transform Item { get; }

            public Transform Parent { get; }

            public int SiblingIndex { get; }

            public int Depth { get; }

            public int InstanceId { get; }

            public bool IsValid => Item != null && Parent != null;
        }
    }
}
