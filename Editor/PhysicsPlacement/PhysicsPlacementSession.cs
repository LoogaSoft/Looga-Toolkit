using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LoogaSoft.Toolkit.PhysicsPlacement
{
    [InitializeOnLoad]
    internal static class PhysicsPlacementSession
    {
        private const int RequiredSettledSteps = 12;

        private static readonly List<TransformSnapshot> TransformSnapshots = new();
        private static readonly List<RigidbodySnapshot> RigidbodySnapshots = new();
        private static readonly List<Rigidbody> SimulatedBodies = new();
        private static readonly List<Component> TemporaryComponents = new();
        private static readonly List<GameObject> SelectedRoots = new();

        private static SimulationMode _originalSimulationMode;
        private static Vector3 _originalGravity;
        private static double _lastUpdateTime;
        private static float _accumulator;
        private static float _simulatedTime;
        private static int _settledSteps;
        private static bool _isCleaningUp;

        static PhysicsPlacementSession()
        {
            AssemblyReloadEvents.beforeAssemblyReload += CancelForSafety;
            EditorApplication.quitting += CancelForSafety;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorSceneManager.sceneSaving += HandleSceneSaving;
            Undo.undoRedoPerformed += HandleUndoRedo;
        }

        internal static event Action StateChanged;

        internal static PhysicsPlacementSessionState State { get; private set; }
            = PhysicsPlacementSessionState.Inactive;

        internal static bool IsActive => State != PhysicsPlacementSessionState.Inactive;
        internal static float SimulatedTime => _simulatedTime;
        internal static int SimulatedObjectCount => SimulatedBodies.Count;

        internal static bool Start(IReadOnlyList<GameObject> selection)
        {
            if (IsActive || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return false;
            }

            _originalSimulationMode = Physics.simulationMode;
            _originalGravity = Physics.gravity;

            try
            {
                CollectSelectionRoots(selection);
                if (SelectedRoots.Count == 0)
                {
                    return false;
                }

                CaptureTransforms();
                CaptureRigidbodies();
                PhysicsPlacementRecoveryJournal.Write(TransformSnapshots, RigidbodySnapshots);
                ConfigureRigidbodies();
                AddRequiredColliders();

                Physics.simulationMode = SimulationMode.Script;
                Physics.SyncTransforms();

                _lastUpdateTime = EditorApplication.timeSinceStartup;
                _accumulator = 0f;
                _simulatedTime = 0f;
                _settledSteps = 0;
                State = PhysicsPlacementSessionState.Simulating;
                EditorApplication.update += Update;
                StateChanged?.Invoke();
                SceneView.RepaintAll();
                return true;
            }
            catch (Exception exception)
            {
                RestoreFailedStart();
                Debug.LogException(exception);
                return false;
            }
        }

        internal static void Pause()
        {
            if (State != PhysicsPlacementSessionState.Simulating)
            {
                return;
            }

            State = PhysicsPlacementSessionState.Paused;
            StateChanged?.Invoke();
            SceneView.RepaintAll();
        }

        internal static void Resume()
        {
            if (State != PhysicsPlacementSessionState.Paused &&
                State != PhysicsPlacementSessionState.Settled)
            {
                return;
            }

            _lastUpdateTime = EditorApplication.timeSinceStartup;
            _settledSteps = 0;
            WakeBodies();
            State = PhysicsPlacementSessionState.Simulating;
            StateChanged?.Invoke();
        }

        internal static void Step()
        {
            if (!IsActive || State == PhysicsPlacementSessionState.Simulating)
            {
                return;
            }

            try
            {
                SimulateStep();
                SceneView.RepaintAll();
            }
            catch (Exception exception)
            {
                Cleanup(true);
                Debug.LogException(exception);
            }
        }

        internal static void Reset()
        {
            if (!IsActive)
            {
                return;
            }

            for (int index = 0; index < TransformSnapshots.Count; index++)
            {
                TransformSnapshots[index].Restore();
            }

            for (int index = 0; index < SimulatedBodies.Count; index++)
            {
                Rigidbody body = SimulatedBodies[index];
                if (body == null)
                {
                    continue;
                }

                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.Sleep();
            }

            Physics.SyncTransforms();
            _simulatedTime = 0f;
            _settledSteps = 0;
            State = PhysicsPlacementSessionState.Paused;
            StateChanged?.Invoke();
            SceneView.RepaintAll();
        }

        internal static void Apply()
        {
            if (!IsActive)
            {
                return;
            }

            Vector3[] finalPositions = new Vector3[TransformSnapshots.Count];
            Quaternion[] finalRotations = new Quaternion[TransformSnapshots.Count];
            Vector3[] finalScales = new Vector3[TransformSnapshots.Count];
            UnityEngine.Object[] transforms = new UnityEngine.Object[TransformSnapshots.Count];
            for (int index = 0; index < TransformSnapshots.Count; index++)
            {
                Transform transform = TransformSnapshots[index].Transform;
                finalPositions[index] = transform.localPosition;
                finalRotations[index] = transform.localRotation;
                finalScales[index] = transform.localScale;
                transforms[index] = transform;
                TransformSnapshots[index].Restore();
            }

            Undo.RecordObjects(transforms, "Apply Physics Placement");
            for (int index = 0; index < TransformSnapshots.Count; index++)
            {
                Transform transform = TransformSnapshots[index].Transform;
                transform.localPosition = finalPositions[index];
                transform.localRotation = finalRotations[index];
                transform.localScale = finalScales[index];
                EditorUtility.SetDirty(transform);
            }

            Undo.FlushUndoRecordObjects();
            Cleanup(false);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        internal static void Cancel()
        {
            if (!IsActive)
            {
                return;
            }

            Cleanup(true);
        }

        internal static void CancelForSafety()
        {
            if (IsActive)
            {
                Cleanup(true);
            }
        }

        private static void Update()
        {
            if (State != PhysicsPlacementSessionState.Simulating)
            {
                return;
            }

            try
            {
                PhysicsPlacementPreferences preferences = PhysicsPlacementPreferences.instance;
                double currentTime = EditorApplication.timeSinceStartup;
                float elapsed = Mathf.Min(0.1f, (float)(currentTime - _lastUpdateTime));
                _lastUpdateTime = currentTime;
                _accumulator += elapsed;

                int steps = 0;
                while (_accumulator >= preferences.FixedStep &&
                       steps < preferences.MaximumStepsPerUpdate)
                {
                    SimulateStep();
                    _accumulator -= preferences.FixedStep;
                    steps++;
                }

                if (_simulatedTime >= preferences.MaximumDuration ||
                    _settledSteps >= RequiredSettledSteps)
                {
                    State = PhysicsPlacementSessionState.Settled;
                    StateChanged?.Invoke();
                }

                if (steps > 0)
                {
                    SceneView.RepaintAll();
                }
            }
            catch (Exception exception)
            {
                Cleanup(true);
                Debug.LogException(exception);
            }
        }

        private static void SimulateStep()
        {
            PhysicsPlacementPreferences preferences = PhysicsPlacementPreferences.instance;
            Physics.defaultPhysicsScene.Simulate(preferences.FixedStep);
            _simulatedTime += preferences.FixedStep;

            bool allSettled = true;
            float linearThresholdSquared = preferences.LinearSleepThreshold *
                                           preferences.LinearSleepThreshold;
            float angularThresholdSquared = preferences.AngularSleepThreshold *
                                            preferences.AngularSleepThreshold;
            for (int index = 0; index < SimulatedBodies.Count; index++)
            {
                Rigidbody body = SimulatedBodies[index];
                if (body == null || body.isKinematic)
                {
                    continue;
                }

                if (!body.IsSleeping() &&
                    (body.linearVelocity.sqrMagnitude > linearThresholdSquared ||
                     body.angularVelocity.sqrMagnitude > angularThresholdSquared))
                {
                    allSettled = false;
                    break;
                }
            }

            _settledSteps = allSettled ? _settledSteps + 1 : 0;
        }

        private static void CollectSelectionRoots(IReadOnlyList<GameObject> selection)
        {
            SelectedRoots.Clear();
            HashSet<GameObject> selected = new(selection);
            for (int index = 0; index < selection.Count; index++)
            {
                GameObject candidate = selection[index];
                if (candidate == null || !candidate.scene.IsValid() || candidate.scene != SceneManager.GetActiveScene())
                {
                    continue;
                }

                Transform parent = candidate.transform.parent;
                bool hasSelectedAncestor = false;
                while (parent != null)
                {
                    if (selected.Contains(parent.gameObject))
                    {
                        hasSelectedAncestor = true;
                        break;
                    }

                    parent = parent.parent;
                }

                if (!hasSelectedAncestor)
                {
                    SelectedRoots.Add(candidate);
                }
            }
        }

        private static void CaptureTransforms()
        {
            TransformSnapshots.Clear();
            HashSet<Transform> captured = new();
            for (int rootIndex = 0; rootIndex < SelectedRoots.Count; rootIndex++)
            {
                Transform[] transforms = SelectedRoots[rootIndex].GetComponentsInChildren<Transform>(true);
                for (int index = 0; index < transforms.Length; index++)
                {
                    if (captured.Add(transforms[index]))
                    {
                        TransformSnapshots.Add(new TransformSnapshot(transforms[index]));
                    }
                }
            }
        }

        private static void CaptureRigidbodies()
        {
            RigidbodySnapshots.Clear();
            SimulatedBodies.Clear();
            HashSet<Rigidbody> captured = new();

            Rigidbody[] sceneBodies = UnityEngine.Object.FindObjectsByType<Rigidbody>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < sceneBodies.Length; index++)
            {
                Rigidbody body = sceneBodies[index];
                if (body.gameObject.scene != SceneManager.GetActiveScene() || !captured.Add(body))
                {
                    continue;
                }

                RigidbodySnapshots.Add(new RigidbodySnapshot(body));
            }
        }

        private static void ConfigureRigidbodies()
        {
            PhysicsPlacementPreferences preferences = PhysicsPlacementPreferences.instance;
            for (int index = 0; index < RigidbodySnapshots.Count; index++)
            {
                Rigidbody body = RigidbodySnapshots[index].Body;
                if (body == null)
                {
                    continue;
                }

                if (IsUnderSelection(body.transform))
                {
                    ConfigureSimulatedBody(body, preferences);
                    SimulatedBodies.Add(body);
                }
                else
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.isKinematic = true;
                }
            }

            for (int index = 0; index < SelectedRoots.Count; index++)
            {
                GameObject root = SelectedRoots[index];
                if (HasSimulatedBody(root.transform))
                {
                    continue;
                }

                Rigidbody body = root.AddComponent<Rigidbody>();
                body.hideFlags = HideFlags.HideInInspector | HideFlags.DontSaveInEditor;
                TemporaryComponents.Add(body);
                ConfigureSimulatedBody(body, preferences);
                SimulatedBodies.Add(body);
            }
        }

        private static bool HasSimulatedBody(Transform root)
        {
            for (int index = 0; index < SimulatedBodies.Count; index++)
            {
                Rigidbody body = SimulatedBodies[index];
                if (body != null && (body.transform == root || body.transform.IsChildOf(root)))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ConfigureSimulatedBody(
            Rigidbody body,
            PhysicsPlacementPreferences preferences)
        {
            body.isKinematic = false;
            body.useGravity = true;
            body.detectCollisions = true;
            body.constraints = preferences.BuildConstraints();
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.interpolation = RigidbodyInterpolation.None;
            body.solverIterations = preferences.SolverIterations;
            body.solverVelocityIterations = preferences.SolverVelocityIterations;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
        }

        private static void AddRequiredColliders()
        {
            PhysicsPlacementPreferences preferences = PhysicsPlacementPreferences.instance;
            for (int index = 0; index < SelectedRoots.Count; index++)
            {
                GameObject root = SelectedRoots[index];
                Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
                if (HasEnabledCollider(colliders))
                {
                    continue;
                }

                PhysicsPlacementColliderFactory.AddTemporaryCollider(
                    root,
                    preferences.ColliderStrategy,
                    TemporaryComponents);
            }

            if (preferences.GenerateEnvironmentColliders)
            {
                AddEnvironmentColliders(preferences);
            }
        }

        private static void AddEnvironmentColliders(PhysicsPlacementPreferences preferences)
        {
            Bounds selectionBounds = CalculateSelectionBounds();
            selectionBounds.Expand(preferences.EnvironmentRange * 2f);
            Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                GameObject target = renderer.gameObject;
                if (target.scene != SceneManager.GetActiveScene() ||
                    IsUnderSelection(target.transform) ||
                    !selectionBounds.Intersects(renderer.bounds) ||
                    target.GetComponentInParent<Collider>() != null)
                {
                    continue;
                }

                PhysicsPlacementColliderFactory.AddTemporaryCollider(
                    target,
                    PhysicsPlacementColliderStrategy.Performance,
                    TemporaryComponents);
            }
        }

        private static Bounds CalculateSelectionBounds()
        {
            bool initialized = false;
            Bounds bounds = default;
            for (int index = 0; index < SelectedRoots.Count; index++)
            {
                Renderer[] renderers = SelectedRoots[index].GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    if (!initialized)
                    {
                        bounds = renderers[rendererIndex].bounds;
                        initialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderers[rendererIndex].bounds);
                    }
                }
            }

            return initialized ? bounds : new Bounds(SelectedRoots[0].transform.position, Vector3.one);
        }

        private static bool HasEnabledCollider(IReadOnlyList<Collider> colliders)
        {
            for (int index = 0; index < colliders.Count; index++)
            {
                if (colliders[index] != null && colliders[index].enabled)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsUnderSelection(Transform candidate)
        {
            for (int index = 0; index < SelectedRoots.Count; index++)
            {
                Transform root = SelectedRoots[index].transform;
                if (candidate == root || candidate.IsChildOf(root))
                {
                    return true;
                }
            }

            return false;
        }

        private static void WakeBodies()
        {
            for (int index = 0; index < SimulatedBodies.Count; index++)
            {
                SimulatedBodies[index]?.WakeUp();
            }
        }

        private static void Cleanup(bool restoreTransforms)
        {
            if (_isCleaningUp)
            {
                return;
            }

            _isCleaningUp = true;
            EditorApplication.update -= Update;

            if (restoreTransforms)
            {
                for (int index = 0; index < TransformSnapshots.Count; index++)
                {
                    TransformSnapshots[index].Restore();
                }
            }

            for (int index = 0; index < RigidbodySnapshots.Count; index++)
            {
                RigidbodySnapshots[index].Restore();
            }

            for (int index = TemporaryComponents.Count - 1; index >= 0; index--)
            {
                Component component = TemporaryComponents[index];
                if (component != null)
                {
                    UnityEngine.Object.DestroyImmediate(component);
                }
            }

            Physics.simulationMode = _originalSimulationMode;
            Physics.gravity = _originalGravity;
            Physics.SyncTransforms();
            PhysicsPlacementRecoveryJournal.Delete();

            TransformSnapshots.Clear();
            RigidbodySnapshots.Clear();
            SimulatedBodies.Clear();
            TemporaryComponents.Clear();
            SelectedRoots.Clear();
            State = PhysicsPlacementSessionState.Inactive;
            _isCleaningUp = false;
            StateChanged?.Invoke();
            SceneView.RepaintAll();
        }

        private static void RestoreFailedStart()
        {
            for (int index = 0; index < TransformSnapshots.Count; index++)
            {
                TransformSnapshots[index].Restore();
            }

            for (int index = 0; index < RigidbodySnapshots.Count; index++)
            {
                RigidbodySnapshots[index].Restore();
            }

            for (int index = TemporaryComponents.Count - 1; index >= 0; index--)
            {
                Component component = TemporaryComponents[index];
                if (component != null)
                {
                    UnityEngine.Object.DestroyImmediate(component);
                }
            }

            Physics.simulationMode = _originalSimulationMode;
            Physics.gravity = _originalGravity;
            PhysicsPlacementRecoveryJournal.Delete();
            TransformSnapshots.Clear();
            RigidbodySnapshots.Clear();
            SimulatedBodies.Clear();
            TemporaryComponents.Clear();
            SelectedRoots.Clear();
            State = PhysicsPlacementSessionState.Inactive;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                CancelForSafety();
            }
        }

        private static void HandleSceneSaving(Scene scene, string path)
        {
            CancelForSafety();
        }

        private static void HandleUndoRedo()
        {
            CancelForSafety();
        }
    }
}
