using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Toolkit.PhysicsPlacement
{
    internal sealed class PhysicsPlacementWindow : EditorWindow
    {
        private const float ScenePanelWidth = 300f;
        private const float ScenePanelHeight = 72f;

        [MenuItem("LoogaSoft/Toolkit/Physics Placement")]
        private static void Open()
        {
            PhysicsPlacementWindow window = GetWindow<PhysicsPlacementWindow>();
            window.titleContent = new GUIContent("Physics Placement");
            window.minSize = new Vector2(340f, 440f);
            window.Show();
        }

        [MenuItem("GameObject/Looga Toolkit/Settle With Physics", false, 21)]
        private static void SettleSelection()
        {
            Open();
            StartSelectedObjects();
        }

        [MenuItem("GameObject/Looga Toolkit/Settle With Physics", true)]
        private static bool ValidateSettleSelection()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && Selection.gameObjects.Length > 0;
        }

        private void OnEnable()
        {
            PhysicsPlacementSession.StateChanged += Repaint;
            SceneView.duringSceneGui += DrawSceneControls;
        }

        private void OnDisable()
        {
            PhysicsPlacementSession.StateChanged -= Repaint;
            SceneView.duringSceneGui -= DrawSceneControls;
        }

        private void OnGUI()
        {
            PhysicsPlacementPreferences preferences = PhysicsPlacementPreferences.instance;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Physics Placement", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Simulate selected scene objects without entering Play Mode. Apply keeps the result. Cancel restores the authored state.",
                MessageType.Info);

            DrawSessionSection();
            EditorGUILayout.Space(6f);
            DrawSimulationSettings(preferences);
            EditorGUILayout.Space(6f);
            DrawColliderSettings(preferences);
            EditorGUILayout.Space(6f);
            DrawColliderBaking(preferences);
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "Active sessions restore automatically before a reload, scene save, Play Mode transition, or editor shutdown. An interrupted session restores from its Library journal when the scene opens.",
                MessageType.None);
        }

        private static void DrawSessionSection()
        {
            EditorGUILayout.LabelField("Session", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("State", PhysicsPlacementSession.State.ToString());
                EditorGUILayout.LabelField("Objects", PhysicsPlacementSession.SimulatedObjectCount.ToString());
                EditorGUILayout.LabelField("Simulation Time", $"{PhysicsPlacementSession.SimulatedTime:0.00} s");

                if (!PhysicsPlacementSession.IsActive)
                {
                    using (new EditorGUI.DisabledScope(Selection.gameObjects.Length == 0))
                    {
                        if (GUILayout.Button("Start Selected Objects", GUILayout.Height(24f)))
                        {
                            StartSelectedObjects();
                        }
                    }

                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (PhysicsPlacementSession.State == PhysicsPlacementSessionState.Simulating)
                    {
                        if (GUILayout.Button("Pause"))
                        {
                            PhysicsPlacementSession.Pause();
                        }
                    }
                    else if (GUILayout.Button("Resume"))
                    {
                        PhysicsPlacementSession.Resume();
                    }

                    using (new EditorGUI.DisabledScope(
                               PhysicsPlacementSession.State == PhysicsPlacementSessionState.Simulating))
                    {
                        if (GUILayout.Button("Step"))
                        {
                            PhysicsPlacementSession.Step();
                        }
                    }

                    if (GUILayout.Button("Reset"))
                    {
                        PhysicsPlacementSession.Reset();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Apply", GUILayout.Height(24f)))
                    {
                        PhysicsPlacementSession.Apply();
                    }

                    if (GUILayout.Button("Cancel", GUILayout.Height(24f)))
                    {
                        PhysicsPlacementSession.Cancel();
                    }
                }
            }
        }

        private static void DrawSimulationSettings(PhysicsPlacementPreferences preferences)
        {
            EditorGUILayout.LabelField("Simulation", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(PhysicsPlacementSession.IsActive))
            {
                preferences.Quality = (PhysicsPlacementQuality)EditorGUILayout.EnumPopup(
                    "Quality",
                    preferences.Quality);
                preferences.MaximumDuration = EditorGUILayout.FloatField(
                    "Maximum Duration",
                    preferences.MaximumDuration);
                preferences.LinearSleepThreshold = EditorGUILayout.FloatField(
                    "Linear Sleep Threshold",
                    preferences.LinearSleepThreshold);
                preferences.AngularSleepThreshold = EditorGUILayout.FloatField(
                    "Angular Sleep Threshold",
                    preferences.AngularSleepThreshold);

                EditorGUILayout.LabelField("Position Locks", EditorStyles.miniBoldLabel);
                DrawAxisLocks(
                    preferences.FreezePositionX,
                    preferences.FreezePositionY,
                    preferences.FreezePositionZ,
                    out bool positionX,
                    out bool positionY,
                    out bool positionZ);
                preferences.FreezePositionX = positionX;
                preferences.FreezePositionY = positionY;
                preferences.FreezePositionZ = positionZ;

                EditorGUILayout.LabelField("Rotation Locks", EditorStyles.miniBoldLabel);
                DrawAxisLocks(
                    preferences.FreezeRotationX,
                    preferences.FreezeRotationY,
                    preferences.FreezeRotationZ,
                    out bool rotationX,
                    out bool rotationY,
                    out bool rotationZ);
                preferences.FreezeRotationX = rotationX;
                preferences.FreezeRotationY = rotationY;
                preferences.FreezeRotationZ = rotationZ;
            }
        }

        private static void DrawColliderSettings(PhysicsPlacementPreferences preferences)
        {
            EditorGUILayout.LabelField("Temporary Colliders", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(PhysicsPlacementSession.IsActive))
            {
                preferences.ColliderStrategy =
                    (PhysicsPlacementColliderStrategy)EditorGUILayout.EnumPopup(
                        "Selected Objects",
                        preferences.ColliderStrategy);
                preferences.GenerateEnvironmentColliders = EditorGUILayout.Toggle(
                    "Generate Nearby Colliders",
                    preferences.GenerateEnvironmentColliders);
                using (new EditorGUI.DisabledScope(!preferences.GenerateEnvironmentColliders))
                {
                    preferences.EnvironmentRange = EditorGUILayout.FloatField(
                        "Nearby Range",
                        preferences.EnvironmentRange);
                }
            }

            EditorGUILayout.LabelField("Cached Fits", PhysicsPlacementColliderFactory.CacheCount.ToString());
            if (GUILayout.Button("Clear Collider Cache"))
            {
                PhysicsPlacementColliderFactory.ClearCache();
            }
        }

        private static void DrawColliderBaking(PhysicsPlacementPreferences preferences)
        {
            EditorGUILayout.LabelField("Permanent Collider Baking", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Bake adds native Unity colliders to selected objects that have no collider. The operation supports Undo.",
                MessageType.None);
            using (new EditorGUI.DisabledScope(
                       PhysicsPlacementSession.IsActive || Selection.gameObjects.Length == 0))
            {
                if (!GUILayout.Button("Bake Missing Colliders"))
                {
                    return;
                }

                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Bake Physics Placement Colliders");
                int baked = 0;
                GameObject[] selection = Selection.gameObjects;
                for (int index = 0; index < selection.Length; index++)
                {
                    if (PhysicsPlacementColliderFactory.BakeCollider(
                            selection[index],
                            preferences.ColliderStrategy))
                    {
                        baked++;
                    }
                }

                Undo.CollapseUndoOperations(undoGroup);
                Debug.Log($"Looga Physics Placement baked {baked} collider(s).");
            }
        }

        private static void DrawAxisLocks(
            bool x,
            bool y,
            bool z,
            out bool resultX,
            out bool resultY,
            out bool resultZ)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                resultX = GUILayout.Toggle(x, "X", EditorStyles.miniButtonLeft);
                resultY = GUILayout.Toggle(y, "Y", EditorStyles.miniButtonMid);
                resultZ = GUILayout.Toggle(z, "Z", EditorStyles.miniButtonRight);
            }
        }

        private static void StartSelectedObjects()
        {
            GameObject[] selected = Selection.gameObjects;
            List<GameObject> objects = new(selected.Length);
            for (int index = 0; index < selected.Length; index++)
            {
                objects.Add(selected[index]);
            }

            if (!PhysicsPlacementSession.Start(objects))
            {
                Debug.LogWarning("Looga Physics Placement could not start with the current selection.");
            }
        }

        private static void DrawSceneControls(SceneView sceneView)
        {
            if (!PhysicsPlacementSession.IsActive)
            {
                return;
            }

            Handles.BeginGUI();
            GUILayout.BeginArea(
                new Rect(12f, 12f, ScenePanelWidth, ScenePanelHeight),
                EditorStyles.helpBox);
            GUILayout.Label(
                $"Physics Placement: {PhysicsPlacementSession.State}",
                EditorStyles.boldLabel);
            using (new GUILayout.HorizontalScope())
            {
                if (PhysicsPlacementSession.State == PhysicsPlacementSessionState.Simulating)
                {
                    if (GUILayout.Button("Pause"))
                    {
                        PhysicsPlacementSession.Pause();
                    }
                }
                else if (GUILayout.Button("Resume"))
                {
                    PhysicsPlacementSession.Resume();
                }

                if (GUILayout.Button("Apply"))
                {
                    PhysicsPlacementSession.Apply();
                }

                if (GUILayout.Button("Cancel"))
                {
                    PhysicsPlacementSession.Cancel();
                }
            }

            GUILayout.EndArea();
            Handles.EndGUI();
        }
    }
}
