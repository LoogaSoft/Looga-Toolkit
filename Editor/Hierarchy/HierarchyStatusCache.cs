using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    [Flags]
    internal enum HierarchyStatus
    {
        None = 0,
        MissingScript = 1 << 0,
        PrefabOverride = 1 << 1,
        Static = 1 << 2,
        EditorOnly = 1 << 3
    }

    [InitializeOnLoad]
    internal static class HierarchyStatusCache
    {
        private static readonly Dictionary<int, HierarchyStatus> Cache = new();
        private static readonly Dictionary<int, HashSet<int>> PrefabOverrideObjects = new();
        private static readonly Dictionary<int, StaticTooltipEntry> StaticTooltips = new();
        private static readonly List<Component> Components = new();
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

        static HierarchyStatusCache()
        {
            EditorApplication.hierarchyChanged += Invalidate;
            EditorApplication.projectChanged += Invalidate;
            Undo.undoRedoPerformed += Invalidate;
        }

        internal static HierarchyStatus Get(GameObject gameObject)
        {
            int instanceId = gameObject.GetInstanceID();
            if (Cache.TryGetValue(instanceId, out HierarchyStatus status))
            {
                return status;
            }

            status = Evaluate(gameObject);
            Cache[instanceId] = status;
            return status;
        }

        internal static void Invalidate()
        {
            Cache.Clear();
            PrefabOverrideObjects.Clear();
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

        private static HierarchyStatus Evaluate(GameObject gameObject)
        {
            HierarchyStatus status = HierarchyStatus.None;

            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject) > 0)
            {
                status |= HierarchyStatus.MissingScript;
            }

            if (HasPrefabOverrides(gameObject))
            {
                status |= HierarchyStatus.PrefabOverride;
            }

            if (gameObject.isStatic)
            {
                status |= HierarchyStatus.Static;
            }

            if (gameObject.CompareTag("EditorOnly"))
            {
                status |= HierarchyStatus.EditorOnly;
            }

            return status;
        }

        private static bool HasPrefabOverrides(GameObject gameObject)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(gameObject))
            {
                return false;
            }

            if (PrefabUtility.IsAddedGameObjectOverride(gameObject))
            {
                return true;
            }

            Components.Clear();
            gameObject.GetComponents(Components);

            for (int index = 0; index < Components.Count; index++)
            {
                Component component = Components[index];
                if (component != null && PrefabUtility.IsAddedComponentOverride(component))
                {
                    return true;
                }
            }

            GameObject instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject);
            if (instanceRoot == null)
            {
                return false;
            }

            int rootId = instanceRoot.GetInstanceID();
            if (!PrefabOverrideObjects.TryGetValue(rootId, out HashSet<int> overriddenObjects))
            {
                overriddenObjects = new HashSet<int>();
                var overrides = PrefabUtility.GetObjectOverrides(instanceRoot, false);

                for (int index = 0; index < overrides.Count; index++)
                {
                    UnityEngine.Object instanceObject = overrides[index].instanceObject;
                    GameObject overriddenGameObject = instanceObject switch
                    {
                        GameObject overriddenObject => overriddenObject,
                        Component overriddenComponent => overriddenComponent.gameObject,
                        _ => null
                    };

                    if (overriddenGameObject != null)
                    {
                        overriddenObjects.Add(overriddenGameObject.GetInstanceID());
                    }
                }

                PrefabOverrideObjects[rootId] = overriddenObjects;
            }

            return overriddenObjects.Contains(gameObject.GetInstanceID());
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
            public StaticTooltipEntry(StaticEditorFlags flags, string tooltip)
            {
                Flags = flags;
                Tooltip = tooltip;
            }

            public StaticEditorFlags Flags { get; }
            public string Tooltip { get; }
        }
    }
}
