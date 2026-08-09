using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Toolkit.PhysicsPlacement
{
    internal static class PhysicsPlacementColliderFactory
    {
        private const string CachePath = "Library/LoogaToolkit/PhysicsPlacement/collider-cache.json";

        private static readonly Dictionary<string, ColliderDescriptor> Cache = new(StringComparer.Ordinal);
        private static bool _cacheLoaded;

        internal static int CacheCount
        {
            get
            {
                EnsureCacheLoaded();
                return Cache.Count;
            }
        }

        internal static Collider AddTemporaryCollider(
            GameObject target,
            PhysicsPlacementColliderStrategy strategy,
            List<Component> temporaryComponents)
        {
            ColliderDescriptor descriptor = GetDescriptor(target, strategy);
            Collider collider = CreateCollider(target, descriptor, false);
            if (collider == null)
            {
                return null;
            }

            collider.hideFlags = HideFlags.HideInInspector | HideFlags.DontSaveInEditor;
            temporaryComponents.Add(collider);
            return collider;
        }

        internal static bool BakeCollider(GameObject target, PhysicsPlacementColliderStrategy strategy)
        {
            if (target.GetComponent<Collider>() != null)
            {
                return false;
            }

            ColliderDescriptor descriptor = GetDescriptor(target, strategy);
            Collider collider = CreateCollider(target, descriptor, true);
            if (collider == null)
            {
                return false;
            }

            EditorUtility.SetDirty(target);
            return true;
        }

        internal static void ClearCache()
        {
            Cache.Clear();
            _cacheLoaded = true;
            if (File.Exists(CachePath))
            {
                File.Delete(CachePath);
            }
        }

        private static ColliderDescriptor GetDescriptor(
            GameObject target,
            PhysicsPlacementColliderStrategy strategy)
        {
            string key = BuildCacheKey(target, strategy);
            EnsureCacheLoaded();
            if (Cache.TryGetValue(key, out ColliderDescriptor cached))
            {
                return cached;
            }

            ColliderDescriptor descriptor = BuildDescriptor(target, strategy);
            descriptor.Key = key;
            Cache[key] = descriptor;
            SaveCache();
            return descriptor;
        }

        private static ColliderDescriptor BuildDescriptor(
            GameObject target,
            PhysicsPlacementColliderStrategy strategy)
        {
            Bounds bounds = CalculateLocalBounds(target);
            MeshFilter[] meshFilters = target.GetComponentsInChildren<MeshFilter>(true);
            if (strategy == PhysicsPlacementColliderStrategy.Precision && meshFilters.Length == 1)
            {
                MeshFilter meshFilter = meshFilters[0];
                Mesh mesh = meshFilter.sharedMesh;
                if (mesh != null && meshFilter.transform == target.transform)
                {
                    string path = AssetDatabase.GetAssetPath(mesh);
                    if (!string.IsNullOrEmpty(path))
                    {
                        return new ColliderDescriptor
                        {
                            ColliderShape = ColliderDescriptor.Shape.ConvexMesh,
                            MeshAssetPath = path,
                            Center = bounds.center,
                            Size = bounds.size
                        };
                    }
                }
            }

            if (strategy == PhysicsPlacementColliderStrategy.Performance)
            {
                return CreateBoxDescriptor(bounds);
            }

            Vector3 size = bounds.size;
            float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            float smallest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            if (smallest > 0.0001f && largest / smallest < 1.2f)
            {
                return new ColliderDescriptor
                {
                    ColliderShape = ColliderDescriptor.Shape.Sphere,
                    Center = bounds.center,
                    Radius = largest * 0.5f
                };
            }

            int direction = FindLongestAxis(size);
            float length = size[direction];
            float firstCrossAxis = size[(direction + 1) % 3];
            float secondCrossAxis = size[(direction + 2) % 3];
            float crossSection = Mathf.Max(firstCrossAxis, secondCrossAxis);
            if (crossSection > 0.0001f && length / crossSection > 1.5f)
            {
                float radius = crossSection * 0.5f;
                return new ColliderDescriptor
                {
                    ColliderShape = ColliderDescriptor.Shape.Capsule,
                    Center = bounds.center,
                    Direction = direction,
                    Radius = radius,
                    Height = Mathf.Max(length, radius * 2f)
                };
            }

            return CreateBoxDescriptor(bounds);
        }

        private static Collider CreateCollider(
            GameObject target,
            ColliderDescriptor descriptor,
            bool useUndo)
        {
            switch (descriptor.ColliderShape)
            {
                case ColliderDescriptor.Shape.Sphere:
                {
                    SphereCollider collider = AddComponent<SphereCollider>(target, useUndo);
                    collider.center = descriptor.Center;
                    collider.radius = descriptor.Radius;
                    return collider;
                }

                case ColliderDescriptor.Shape.Capsule:
                {
                    CapsuleCollider collider = AddComponent<CapsuleCollider>(target, useUndo);
                    collider.center = descriptor.Center;
                    collider.direction = descriptor.Direction;
                    collider.radius = descriptor.Radius;
                    collider.height = descriptor.Height;
                    return collider;
                }

                case ColliderDescriptor.Shape.ConvexMesh:
                {
                    Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(descriptor.MeshAssetPath);
                    if (mesh == null)
                    {
                        return CreateBoxCollider(target, descriptor, useUndo);
                    }

                    MeshCollider collider = AddComponent<MeshCollider>(target, useUndo);
                    collider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation |
                                              MeshColliderCookingOptions.EnableMeshCleaning |
                                              MeshColliderCookingOptions.WeldColocatedVertices |
                                              MeshColliderCookingOptions.UseFastMidphase;
                    collider.sharedMesh = mesh;
                    collider.convex = true;
                    return collider;
                }

                default:
                    return CreateBoxCollider(target, descriptor, useUndo);
            }
        }

        private static BoxCollider CreateBoxCollider(
            GameObject target,
            ColliderDescriptor descriptor,
            bool useUndo)
        {
            BoxCollider collider = AddComponent<BoxCollider>(target, useUndo);
            collider.center = descriptor.Center;
            collider.size = descriptor.Size;
            return collider;
        }

        private static T AddComponent<T>(GameObject target, bool useUndo)
            where T : Component
        {
            return useUndo ? Undo.AddComponent<T>(target) : target.AddComponent<T>();
        }

        private static ColliderDescriptor CreateBoxDescriptor(Bounds bounds)
        {
            return new ColliderDescriptor
            {
                ColliderShape = ColliderDescriptor.Shape.Box,
                Center = bounds.center,
                Size = EnsureMinimumSize(bounds.size)
            };
        }

        private static Bounds CalculateLocalBounds(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(Vector3.zero, Vector3.one * 0.25f);
            }

            Matrix4x4 worldToLocal = target.transform.worldToLocalMatrix;
            bool initialized = false;
            Bounds localBounds = default;
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Bounds worldBounds = renderers[rendererIndex].bounds;
                Vector3 center = worldBounds.center;
                Vector3 extents = worldBounds.extents;
                for (int cornerIndex = 0; cornerIndex < 8; cornerIndex++)
                {
                    Vector3 corner = center + Vector3.Scale(
                        extents,
                        new Vector3(
                            (cornerIndex & 1) == 0 ? -1f : 1f,
                            (cornerIndex & 2) == 0 ? -1f : 1f,
                            (cornerIndex & 4) == 0 ? -1f : 1f));
                    Vector3 localCorner = worldToLocal.MultiplyPoint3x4(corner);
                    if (!initialized)
                    {
                        localBounds = new Bounds(localCorner, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(localCorner);
                    }
                }
            }

            localBounds.size = EnsureMinimumSize(localBounds.size);
            return localBounds;
        }

        private static Vector3 EnsureMinimumSize(Vector3 size)
        {
            const float minimum = 0.01f;
            size.x = Mathf.Max(minimum, size.x);
            size.y = Mathf.Max(minimum, size.y);
            size.z = Mathf.Max(minimum, size.z);
            return size;
        }

        private static int FindLongestAxis(Vector3 size)
        {
            if (size.x >= size.y && size.x >= size.z)
            {
                return 0;
            }

            return size.y >= size.z ? 1 : 2;
        }

        private static string BuildCacheKey(
            GameObject target,
            PhysicsPlacementColliderStrategy strategy)
        {
            StringBuilder builder = new();
            builder.Append((int)strategy).Append('|');
            MeshFilter[] filters = target.GetComponentsInChildren<MeshFilter>(true);
            for (int index = 0; index < filters.Length; index++)
            {
                Mesh mesh = filters[index].sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long localId);
                Matrix4x4 matrix = target.transform.worldToLocalMatrix * filters[index].transform.localToWorldMatrix;
                builder.Append(guid).Append(':').Append(localId).Append(':');
                for (int component = 0; component < 16; component++)
                {
                    builder.Append(matrix[component].ToString("R", CultureInfo.InvariantCulture)).Append(',');
                }
            }

            if (filters.Length == 0)
            {
                Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
                for (int index = 0; index < renderers.Length; index++)
                {
                    Bounds bounds = renderers[index].bounds;
                    builder.Append(bounds.center.ToString("R")).Append(bounds.size.ToString("R"));
                }
            }

            return Hash128.Compute(builder.ToString()).ToString();
        }

        private static void EnsureCacheLoaded()
        {
            if (_cacheLoaded)
            {
                return;
            }

            _cacheLoaded = true;
            if (!File.Exists(CachePath))
            {
                return;
            }

            try
            {
                ColliderCacheData data = JsonUtility.FromJson<ColliderCacheData>(File.ReadAllText(CachePath));
                if (data?.Entries == null)
                {
                    return;
                }

                for (int index = 0; index < data.Entries.Count; index++)
                {
                    ColliderDescriptor descriptor = data.Entries[index];
                    if (!string.IsNullOrEmpty(descriptor.Key))
                    {
                        Cache[descriptor.Key] = descriptor;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Looga Physics Placement ignored an invalid collider cache. {exception.Message}");
            }
        }

        private static void SaveCache()
        {
            string directory = Path.GetDirectoryName(CachePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            ColliderCacheData data = new() { Entries = new List<ColliderDescriptor>(Cache.Values) };
            File.WriteAllText(CachePath, JsonUtility.ToJson(data));
        }

        [Serializable]
        private sealed class ColliderCacheData
        {
            public List<ColliderDescriptor> Entries = new();
        }
    }
}
