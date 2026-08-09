using System.Collections.Generic;
using UnityEngine;

namespace LoogaSoft.Toolkit.TransformAuthoring
{
    internal readonly struct TransformBounds
    {
        public TransformBounds(Bounds localBounds, Vector3 worldSize)
        {
            LocalBounds = localBounds;
            WorldSize = worldSize;
        }

        public Bounds LocalBounds { get; }

        public Vector3 WorldSize { get; }
    }

    internal static class TransformBoundsUtility
    {
        private static readonly List<Renderer> Renderers = new();
        private static readonly List<Collider> Colliders = new();

        public static bool TryCalculate(Transform root, out TransformBounds result)
        {
            result = default;
            if (root == null)
                return false;

            bool hasBounds = false;
            Bounds localBounds = default;
            root.GetComponentsInChildren(true, Renderers);

            for (int i = 0; i < Renderers.Count; i++)
            {
                Renderer renderer = Renderers[i];
                if (renderer == null)
                    continue;

                if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
                {
                    EncapsulateBounds(
                        root.worldToLocalMatrix * skinnedMeshRenderer.transform.localToWorldMatrix,
                        skinnedMeshRenderer.localBounds,
                        ref localBounds,
                        ref hasBounds);
                    continue;
                }

                if (renderer is MeshRenderer &&
                    renderer.TryGetComponent(out MeshFilter meshFilter) &&
                    meshFilter.sharedMesh != null)
                {
                    EncapsulateBounds(
                        root.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix,
                        meshFilter.sharedMesh.bounds,
                        ref localBounds,
                        ref hasBounds);
                    continue;
                }

                EncapsulateWorldBounds(root, renderer.bounds, ref localBounds, ref hasBounds);
            }

            // Use colliders only when the hierarchy has no rendered geometry. This prevents
            // oversized gameplay colliders from changing the visual size of rendered objects.
            if (!hasBounds)
            {
                root.GetComponentsInChildren(true, Colliders);
                for (int i = 0; i < Colliders.Count; i++)
                {
                    Collider collider = Colliders[i];
                    if (collider == null || !collider.enabled)
                        continue;

                    EncapsulateWorldBounds(root, collider.bounds, ref localBounds, ref hasBounds);
                }
            }

            Renderers.Clear();
            Colliders.Clear();

            if (!hasBounds)
                return false;

            Vector3 localSize = localBounds.size;
            Vector3 worldSize = new(
                root.TransformVector(Vector3.right * localSize.x).magnitude,
                root.TransformVector(Vector3.up * localSize.y).magnitude,
                root.TransformVector(Vector3.forward * localSize.z).magnitude);
            result = new TransformBounds(localBounds, worldSize);
            return true;
        }

        private static void EncapsulateWorldBounds(
            Transform root,
            Bounds worldBounds,
            ref Bounds localBounds,
            ref bool hasBounds)
        {
            EncapsulateBounds(root.worldToLocalMatrix, worldBounds, ref localBounds, ref hasBounds);
        }

        private static void EncapsulateBounds(
            Matrix4x4 toRootLocal,
            Bounds sourceBounds,
            ref Bounds destination,
            ref bool hasBounds)
        {
            Vector3 center = sourceBounds.center;
            Vector3 extents = sourceBounds.extents;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                        Vector3 localCorner = toRootLocal.MultiplyPoint3x4(corner);
                        if (!hasBounds)
                        {
                            destination = new Bounds(localCorner, Vector3.zero);
                            hasBounds = true;
                        }
                        else
                        {
                            destination.Encapsulate(localCorner);
                        }
                    }
                }
            }
        }
    }
}
