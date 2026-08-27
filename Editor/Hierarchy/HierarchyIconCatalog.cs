using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    internal static class HierarchyIconCatalog
    {
        private static readonly HierarchyIconOption[] Options =
        {
            new("GameObject", "GameObject Icon"),
            new("Prefab", "Prefab Icon"),
            new("Folder", "Folder Icon"),
            new("Camera", "Camera Icon"),
            new("Light", "Light Icon"),
            new("Audio Source", "AudioSource Icon"),
            new("Particle System", "ParticleSystem Icon"),
            new("Rigidbody", "Rigidbody Icon"),
            new("Box Collider", "BoxCollider Icon"),
            new("Sphere Collider", "SphereCollider Icon"),
            new("Capsule Collider", "CapsuleCollider Icon"),
            new("Mesh Renderer", "MeshRenderer Icon"),
            new("Skinned Mesh Renderer", "SkinnedMeshRenderer Icon"),
            new("Animator", "Animator Icon"),
            new("Animation", "Animation Icon"),
            new("Canvas", "Canvas Icon"),
            new("Terrain", "Terrain Icon"),
            new("Reflection Probe", "ReflectionProbe Icon"),
            new("Wind Zone", "WindZone Icon"),
            new("Navigation Agent", "NavMeshAgent Icon"),
            new("Occlusion Area", "OcclusionArea Icon"),
            new("Scene", "SceneAsset Icon"),
            new("Material", "Material Icon"),
            new("Texture", "Texture Icon"),
            new("Script", "cs Script Icon"),
            new("Settings", "Settings Icon"),
            new("Favorite", "Favorite"),
            new("Info", "console.infoicon"),
            new("Warning", "console.warnicon"),
            new("Error", "console.erroricon")
        };

        private static readonly Dictionary<string, Texture> Textures = new();

        internal static IReadOnlyList<HierarchyIconOption> All => Options;

        internal static Texture GetTexture(string iconName)
        {
            if (string.IsNullOrEmpty(iconName))
            {
                return null;
            }

            if (Textures.TryGetValue(iconName, out Texture texture))
            {
                return texture;
            }

            texture = EditorGUIUtility.IconContent(iconName).image;
            Textures[iconName] = texture;
            return texture;
        }
    }

    internal readonly struct HierarchyIconOption
    {
        internal HierarchyIconOption(string name, string iconName)
        {
            Name = name;
            IconName = iconName;
        }

        internal string Name { get; }

        internal string IconName { get; }
    }
}
