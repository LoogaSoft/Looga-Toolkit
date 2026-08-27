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

        private static readonly GUIContent RowIconContent = new();
        private static GUIStyle _rowLabelStyle;
        private static GUIStyle _selectedRowLabelStyle;
        private static GUIStyle _prefabRowLabelStyle;

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
                bool isRenaming = EditorGUIUtility.editingTextField &&
                    Selection.activeGameObject == gameObject;
                if (!isRenaming)
                {
                    DrawColor(rowRect, color, levelsFromOwner);
                    DrawRowContent(gameObject, rowRect);
                }
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

        private static void DrawRowContent(GameObject gameObject, Rect rowRect)
        {
            if (gameObject.transform.childCount > 0)
            {
                Rect foldoutRect = new(
                    rowRect.x - IndentWidth,
                    rowRect.y,
                    IndentWidth,
                    rowRect.height);
                EditorGUI.Foldout(
                    foldoutRect,
                    HierarchyGuideRenderer.IsExpanded(gameObject),
                    GUIContent.none,
                    false);
            }

            Texture icon = EditorGUIUtility.GetIconForObject(gameObject);
            icon ??= EditorGUIUtility.ObjectContent(gameObject, typeof(GameObject)).image;
            if (icon != null)
            {
                Rect iconRect = new(
                    rowRect.x,
                    rowRect.y + Mathf.Floor((rowRect.height - IconSize) * 0.5f),
                    IconSize,
                    IconSize);
                RowIconContent.image = icon;
                GUI.Label(iconRect, RowIconContent, GUIStyle.none);
            }

            Rect labelRect = new(
                rowRect.x + IconSize + LabelSpacing,
                rowRect.y,
                rowRect.width - IconSize - LabelSpacing,
                rowRect.height);

            bool previousEnabled = GUI.enabled;
            GUI.enabled = gameObject.activeInHierarchy;
            GUI.Label(labelRect, gameObject.name, GetRowLabelStyle(gameObject));
            GUI.enabled = previousEnabled;
        }

        private static GUIStyle GetRowLabelStyle(GameObject gameObject)
        {
            if (Selection.Contains(gameObject))
            {
                _selectedRowLabelStyle ??= CreateRowLabelStyle(Color.white);
                return _selectedRowLabelStyle;
            }

            if (PrefabUtility.IsAnyPrefabInstanceRoot(gameObject))
            {
                Color prefabColor = EditorGUIUtility.isProSkin
                    ? new Color(0.40f, 0.68f, 1f, 1f)
                    : new Color(0.08f, 0.34f, 0.72f, 1f);
                _prefabRowLabelStyle ??= CreateRowLabelStyle(prefabColor);
                return _prefabRowLabelStyle;
            }

            _rowLabelStyle ??= CreateRowLabelStyle(EditorStyles.label.normal.textColor);
            return _rowLabelStyle;
        }

        private static GUIStyle CreateRowLabelStyle(Color textColor)
        {
            GUIStyle style = new(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0)
            };
            style.normal.textColor = textColor;
            return style;
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
