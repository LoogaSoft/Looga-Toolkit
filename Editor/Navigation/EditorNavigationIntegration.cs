using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Navigation.Editor
{
    /// <summary>
    /// Adds bounded navigation history to Unity Inspector and Project windows.
    /// </summary>
    [InitializeOnLoad]
    internal static class EditorNavigationIntegration
    {
        private const double WindowScanInterval = 1d;
        private const double FolderObservationInterval = 0.15d;

        private static readonly Type InspectorWindowType =
            typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
        private static readonly Type ProjectBrowserType =
            typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
        private static readonly PropertyInfo InspectorLockedProperty = InspectorWindowType?.GetProperty(
            "isLocked",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly Dictionary<EditorWindow, InspectorNavigationBar> InspectorBars = new();
        private static readonly Dictionary<EditorWindow, ProjectWindowNavigation> ProjectBars = new();

        private static double _nextWindowScan;
        private static double _nextFolderObservation;
        private static bool _scanRequested = true;

        static EditorNavigationIntegration()
        {
            EditorApplication.delayCall += Initialize;
        }

        private static void Initialize()
        {
            InspectorSelectionHistory.Initialize();

            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
            EditorWindow.windowFocusChanged -= OnWindowFocusChanged;
            EditorWindow.windowFocusChanged += OnWindowFocusChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;

            ScanWindows();
        }

        private static void Dispose()
        {
            EditorApplication.update -= Update;
            Selection.selectionChanged -= OnSelectionChanged;
            EditorWindow.windowFocusChanged -= OnWindowFocusChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;

            foreach (InspectorNavigationBar bar in InspectorBars.Values)
                bar.Dispose();

            foreach (ProjectWindowNavigation navigation in ProjectBars.Values)
                navigation.Dispose();

            InspectorBars.Clear();
            ProjectBars.Clear();
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            double now = EditorApplication.timeSinceStartup;
            if (_scanRequested || now >= _nextWindowScan)
            {
                _scanRequested = false;
                _nextWindowScan = now + WindowScanInterval;
                ScanWindows();
            }

            if (now < _nextFolderObservation)
                return;

            _nextFolderObservation = now + FolderObservationInterval;
            foreach (ProjectWindowNavigation navigation in ProjectBars.Values)
                navigation.ObserveCurrentFolder();
        }

        private static void OnSelectionChanged()
        {
            InspectorSelectionHistory.ObserveCurrentSelection();
        }

        private static void OnWindowFocusChanged()
        {
            _scanRequested = true;
        }

        private static void ScanWindows()
        {
            ScanInspectorWindows();
            ScanProjectWindows();
        }

        private static void ScanInspectorWindows()
        {
            if (InspectorWindowType == null)
                return;

            UnityEngine.Object[] foundObjects = Resources.FindObjectsOfTypeAll(InspectorWindowType);
            HashSet<EditorWindow> foundWindows = new();
            for (int i = 0; i < foundObjects.Length; i++)
            {
                if (foundObjects[i] is not EditorWindow window)
                    continue;

                foundWindows.Add(window);
                bool locked = InspectorLockedProperty != null &&
                    InspectorLockedProperty.GetValue(window) is true;
                if (locked)
                {
                    RemoveInspectorBar(window);
                    continue;
                }

                if (!InspectorBars.TryGetValue(window, out InspectorNavigationBar bar))
                {
                    bar = new InspectorNavigationBar(window);
                    InspectorBars.Add(window, bar);
                }

                if (!bar.IsAttached)
                    bar.Attach();
            }

            List<EditorWindow> removedWindows = new();
            foreach (KeyValuePair<EditorWindow, InspectorNavigationBar> pair in InspectorBars)
            {
                if (pair.Key == null || !foundWindows.Contains(pair.Key))
                    removedWindows.Add(pair.Key);
            }

            for (int i = 0; i < removedWindows.Count; i++)
                RemoveInspectorBar(removedWindows[i]);
        }

        private static void ScanProjectWindows()
        {
            if (ProjectBrowserType == null || !ProjectBrowserBridge.IsAvailable)
                return;

            UnityEngine.Object[] foundObjects = Resources.FindObjectsOfTypeAll(ProjectBrowserType);
            HashSet<EditorWindow> foundWindows = new();
            for (int i = 0; i < foundObjects.Length; i++)
            {
                if (foundObjects[i] is not EditorWindow window)
                    continue;

                foundWindows.Add(window);
                if (!ProjectBars.TryGetValue(window, out ProjectWindowNavigation navigation))
                {
                    navigation = new ProjectWindowNavigation(window);
                    ProjectBars.Add(window, navigation);
                }

                if (!navigation.IsAttached)
                    navigation.Attach();
            }

            List<EditorWindow> removedWindows = new();
            foreach (KeyValuePair<EditorWindow, ProjectWindowNavigation> pair in ProjectBars)
            {
                if (pair.Key == null || !foundWindows.Contains(pair.Key))
                    removedWindows.Add(pair.Key);
            }

            for (int i = 0; i < removedWindows.Count; i++)
                RemoveProjectBar(removedWindows[i]);
        }

        private static void RemoveInspectorBar(EditorWindow window)
        {
            if (!InspectorBars.TryGetValue(window, out InspectorNavigationBar bar))
                return;

            bar.Dispose();
            InspectorBars.Remove(window);
        }

        private static void RemoveProjectBar(EditorWindow window)
        {
            if (!ProjectBars.TryGetValue(window, out ProjectWindowNavigation navigation))
                return;

            navigation.Dispose();
            ProjectBars.Remove(window);
        }

        private sealed class ProjectWindowNavigation : IDisposable
        {
            private readonly EditorWindow _window;
            private readonly ProjectFolderHistory _history = new();
            private readonly ProjectNavigationBar _bar;

            public ProjectWindowNavigation(EditorWindow window)
            {
                _window = window;
                _bar = new ProjectNavigationBar(window, _history);
                ObserveCurrentFolder();
            }

            public bool IsAttached => _bar.IsAttached;

            public void Attach()
            {
                _bar.Attach();
            }

            public void ObserveCurrentFolder()
            {
                if (_window == null)
                    return;

                _history.Observe(ProjectBrowserBridge.GetActiveFolderPath(_window));
            }

            public void Dispose()
            {
                _bar.Dispose();
            }
        }
    }

    internal static class ProjectBrowserBridge
    {
        private static readonly Type ProjectBrowserType =
            typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
        private static readonly MethodInfo GetActiveFolderPathMethod = ProjectBrowserType?.GetMethod(
            "GetActiveFolderPath",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ShowFolderContentsMethod = ProjectBrowserType?.GetMethod(
            "ShowFolderContents",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static bool _warningLogged;

        public static bool IsAvailable =>
            ProjectBrowserType != null &&
            GetActiveFolderPathMethod != null &&
            ShowFolderContentsMethod != null;

        public static string GetActiveFolderPath(EditorWindow window)
        {
            if (!IsAvailable || window == null)
                return string.Empty;

            try
            {
                return GetActiveFolderPathMethod.Invoke(window, null) as string ?? string.Empty;
            }
            catch (Exception exception)
            {
                LogWarningOnce(exception);
                return string.Empty;
            }
        }

        public static bool OpenFolder(EditorWindow window, string path)
        {
            if (!IsAvailable || window == null || !AssetDatabase.IsValidFolder(path))
                return false;

            DefaultAsset folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
            if (folder == null)
                return false;

            try
            {
                ShowFolderContentsMethod.Invoke(window, new object[] { folder.GetEntityId(), true });
                window.Repaint();
                return true;
            }
            catch (Exception exception)
            {
                LogWarningOnce(exception);
                return false;
            }
        }

        private static void LogWarningOnce(Exception exception)
        {
            if (_warningLogged)
                return;

            _warningLogged = true;
            Debug.LogWarning(
                $"Looga editor navigation could not access the current Unity Project browser API. " +
                $"Project history is disabled for this editor version. {exception.GetBaseException().Message}");
        }
    }
}
