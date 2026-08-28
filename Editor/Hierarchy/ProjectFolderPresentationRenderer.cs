using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    [InitializeOnLoad]
    internal static class ProjectFolderPresentationRenderer
    {
        private const float ListIconSize = 16f;
        private const float GridLabelHeight = 18f;
        private const float GlyphScale = 0.46f;

        static ProjectFolderPresentationRenderer()
        {
            EditorApplication.projectWindowItemOnGUI -= DrawFolder;
            EditorApplication.projectWindowItemOnGUI += DrawFolder;
        }

        private static void DrawFolder(string guid, Rect itemRect)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            ProjectFolderInteractionHandler.Handle(guid, path, itemRect);
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            ProjectFolderPresentationStore.instance.TryGet(
                guid,
                out ProjectFolderPresentation presentation);
            ResolveColor(guid, presentation, out bool hasColor, out Color color);
            ResolveIcon(guid, presentation, out bool hasIcon, out Texture glyph);
            if (!hasColor && !hasIcon)
            {
                return;
            }

            Rect iconRect = ResolveIconRect(itemRect);
            Texture folderIcon = EditorGUIUtility.IconContent("Folder Icon").image;
            if (folderIcon != null)
            {
                Color tint = hasColor ? color : Color.white;
                GUI.DrawTexture(
                    iconRect,
                    folderIcon,
                    ScaleMode.ScaleToFit,
                    true,
                    0f,
                    tint,
                    0f,
                    0f);
            }

            if (hasIcon && glyph != null)
            {
                float glyphSize = Mathf.Max(9f, Mathf.Min(iconRect.width, iconRect.height) * GlyphScale);
                Rect glyphRect = new(
                    iconRect.center.x - glyphSize * 0.5f,
                    iconRect.center.y - glyphSize * 0.5f,
                    glyphSize,
                    glyphSize);
                GUI.DrawTexture(glyphRect, glyph, ScaleMode.ScaleToFit, true);
            }
        }

        private static void ResolveColor(
            string guid,
            ProjectFolderPresentation presentation,
            out bool hasColor,
            out Color color)
        {
            if (ProjectFolderPresentationPreview.TryGetColor(
                    guid,
                    out hasColor,
                    out color))
            {
                return;
            }

            hasColor = presentation != null && presentation.HasColor;
            color = hasColor ? presentation.Color : Color.white;
        }

        private static void ResolveIcon(
            string guid,
            ProjectFolderPresentation presentation,
            out bool hasIcon,
            out Texture icon)
        {
            if (ProjectFolderPresentationPreview.TryGetIcon(
                    guid,
                    out hasIcon,
                    out string previewIconName))
            {
                icon = hasIcon
                    ? HierarchyIconCatalog.GetTexture(previewIconName)
                    : null;
                hasIcon = icon != null;
                return;
            }

            hasIcon = presentation != null && presentation.HasIcon;
            icon = hasIcon
                ? HierarchyIconCatalog.GetTexture(presentation.IconName)
                : null;
            hasIcon = icon != null;
        }

        private static Rect ResolveIconRect(Rect itemRect)
        {
            if (itemRect.height <= ListIconSize + 2f)
            {
                return new Rect(itemRect.x, itemRect.y, ListIconSize, itemRect.height);
            }

            float availableHeight = Mathf.Max(ListIconSize, itemRect.height - GridLabelHeight);
            float iconSize = Mathf.Min(itemRect.width, availableHeight);
            return new Rect(
                itemRect.center.x - iconSize * 0.5f,
                itemRect.y,
                iconSize,
                iconSize);
        }
    }

    internal static class ProjectFolderInteractionHandler
    {
        internal static void Handle(string guid, string path, Rect itemRect)
        {
            Event current = Event.current;
            if (current.type != EventType.MouseDown ||
                current.button != 0 ||
                !current.alt ||
                !itemRect.Contains(current.mousePosition))
            {
                return;
            }

            string[] targetGuids = ResolveTargets(guid, path);
            Rect anchor = new(current.mousePosition, Vector2.zero);
            HierarchyPresentationPopup.OpenProjectFolders(anchor, targetGuids);
            current.Use();
        }

        private static string[] ResolveTargets(string clickedGuid, string clickedPath)
        {
            Object[] selectedObjects = Selection.objects;
            List<string> selectedFolderGuids = new(selectedObjects.Length);
            bool clickedFolderIsSelected = false;

            for (int index = 0; index < selectedObjects.Length; index++)
            {
                string selectedPath = AssetDatabase.GetAssetPath(selectedObjects[index]);
                if (!AssetDatabase.IsValidFolder(selectedPath))
                {
                    continue;
                }

                string selectedGuid = AssetDatabase.AssetPathToGUID(selectedPath);
                selectedFolderGuids.Add(selectedGuid);
                clickedFolderIsSelected |= selectedGuid == clickedGuid;
            }

            if (clickedFolderIsSelected)
            {
                return selectedFolderGuids.ToArray();
            }

            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(clickedPath);
            return new[] { clickedGuid };
        }
    }
}
