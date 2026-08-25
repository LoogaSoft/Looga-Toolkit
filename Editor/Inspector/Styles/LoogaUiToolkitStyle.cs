using UnityEditor;
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
        public const float FoldoutTriangleInset = 10f;

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
            header.style.paddingLeft = 0f;

            Label label = header.Q<Label>(className: "unity-toggle__label");
            if (label != null)
                label.style.unityFontStyleAndWeight = FontStyle.Normal;

            VisualElement input = header.Q<VisualElement>(className: "unity-toggle__input");
            if (input != null)
            {
                input.style.marginLeft = FoldoutTriangleInset;
                input.style.marginRight = 4f;
            }

            VisualElement triangle = header.Q<VisualElement>(className: "unity-toggle__checkmark");
            if (triangle != null)
            {
                triangle.style.unityBackgroundImageTintColor = FoldoutTriangleColor;
                triangle.style.opacity = 1f;
            }
        }

        public static void DisableCollectionRowHover(BaseVerticalCollectionView collection)
        {
            AddSharedStyleSheet(collection);
            collection.AddToClassList(NoCollectionRowHoverClass);
        }

        private static Color FoldoutTriangleColor => EditorGUIUtility.isProSkin
            ? new Color(0.76f, 0.76f, 0.76f)
            : new Color(0.28f, 0.28f, 0.28f);

        private static void AddSharedStyleSheet(VisualElement root)
        {
            _sharedStyleSheet ??= AssetDatabase.LoadAssetAtPath<StyleSheet>(SharedStyleSheetPath);
            if (_sharedStyleSheet != null)
                root.styleSheets.Add(_sharedStyleSheet);
        }
    }
}
