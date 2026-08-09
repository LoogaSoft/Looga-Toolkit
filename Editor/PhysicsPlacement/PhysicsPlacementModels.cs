using System;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Toolkit.PhysicsPlacement
{
    /// <summary>Defines the fixed-step quality used by an edit-mode simulation.</summary>
    internal enum PhysicsPlacementQuality
    {
        Draft,
        Balanced,
        High,
        Ultra
    }

    /// <summary>Defines how the tool creates a collider when an object has no collider.</summary>
    internal enum PhysicsPlacementColliderStrategy
    {
        Performance,
        Balanced,
        Precision
    }

    /// <summary>Reports the current state of the edit-mode simulation.</summary>
    internal enum PhysicsPlacementSessionState
    {
        Inactive,
        Simulating,
        Paused,
        Settled
    }

    [Serializable]
    [FilePath(
        "Library/LoogaToolkit/PhysicsPlacement/preferences.asset",
        FilePathAttribute.Location.ProjectFolder)]
    internal sealed class PhysicsPlacementPreferences : ScriptableSingleton<PhysicsPlacementPreferences>
    {
        [SerializeField] private PhysicsPlacementQuality _quality = PhysicsPlacementQuality.Balanced;
        [SerializeField] private PhysicsPlacementColliderStrategy _colliderStrategy = PhysicsPlacementColliderStrategy.Balanced;
        [SerializeField] private bool _generateEnvironmentColliders = true;
        [SerializeField, Min(0.1f)] private float _environmentRange = 3f;
        [SerializeField, Min(0.1f)] private float _maximumDuration = 10f;
        [SerializeField, Min(0.001f)] private float _linearSleepThreshold = 0.025f;
        [SerializeField, Min(0.001f)] private float _angularSleepThreshold = 0.025f;
        [SerializeField] private bool _freezePositionX;
        [SerializeField] private bool _freezePositionY;
        [SerializeField] private bool _freezePositionZ;
        [SerializeField] private bool _freezeRotationX;
        [SerializeField] private bool _freezeRotationY;
        [SerializeField] private bool _freezeRotationZ;

        internal PhysicsPlacementQuality Quality
        {
            get => _quality;
            set => SetAndSave(ref _quality, value);
        }

        internal PhysicsPlacementColliderStrategy ColliderStrategy
        {
            get => _colliderStrategy;
            set => SetAndSave(ref _colliderStrategy, value);
        }

        internal bool GenerateEnvironmentColliders
        {
            get => _generateEnvironmentColliders;
            set => SetAndSave(ref _generateEnvironmentColliders, value);
        }

        internal float EnvironmentRange
        {
            get => _environmentRange;
            set => SetAndSave(ref _environmentRange, Mathf.Max(0.1f, value));
        }

        internal float MaximumDuration
        {
            get => _maximumDuration;
            set => SetAndSave(ref _maximumDuration, Mathf.Max(0.1f, value));
        }

        internal float LinearSleepThreshold
        {
            get => _linearSleepThreshold;
            set => SetAndSave(ref _linearSleepThreshold, Mathf.Max(0.001f, value));
        }

        internal float AngularSleepThreshold
        {
            get => _angularSleepThreshold;
            set => SetAndSave(ref _angularSleepThreshold, Mathf.Max(0.001f, value));
        }

        internal bool FreezePositionX
        {
            get => _freezePositionX;
            set => SetAndSave(ref _freezePositionX, value);
        }

        internal bool FreezePositionY
        {
            get => _freezePositionY;
            set => SetAndSave(ref _freezePositionY, value);
        }

        internal bool FreezePositionZ
        {
            get => _freezePositionZ;
            set => SetAndSave(ref _freezePositionZ, value);
        }

        internal bool FreezeRotationX
        {
            get => _freezeRotationX;
            set => SetAndSave(ref _freezeRotationX, value);
        }

        internal bool FreezeRotationY
        {
            get => _freezeRotationY;
            set => SetAndSave(ref _freezeRotationY, value);
        }

        internal bool FreezeRotationZ
        {
            get => _freezeRotationZ;
            set => SetAndSave(ref _freezeRotationZ, value);
        }

        internal float FixedStep => _quality switch
        {
            PhysicsPlacementQuality.Draft => 1f / 30f,
            PhysicsPlacementQuality.Balanced => 1f / 60f,
            PhysicsPlacementQuality.High => 1f / 90f,
            PhysicsPlacementQuality.Ultra => 1f / 120f,
            _ => 1f / 60f
        };

        internal int MaximumStepsPerUpdate => _quality switch
        {
            PhysicsPlacementQuality.Draft => 2,
            PhysicsPlacementQuality.Balanced => 4,
            PhysicsPlacementQuality.High => 6,
            PhysicsPlacementQuality.Ultra => 8,
            _ => 4
        };

        internal int SolverIterations => _quality switch
        {
            PhysicsPlacementQuality.Draft => 6,
            PhysicsPlacementQuality.Balanced => 8,
            PhysicsPlacementQuality.High => 12,
            PhysicsPlacementQuality.Ultra => 16,
            _ => 8
        };

        internal int SolverVelocityIterations => _quality switch
        {
            PhysicsPlacementQuality.Draft => 1,
            PhysicsPlacementQuality.Balanced => 2,
            PhysicsPlacementQuality.High => 4,
            PhysicsPlacementQuality.Ultra => 6,
            _ => 2
        };

        internal RigidbodyConstraints BuildConstraints()
        {
            RigidbodyConstraints constraints = RigidbodyConstraints.None;
            constraints |= _freezePositionX ? RigidbodyConstraints.FreezePositionX : RigidbodyConstraints.None;
            constraints |= _freezePositionY ? RigidbodyConstraints.FreezePositionY : RigidbodyConstraints.None;
            constraints |= _freezePositionZ ? RigidbodyConstraints.FreezePositionZ : RigidbodyConstraints.None;
            constraints |= _freezeRotationX ? RigidbodyConstraints.FreezeRotationX : RigidbodyConstraints.None;
            constraints |= _freezeRotationY ? RigidbodyConstraints.FreezeRotationY : RigidbodyConstraints.None;
            constraints |= _freezeRotationZ ? RigidbodyConstraints.FreezeRotationZ : RigidbodyConstraints.None;
            return constraints;
        }

        private void SetAndSave<T>(ref T field, T value)
        {
            if (Equals(field, value))
            {
                return;
            }

            field = value;
            Save(true);
        }
    }

    [Serializable]
    internal sealed class ColliderDescriptor
    {
        internal enum Shape
        {
            Box,
            Sphere,
            Capsule,
            ConvexMesh
        }

        public string Key;
        public Shape ColliderShape;
        public Vector3 Center;
        public Vector3 Size;
        public float Radius;
        public float Height;
        public int Direction;
        public string MeshAssetPath;
    }
}
