using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Inspector.Editor
{
    /// <summary>
    /// Provides shared geometry and interaction styling for Looga UI Toolkit editors.
    /// </summary>
    public static class LoogaUiToolkitStyle
    {
        public const float InteractiveRowHeight = 38f;
        public const float InteractiveRowSpacing = 4f;
        public const float RowHorizontalPadding = 8f;
        public const float ContentPadding = 12f;
        public const float SectionSpacing = 8f;

        public static readonly ProfilerMarker MenuPreviewRefresh =
            new("Looga.UI.MenuPreview.Refresh");
        public static readonly ProfilerMarker PackageWorkspaceRefresh =
            new("Looga.Toolkit.PackageWorkspace.Refresh");
        public static readonly ProfilerMarker DesignSystemRefresh =
            new("Looga.UI.DesignSystem.Refresh");

        private const string SharedStyleSheetPath =
            "Packages/com.loogasoft.loogatoolkit/Editor/Inspector/Styles/LoogaUiToolkitStyle.uss";
        private const string NoCollectionRowHoverClass = "looga-no-collection-row-hover";

        private static StyleSheet _sharedStyleSheet;

        public static void StyleInteractiveRow(
            VisualElement row,
            FontStyle fontStyle = FontStyle.Normal)
        {
            Color normal = EditorGUIUtility.isProSkin
                ? new Color(0.32f, 0.32f, 0.32f)
                : new Color(0.76f, 0.76f, 0.76f);
            Color hover = EditorGUIUtility.isProSkin
                ? new Color(0.38f, 0.38f, 0.38f)
                : new Color(0.82f, 0.82f, 0.82f);

            row.style.height = InteractiveRowHeight;
            row.style.minHeight = InteractiveRowHeight;
            row.style.marginLeft = 0f;
            row.style.marginRight = 0f;
            row.style.marginTop = 0f;
            row.style.marginBottom = InteractiveRowSpacing;
            row.style.paddingLeft = RowHorizontalPadding;
            row.style.backgroundColor = normal;
            row.style.unityFontStyleAndWeight = fontStyle;
            row.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.RegisterCallback<PointerEnterEvent>(_ => row.style.backgroundColor = hover);
            row.RegisterCallback<PointerLeaveEvent>(_ => row.style.backgroundColor = normal);
        }

        public static void StyleFoldout(Foldout foldout)
        {
            Toggle header = foldout.Q<Toggle>();
            if (header == null)
                return;

            StyleInteractiveRow(header);
            header.style.marginBottom = 0f;
            header.style.paddingLeft = RowHorizontalPadding;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            Label label = header.Q<Label>(className: "unity-toggle__label");
            if (label != null)
            {
                label.style.unityFontStyleAndWeight = FontStyle.Normal;
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                label.style.alignSelf = Align.Center;
                label.style.height = InteractiveRowHeight;
                label.style.flexGrow = 1f;
                label.style.flexShrink = 1f;
                label.style.marginLeft = 0f;
                label.style.marginRight = 0f;
                label.style.marginTop = 0f;
                label.style.marginBottom = 0f;
                label.style.paddingLeft = 0f;
                label.style.paddingRight = 0f;
                label.style.paddingTop = 0f;
                label.style.paddingBottom = 0f;
            }

            VisualElement input = header.Q<VisualElement>(className: "unity-toggle__input");
            if (input != null)
            {
                input.style.opacity = 1f;
                input.style.position = Position.Relative;
                input.style.marginLeft = 0f;
                input.style.marginRight = 0f;
                input.style.marginTop = 0f;
                input.style.marginBottom = 0f;
                input.style.paddingLeft = 0f;
                input.style.paddingRight = 0f;
                input.style.width = StyleKeyword.Auto;
                input.style.minWidth = 0f;
                input.style.flexGrow = 1f;
                input.style.flexShrink = 1f;
                input.style.flexDirection = FlexDirection.Row;
                input.style.alignItems = Align.Center;
                input.style.alignSelf = Align.Center;
            }

            VisualElement triangle = header.Q<VisualElement>(className: "unity-toggle__checkmark");
            if (triangle != null)
            {
                triangle.style.display = DisplayStyle.None;
            }

            if (input != null && input.Q<LoogaFoldoutTriangle>() == null)
                input.Insert(0, new LoogaFoldoutTriangle(foldout));
        }

        public static VisualElement CreateInspectorRoot()
        {
            VisualElement root = new();
            AddSharedStyleSheet(root);
            root.style.paddingLeft = ContentPadding;
            root.style.paddingRight = ContentPadding;
            root.style.paddingTop = ContentPadding;
            root.style.paddingBottom = ContentPadding;
            return root;
        }

        public static VisualElement CreateSection(string title, string description = null)
        {
            VisualElement section = new();
            section.style.marginBottom = SectionSpacing;

            Label heading = new(title);
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.fontSize = 13f;
            heading.style.marginBottom = string.IsNullOrWhiteSpace(description) ? 4f : 2f;
            section.Add(heading);

            if (!string.IsNullOrWhiteSpace(description))
            {
                Label detail = new(description);
                detail.style.whiteSpace = WhiteSpace.Normal;
                detail.style.opacity = 0.78f;
                detail.style.marginBottom = 6f;
                section.Add(detail);
            }

            return section;
        }

        public static VisualElement CreateCard()
        {
            VisualElement card = new();
            card.style.paddingLeft = 10f;
            card.style.paddingRight = 10f;
            card.style.paddingTop = 8f;
            card.style.paddingBottom = 8f;
            card.style.marginBottom = 4f;
            card.style.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.24f, 0.24f, 0.24f)
                : new Color(0.82f, 0.82f, 0.82f);
            return card;
        }

        /// <summary>
        /// Creates a Unity-styled result card for editor workspaces and navigation pages.
        /// </summary>
        public static VisualElement CreateWorkspaceCard()
        {
            VisualElement card = CreateCard();
            float borderWidth = LoogaEditorStyle.Pixels(1f);
            Color borderColor = LoogaEditorStyle.SeparatorColor;

            card.style.backgroundColor = LoogaEditorStyle.HoverColor;
            card.style.borderLeftWidth = borderWidth;
            card.style.borderRightWidth = borderWidth;
            card.style.borderTopWidth = borderWidth;
            card.style.borderBottomWidth = borderWidth;
            card.style.borderLeftColor = borderColor;
            card.style.borderRightColor = borderColor;
            card.style.borderTopColor = borderColor;
            card.style.borderBottomColor = borderColor;
            card.style.borderTopLeftRadius = 2f;
            card.style.borderTopRightRadius = 2f;
            card.style.borderBottomLeftRadius = 2f;
            card.style.borderBottomRightRadius = 2f;
            return card;
        }

        /// <summary>
        /// Aligns a workspace notice with the cards that follow it.
        /// </summary>
        public static void StyleWorkspaceNotice(VisualElement notice)
        {
            if (notice == null)
                return;

            notice.style.alignSelf = Align.Stretch;
            notice.style.flexGrow = 1f;
            notice.style.marginLeft = 0f;
            notice.style.marginRight = 0f;
        }

        public static VisualElement CreateButtonRow(params VisualElement[] controls)
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 6f;
            foreach (VisualElement control in controls)
            {
                control.style.marginLeft = 0f;
                control.style.marginRight = 4f;
                row.Add(control);
            }

            return row;
        }

        public static VisualElement CreateNavigationRow(float height = 32f)
        {
            VisualElement row = new();
            row.style.height = height;
            row.style.minHeight = height;
            row.style.maxHeight = height;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.Center;
            row.style.paddingLeft = 6f;
            row.style.paddingRight = 6f;

            Label label = new();
            label.style.height = height;
            label.style.minHeight = height;
            label.style.maxHeight = height;
            label.style.flexGrow = 1f;
            label.style.flexShrink = 1f;
            label.style.alignSelf = Align.Center;
            label.style.marginLeft = 0f;
            label.style.marginRight = 0f;
            label.style.marginTop = 0f;
            label.style.marginBottom = 0f;
            label.style.paddingLeft = 0f;
            label.style.paddingRight = 0f;
            label.style.paddingTop = 0f;
            label.style.paddingBottom = 0f;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.Add(label);
            return row;
        }

        /// <summary>
        /// Shows a divider below a navigation row unless that row is the final entry.
        /// </summary>
        public static void SetNavigationRowSeparator(VisualElement row, bool visible)
        {
            if (row == null)
                return;

            row.style.borderBottomWidth = visible ? LoogaEditorStyle.Pixels(1f) : 0f;
            row.style.borderBottomColor = LoogaEditorStyle.TreeLineColor;
        }

        public static Toolbar CreateTabBar(
            string[] labels,
            int selectedIndex,
            Action<int> selectionChanged)
        {
            Toolbar toolbar = new();
            toolbar.style.flexShrink = 0f;
            List<ToolbarToggle> tabs = new(labels.Length);
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                ToolbarToggle tab = new() { text = labels[i] };
                tab.style.flexGrow = 1f;
                tab.SetValueWithoutNotify(i == selectedIndex);
                tab.RegisterValueChangedCallback(evt =>
                {
                    if (!evt.newValue)
                    {
                        if (tabs.TrueForAll(candidate => !candidate.value))
                            tab.SetValueWithoutNotify(true);
                        return;
                    }

                    foreach (ToolbarToggle candidate in tabs)
                    {
                        if (candidate != tab)
                            candidate.SetValueWithoutNotify(false);
                    }

                    selectionChanged?.Invoke(index);
                });
                tabs.Add(tab);
                toolbar.Add(tab);
            }

            return toolbar;
        }

        public static PropertyField CreatePropertyField(
            SerializedObject owner,
            string propertyName,
            string label = null)
        {
            SerializedProperty property = owner?.FindProperty(propertyName);
            if (property == null)
                return null;

            PropertyField field = string.IsNullOrWhiteSpace(label)
                ? new PropertyField(property)
                : new PropertyField(property, label);
            field.Bind(owner);
            return field;
        }

        public static void DisableCollectionRowHover(BaseVerticalCollectionView collection)
        {
            AddSharedStyleSheet(collection);
            collection.AddToClassList(NoCollectionRowHoverClass);
        }

        internal static Color FoldoutTriangleColor => EditorStyles.popup.normal.textColor;

        public static void AddSharedStyleSheet(VisualElement root)
        {
            _sharedStyleSheet ??= AssetDatabase.LoadAssetAtPath<StyleSheet>(SharedStyleSheetPath);
            if (_sharedStyleSheet != null)
                root.styleSheets.Add(_sharedStyleSheet);
        }
    }

    internal sealed class LoogaFoldoutTriangle : VisualElement
    {
        private readonly Foldout _foldout;

        public LoogaFoldoutTriangle(Foldout foldout)
        {
            _foldout = foldout;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Relative;
            style.width = 10f;
            style.minWidth = 10f;
            style.maxWidth = 10f;
            style.height = 10f;
            style.minHeight = 10f;
            style.maxHeight = 10f;
            style.flexShrink = 0f;
            style.marginRight = 6f;
            style.opacity = 1f;
            generateVisualContent += DrawTriangle;
            _foldout.RegisterValueChangedCallback(_ => MarkDirtyRepaint());
            RegisterCallback<AttachToPanelEvent>(_ => MarkDirtyRepaint());
        }

        private void DrawTriangle(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            painter.fillColor = LoogaUiToolkitStyle.FoldoutTriangleColor;
            painter.BeginPath();
            if (_foldout.value)
            {
                painter.MoveTo(new Vector2(1f, 2.5f));
                painter.LineTo(new Vector2(9f, 2.5f));
                painter.LineTo(new Vector2(5f, 8.5f));
            }
            else
            {
                painter.MoveTo(new Vector2(2.5f, 1f));
                painter.LineTo(new Vector2(8.5f, 5f));
                painter.LineTo(new Vector2(2.5f, 9f));
            }

            painter.ClosePath();
            painter.Fill();
        }
    }
}
