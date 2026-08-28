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
            HierarchyComponentEntry[] components,
            int monoBehaviourCount,
            int missingScriptCount)
        {
            Components = components;
            MonoBehaviourCount = monoBehaviourCount;
            MissingScriptCount = missingScriptCount;
            MonoBehaviourTooltip = BuildMonoBehaviourTooltip(monoBehaviourCount, missingScriptCount);
            ComponentTooltip = BuildComponentTooltip(components, monoBehaviourCount, missingScriptCount);
        }

        internal HierarchyComponentEntry[] Components { get; }

        internal int MonoBehaviourCount { get; }

        internal int MissingScriptCount { get; }

        internal string MonoBehaviourTooltip { get; }

        internal string ComponentTooltip { get; }

        internal bool HasComponents => Components.Length > 0 || MonoBehaviourCount > 0;

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

        private static string BuildComponentTooltip(
            HierarchyComponentEntry[] components,
            int monoBehaviourCount,
            int missingScriptCount)
        {
            StringBuilder tooltip = new("Components");
            for (int i = 0; i < components.Length; i++)
            {
                HierarchyComponentEntry entry = components[i];
                tooltip.Append('\n');
                tooltip.Append(ObjectNames.NicifyVariableName(entry.ComponentType.Name));
                if (entry.Count > 1)
                {
                    tooltip.Append(" (");
                    tooltip.Append(entry.Count);
                    tooltip.Append(')');
                }
            }

            if (monoBehaviourCount > 0)
            {
                tooltip.Append('\n');
                tooltip.Append(monoBehaviourCount);
                tooltip.Append(monoBehaviourCount == 1 ? " MonoBehaviour" : " MonoBehaviours");
            }

            if (missingScriptCount > 0)
            {
                tooltip.Append('\n');
                tooltip.Append(missingScriptCount);
                tooltip.Append(missingScriptCount == 1 ? " missing script" : " missing scripts");
            }

            return tooltip.ToString();
        }
    }

    [InitializeOnLoad]
    internal static class HierarchyComponentCache
    {
        private static readonly Dictionary<int, HierarchyComponentSummary> Cache = new();
        private static readonly List<Component> Components = new();
        private static readonly List<HierarchyComponentEntry> ComponentEntries = new();
        private static readonly Texture GenericScriptIcon = EditorGUIUtility.IconContent("cs Script Icon").image;

        static HierarchyComponentCache()
        {
            EditorApplication.hierarchyChanged += Invalidate;
            EditorApplication.projectChanged += Invalidate;
            Undo.undoRedoPerformed += Invalidate;
        }

        internal static event Action Invalidated;

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
            Invalidated?.Invoke();
            EditorApplication.RepaintHierarchyWindow();
        }

        private static HierarchyComponentSummary Evaluate(GameObject gameObject)
        {
            Components.Clear();
            ComponentEntries.Clear();
            gameObject.GetComponents(Components);

            int missingScriptCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
            int monoBehaviourCount = missingScriptCount;
            bool hasMeshRenderer = gameObject.TryGetComponent(out MeshRenderer _);

            for (int componentIndex = 0; componentIndex < Components.Count; componentIndex++)
            {
                Component component = Components[componentIndex];
                if (component == null ||
                    component is Transform ||
                    (hasMeshRenderer && component is MeshFilter))
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

    }
}
