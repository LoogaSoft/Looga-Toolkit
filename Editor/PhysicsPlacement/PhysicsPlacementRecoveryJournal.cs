using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LoogaSoft.Toolkit.PhysicsPlacement
{
    [InitializeOnLoad]
    internal static class PhysicsPlacementRecoveryJournal
    {
        private const string JournalPath = "Library/LoogaToolkit/PhysicsPlacement/active-session.json";

        static PhysicsPlacementRecoveryJournal()
        {
            EditorApplication.delayCall += TryRestorePendingSession;
            EditorSceneManager.sceneOpened += HandleSceneOpened;
        }

        internal static void Write(
            IReadOnlyList<TransformSnapshot> transforms,
            IReadOnlyList<RigidbodySnapshot> bodies)
        {
            RecoveryData data = new()
            {
                SessionId = Guid.NewGuid().ToString("N"),
                CreatedUtc = DateTime.UtcNow.ToString("O")
            };

            for (int index = 0; index < transforms.Count; index++)
            {
                TransformSnapshot snapshot = transforms[index];
                data.Transforms.Add(new TransformRecoveryRecord
                {
                    ObjectId = GetObjectId(snapshot.Transform),
                    LocalPosition = snapshot.LocalPosition,
                    LocalRotation = snapshot.LocalRotation,
                    LocalScale = snapshot.LocalScale
                });
            }

            for (int index = 0; index < bodies.Count; index++)
            {
                RigidbodySnapshot snapshot = bodies[index];
                data.Bodies.Add(new RigidbodyRecoveryRecord
                {
                    ObjectId = GetObjectId(snapshot.Body),
                    IsKinematic = snapshot.IsKinematic,
                    UseGravity = snapshot.UseGravity,
                    DetectCollisions = snapshot.DetectCollisions,
                    Constraints = (int)snapshot.Constraints,
                    CollisionDetectionMode = (int)snapshot.CollisionDetectionMode,
                    Interpolation = (int)snapshot.Interpolation,
                    LinearDamping = snapshot.LinearDamping,
                    AngularDamping = snapshot.AngularDamping,
                    SolverIterations = snapshot.SolverIterations,
                    SolverVelocityIterations = snapshot.SolverVelocityIterations
                });
            }

            string directory = Path.GetDirectoryName(JournalPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporaryPath = JournalPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(data, true));
            if (File.Exists(JournalPath))
            {
                File.Delete(JournalPath);
            }

            File.Move(temporaryPath, JournalPath);
        }

        internal static void Delete()
        {
            if (File.Exists(JournalPath))
            {
                File.Delete(JournalPath);
            }
        }

        private static void HandleSceneOpened(Scene scene, OpenSceneMode mode)
        {
            EditorApplication.delayCall += TryRestorePendingSession;
        }

        private static void TryRestorePendingSession()
        {
            if (!File.Exists(JournalPath) || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            RecoveryData data;
            try
            {
                data = JsonUtility.FromJson<RecoveryData>(File.ReadAllText(JournalPath));
            }
            catch (Exception exception)
            {
                Debug.LogError($"Looga Physics Placement could not read its recovery journal. {exception.Message}");
                return;
            }

            if (data == null)
            {
                Delete();
                return;
            }

            int restoredTransforms = 0;
            for (int index = 0; index < data.Transforms.Count; index++)
            {
                TransformRecoveryRecord record = data.Transforms[index];
                Transform transform = Resolve<Transform>(record.ObjectId);
                if (transform == null)
                {
                    continue;
                }

                transform.localPosition = record.LocalPosition;
                transform.localRotation = record.LocalRotation;
                transform.localScale = record.LocalScale;
                EditorUtility.SetDirty(transform);
                restoredTransforms++;
            }

            for (int index = 0; index < data.Bodies.Count; index++)
            {
                RigidbodyRecoveryRecord record = data.Bodies[index];
                Rigidbody body = Resolve<Rigidbody>(record.ObjectId);
                if (body == null)
                {
                    continue;
                }

                body.isKinematic = record.IsKinematic;
                body.useGravity = record.UseGravity;
                body.detectCollisions = record.DetectCollisions;
                body.constraints = (RigidbodyConstraints)record.Constraints;
                body.collisionDetectionMode = (CollisionDetectionMode)record.CollisionDetectionMode;
                body.interpolation = (RigidbodyInterpolation)record.Interpolation;
                body.linearDamping = record.LinearDamping;
                body.angularDamping = record.AngularDamping;
                body.solverIterations = record.SolverIterations;
                body.solverVelocityIterations = record.SolverVelocityIterations;
                EditorUtility.SetDirty(body);
            }

            if (restoredTransforms == 0 && data.Transforms.Count > 0)
            {
                return;
            }

            Delete();
            SceneView.RepaintAll();
            Debug.LogWarning(
                $"Looga Physics Placement restored {restoredTransforms} transform(s) from an interrupted session.");
        }

        private static string GetObjectId(UnityEngine.Object target)
        {
            return GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
        }

        private static T Resolve<T>(string value)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(value) || !GlobalObjectId.TryParse(value, out GlobalObjectId objectId))
            {
                return null;
            }

            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(objectId) as T;
        }

        [Serializable]
        private sealed class RecoveryData
        {
            public string SessionId;
            public string CreatedUtc;
            public List<TransformRecoveryRecord> Transforms = new();
            public List<RigidbodyRecoveryRecord> Bodies = new();
        }

        [Serializable]
        private sealed class TransformRecoveryRecord
        {
            public string ObjectId;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 LocalScale;
        }

        [Serializable]
        private sealed class RigidbodyRecoveryRecord
        {
            public string ObjectId;
            public bool IsKinematic;
            public bool UseGravity;
            public bool DetectCollisions;
            public int Constraints;
            public int CollisionDetectionMode;
            public int Interpolation;
            public float LinearDamping;
            public float AngularDamping;
            public int SolverIterations;
            public int SolverVelocityIterations;
        }
    }

    internal sealed class TransformSnapshot
    {
        internal TransformSnapshot(Transform transform)
        {
            Transform = transform;
            LocalPosition = transform.localPosition;
            LocalRotation = transform.localRotation;
            LocalScale = transform.localScale;
        }

        internal Transform Transform { get; }
        internal Vector3 LocalPosition { get; }
        internal Quaternion LocalRotation { get; }
        internal Vector3 LocalScale { get; }

        internal void Restore()
        {
            if (Transform == null)
            {
                return;
            }

            Transform.localPosition = LocalPosition;
            Transform.localRotation = LocalRotation;
            Transform.localScale = LocalScale;
            EditorUtility.SetDirty(Transform);
        }
    }

    internal sealed class RigidbodySnapshot
    {
        internal RigidbodySnapshot(Rigidbody body)
        {
            Body = body;
            IsKinematic = body.isKinematic;
            UseGravity = body.useGravity;
            DetectCollisions = body.detectCollisions;
            Constraints = body.constraints;
            CollisionDetectionMode = body.collisionDetectionMode;
            Interpolation = body.interpolation;
            LinearDamping = body.linearDamping;
            AngularDamping = body.angularDamping;
            SolverIterations = body.solverIterations;
            SolverVelocityIterations = body.solverVelocityIterations;
        }

        internal Rigidbody Body { get; }
        internal bool IsKinematic { get; }
        internal bool UseGravity { get; }
        internal bool DetectCollisions { get; }
        internal RigidbodyConstraints Constraints { get; }
        internal CollisionDetectionMode CollisionDetectionMode { get; }
        internal RigidbodyInterpolation Interpolation { get; }
        internal float LinearDamping { get; }
        internal float AngularDamping { get; }
        internal int SolverIterations { get; }
        internal int SolverVelocityIterations { get; }

        internal void Restore()
        {
            if (Body == null)
            {
                return;
            }

            Body.isKinematic = IsKinematic;
            Body.useGravity = UseGravity;
            Body.detectCollisions = DetectCollisions;
            Body.constraints = Constraints;
            Body.collisionDetectionMode = CollisionDetectionMode;
            Body.interpolation = Interpolation;
            Body.linearDamping = LinearDamping;
            Body.angularDamping = AngularDamping;
            Body.solverIterations = SolverIterations;
            Body.solverVelocityIterations = SolverVelocityIterations;
            EditorUtility.SetDirty(Body);
        }
    }
}
