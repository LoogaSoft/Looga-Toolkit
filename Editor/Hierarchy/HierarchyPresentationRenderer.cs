using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    internal static class HierarchyPresentationRenderer
    {
        private const float IndentWidth = 14f;
        private const float HeaderOpacity = 0.46f;
        private const float DescendantOpacity = 0.18f;
        private const float IconSize = 16f;
        private const float LabelSpacing = 2f;

        private static readonly GUIContent RowNameContent = new();
        private static readonly Dictionary<int, TruncatedNameEntry> TruncatedNames = new();

        private static GUIStyle _foldoutStyle;
        private static GUIStyle _lineStyle;
        private static GUIStyle _prefabLabelStyle;
        private static GUIStyle _disabledLabelStyle;
        private static GUIStyle _disabledPrefabLabelStyle;

        internal static bool Draw(GameObject gameObject, Rect rowRect)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return false;
            }

            HierarchyPresentationStore.instance.TryGet(gameObject, out HierarchyPresentation presentation);
            bool hasColor = TryResolveColor(gameObject, out Color color, out int levelsFromOwner);
            bool hasCustomIcon = TryResolveCustomIcon(
                gameObject,
                presentation,
                out Texture customIcon);

            if (presentation != null &&
                presentation.HasIcon &&
                !HierarchyPresentationPreview.IsPreviewingIcon(gameObject))
            {
                SynchronizeNativeIcon(gameObject, presentation.IconName);
            }

            RowState rowState = ResolveRowState(gameObject, rowRect);
            if (hasColor)
            {
                bool isRenaming = EditorGUIUtility.editingTextField &&
                    Selection.activeGameObject == gameObject;
                if (!isRenaming)
                {
                    ClearNativeName(gameObject, rowRect, rowState);
                    if (hasCustomIcon)
                    {
                        ClearNativeIcon(rowRect, rowState);
                    }

                    DrawColor(rowRect, color, levelsFromOwner);
                    DrawNativeRowContent(
                        gameObject,
                        rowRect,
                        rowState,
                        customIcon);
                }
            }
            else if (hasCustomIcon)
            {
                ClearNativeIcon(rowRect, rowState);
                DrawNativeIcon(gameObject, rowRect, rowState, customIcon);
            }

            return false;
        }

        internal static Color ResolveRowBackground(GameObject gameObject, Rect rowRect)
        {
            Color background = ResolveNativeBackground(ResolveRowState(gameObject, rowRect));
            if (!TryResolveColor(gameObject, out Color color, out int levelsFromOwner))
            {
                return background;
            }

            float opacity = levelsFromOwner == 0 ? HeaderOpacity : DescendantOpacity;
            float alpha = Mathf.Clamp01(color.a * opacity);
            return new Color(
                Mathf.Lerp(background.r, color.r, alpha),
                Mathf.Lerp(background.g, color.g, alpha),
                Mathf.Lerp(background.b, color.b, alpha),
                1f);
        }

        internal static void DrawTruncatedNameIfNeeded(
            GameObject gameObject,
            Rect rowRect,
            float right)
        {
            GUIStyle labelStyle = GetNativeLabelStyle(gameObject);
            float labelX = rowRect.x + IconSize + LabelSpacing;
            float availableWidth = Mathf.Max(0f, right - labelX);
            RowNameContent.text = gameObject.name;
            if (labelStyle.CalcSize(RowNameContent).x <= availableWidth)
            {
                return;
            }

            RowState rowState = ResolveRowState(gameObject, rowRect);
            Rect clearRect = new(
                labelX,
                rowRect.y,
                Mathf.Max(0f, rowRect.xMax - labelX),
                rowRect.height);
            EditorGUI.DrawRect(clearRect, ResolveRowBackground(gameObject, rowRect));

            if (availableWidth <= 0f)
            {
                return;
            }

            string displayName = GetTruncatedName(gameObject, labelStyle, availableWidth);
            Rect labelRect = new(labelX, rowRect.y, availableWidth, rowRect.height);
            labelStyle.Draw(
                labelRect,
                displayName,
                false,
                false,
                rowState.Selected,
                rowState.Focused);
        }

        private static bool TryResolveCustomIcon(
            GameObject gameObject,
            HierarchyPresentation presentation,
            out Texture icon)
        {
            if (HierarchyPresentationPreview.TryGetIcon(
                    gameObject,
                    out bool previewHasIcon,
                    out string previewIconName))
            {
                icon = previewHasIcon
                    ? HierarchyIconCatalog.GetTexture(previewIconName)
                    : null;
                return icon != null;
            }

            icon = presentation != null && presentation.HasIcon
                ? HierarchyIconCatalog.GetTexture(presentation.IconName)
                : null;
            return icon != null;
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

        private static RowState ResolveRowState(GameObject gameObject, Rect rowRect)
        {
            bool selected = Selection.Contains(gameObject);
            bool focused = EditorWindow.focusedWindow != null &&
                EditorWindow.focusedWindow.GetType().Name == "SceneHierarchyWindow";
            bool hovered = rowRect.Contains(Event.current.mousePosition);
            return new RowState(selected, focused, hovered);
        }

        private static void ClearNativeName(
            GameObject gameObject,
            Rect rowRect,
            RowState rowState)
        {
            GUIStyle labelStyle = GetNativeLabelStyle(gameObject);
            RowNameContent.text = gameObject.name;
            float nameWidth = labelStyle.CalcSize(RowNameContent).x + LabelSpacing;
            float availableWidth = Mathf.Max(0f, rowRect.width - IconSize);
            Rect clearRect = new(
                rowRect.x + IconSize,
                rowRect.y,
                Mathf.Min(nameWidth, availableWidth),
                rowRect.height);

            EditorGUI.DrawRect(clearRect, ResolveNativeBackground(rowState));
        }

        private static void ClearNativeIcon(Rect rowRect, RowState rowState)
        {
            Rect clearRect = new(rowRect.x, rowRect.y, IconSize, rowRect.height);
            EditorGUI.DrawRect(clearRect, ResolveNativeBackground(rowState));
        }

        private static void DrawNativeRowContent(
            GameObject gameObject,
            Rect rowRect,
            RowState rowState,
            Texture customIcon)
        {
            DrawNativeFoldout(gameObject, rowRect);
            DrawNativeIcon(gameObject, rowRect, rowState, customIcon);
            DrawNativeName(gameObject, rowRect, rowState);
        }

        private static void DrawNativeFoldout(GameObject gameObject, Rect rowRect)
        {
            if (gameObject.transform.childCount > 0)
            {
                GUIStyle foldoutStyle = GetFoldoutStyle();
                float foldoutWidth = foldoutStyle.fixedWidth > 0f
                    ? foldoutStyle.fixedWidth
                    : IndentWidth;
                Rect foldoutRect = new(
                    rowRect.x - foldoutWidth - GetLineStyle().margin.left,
                    rowRect.y,
                    foldoutWidth,
                    rowRect.height);

                foldoutStyle.Draw(
                    foldoutRect,
                    GUIContent.none,
                    false,
                    false,
                    HierarchyGuideRenderer.IsExpanded(gameObject),
                    false);
            }
        }

        private static void DrawNativeIcon(
            GameObject gameObject,
            Rect rowRect,
            RowState rowState,
            Texture customIcon)
        {
            Texture icon = customIcon ?? PrefabUtility.GetIconForGameObject(gameObject);
            icon ??= EditorGUIUtility.ObjectContent(
                gameObject,
                typeof(GameObject)).image;
            if (icon == null)
            {
                return;
            }

            if (rowState.Selected && rowState.Focused && icon.name == "GameObject Icon")
            {
                icon = EditorGUIUtility.IconContent("GameObject On Icon").image ?? icon;
            }

            Rect iconRect = new(rowRect.x, rowRect.y, IconSize, rowRect.height);
            Color iconColor = gameObject.activeInHierarchy
                ? Color.white
                : new Color(1f, 1f, 1f, 0.5f);
            GUI.DrawTexture(
                iconRect,
                icon,
                ScaleMode.ScaleToFit,
                true,
                0f,
                iconColor,
                0f,
                0f);

            if (PrefabUtility.IsAddedGameObjectOverride(gameObject))
            {
                Texture addedOverlay = EditorGUIUtility.IconContent(
                    "PrefabOverlayAdded Icon").image;
                if (addedOverlay != null)
                {
                    GUI.DrawTexture(iconRect, addedOverlay, ScaleMode.ScaleToFit, true);
                }
            }
        }

        private static void DrawNativeName(
            GameObject gameObject,
            Rect rowRect,
            RowState rowState)
        {
            Rect labelRect = new(
                rowRect.x + IconSize + LabelSpacing,
                rowRect.y,
                rowRect.width - IconSize - LabelSpacing,
                rowRect.height);

            GetNativeLabelStyle(gameObject).Draw(
                labelRect,
                gameObject.name,
                false,
                false,
                rowState.Selected,
                rowState.Focused);
        }

        private static GUIStyle GetNativeLabelStyle(GameObject gameObject)
        {
            if (PrefabUtility.IsPartOfAnyPrefab(gameObject))
            {
                if (gameObject.activeInHierarchy)
                {
                    _prefabLabelStyle ??= GUI.skin.GetStyle("PR PrefabLabel");
                    return _prefabLabelStyle;
                }

                _disabledPrefabLabelStyle ??= GUI.skin.GetStyle(
                    "PR DisabledPrefabLabel");
                return _disabledPrefabLabelStyle;
            }

            if (!gameObject.activeInHierarchy)
            {
                _disabledLabelStyle ??= GUI.skin.GetStyle("PR DisabledLabel");
                return _disabledLabelStyle;
            }

            return GetLineStyle();
        }

        private static string GetTruncatedName(
            GameObject gameObject,
            GUIStyle labelStyle,
            float availableWidth)
        {
            int instanceId = gameObject.GetInstanceID();
            string fullName = gameObject.name;
            int widthInPixels = Mathf.RoundToInt(
                availableWidth * EditorGUIUtility.pixelsPerPoint);
            if (TruncatedNames.TryGetValue(instanceId, out TruncatedNameEntry entry) &&
                entry.FullName == fullName &&
                entry.WidthInPixels == widthInPixels &&
                entry.Style == labelStyle)
            {
                return entry.DisplayName;
            }

            const string ellipsis = "...";
            RowNameContent.text = ellipsis;
            if (labelStyle.CalcSize(RowNameContent).x > availableWidth)
            {
                TruncatedNames[instanceId] = new TruncatedNameEntry(
                    fullName,
                    string.Empty,
                    widthInPixels,
                    labelStyle);
                return string.Empty;
            }

            int minimumLength = 0;
            int maximumLength = fullName.Length;
            while (minimumLength < maximumLength)
            {
                int candidateLength = (minimumLength + maximumLength + 1) / 2;
                RowNameContent.text = fullName.Substring(0, candidateLength) + ellipsis;
                if (labelStyle.CalcSize(RowNameContent).x <= availableWidth)
                {
                    minimumLength = candidateLength;
                }
                else
                {
                    maximumLength = candidateLength - 1;
                }
            }

            string displayName = fullName.Substring(0, minimumLength) + ellipsis;
            TruncatedNames[instanceId] = new TruncatedNameEntry(
                fullName,
                displayName,
                widthInPixels,
                labelStyle);
            return displayName;
        }

        private static GUIStyle GetFoldoutStyle()
        {
            _foldoutStyle ??= GUI.skin.GetStyle("IN Foldout");
            return _foldoutStyle;
        }

        private static GUIStyle GetLineStyle()
        {
            _lineStyle ??= GUI.skin.GetStyle("TV Line");
            return _lineStyle;
        }

        private static Color ResolveNativeBackground(RowState rowState)
        {
            if (rowState.Selected)
            {
                if (rowState.Focused)
                {
                    return EditorGUIUtility.isProSkin
                        ? new Color(0.17f, 0.365f, 0.535f, 1f)
                        : new Color(0.24f, 0.45f, 0.666f, 1f);
                }

                float unfocusedValue = EditorGUIUtility.isProSkin ? 0.3f : 0.68f;
                return new Color(
                    unfocusedValue,
                    unfocusedValue,
                    unfocusedValue,
                    1f);
            }

            if (rowState.Hovered)
            {
                float hoverValue = EditorGUIUtility.isProSkin ? 0.265f : 0.7f;
                return new Color(hoverValue, hoverValue, hoverValue, 1f);
            }

            float backgroundValue = EditorGUIUtility.isProSkin ? 0.22f : 0.78f;
            return new Color(
                backgroundValue,
                backgroundValue,
                backgroundValue,
                1f);
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

        private readonly struct RowState
        {
            internal RowState(bool selected, bool focused, bool hovered)
            {
                Selected = selected;
                Focused = focused;
                Hovered = hovered;
            }

            internal bool Selected { get; }

            internal bool Focused { get; }

            internal bool Hovered { get; }
        }

        private readonly struct TruncatedNameEntry
        {
            internal TruncatedNameEntry(
                string fullName,
                string displayName,
                int widthInPixels,
                GUIStyle style)
            {
                FullName = fullName;
                DisplayName = displayName;
                WidthInPixels = widthInPixels;
                Style = style;
            }

            internal string FullName { get; }

            internal string DisplayName { get; }

            internal int WidthInPixels { get; }

            internal GUIStyle Style { get; }
        }
    }

}
