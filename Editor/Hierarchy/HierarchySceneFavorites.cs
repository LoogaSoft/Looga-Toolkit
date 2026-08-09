using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LoogaSoft.Hierarchy.Editor
{
    /// <summary>
    /// Shows personal favorites as transient rows under each loaded scene.
    /// Unity never serializes these rows or includes them in builds.
    /// The rows remain during the Edit-to-Play transition.
    /// This behavior prevents native inspectors from retaining invalid targets.
    /// </summary>
    [InitializeOnLoad]
    internal static class HierarchySceneFavorites
    {
        private const string Marker = "\u200B\u200C\u200B";
        private const string RootLabel = "Favorites";
        private const float IconSize = 14f;
        private const float ContentSpacing = 4f;
        private const HideFlags NodeFlags =
            HideFlags.NotEditable |
            HideFlags.DontSaveInEditor |
            HideFlags.DontSaveInBuild;

        private static readonly Dictionary<int, string> ProxyTargets = new();
        private static readonly GUIContent FavoriteIcon = EditorGUIUtility.IconContent("Favorite");
        private static GUIStyle _proxyStyle;
        private static bool _syncScheduled;
        private static bool _synchronizing;

        static HierarchySceneFavorites()
        {
            HierarchyFavoriteStore.Changed += ScheduleSynchronization;
            EditorApplication.hierarchyChanged += ScheduleSynchronization;
            EditorApplication.playModeStateChanged += HandlePlayModeState;
            EditorSceneManager.sceneOpened += HandleSceneOpened;
            EditorSceneManager.sceneClosed += HandleSceneClosed;
            EditorApplication.delayCall += Synchronize;
        }

        internal static bool IsSynthetic(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            Transform current = gameObject.transform;
            while (current != null)
            {
                if (current.name.EndsWith(Marker, StringComparison.Ordinal))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        /// <summary>
        /// Draws and handles a synthetic favorite row.
        /// Returns true when normal decorators must skip the represented object.
        /// </summary>
        internal static bool Draw(GameObject gameObject, Rect rowRect)
        {
            if (!IsSynthetic(gameObject))
            {
                return false;
            }

            bool isRoot = gameObject.transform.parent == null;
            if (Event.current.type == EventType.Repaint)
            {
                DrawRow(gameObject, rowRect, isRoot);
            }

            if (!isRoot)
            {
                HandleProxyInput(gameObject.GetInstanceID(), rowRect);
            }

            return true;
        }

        private static void ScheduleSynchronization()
        {
            if (_synchronizing || _syncScheduled || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            _syncScheduled = true;
            EditorApplication.delayCall += Synchronize;
        }

        private static void Synchronize()
        {
            _syncScheduled = false;
            if (_synchronizing || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            _synchronizing = true;
            try
            {
                ProxyTargets.Clear();

                for (int index = 0; index < SceneManager.sceneCount; index++)
                {
                    Scene scene = SceneManager.GetSceneAt(index);
                    if (CanPresentFavorites(scene))
                    {
                        SynchronizeScene(scene);
                    }
                }
            }
            finally
            {
                _synchronizing = false;
            }

            EditorApplication.RepaintHierarchyWindow();
        }

        private static bool CanPresentFavorites(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene))
            {
                return false;
            }

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            return prefabStage == null || prefabStage.scene != scene;
        }

        private static void SynchronizeScene(Scene scene)
        {
            GameObject root = FindOrCreateRoot(scene);
            List<HierarchyFavorite> favorites = GetFavorites(scene);

            string rootName = $"{RootLabel} ({favorites.Count}){Marker}";
            if (root.name != rootName)
            {
                root.name = rootName;
            }

            ApplyNodeFlags(root);
            if (root.transform.GetSiblingIndex() != 0)
            {
                root.transform.SetSiblingIndex(0);
            }

            SynchronizeProxies(root.transform, favorites);
        }

        private static void SynchronizeProxies(
            Transform parent,
            IReadOnlyList<HierarchyFavorite> favorites)
        {
            for (int index = 0; index < favorites.Count; index++)
            {
                HierarchyFavorite favorite = favorites[index];
                GameObject target = HierarchyObjectId.Resolve(favorite.ObjectId);
                string label = target != null ? target.name : $"{favorite.DisplayName} (Missing)";
                GameObject proxy = index < parent.childCount
                    ? parent.GetChild(index).gameObject
                    : CreateProxy(parent);

                ConfigureProxy(proxy, index, label, favorite.ObjectId, target);
            }

            for (int index = parent.childCount - 1; index >= favorites.Count; index--)
            {
                GameObject staleProxy = parent.GetChild(index).gameObject;
                ProxyTargets.Remove(staleProxy.GetInstanceID());
                UnityEngine.Object.DestroyImmediate(staleProxy);
            }
        }

        private static GameObject FindOrCreateRoot(Scene scene)
        {
            GameObject root = null;
            GameObject[] sceneRoots = scene.GetRootGameObjects();

            for (int index = 0; index < sceneRoots.Length; index++)
            {
                GameObject candidate = sceneRoots[index];
                if (!candidate.name.EndsWith(Marker, StringComparison.Ordinal))
                {
                    continue;
                }

                if (root == null)
                {
                    root = candidate;
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(candidate);
                }
            }

            if (root != null)
            {
                return root;
            }

            root = new GameObject($"{RootLabel} (0){Marker}");
            ApplyNodeFlags(root);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetSiblingIndex(0);

            Texture2D favoriteIcon = EditorGUIUtility.IconContent("Favorite").image as Texture2D;
            if (favoriteIcon != null)
            {
                EditorGUIUtility.SetIconForObject(root, favoriteIcon);
            }

            return root;
        }

        private static List<HierarchyFavorite> GetFavorites(Scene scene)
        {
            IReadOnlyList<HierarchyFavorite> entries = HierarchyFavoriteStore.instance.Entries;
            List<HierarchyFavorite> favorites = new();

            for (int index = 0; index < entries.Count; index++)
            {
                HierarchyFavorite favorite = entries[index];
                GameObject target = HierarchyObjectId.Resolve(favorite.ObjectId);
                bool belongsToScene = target != null
                    ? target.scene == scene
                    : !string.IsNullOrEmpty(scene.path) && favorite.ScenePath == scene.path;

                if (belongsToScene)
                {
                    favorites.Add(favorite);
                }
            }

            return favorites;
        }

        private static GameObject CreateProxy(Transform parent)
        {
            GameObject proxy = new($"Favorite{Marker}");
            ApplyNodeFlags(proxy);
            proxy.transform.SetParent(parent, false);
            return proxy;
        }

        private static void ConfigureProxy(
            GameObject proxy,
            int siblingIndex,
            string label,
            string objectId,
            GameObject target)
        {
            string proxyName = $"{label}{Marker}";
            if (proxy.name != proxyName)
            {
                proxy.name = proxyName;
            }

            ApplyNodeFlags(proxy);
            if (proxy.transform.GetSiblingIndex() != siblingIndex)
            {
                proxy.transform.SetSiblingIndex(siblingIndex);
            }

            if (!string.IsNullOrEmpty(objectId))
            {
                ProxyTargets[proxy.GetInstanceID()] = objectId;
            }

            Texture2D icon = target != null ? AssetPreview.GetMiniThumbnail(target) as Texture2D : null;
            if (icon != null)
            {
                EditorGUIUtility.SetIconForObject(proxy, icon);
            }
        }

        private static void ApplyNodeFlags(GameObject gameObject)
        {
            if (gameObject.hideFlags != NodeFlags)
            {
                gameObject.hideFlags = NodeFlags;
            }

            HideFlags transformFlags = NodeFlags | HideFlags.HideInInspector;
            if (gameObject.transform.hideFlags != transformFlags)
            {
                gameObject.transform.hideFlags = transformFlags;
            }
        }

        private static void DrawRow(GameObject gameObject, Rect rowRect, bool isRoot)
        {
            EnsureStyles();

            bool hovered = rowRect.Contains(Event.current.mousePosition);
            if (isRoot)
            {
                HierarchyHeaderStyle.Draw(
                    rowRect,
                    FavoriteIcon,
                    StripMarker(gameObject.name),
                    new Color(1f, 0.72f, 0.18f, 0.95f),
                    hovered,
                    false,
                    drawAccentRail: false,
                    backgroundOverride: ResolveFavoritesBackground(hovered));
                return;
            }

            Color background = ResolveBackground(isRoot, hovered);
            EditorGUI.DrawRect(rowRect, background);

            Rect contentRect = rowRect;

            GUIContent icon = ResolveIcon(gameObject, isRoot);
            if (icon.image != null)
            {
                Rect iconRect = new(
                    contentRect.x,
                    contentRect.y + Mathf.Floor((contentRect.height - IconSize) * 0.5f),
                    IconSize,
                    IconSize);

                Color previousColor = GUI.color;
                GUI.color = isRoot ? new Color(1f, 0.78f, 0.24f, 1f) : Color.white;
                GUI.DrawTexture(iconRect, icon.image, ScaleMode.ScaleToFit, true);
                GUI.color = previousColor;
                contentRect.xMin = iconRect.xMax + ContentSpacing;
            }

            GUI.Label(contentRect, StripMarker(gameObject.name), _proxyStyle);
        }

        private static Color ResolveBackground(bool isRoot, bool hovered)
        {
            if (EditorGUIUtility.isProSkin)
            {
                if (isRoot)
                {
                    return hovered
                        ? new Color(0.175f, 0.175f, 0.175f, 1f)
                        : new Color(0.135f, 0.135f, 0.135f, 1f);
                }

                return hovered
                    ? new Color(0.255f, 0.255f, 0.255f, 0.96f)
                    : new Color(0.225f, 0.225f, 0.225f, 0.92f);
            }

            if (isRoot)
            {
                return hovered
                    ? new Color(0.70f, 0.70f, 0.70f, 1f)
                    : new Color(0.76f, 0.76f, 0.76f, 1f);
            }

            return hovered
                ? new Color(0.86f, 0.86f, 0.86f, 0.96f)
                : new Color(0.90f, 0.90f, 0.90f, 0.92f);
        }

        private static Color ResolveFavoritesBackground(bool hovered)
        {
            if (EditorGUIUtility.isProSkin)
            {
                return hovered
                    ? new Color(0.32f, 0.32f, 0.32f, 1f)
                    : new Color(0.28f, 0.28f, 0.28f, 1f);
            }

            return hovered
                ? new Color(0.84f, 0.84f, 0.84f, 1f)
                : new Color(0.80f, 0.80f, 0.80f, 1f);
        }

        private static GUIContent ResolveIcon(GameObject gameObject, bool isRoot)
        {
            if (isRoot)
            {
                return FavoriteIcon;
            }

            if (!ProxyTargets.TryGetValue(gameObject.GetInstanceID(), out string objectId))
            {
                return GUIContent.none;
            }

            GameObject target = HierarchyObjectId.Resolve(objectId);
            Texture2D icon = target != null ? AssetPreview.GetMiniThumbnail(target) as Texture2D : null;
            return icon != null ? new GUIContent(icon) : GUIContent.none;
        }

        private static string StripMarker(string label)
        {
            return label.EndsWith(Marker, StringComparison.Ordinal)
                ? label[..^Marker.Length]
                : label;
        }

        private static void EnsureStyles()
        {
            if (_proxyStyle != null)
            {
                return;
            }

            _proxyStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };

            Color textColor = EditorStyles.label.normal.textColor;
            _proxyStyle.normal.textColor = textColor;
        }

        private static void HandleProxyInput(int instanceId, Rect rowRect)
        {
            if (!ProxyTargets.TryGetValue(instanceId, out string objectId) ||
                Event.current.type != EventType.MouseDown ||
                Event.current.button != 0 ||
                !rowRect.Contains(Event.current.mousePosition))
            {
                return;
            }

            GameObject target = HierarchyObjectId.Resolve(objectId);
            if (target != null)
            {
                Selection.activeGameObject = target;
                EditorGUIUtility.PingObject(target);
            }

            Event.current.Use();
        }

        private static void HandlePlayModeState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                ScheduleSynchronization();
            }
        }

        private static void HandleSceneOpened(Scene scene, OpenSceneMode mode)
        {
            ScheduleSynchronization();
        }

        private static void HandleSceneClosed(Scene scene)
        {
            ScheduleSynchronization();
        }
    }
}
