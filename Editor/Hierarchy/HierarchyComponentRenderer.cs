using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    /// <summary>
    /// Draws one component summary control and reveals its cached component icons on hover.
    /// </summary>
    internal static class HierarchyComponentRenderer
    {
        private const float IndicatorSize = 14f;
        private const float IndicatorSpacing = 1f;
        private const float CountLabelGap = 1f;
        private const float CountCharacterWidth = 5f;
        private const float RevealDuration = 0.12f;

        private static readonly Dictionary<Type, GUIContent> ComponentContents = new();
        private static readonly Dictionary<int, string> CountLabels = new();
        private static readonly Dictionary<int, RevealState> RevealStates = new();

        private static readonly GUIContent ScriptContent =
            CreateIconContent("cs Script Icon", "C#", "MonoBehaviours");
        private static readonly GUIContent OverflowContent =
            new("…", "More component types are hidden by the configured icon limit.");
        private static readonly GUIContent SummaryContent = new();

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
            HierarchyComponentCache.Invalidated += RevealStates.Clear;
        }

        internal static float GetReservedWidth(
            GameObject gameObject,
            Rect rowRect,
            int maximumComponentIcons)
        {
            HierarchyComponentSummary summary = HierarchyComponentCache.Get(gameObject);
            if (!summary.HasComponents)
            {
                return 0f;
            }

            DetailLayout layout = CalculateDetailLayout(summary, maximumComponentIcons);
            return IndicatorSize +
                IndicatorSpacing +
                layout.Width * GetRevealProgress(gameObject.GetInstanceID());
        }

        internal static void Draw(GameObject gameObject, Rect rowRect, int maximumComponentIcons)
        {
            HierarchyComponentSummary summary = HierarchyComponentCache.Get(gameObject);
            int instanceId = gameObject.GetInstanceID();
            if (!summary.HasComponents)
            {
                RevealStates.Remove(instanceId);
                return;
            }

            DetailLayout layout = CalculateDetailLayout(summary, maximumComponentIcons);
            Rect summaryRect = GetSummaryRect(rowRect);
            float currentProgress = GetRevealProgress(instanceId);
            Rect expandedRect = new(
                summaryRect.x - layout.Width,
                rowRect.y,
                layout.Width + summaryRect.width,
                rowRect.height);
            bool pointerOverControl = summaryRect.Contains(Event.current.mousePosition) ||
                (currentProgress > 0f && expandedRect.Contains(Event.current.mousePosition));
            float progress = UpdateReveal(instanceId, pointerOverControl);

            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            float revealedWidth = layout.Width * progress;
            Rect clearRect = new(
                summaryRect.x - revealedWidth,
                rowRect.y,
                summaryRect.width + revealedWidth,
                rowRect.height);
            EditorGUI.DrawRect(
                clearRect,
                HierarchyPresentationRenderer.ResolveRowBackground(gameObject, rowRect));

            if (revealedWidth > 0.01f)
            {
                DrawDetails(summary, rowRect, summaryRect.x, layout, progress);
            }

            SummaryContent.tooltip = summary.ComponentTooltip;
            DrawSummaryGlyph(summaryRect);
            GUI.Label(summaryRect, SummaryContent, GUIStyle.none);
        }

        private static void DrawDetails(
            HierarchyComponentSummary summary,
            Rect rowRect,
            float expansionRight,
            DetailLayout layout,
            float progress)
        {
            float revealedWidth = layout.Width * progress;
            Rect clipRect = new(
                expansionRight - revealedWidth,
                rowRect.y,
                revealedWidth,
                rowRect.height);
            GUI.BeginGroup(clipRect);

            Rect localRowRect = new(
                rowRect.x - clipRect.x,
                0f,
                rowRect.width,
                rowRect.height);
            float right = expansionRight - clipRect.x + layout.Width * (1f - progress);

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
                    localRowRect,
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
                    localRowRect,
                    ref right);
            }

            if (layout.ShowOverflow)
            {
                int hiddenCount = summary.Components.Length - layout.VisibleComponentCount;
                DrawIcon(OverflowContent, hiddenCount, true, Color.white, localRowRect, ref right);
            }

            GUI.EndGroup();
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

        private static void DrawSummaryGlyph(Rect rect)
        {
            float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            float lineThickness = Mathf.Max(1f / pixelsPerPoint, 1f);
            float dotSize = Mathf.Max(2f / pixelsPerPoint, 2f);
            float dotX = SnapToPixel(rect.x + 1.5f, pixelsPerPoint);
            float barX = SnapToPixel(rect.x + 5f, pixelsPerPoint);
            float barWidth = Mathf.Max(1f, rect.xMax - barX - 1.5f);
            Color color = EditorGUIUtility.isProSkin
                ? new Color(0.76f, 0.80f, 0.86f, 1f)
                : new Color(0.28f, 0.32f, 0.38f, 1f);

            for (int index = 0; index < 3; index++)
            {
                float centerY = SnapToPixel(rect.y + 3f + index * 4f, pixelsPerPoint);
                EditorGUI.DrawRect(
                    new Rect(dotX, centerY - dotSize * 0.5f, dotSize, dotSize),
                    color);
                EditorGUI.DrawRect(
                    new Rect(barX, centerY - lineThickness * 0.5f, barWidth, lineThickness),
                    color);
            }
        }

        private static DetailLayout CalculateDetailLayout(
            HierarchyComponentSummary summary,
            int maximumComponentIcons)
        {
            GetVisibleComponents(
                summary,
                maximumComponentIcons,
                out int visibleComponentCount,
                out bool showOverflow);

            bool showScript = summary.MonoBehaviourCount > 0;
            float width = showScript
                ? GetIndicatorWidth(summary.MonoBehaviourCount, true) + IndicatorSpacing
                : 0f;

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

            return new DetailLayout(showScript, visibleComponentCount, showOverflow, width);
        }

        private static void GetVisibleComponents(
            HierarchyComponentSummary summary,
            int maximumComponentIcons,
            out int visibleComponentCount,
            out bool showOverflow)
        {
            int remainingSlots = Mathf.Max(
                0,
                maximumComponentIcons - (summary.MonoBehaviourCount > 0 ? 1 : 0));
            visibleComponentCount = Mathf.Min(summary.Components.Length, remainingSlots);
            showOverflow = summary.Components.Length > visibleComponentCount && remainingSlots > 1;
            if (showOverflow)
            {
                visibleComponentCount--;
            }
        }

        private static float UpdateReveal(int instanceId, bool pointerOverControl)
        {
            double now = EditorApplication.timeSinceStartup;
            if (!RevealStates.TryGetValue(instanceId, out RevealState state))
            {
                state = new RevealState(0f, now);
            }

            float deltaTime = Mathf.Clamp((float)(now - state.LastUpdate), 0f, 0.1f);
            float target = pointerOverControl ? 1f : 0f;
            float progress = Mathf.MoveTowards(
                state.Progress,
                target,
                RevealDuration > 0f ? deltaTime / RevealDuration : 1f);
            bool changed = !Mathf.Approximately(progress, state.Progress);
            RevealStates[instanceId] = new RevealState(progress, now);

            if (changed || !Mathf.Approximately(progress, target))
            {
                EditorApplication.RepaintHierarchyWindow();
            }

            return progress;
        }

        private static float GetRevealProgress(int instanceId)
        {
            return RevealStates.TryGetValue(instanceId, out RevealState state)
                ? state.Progress
                : 0f;
        }

        private static Rect GetSummaryRect(Rect rowRect)
        {
            return new Rect(
                rowRect.xMax - IndicatorSize - 1f,
                rowRect.y + Mathf.Floor((rowRect.height - IndicatorSize) * 0.5f),
                IndicatorSize,
                IndicatorSize);
        }

        private static Rect GetIndicatorRect(Rect rowRect, float right, float width)
        {
            return new Rect(
                right - width,
                rowRect.y + Mathf.Floor((rowRect.height - IndicatorSize) * 0.5f),
                width,
                IndicatorSize);
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

        private static float SnapToPixel(float value, float pixelsPerPoint)
        {
            return Mathf.Round(value * pixelsPerPoint) / pixelsPerPoint;
        }

        private readonly struct DetailLayout
        {
            internal DetailLayout(
                bool showScript,
                int visibleComponentCount,
                bool showOverflow,
                float width)
            {
                ShowScript = showScript;
                VisibleComponentCount = visibleComponentCount;
                ShowOverflow = showOverflow;
                Width = width;
            }

            internal bool ShowScript { get; }

            internal int VisibleComponentCount { get; }

            internal bool ShowOverflow { get; }

            internal float Width { get; }
        }

        private readonly struct RevealState
        {
            internal RevealState(float progress, double lastUpdate)
            {
                Progress = progress;
                LastUpdate = lastUpdate;
            }

            internal float Progress { get; }

            internal double LastUpdate { get; }
        }
    }
}
