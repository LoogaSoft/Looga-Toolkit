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
        private const float NameSafetyGap = 8f;
        private const float RevealDuration = 0.12f;
        private const float InitialRevealProgress = 0.18f;
        private const string SummaryIconPath =
            "Packages/com.loogasoft.loogatoolkit/Editor/Inspector/Icons/Remix/more-2-line.png";

        private static readonly Dictionary<Type, GUIContent> ComponentContents = new();
        private static readonly Dictionary<int, string> CountLabels = new();
        private static readonly Dictionary<int, RevealState> RevealStates = new();
        private static readonly List<int> RevealStateIds = new();

        private static readonly GUIContent ScriptContent =
            CreateIconContent("cs Script Icon", "C#", "MonoBehaviours");
        private static readonly GUIContent OverflowContent =
            new("…", "More component types are hidden by the configured icon limit.");
        private static readonly GUIContent SummaryContent = new();
        private static Texture2D _summaryIcon;

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
            EditorApplication.update += TickRevealAnimations;
            EditorApplication.delayCall += EnableHierarchyMouseMoveEvents;
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
                summaryRect.xMax - layout.Width,
                rowRect.y,
                layout.Width,
                rowRect.height);
            bool pointerOverControl = summaryRect.Contains(Event.current.mousePosition) ||
                (currentProgress > 0f && expandedRect.Contains(Event.current.mousePosition));
            float progress = UpdateReveal(instanceId, pointerOverControl);

            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            float revealedWidth = layout.Width * progress;
            float occupiedWidth = Mathf.Max(summaryRect.width, revealedWidth);
            Rect clearRect = new(
                summaryRect.xMax - occupiedWidth - NameSafetyGap,
                rowRect.y,
                occupiedWidth + NameSafetyGap,
                rowRect.height);
            EditorGUI.DrawRect(
                clearRect,
                HierarchyPresentationRenderer.ResolveRowBackground(gameObject, rowRect));

            float nameRight = summaryRect.x - NameSafetyGap;
            if (progress > 0.01f)
            {
                nameRight = summaryRect.xMax - layout.Width - NameSafetyGap;
            }

            HierarchyPresentationRenderer.DrawTruncatedNameIfNeeded(
                gameObject,
                rowRect,
                nameRight);

            if (revealedWidth > 0.01f)
            {
                DrawDetails(summary, rowRect, summaryRect.xMax, layout, progress);
            }

            if (progress <= 0.01f)
            {
                SummaryContent.tooltip = summary.ComponentTooltip;
                DrawSummary(summaryRect);
            }
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

        private static void DrawSummary(Rect rect)
        {
            _summaryIcon ??= AssetDatabase.LoadAssetAtPath<Texture2D>(SummaryIconPath);
            if (_summaryIcon == null)
            {
                SummaryContent.image = null;
                SummaryContent.text = "◦";
                GUI.Label(rect, SummaryContent, FallbackIconStyle);
                return;
            }

            SummaryContent.text = string.Empty;
            SummaryContent.image = _summaryIcon;
            Color previousColor = GUI.color;
            GUI.color = EditorGUIUtility.isProSkin
                ? new Color(0.78f, 0.78f, 0.78f, 1f)
                : new Color(0.36f, 0.36f, 0.36f, 1f);
            GUI.Label(rect, SummaryContent, IconStyle);
            GUI.color = previousColor;
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
                state = new RevealState(0f, 0f, now);
            }

            state = AdvanceReveal(state, now);
            float target = pointerOverControl ? 1f : 0f;
            if (!Mathf.Approximately(target, state.Target))
            {
                float progress = state.Progress;
                if (target > 0f && progress < InitialRevealProgress)
                {
                    progress = InitialRevealProgress;
                }

                state = new RevealState(progress, target, now);
                RevealStates[instanceId] = state;
                EditorApplication.RepaintHierarchyWindow();
            }
            else
            {
                RevealStates[instanceId] = state;
            }

            return state.Progress;
        }

        private static void TickRevealAnimations()
        {
            if (RevealStates.Count == 0)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            bool changed = false;
            RevealStateIds.Clear();
            foreach (int instanceId in RevealStates.Keys)
            {
                RevealStateIds.Add(instanceId);
            }

            for (int index = 0; index < RevealStateIds.Count; index++)
            {
                int instanceId = RevealStateIds[index];
                RevealState previousState = RevealStates[instanceId];
                RevealState state = AdvanceReveal(previousState, now);
                changed |= !Mathf.Approximately(state.Progress, previousState.Progress);

                if (Mathf.Approximately(state.Progress, 0f) &&
                    Mathf.Approximately(state.Target, 0f))
                {
                    RevealStates.Remove(instanceId);
                }
                else
                {
                    RevealStates[instanceId] = state;
                }
            }

            if (changed)
            {
                EditorApplication.RepaintHierarchyWindow();
            }
        }

        private static RevealState AdvanceReveal(RevealState state, double now)
        {
            float deltaTime = Mathf.Clamp((float)(now - state.LastUpdate), 0f, 0.1f);
            float progress = Mathf.MoveTowards(
                state.Progress,
                state.Target,
                RevealDuration > 0f ? deltaTime / RevealDuration : 1f);
            return new RevealState(progress, state.Target, now);
        }

        private static void EnableHierarchyMouseMoveEvents()
        {
            EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (int index = 0; index < windows.Length; index++)
            {
                EditorWindow window = windows[index];
                if (window != null && window.GetType().Name == "SceneHierarchyWindow")
                {
                    window.wantsMouseMove = true;
                }
            }
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
            internal RevealState(float progress, float target, double lastUpdate)
            {
                Progress = progress;
                Target = target;
                LastUpdate = lastUpdate;
            }

            internal float Progress { get; }

            internal float Target { get; }

            internal double LastUpdate { get; }
        }
    }
}
