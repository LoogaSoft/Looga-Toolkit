using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    internal static class ProjectFolderPresentationPreview
    {
        private static readonly HashSet<string> TargetGuids = new();

        private static PreviewKind _kind;
        private static bool _hasValue;
        private static Color _color;
        private static string _iconName;

        internal static void Begin(IReadOnlyList<string> targetGuids)
        {
            TargetGuids.Clear();
            for (int index = 0; index < targetGuids.Count; index++)
            {
                TargetGuids.Add(targetGuids[index]);
            }

            _kind = PreviewKind.None;
            EditorApplication.RepaintProjectWindow();
        }

        internal static void SetColor(bool hasColor, Color color)
        {
            _kind = PreviewKind.Color;
            _hasValue = hasColor;
            _color = color;
            EditorApplication.RepaintProjectWindow();
        }

        internal static void SetIcon(bool hasIcon, string iconName)
        {
            _kind = PreviewKind.Icon;
            _hasValue = hasIcon;
            _iconName = iconName;
            EditorApplication.RepaintProjectWindow();
        }

        internal static void ClearOption()
        {
            if (_kind == PreviewKind.None)
            {
                return;
            }

            _kind = PreviewKind.None;
            EditorApplication.RepaintProjectWindow();
        }

        internal static void End()
        {
            bool hadPreview = _kind != PreviewKind.None || TargetGuids.Count > 0;
            _kind = PreviewKind.None;
            TargetGuids.Clear();
            if (hadPreview)
            {
                EditorApplication.RepaintProjectWindow();
            }
        }

        internal static bool TryGetColor(
            string guid,
            out bool hasColor,
            out Color color)
        {
            bool applies = _kind == PreviewKind.Color && TargetGuids.Contains(guid);
            hasColor = applies && _hasValue;
            color = _color;
            return applies;
        }

        internal static bool TryGetIcon(
            string guid,
            out bool hasIcon,
            out string iconName)
        {
            bool applies = _kind == PreviewKind.Icon && TargetGuids.Contains(guid);
            hasIcon = applies && _hasValue;
            iconName = applies ? _iconName : string.Empty;
            return applies;
        }

        private enum PreviewKind
        {
            None,
            Color,
            Icon
        }
    }
}
