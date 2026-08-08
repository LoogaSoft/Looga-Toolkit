using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Inspector.Editor
{
    public static class LoogaEditorTabs
    {
        private const float TabHeight = 22f;
        private const float TabRowGap = 2f;
        private const float TabGap = 0f;
        private const float TabTextPadding = 24f;
        private static readonly Dictionary<string, float> ToolbarWidthCache = new();
        private static GUIStyle _tabButtonStyle;

        public static int DrawWrappingToolbar(int selectedIndex, string[] tabNames, string cacheKey)
        {
            if (tabNames == null || tabNames.Length == 0)
                return selectedIndex;

            selectedIndex = Mathf.Clamp(selectedIndex, 0, tabNames.Length - 1);
            float availableWidth = GetCachedWidth(cacheKey, EditorGUIUtility.currentViewWidth);
            List<List<int>> rows = BuildRows(tabNames, availableWidth);
            float totalHeight = GetRowsHeight(rows.Count);
            Rect fullRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(totalHeight), GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
                ToolbarWidthCache[cacheKey] = fullRect.width;

            return DrawRows(fullRect, rows, tabNames, selectedIndex);
        }

        public static int DrawWrappingToolbarWithRightControl(
            int selectedIndex,
            string[] tabNames,
            string cacheKey,
            float rightControlWidth,
            float rightControlGap,
            System.Action drawRightControl)
        {
            if (tabNames == null || tabNames.Length == 0)
                return selectedIndex;

            selectedIndex = Mathf.Clamp(selectedIndex, 0, tabNames.Length - 1);
            float fullWidth = GetCachedWidth(cacheKey, EditorGUIUtility.currentViewWidth);
            float reservedWidth = drawRightControl != null ? rightControlWidth + rightControlGap : 0f;
            float toolbarWidth = Mathf.Max(1f, fullWidth - reservedWidth);
            List<List<int>> rows = BuildRows(tabNames, toolbarWidth);
            float totalHeight = GetRowsHeight(rows.Count);

            Rect fullRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(totalHeight), GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                ToolbarWidthCache[cacheKey] = fullRect.width;

            Rect tabsRect = new(fullRect.x, fullRect.y, Mathf.Max(1f, fullRect.width - reservedWidth), fullRect.height);
            int newSelectedIndex = DrawRows(tabsRect, rows, tabNames, selectedIndex);

            if (drawRightControl != null)
            {
                Rect controlRect = new(fullRect.xMax - rightControlWidth, fullRect.y, rightControlWidth, TabHeight);
                GUILayout.BeginArea(controlRect);
                drawRightControl();
                GUILayout.EndArea();
            }

            return newSelectedIndex;
        }

        public static int DrawToolbar(Rect position, int selectedIndex, string[] tabNames)
        {
            if (tabNames == null || tabNames.Length == 0)
                return selectedIndex;

            selectedIndex = Mathf.Clamp(selectedIndex, 0, tabNames.Length - 1);
            List<List<int>> rows = BuildRows(tabNames, Mathf.Max(1f, position.width));
            return DrawRows(position, rows, tabNames, selectedIndex);
        }

        public static float GetToolbarHeight(string[] tabNames, float availableWidth)
        {
            if (tabNames == null || tabNames.Length == 0)
                return 0f;

            List<List<int>> rows = BuildRows(tabNames, Mathf.Max(1f, availableWidth));
            return GetRowsHeight(rows.Count);
        }

        private static int DrawRows(Rect fullRect, List<List<int>> rows, string[] tabNames, int selectedIndex)
        {
            EnsureStyles();
            if (Event.current.type == EventType.MouseMove && fullRect.Contains(Event.current.mousePosition))
                RepaintMouseOverWindow();

            int newSelectedIndex = selectedIndex;

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                Rect rowRect = PixelSnap(new Rect(
                    fullRect.x,
                    fullRect.y + rowIndex * (TabHeight + TabRowGap),
                    fullRect.width,
                    TabHeight));
                List<int> row = rows[rowIndex];
                if (row.Count == 0)
                    continue;

                float availableTabWidth = Mathf.Max(1f, rowRect.width - TabGap * (row.Count - 1));
                float tabWidth = availableTabWidth / row.Count;
                for (int localIndex = 0; localIndex < row.Count; localIndex++)
                {
                    int tabIndex = row[localIndex];
                    float tabX = rowRect.x + localIndex * (tabWidth + TabGap);
                    Rect tabRect = PixelSnap(new Rect(
                        tabX,
                        rowRect.y,
                        localIndex == row.Count - 1 ? rowRect.xMax - tabX : tabWidth,
                        rowRect.height));
                    bool selected = tabIndex == selectedIndex;
                    if (GUI.Toggle(
                            tabRect,
                            selected,
                            new GUIContent(tabNames[tabIndex]),
                            _tabButtonStyle) && !selected)
                        newSelectedIndex = tabIndex;
                }
            }

            return newSelectedIndex;
        }

        private static void RepaintMouseOverWindow()
        {
            EditorWindow.mouseOverWindow?.Repaint();
        }

        private static List<List<int>> BuildRows(string[] tabNames, float availableWidth)
        {
            EnsureStyles();

            float[] minWidths = new float[tabNames.Length];
            for (int i = 0; i < tabNames.Length; i++)
            {
                Vector2 size = _tabButtonStyle.CalcSize(PropertyUtils.GetContent(tabNames[i]));
                minWidths[i] = Mathf.Ceil(size.x + TabTextPadding);
            }

            var rows = new List<List<int>>();
            var currentRow = new List<int>();
            float rowMaxWidth = 0f;

            for (int i = 0; i < tabNames.Length; i++)
            {
                float nextMaxWidth = Mathf.Max(rowMaxWidth, minWidths[i]);
                int nextCount = currentRow.Count + 1;
                bool rowWouldFit = nextMaxWidth * nextCount + TabGap * (nextCount - 1) <= availableWidth;

                if (currentRow.Count > 0 && !rowWouldFit)
                {
                    rows.Add(currentRow);
                    currentRow = new List<int>();
                    rowMaxWidth = 0f;
                }

                currentRow.Add(i);
                rowMaxWidth = Mathf.Max(rowMaxWidth, minWidths[i]);
            }

            if (currentRow.Count > 0)
                rows.Add(currentRow);

            return rows;
        }

        private static float GetCachedWidth(string cacheKey, float fallbackWidth)
        {
            return ToolbarWidthCache.TryGetValue(cacheKey, out float width) && width > 0f
                ? width
                : Mathf.Max(1f, fallbackWidth - 40f);
        }

        private static float GetRowsHeight(int rowCount)
        {
            if (rowCount <= 0)
                return 0f;

            return rowCount * TabHeight + (rowCount - 1) * TabRowGap;
        }

        private static void EnsureStyles()
        {
            if (_tabButtonStyle != null)
                return;

            _tabButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                fontStyle = FontStyle.Normal,
                padding = new RectOffset(6, 6, 0, 1),
                fixedHeight = 0f
            };
        }

        private static Rect PixelSnap(Rect rect)
        {
            return Rect.MinMaxRect(
                PixelSnapValue(rect.xMin),
                PixelSnapValue(rect.yMin),
                PixelSnapValue(rect.xMax),
                PixelSnapValue(rect.yMax));
        }

        private static float PixelSnapValue(float value)
        {
            float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            return Mathf.Round(value * pixelsPerPoint) / pixelsPerPoint;
        }

    }
}
