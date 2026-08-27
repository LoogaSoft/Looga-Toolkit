using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    /// <summary>
    /// Draws cached component summaries and static state in Hierarchy rows.
    /// </summary>
    internal static class HierarchyComponentRenderer
    {
        private const float IndicatorSize = 14f;
        private const float IndicatorSpacing = 1f;
        private const float CountLabelGap = 1f;
        private const float CountCharacterWidth = 5f;
        private const float NamePrefixWidth = 18f;
        private const float NameRightPadding = 8f;

        private static readonly Dictionary<Type, GUIContent> ComponentContents = new();
        private static readonly Dictionary<int, string> CountLabels = new();
        private static readonly Dictionary<int, NameWidthEntry> NameWidths = new();

        private static readonly GUIContent ScriptContent =
            CreateIconContent("cs Script Icon", "C#", "MonoBehaviours");
        private static readonly GUIContent OverflowContent =
            new("…", "More component types are hidden by the configured icon limit.");
        private static readonly GUIContent StaticContent = new(string.Empty);
        private static readonly GUIContent NameContent = new();

        private static readonly Vector3[] PushpinHeadVertices = new Vector3[4];
        private static readonly Vector3[] PushpinShoulderVertices = new Vector3[4];
        private static readonly Vector3[] PushpinStemVertices = new Vector3[3];

        private static readonly GUIStyle IconStyle = new(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter,
            imagePosition = ImagePosition.ImageOnly,
            padding = new RectOffset()
        };

        private static readonly GUIStyle FallbackIconStyle = new(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 8,
            padding = new RectOffset()
        };

        private static readonly GUIStyle CountLabelStyle = new(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 8,
            padding = new RectOffset()
        };

        static HierarchyComponentRenderer()
        {
            HierarchyComponentCache.Invalidated += NameWidths.Clear;
        }

        internal static float GetReservedWidth(
            GameObject gameObject,
            Rect rowRect,
            int maximumComponentIcons)
        {
            HierarchyComponentSummary summary = HierarchyComponentCache.Get(gameObject);
            return CalculateLayout(gameObject, rowRect, summary, maximumComponentIcons).Width;
        }

        internal static void Draw(GameObject gameObject, Rect rowRect, int maximumComponentIcons)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            HierarchyComponentSummary summary = HierarchyComponentCache.Get(gameObject);
            IndicatorLayout layout = CalculateLayout(gameObject, rowRect, summary, maximumComponentIcons);
            if (!layout.HasIndicators)
            {
                return;
            }

            float right = rowRect.xMax - 1f;
            if (layout.ShowStatic)
            {
                DrawStatic(gameObject, rowRect, ref right);
            }

            if (layout.ShowScript)
            {
                ScriptContent.tooltip = summary.MonoBehaviourTooltip;
                Color tint = summary.MissingScriptCount > 0
                    ? new Color(1f, 0.58f, 0.24f, 1f)
                    : Color.white;
                DrawIcon(
                    ScriptContent,
                    summary.MonoBehaviourCount,
                    true,
                    tint,
                    rowRect,
                    ref right);
            }

            for (int index = 0; index < layout.VisibleComponentCount; index++)
            {
                HierarchyComponentEntry entry = summary.Components[index];
                DrawIcon(
                    GetComponentContent(entry),
                    entry.Count,
                    entry.Count > 1,
                    Color.white,
                    rowRect,
                    ref right);
            }

            if (layout.ShowOverflow)
            {
                int hiddenCount = summary.Components.Length - layout.VisibleComponentCount;
                DrawIcon(OverflowContent, hiddenCount, true, Color.white, rowRect, ref right);
            }
        }

        private static void DrawStatic(GameObject gameObject, Rect rowRect, ref float right)
        {
            Rect indicatorRect = GetIndicatorRect(rowRect, right);
            StaticContent.tooltip = HierarchyComponentCache.GetStaticTooltip(gameObject);
            DrawPushpin(indicatorRect);
            GUI.Label(indicatorRect, StaticContent, GUIStyle.none);
            right -= IndicatorSize + IndicatorSpacing;
        }

        private static void DrawIcon(
            GUIContent content,
            int count,
            bool showCount,
            Color tint,
            Rect rowRect,
            ref float right)
        {
            float indicatorWidth = GetIndicatorWidth(count, showCount);
            Rect indicatorRect = GetIndicatorRect(rowRect, right, indicatorWidth);
            Rect iconRect = new(indicatorRect.x, indicatorRect.y, IndicatorSize, IndicatorSize);
            Color previousColor = GUI.color;
            GUI.color = tint;
            GUI.Label(
                iconRect,
                content,
                content.image != null ? IconStyle : FallbackIconStyle);
            GUI.color = previousColor;

            if (showCount)
            {
                DrawCountLabel(indicatorRect, count);
            }

            right -= indicatorWidth + IndicatorSpacing;
        }

        private static void DrawCountLabel(Rect indicatorRect, int count)
        {
            string label = GetCountLabel(count);
            Rect labelRect = new(
                indicatorRect.x + IndicatorSize + CountLabelGap,
                indicatorRect.y,
                GetCountLabelWidth(label),
                IndicatorSize);
            GUI.Label(labelRect, label, CountLabelStyle);
        }

        private static Rect GetIndicatorRect(Rect rowRect, float right)
        {
            return GetIndicatorRect(rowRect, right, IndicatorSize);
        }

        private static Rect GetIndicatorRect(Rect rowRect, float right, float width)
        {
            return new Rect(
                right - width,
                rowRect.y + Mathf.Floor((rowRect.height - IndicatorSize) * 0.5f),
                width,
                IndicatorSize);
        }

        private static void GetVisibleComponents(
            HierarchyComponentSummary summary,
            int maximumComponentIcons,
            out int visibleComponentCount,
            out bool showOverflow)
        {
            int remainingSlots = maximumComponentIcons - (summary.MonoBehaviourCount > 0 ? 1 : 0);
            if (remainingSlots <= 0)
            {
                visibleComponentCount = 0;
                showOverflow = false;
                return;
            }

            visibleComponentCount = Mathf.Min(summary.Components.Length, remainingSlots);
            showOverflow = summary.Components.Length > visibleComponentCount && remainingSlots > 1;
            if (showOverflow)
            {
                visibleComponentCount--;
            }
        }

        private static IndicatorLayout CalculateLayout(
            GameObject gameObject,
            Rect rowRect,
            HierarchyComponentSummary summary,
            int maximumComponentIcons)
        {
            GetVisibleComponents(
                summary,
                maximumComponentIcons,
                out int visibleComponentCount,
                out bool showOverflow);

            bool showStatic = summary.IsStatic;
            bool showScript = summary.MonoBehaviourCount > 0;
            float availableWidth = GetAvailableIndicatorWidth(gameObject, rowRect);
            float width = CalculateLayoutWidth(
                summary,
                showStatic,
                showScript,
                visibleComponentCount,
                showOverflow);

            while (width > availableWidth)
            {
                if (showOverflow)
                {
                    showOverflow = false;
                }
                else if (visibleComponentCount > 0)
                {
                    visibleComponentCount--;
                }
                else if (showScript)
                {
                    showScript = false;
                }
                else if (showStatic)
                {
                    showStatic = false;
                }
                else
                {
                    break;
                }

                width = CalculateLayoutWidth(
                    summary,
                    showStatic,
                    showScript,
                    visibleComponentCount,
                    showOverflow);
            }

            return new IndicatorLayout(
                showStatic,
                showScript,
                visibleComponentCount,
                showOverflow,
                width);
        }

        private static float CalculateLayoutWidth(
            HierarchyComponentSummary summary,
            bool showStatic,
            bool showScript,
            int visibleComponentCount,
            bool showOverflow)
        {
            float width = showStatic ? IndicatorSize + IndicatorSpacing : 0f;
            if (showScript)
            {
                width += GetIndicatorWidth(summary.MonoBehaviourCount, true) + IndicatorSpacing;
            }

            for (int index = 0; index < visibleComponentCount; index++)
            {
                HierarchyComponentEntry entry = summary.Components[index];
                width += GetIndicatorWidth(entry.Count, entry.Count > 1) + IndicatorSpacing;
            }

            if (showOverflow)
            {
                int hiddenCount = summary.Components.Length - visibleComponentCount;
                width += GetIndicatorWidth(hiddenCount, true) + IndicatorSpacing;
            }

            return width;
        }

        private static float GetAvailableIndicatorWidth(GameObject gameObject, Rect rowRect)
        {
            float nameWidth = GetNameWidth(gameObject);
            return Mathf.Max(0f, rowRect.width - NamePrefixWidth - nameWidth - NameRightPadding);
        }

        private static float GetNameWidth(GameObject gameObject)
        {
            int instanceId = gameObject.GetInstanceID();
            string objectName = gameObject.name;
            float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            if (NameWidths.TryGetValue(instanceId, out NameWidthEntry entry) &&
                entry.Name == objectName &&
                Mathf.Approximately(entry.PixelsPerPoint, pixelsPerPoint))
            {
                return entry.Width;
            }

            NameContent.text = objectName;
            float width = EditorStyles.label.CalcSize(NameContent).x;
            NameWidths[instanceId] = new NameWidthEntry(objectName, pixelsPerPoint, width);
            return width;
        }

        private static float GetIndicatorWidth(int count, bool showCount)
        {
            if (!showCount)
            {
                return IndicatorSize;
            }

            return IndicatorSize + CountLabelGap + GetCountLabelWidth(GetCountLabel(count));
        }

        private static float GetCountLabelWidth(string label)
        {
            return label.Length * CountCharacterWidth;
        }

        private static void DrawPushpin(Rect indicatorRect)
        {
            float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            float centerX = SnapToPixel(indicatorRect.center.x, pixelsPerPoint);
            float centerY = SnapToPixel(indicatorRect.center.y, pixelsPerPoint);

            SetVertex(PushpinHeadVertices, 0, centerX - 3f, centerY - 4f);
            SetVertex(PushpinHeadVertices, 1, centerX + 3f, centerY - 4f);
            SetVertex(PushpinHeadVertices, 2, centerX + 2f, centerY - 2f);
            SetVertex(PushpinHeadVertices, 3, centerX - 2f, centerY - 2f);

            SetVertex(PushpinShoulderVertices, 0, centerX - 2f, centerY - 2f);
            SetVertex(PushpinShoulderVertices, 1, centerX + 2f, centerY - 2f);
            SetVertex(PushpinShoulderVertices, 2, centerX + 3f, centerY);
            SetVertex(PushpinShoulderVertices, 3, centerX - 3f, centerY);

            SetVertex(PushpinStemVertices, 0, centerX - 0.8f, centerY);
            SetVertex(PushpinStemVertices, 1, centerX + 0.8f, centerY);
            SetVertex(PushpinStemVertices, 2, centerX, centerY + 5f);

            Color previousColor = Handles.color;
            Handles.BeginGUI();
            Handles.color = EditorGUIUtility.isProSkin
                ? new Color(0.76f, 0.80f, 0.86f, 1f)
                : new Color(0.28f, 0.32f, 0.38f, 1f);
            Handles.DrawAAConvexPolygon(PushpinHeadVertices);
            Handles.DrawAAConvexPolygon(PushpinShoulderVertices);
            Handles.DrawAAConvexPolygon(PushpinStemVertices);
            Handles.color = previousColor;
            Handles.EndGUI();
        }

        private static void SetVertex(Vector3[] vertices, int index, float x, float y)
        {
            vertices[index] = new Vector3(x, y, 0f);
        }

        private static float SnapToPixel(float value, float pixelsPerPoint)
        {
            return Mathf.Round(value * pixelsPerPoint) / pixelsPerPoint;
        }

        private static GUIContent GetComponentContent(HierarchyComponentEntry entry)
        {
            Type componentType = entry.ComponentType;
            if (ComponentContents.TryGetValue(componentType, out GUIContent content))
            {
                return content;
            }

            string componentName = ObjectNames.NicifyVariableName(componentType.Name);
            content = entry.Icon != null
                ? new GUIContent(entry.Icon, componentName)
                : new GUIContent(componentName.Substring(0, 1), componentName);
            ComponentContents[componentType] = content;
            return content;
        }

        private static string GetCountLabel(int count)
        {
            int labelKey = count > 99 ? 100 : count;
            if (CountLabels.TryGetValue(labelKey, out string label))
            {
                return label;
            }

            label = count > 99 ? "99+" : count.ToString();
            CountLabels[labelKey] = label;
            return label;
        }

        private static GUIContent CreateIconContent(string iconName, string fallback, string tooltip)
        {
            GUIContent content = EditorGUIUtility.IconContent(iconName);
            if (content.image == null)
            {
                content = new GUIContent(fallback);
            }

            content.tooltip = tooltip;
            return content;
        }

        private readonly struct IndicatorLayout
        {
            internal IndicatorLayout(
                bool showStatic,
                bool showScript,
                int visibleComponentCount,
                bool showOverflow,
                float width)
            {
                ShowStatic = showStatic;
                ShowScript = showScript;
                VisibleComponentCount = visibleComponentCount;
                ShowOverflow = showOverflow;
                Width = width;
            }

            internal bool ShowStatic { get; }

            internal bool ShowScript { get; }

            internal int VisibleComponentCount { get; }

            internal bool ShowOverflow { get; }

            internal float Width { get; }

            internal bool HasIndicators => ShowStatic || ShowScript || VisibleComponentCount > 0 || ShowOverflow;
        }

        private readonly struct NameWidthEntry
        {
            internal NameWidthEntry(string name, float pixelsPerPoint, float width)
            {
                Name = name;
                PixelsPerPoint = pixelsPerPoint;
                Width = width;
            }

            internal string Name { get; }

            internal float PixelsPerPoint { get; }

            internal float Width { get; }
        }
    }
}
