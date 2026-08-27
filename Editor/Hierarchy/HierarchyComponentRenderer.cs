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
        private const float CountBadgeHeight = 9f;

        private static readonly Dictionary<Type, GUIContent> ComponentContents = new();
        private static readonly Dictionary<int, string> CountLabels = new();

        private static readonly GUIContent ScriptContent =
            CreateIconContent("cs Script Icon", "C#", "MonoBehaviours");
        private static readonly GUIContent OverflowContent =
            new("…", "More component types are hidden by the configured icon limit.");
        private static readonly GUIContent StaticContent = new("S");

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

        private static readonly GUIStyle StaticStyle = new(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 10
        };

        private static GUIStyle _countBadgeStyle;

        internal static float GetReservedWidth(GameObject gameObject, int maximumComponentIcons)
        {
            HierarchyComponentSummary summary = HierarchyComponentCache.Get(gameObject);
            int indicatorCount = summary.GetVisibleIndicatorCount(maximumComponentIcons);
            return indicatorCount * (IndicatorSize + IndicatorSpacing);
        }

        internal static void Draw(GameObject gameObject, Rect rowRect, int maximumComponentIcons)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            HierarchyComponentSummary summary = HierarchyComponentCache.Get(gameObject);
            if (summary.GetVisibleIndicatorCount(maximumComponentIcons) == 0)
            {
                return;
            }

            float right = rowRect.xMax - 1f;
            if (summary.IsStatic)
            {
                DrawStatic(gameObject, rowRect, ref right);
            }

            int remainingSlots = maximumComponentIcons;
            if (summary.MonoBehaviourCount > 0 && remainingSlots > 0)
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
                remainingSlots--;
            }

            int visibleComponentCount = Mathf.Min(summary.Components.Length, remainingSlots);
            bool showOverflow = summary.Components.Length > visibleComponentCount && remainingSlots > 1;
            if (showOverflow)
            {
                visibleComponentCount--;
            }

            for (int index = 0; index < visibleComponentCount; index++)
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

            if (showOverflow)
            {
                int hiddenCount = summary.Components.Length - visibleComponentCount;
                DrawIcon(OverflowContent, hiddenCount, true, Color.white, rowRect, ref right);
            }
        }

        private static void DrawStatic(GameObject gameObject, Rect rowRect, ref float right)
        {
            Rect indicatorRect = GetIndicatorRect(rowRect, right);
            StaticContent.tooltip = HierarchyComponentCache.GetStaticTooltip(gameObject);
            GUI.Label(indicatorRect, StaticContent, StaticStyle);
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
            Rect indicatorRect = GetIndicatorRect(rowRect, right);
            Color previousColor = GUI.color;
            GUI.color = tint;
            GUI.Label(
                indicatorRect,
                content,
                content.image != null ? IconStyle : FallbackIconStyle);
            GUI.color = previousColor;

            if (showCount)
            {
                DrawCountBadge(indicatorRect, count);
            }

            right -= IndicatorSize + IndicatorSpacing;
        }

        private static void DrawCountBadge(Rect indicatorRect, int count)
        {
            string label = GetCountLabel(count);
            float badgeWidth = count < 10 ? CountBadgeHeight : CountBadgeHeight + 4f;
            Rect badgeRect = new(
                indicatorRect.xMax - badgeWidth,
                indicatorRect.yMax - CountBadgeHeight,
                badgeWidth,
                CountBadgeHeight);
            GUI.Label(badgeRect, label, CountBadgeStyle);
        }

        private static Rect GetIndicatorRect(Rect rowRect, float right)
        {
            return new Rect(
                right - IndicatorSize,
                rowRect.y + Mathf.Floor((rowRect.height - IndicatorSize) * 0.5f),
                IndicatorSize,
                IndicatorSize);
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

        private static GUIStyle CountBadgeStyle
        {
            get
            {
                if (_countBadgeStyle != null)
                {
                    return _countBadgeStyle;
                }

                GUIStyle unityBadgeStyle = GUI.skin.FindStyle("CN CountBadge");
                _countBadgeStyle = unityBadgeStyle != null
                    ? new GUIStyle(unityBadgeStyle)
                    : new GUIStyle(EditorStyles.miniBoldLabel);
                _countBadgeStyle.alignment = TextAnchor.MiddleCenter;
                _countBadgeStyle.fontSize = 8;
                _countBadgeStyle.padding = new RectOffset();
                return _countBadgeStyle;
            }
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
    }
}
