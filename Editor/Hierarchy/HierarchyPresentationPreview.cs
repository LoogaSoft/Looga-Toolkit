using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    internal static class HierarchyPresentationPreview
    {
        private static readonly HashSet<int> TargetIds = new();

        private static PreviewKind _kind;
        private static bool _hasValue;
        private static Color _color;
        private static string _iconName;

        internal static void Begin(IReadOnlyList<int> targetIds)
        {
            TargetIds.Clear();
            for (int index = 0; index < targetIds.Count; index++)
            {
                TargetIds.Add(targetIds[index]);
            }

            _kind = PreviewKind.None;
            EditorApplication.RepaintHierarchyWindow();
        }

        internal static void SetColor(bool hasColor, Color color)
        {
            if (_kind == PreviewKind.Color &&
                _hasValue == hasColor &&
                ColorsMatch(_color, color))
            {
                return;
            }

            _kind = PreviewKind.Color;
            _hasValue = hasColor;
            _color = color;
            EditorApplication.RepaintHierarchyWindow();
        }

        internal static void SetIcon(bool hasIcon, string iconName)
        {
            if (_kind == PreviewKind.Icon &&
                _hasValue == hasIcon &&
                _iconName == iconName)
            {
                return;
            }

            _kind = PreviewKind.Icon;
            _hasValue = hasIcon;
            _iconName = iconName;
            EditorApplication.RepaintHierarchyWindow();
        }

        internal static void ClearOption()
        {
            if (_kind == PreviewKind.None)
            {
                return;
            }

            _kind = PreviewKind.None;
            EditorApplication.RepaintHierarchyWindow();
        }

        internal static void End()
        {
            bool hadPreview = _kind != PreviewKind.None || TargetIds.Count > 0;
            _kind = PreviewKind.None;
            TargetIds.Clear();

            if (hadPreview)
            {
                EditorApplication.RepaintHierarchyWindow();
            }
        }

        internal static bool TryGetColor(
            GameObject gameObject,
            out bool hasColor,
            out Color color)
        {
            bool applies = _kind == PreviewKind.Color && TargetIds.Contains(gameObject.GetInstanceID());
            hasColor = applies && _hasValue;
            color = _color;
            return applies;
        }

        internal static bool TryGetIcon(
            GameObject gameObject,
            out bool hasIcon,
            out string iconName)
        {
            bool applies = _kind == PreviewKind.Icon && TargetIds.Contains(gameObject.GetInstanceID());
            hasIcon = applies && _hasValue;
            iconName = _iconName;
            return applies;
        }

        private static bool ColorsMatch(Color left, Color right)
        {
            const float tolerance = 0.001f;
            return Mathf.Abs(left.r - right.r) < tolerance &&
                   Mathf.Abs(left.g - right.g) < tolerance &&
                   Mathf.Abs(left.b - right.b) < tolerance &&
                   Mathf.Abs(left.a - right.a) < tolerance;
        }

        private enum PreviewKind
        {
            None,
            Color,
            Icon
        }
    }
}
