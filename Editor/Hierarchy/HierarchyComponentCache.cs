using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    internal readonly struct HierarchyComponentEntry
    {
        internal HierarchyComponentEntry(Type componentType, Texture icon, int count)
        {
            ComponentType = componentType;
            Icon = icon;
            Count = count;
        }

        internal Type ComponentType { get; }

        internal Texture Icon { get; }

        internal int Count { get; }
    }

    internal sealed class HierarchyComponentSummary
    {
        internal HierarchyComponentSummary(
            bool isStatic,
            HierarchyComponentEntry[] components,
            int monoBehaviourCount,
            int missingScriptCount)
        {
            IsStatic = isStatic;
            Components = components;
            MonoBehaviourCount = monoBehaviourCount;
            MissingScriptCount = missingScriptCount;
            MonoBehaviourTooltip = BuildMonoBehaviourTooltip(monoBehaviourCount, missingScriptCount);
        }

        internal bool IsStatic { get; }

        internal HierarchyComponentEntry[] Components { get; }

        internal int MonoBehaviourCount { get; }

        internal int MissingScriptCount { get; }

        internal string MonoBehaviourTooltip { get; }

        internal int GetVisibleIndicatorCount(int maximumComponentIcons)
        {
            int componentIndicatorCount = Components.Length + (MonoBehaviourCount > 0 ? 1 : 0);
            return Mathf.Min(componentIndicatorCount, maximumComponentIcons) + (IsStatic ? 1 : 0);
        }

        private static string BuildMonoBehaviourTooltip(int monoBehaviourCount, int missingScriptCount)
        {
            string tooltip =
                $"{monoBehaviourCount} generic MonoBehaviour{(monoBehaviourCount == 1 ? string.Empty : "s")}";
            if (missingScriptCount > 0)
            {
                tooltip += $"\n{missingScriptCount} missing script{(missingScriptCount == 1 ? string.Empty : "s")}";
            }

            return tooltip;
        }
    }

    [InitializeOnLoad]
    internal static class HierarchyComponentCache
    {
        private static readonly Dictionary<int, HierarchyComponentSummary> Cache = new();
        private static readonly Dictionary<int, StaticTooltipEntry> StaticTooltips = new();
        private static readonly List<Component> Components = new();
        private static readonly List<HierarchyComponentEntry> ComponentEntries = new();
        private static readonly Texture GenericScriptIcon = EditorGUIUtility.IconContent("cs Script Icon").image;

        // Unity 6 keeps these legacy bits in serialized scenes but marks their enum names obsolete.
        private const StaticEditorFlags NavigationStaticFlag = (StaticEditorFlags)8;
        private const StaticEditorFlags OffMeshLinkGenerationFlag = (StaticEditorFlags)32;
        private const StaticEditorFlags KnownStaticFlags =
            StaticEditorFlags.ContributeGI |
            StaticEditorFlags.OccluderStatic |
            StaticEditorFlags.BatchingStatic |
            NavigationStaticFlag |
            StaticEditorFlags.OccludeeStatic |
            OffMeshLinkGenerationFlag |
            StaticEditorFlags.ReflectionProbeStatic;

        static HierarchyComponentCache()
        {
            EditorApplication.hierarchyChanged += Invalidate;
            EditorApplication.projectChanged += Invalidate;
            Undo.undoRedoPerformed += Invalidate;
        }

        internal static HierarchyComponentSummary Get(GameObject gameObject)
        {
            int instanceId = gameObject.GetInstanceID();
            if (Cache.TryGetValue(instanceId, out HierarchyComponentSummary summary))
            {
                return summary;
            }

            summary = Evaluate(gameObject);
            Cache[instanceId] = summary;
            return summary;
        }

        internal static void Invalidate()
        {
            Cache.Clear();
            StaticTooltips.Clear();
            EditorApplication.RepaintHierarchyWindow();
        }

        internal static string GetStaticTooltip(GameObject gameObject)
        {
            int instanceId = gameObject.GetInstanceID();
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(gameObject);
            if (StaticTooltips.TryGetValue(instanceId, out StaticTooltipEntry entry) && entry.Flags == flags)
            {
                return entry.Tooltip;
            }

            string tooltip = BuildStaticTooltip(flags);
            StaticTooltips[instanceId] = new StaticTooltipEntry(flags, tooltip);
            return tooltip;
        }

        private static HierarchyComponentSummary Evaluate(GameObject gameObject)
        {
            Components.Clear();
            ComponentEntries.Clear();
            gameObject.GetComponents(Components);

            int missingScriptCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
            int monoBehaviourCount = missingScriptCount;

            for (int componentIndex = 0; componentIndex < Components.Count; componentIndex++)
            {
                Component component = Components[componentIndex];
                if (component == null || component is Transform)
                {
                    continue;
                }

                Type componentType = component.GetType();
                Texture componentIcon = EditorGUIUtility.ObjectContent(component, componentType).image;
                if (component is MonoBehaviour && !HasDistinctMonoBehaviourIcon(componentIcon))
                {
                    monoBehaviourCount++;
                    continue;
                }

                int entryIndex = FindEntry(componentType);
                if (entryIndex >= 0)
                {
                    HierarchyComponentEntry entry = ComponentEntries[entryIndex];
                    ComponentEntries[entryIndex] =
                        new HierarchyComponentEntry(componentType, entry.Icon, entry.Count + 1);
                }
                else
                {
                    ComponentEntries.Add(new HierarchyComponentEntry(componentType, componentIcon, 1));
                }
            }

            return new HierarchyComponentSummary(
                gameObject.isStatic,
                ComponentEntries.ToArray(),
                monoBehaviourCount,
                missingScriptCount);
        }

        private static int FindEntry(Type componentType)
        {
            for (int index = 0; index < ComponentEntries.Count; index++)
            {
                if (ComponentEntries[index].ComponentType == componentType)
                {
                    return index;
                }
            }

            return -1;
        }

        private static bool HasDistinctMonoBehaviourIcon(Texture icon)
        {
            return icon != null &&
                icon != GenericScriptIcon &&
                !icon.name.EndsWith("Script Icon", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildStaticTooltip(StaticEditorFlags flags)
        {
            if ((flags & KnownStaticFlags) == KnownStaticFlags)
            {
                return "Fully Static";
            }

            StringBuilder tooltip = new();
            AppendStaticFlag(tooltip, flags, StaticEditorFlags.ContributeGI, "Contribute GI");
            AppendStaticFlag(tooltip, flags, StaticEditorFlags.OccluderStatic, "Occluder Static");
            AppendStaticFlag(tooltip, flags, StaticEditorFlags.BatchingStatic, "Batching Static");
            AppendStaticFlag(tooltip, flags, NavigationStaticFlag, "Navigation Static");
            AppendStaticFlag(tooltip, flags, StaticEditorFlags.OccludeeStatic, "Occludee Static");
            AppendStaticFlag(tooltip, flags, OffMeshLinkGenerationFlag, "Off Mesh Link Generation");
            AppendStaticFlag(tooltip, flags, StaticEditorFlags.ReflectionProbeStatic, "Reflection Probe Static");
            return tooltip.Length > 0 ? tooltip.ToString() : "Static";
        }

        private static void AppendStaticFlag(
            StringBuilder tooltip,
            StaticEditorFlags flags,
            StaticEditorFlags expected,
            string label)
        {
            if ((flags & expected) == 0)
            {
                return;
            }

            if (tooltip.Length > 0)
            {
                tooltip.AppendLine();
            }

            tooltip.Append(label);
        }

        private readonly struct StaticTooltipEntry
        {
            internal StaticTooltipEntry(StaticEditorFlags flags, string tooltip)
            {
                Flags = flags;
                Tooltip = tooltip;
            }

            internal StaticEditorFlags Flags { get; }

            internal string Tooltip { get; }
        }
    }
}
