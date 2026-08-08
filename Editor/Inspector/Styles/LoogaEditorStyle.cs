using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Inspector.Editor
{
    /// <summary>
    /// Provides shared colors and pixel helpers for Looga editor UI.
    /// Colors stay close to Unity's editor skin so Looga controls fit beside native controls.
    /// </summary>
    public static class LoogaEditorStyle
    {
        public const int AccentRailWidth = 4;

        public static Color BoxColor => SkinColor(
            new Color(0.220f, 0.220f, 0.220f, 1f),
            new Color(0.820f, 0.820f, 0.820f, 1f));

        public static Color AlternateBoxColor => SkinColor(
            new Color(0.255f, 0.255f, 0.255f, 1f),
            new Color(0.860f, 0.860f, 0.860f, 1f));

        public static Color HoverColor => SkinColor(
            new Color(0.300f, 0.300f, 0.300f, 1f),
            new Color(0.900f, 0.900f, 0.900f, 1f));

        public static Color AccentRailColor => SkinColor(
            new Color(0.34f, 0.34f, 0.34f, 1f),
            new Color(0.62f, 0.62f, 0.62f, 1f));

        public static Color ActionAccentColor => SkinColor(
            new Color(0.24f, 0.49f, 0.74f, 1f),
            new Color(0.24f, 0.49f, 0.74f, 1f));

        public static Color SeparatorColor => SkinColor(
            new Color(0.145f, 0.145f, 0.145f, 1f),
            new Color(0.68f, 0.68f, 0.68f, 1f));

        public static Color ArrowColor => SkinColor(
            EditorStyles.foldout.normal.textColor,
            EditorStyles.foldout.normal.textColor);

        public static Color DragHandleColor => SkinColor(
            new Color(0.48f, 0.48f, 0.48f, 1f),
            new Color(0.36f, 0.36f, 0.36f, 1f));

        public static Color ListRowColor => SkinColor(
            new Color(0.245f, 0.245f, 0.245f, 1f),
            new Color(0.850f, 0.850f, 0.850f, 1f));

        public static Color ListHoverColor => SkinColor(
            new Color(0.300f, 0.300f, 0.300f, 1f),
            new Color(0.910f, 0.910f, 0.910f, 1f));

        public static Color SelectionColor => SkinColor(
            new Color(0.24f, 0.49f, 0.74f, 1f),
            new Color(0.24f, 0.49f, 0.74f, 1f));

        public static Color TreeLineColor => SkinColor(
            new Color(0.37f, 0.37f, 0.37f, 1f),
            new Color(0.55f, 0.55f, 0.55f, 1f));

        public static Color TabBarColor => SkinColor(
            new Color(0.190f, 0.190f, 0.190f, 1f),
            new Color(0.760f, 0.760f, 0.760f, 1f));

        public static Color TabColor => SkinColor(
            new Color(0.220f, 0.220f, 0.220f, 1f),
            new Color(0.820f, 0.820f, 0.820f, 1f));

        public static Color SelectedTabColor => SkinColor(
            new Color(0.300f, 0.300f, 0.300f, 1f),
            new Color(0.900f, 0.900f, 0.900f, 1f));

        public static Color TabHoverColor => SkinColor(
            new Color(0.290f, 0.290f, 0.290f, 1f),
            new Color(0.880f, 0.880f, 0.880f, 1f));

        public static Color TextColor => EditorStyles.label.normal.textColor;

        public static Rect PixelSnap(Rect rect)
        {
            return Rect.MinMaxRect(
                PixelSnapValue(rect.xMin),
                PixelSnapValue(rect.yMin),
                PixelSnapValue(rect.xMax),
                PixelSnapValue(rect.yMax));
        }

        public static float PixelSnapValue(float value)
        {
            float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            return Mathf.Round(value * pixelsPerPoint) / pixelsPerPoint;
        }

        public static float PixelCeil(float value)
        {
            float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            return Mathf.Ceil(value * pixelsPerPoint) / pixelsPerPoint;
        }

        public static float Pixels(float pixelCount)
        {
            return pixelCount / EditorGUIUtility.pixelsPerPoint;
        }

        private static Color SkinColor(Color pro, Color personal)
        {
            return EditorGUIUtility.isProSkin ? pro : personal;
        }
    }
}
