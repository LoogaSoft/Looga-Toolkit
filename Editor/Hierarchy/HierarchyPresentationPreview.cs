using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    internal static class HierarchyPresentationPreview
    {
        private static readonly HashSet<int> TargetIds = new();
        private static readonly Dictionary<int, Texture2D> OriginalIcons = new();

        private static PreviewKind _kind;
        private static bool _hasValue;
        private static Color _color;
        private static string _iconName;

        internal static void Begin(IReadOnlyList<int> targetIds)
        {
            RestoreOriginalIcons();
            TargetIds.Clear();
            OriginalIcons.Clear();

            for (int index = 0; index < targetIds.Count; index++)
            {
                int targetId = targetIds[index];
                TargetIds.Add(targetId);

                GameObject target = ResolveTarget(targetId);
                if (target != null)
                {
                    OriginalIcons[targetId] = EditorGUIUtility.GetIconForObject(target);
                }
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

            if (_kind == PreviewKind.Icon)
            {
                RestoreOriginalIcons();
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
            ApplyNativeIcon(hasIcon
                ? HierarchyIconCatalog.GetTexture(iconName) as Texture2D
                : null);
        }

        internal static void CommitIcon(bool hasIcon, string iconName)
        {
            Texture2D icon = hasIcon
                ? HierarchyIconCatalog.GetTexture(iconName) as Texture2D
                : null;

            foreach (int targetId in TargetIds)
            {
                OriginalIcons[targetId] = icon;
            }

            _kind = PreviewKind.Icon;
            _hasValue = hasIcon;
            _iconName = iconName;
            ApplyNativeIcon(icon);
        }

        internal static void ClearOption()
        {
            if (_kind == PreviewKind.None)
            {
                return;
            }

            if (_kind == PreviewKind.Icon)
            {
                RestoreOriginalIcons();
            }

            _kind = PreviewKind.None;
            EditorApplication.RepaintHierarchyWindow();
        }

        internal static void End()
        {
            bool hadPreview = _kind != PreviewKind.None || TargetIds.Count > 0;
            RestoreOriginalIcons();
            _kind = PreviewKind.None;
            TargetIds.Clear();
            OriginalIcons.Clear();

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

        internal static bool IsPreviewingIcon(GameObject gameObject)
        {
            return _kind == PreviewKind.Icon && TargetIds.Contains(gameObject.GetInstanceID());
        }

        private static void ApplyNativeIcon(Texture2D icon)
        {
            foreach (int targetId in TargetIds)
            {
                GameObject target = ResolveTarget(targetId);
                if (target != null && EditorGUIUtility.GetIconForObject(target) != icon)
                {
                    EditorGUIUtility.SetIconForObject(target, icon);
                }
            }

            EditorApplication.RepaintHierarchyWindow();
        }

        private static void RestoreOriginalIcons()
        {
            foreach (KeyValuePair<int, Texture2D> entry in OriginalIcons)
            {
                GameObject target = ResolveTarget(entry.Key);
                if (target != null && EditorGUIUtility.GetIconForObject(target) != entry.Value)
                {
                    EditorGUIUtility.SetIconForObject(target, entry.Value);
                }
            }
        }

        private static GameObject ResolveTarget(int instanceId)
        {
#pragma warning disable CS0618
            return EditorUtility.InstanceIDToObject(instanceId) as GameObject;
#pragma warning restore CS0618
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
