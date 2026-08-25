using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace LoogaSoft.Tools.Editor
{
    internal sealed class EditorPerformanceRecorderWindow : EditorWindow
    {
        private const string MenuPath = "LoogaSoft/Toolkit/Editor Performance Recorder";

        [MenuItem(MenuPath, priority = 25)]
        private static void Open()
        {
            GetWindow<EditorPerformanceRecorderWindow>("Performance Recorder");
        }

        private void OnEnable()
        {
            EditorPerformanceRecorder.StateChanged += Repaint;
        }

        private void OnDisable()
        {
            EditorPerformanceRecorder.StateChanged -= Repaint;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Editor Performance Recorder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Record only the actions that reproduce the delay. The capture includes Editor stalls, memory, " +
                "selection, drag, import, compilation, and a native Unity Profiler timeline.",
                MessageType.Info);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Status", EditorPerformanceRecorder.IsRecording ? "Recording" : "Idle");

            if (EditorPerformanceRecorder.IsRecording)
            {
                EditorGUILayout.LabelField("Elapsed", EditorPerformanceRecorder.Elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s");
                EditorGUILayout.LabelField("Recorded stalls", EditorPerformanceRecorder.StallCount.ToString(CultureInfo.InvariantCulture));
            }

            EditorGUILayout.Space(6f);

            using (new EditorGUI.DisabledScope(EditorPerformanceRecorder.IsRecording))
            {
                if (GUILayout.Button("Start Recording", GUILayout.Height(28f)))
                {
                    EditorPerformanceRecorder.Start();
                }
            }

            using (new EditorGUI.DisabledScope(!EditorPerformanceRecorder.IsRecording))
            {
                if (GUILayout.Button("Stop And Save", GUILayout.Height(28f)))
                {
                    EditorPerformanceRecorder.Stop("Stopped by user");
                }
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "Keep captures short, ideally 10 to 30 seconds. Recording stops automatically after two minutes.",
                MessageType.None);

            string lastCapture = EditorPerformanceRecorder.LastCaptureDirectory;
            if (string.IsNullOrEmpty(lastCapture))
            {
                return;
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Last Capture", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(lastCapture, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

            if (GUILayout.Button("Reveal Capture Folder"))
            {
                EditorUtility.RevealInFinder(lastCapture);
            }
        }
    }

    [InitializeOnLoad]
    internal static class EditorPerformanceRecorder
    {
        private const double SampleIntervalSeconds = 0.25d;
        private const double StallThresholdSeconds = 0.10d;
        private const double MaximumDurationSeconds = 120d;

        private static readonly List<PerformanceSample> Samples = new List<PerformanceSample>(512);
        private static readonly List<PerformanceEvent> Events = new List<PerformanceEvent>(128);
        private static readonly Process CurrentProcess = Process.GetCurrentProcess();

        private static bool _previousProfilerEnabled;
        private static bool _previousProfileEditor;
        private static double _startedAt;
        private static double _lastUpdateAt;
        private static double _lastSampleAt;
        private static string _captureDirectory;
        private static string _editorLogPath;
        private static long _editorLogStartPosition;
        private static string _lastDragSignature = string.Empty;

        static EditorPerformanceRecorder()
        {
            LastCaptureDirectory = EditorPrefs.GetString("LoogaSoft.EditorPerformanceRecorder.LastCapture", string.Empty);
        }

        internal static event Action StateChanged;

        internal static bool IsRecording { get; private set; }

        internal static double Elapsed => IsRecording ? EditorApplication.timeSinceStartup - _startedAt : 0d;

        internal static int StallCount { get; private set; }

        internal static string LastCaptureDirectory { get; private set; }

        internal static void Start()
        {
            if (IsRecording)
            {
                return;
            }

            Samples.Clear();
            Events.Clear();
            StallCount = 0;
            _lastDragSignature = string.Empty;
            _startedAt = EditorApplication.timeSinceStartup;
            _lastUpdateAt = _startedAt;
            _lastSampleAt = double.NegativeInfinity;
            _captureDirectory = CreateCaptureDirectory();
            _editorLogPath = Application.consoleLogPath;
            _editorLogStartPosition = GetFileLength(_editorLogPath);

            _previousProfilerEnabled = ProfilerDriver.enabled;
            _previousProfileEditor = ProfilerDriver.profileEditor;
            ProfilerDriver.ClearAllFrames();
            ProfilerDriver.profileEditor = true;
            ProfilerDriver.enabled = true;

            Subscribe();
            IsRecording = true;
            RecordEvent("Capture", "Recording started");
            CaptureSample(0d);
            StateChanged?.Invoke();
        }

        internal static void Stop(string reason)
        {
            if (!IsRecording)
            {
                return;
            }

            RecordEvent("Capture", reason);
            CaptureSample(EditorApplication.timeSinceStartup - _lastUpdateAt);
            IsRecording = false;
            Unsubscribe();

            string profilePath = Path.Combine(_captureDirectory, "unity-profiler.data");
            bool profileSaved = false;
            try
            {
                profileSaved = ProfilerDriver.SaveProfile(profilePath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Looga Toolkit could not save the native Unity Profiler capture: {exception.Message}");
            }
            finally
            {
                ProfilerDriver.enabled = _previousProfilerEnabled;
                ProfilerDriver.profileEditor = _previousProfileEditor;
            }

            WriteCaptureFiles(profileSaved);
            WriteEditorLogExcerpt();
            LastCaptureDirectory = _captureDirectory;
            EditorPrefs.SetString("LoogaSoft.EditorPerformanceRecorder.LastCapture", LastCaptureDirectory);
            Debug.Log($"Looga Toolkit saved the Editor performance capture to '{LastCaptureDirectory}'.");
            StateChanged?.Invoke();
        }

        internal static void RecordAssetChanges(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!IsRecording)
            {
                return;
            }

            int total = importedAssets.Length + deletedAssets.Length + movedAssets.Length;
            if (total == 0)
            {
                return;
            }

            string detail = $"Imported {importedAssets.Length}, deleted {deletedAssets.Length}, moved {movedAssets.Length}. " +
                            BuildPathPreview(importedAssets, movedAssets, deletedAssets, movedFromAssetPaths);
            RecordEvent("Assets", detail);
        }

        private static void Subscribe()
        {
            EditorApplication.update += OnEditorUpdate;
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.projectChanged += OnProjectChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.focusChanged += OnFocusChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        private static void Unsubscribe()
        {
            EditorApplication.update -= OnEditorUpdate;
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.focusChanged -= OnFocusChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        }

        private static void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            double updateGap = now - _lastUpdateAt;
            _lastUpdateAt = now;
            bool sampled = false;

            if (updateGap >= StallThresholdSeconds)
            {
                StallCount++;
                RecordEvent("Stall", $"Editor update was delayed by {updateGap * 1000d:F1} ms. Selection: {DescribeSelection()}");
                CaptureSample(updateGap);
                sampled = true;
            }
            else if (now - _lastSampleAt >= SampleIntervalSeconds)
            {
                CaptureSample(updateGap);
                sampled = true;
            }

            TrackDragState();

            if (now - _startedAt >= MaximumDurationSeconds)
            {
                Stop("Stopped automatically at the two-minute limit");
            }

            if (sampled)
            {
                StateChanged?.Invoke();
            }
        }

        private static void CaptureSample(double updateGap)
        {
            double now = EditorApplication.timeSinceStartup;
            _lastSampleAt = now;
            CurrentProcess.Refresh();

            Samples.Add(new PerformanceSample(
                (now - _startedAt) * 1000d,
                updateGap * 1000d,
                CurrentProcess.WorkingSet64,
                CurrentProcess.PrivateMemorySize64,
                GC.GetTotalMemory(false),
                Profiler.GetTotalAllocatedMemoryLong(),
                Profiler.GetMonoUsedSizeLong(),
                Profiler.GetMonoHeapSizeLong(),
                DescribeSelection(),
                _lastDragSignature));
        }

        private static void TrackDragState()
        {
            Object[] references = DragAndDrop.objectReferences;
            string signature = references == null || references.Length == 0
                ? string.Empty
                : string.Join(" | ", references.Select(DescribeObject));

            if (signature == _lastDragSignature)
            {
                return;
            }

            _lastDragSignature = signature;
            RecordEvent("Drag", string.IsNullOrEmpty(signature) ? "Drag ended" : "Dragging " + signature);
        }

        private static void OnSelectionChanged()
        {
            RecordEvent("Selection", DescribeSelection());
        }

        private static void OnProjectChanged()
        {
            RecordEvent("Project", "Project contents changed");
        }

        private static void OnHierarchyChanged()
        {
            RecordEvent("Hierarchy", "Hierarchy contents changed");
        }

        private static void OnFocusChanged(bool hasFocus)
        {
            RecordEvent("Focus", hasFocus ? "Unity gained focus" : "Unity lost focus");
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            RecordEvent("Play Mode", state.ToString());
        }

        private static void OnUndoRedoPerformed()
        {
            RecordEvent("Undo", "Undo or redo performed");
        }

        private static void OnCompilationStarted(object context)
        {
            RecordEvent("Compilation", "Compilation started");
        }

        private static void OnCompilationFinished(object context)
        {
            RecordEvent("Compilation", "Compilation finished");
        }

        private static void OnBeforeAssemblyReload()
        {
            Stop("Stopped before assembly reload");
        }

        private static void RecordEvent(string category, string detail)
        {
            if (!IsRecording && category != "Capture")
            {
                return;
            }

            Events.Add(new PerformanceEvent(
                (EditorApplication.timeSinceStartup - _startedAt) * 1000d,
                category,
                detail));
        }

        private static string DescribeSelection()
        {
            Object activeObject = Selection.activeObject;
            return activeObject == null ? "None" : DescribeObject(activeObject);
        }

        private static string DescribeObject(Object target)
        {
            if (target == null)
            {
                return "Missing object";
            }

            string assetPath = AssetDatabase.GetAssetPath(target);
            if (!string.IsNullOrEmpty(assetPath))
            {
                return $"{target.GetType().Name}: {assetPath}";
            }

            GameObject gameObject = target as GameObject;
            if (target is Component component)
            {
                gameObject = component.gameObject;
            }

            return gameObject == null
                ? $"{target.GetType().Name}: {target.name}"
                : $"{target.GetType().Name}: {BuildHierarchyPath(gameObject.transform)}";
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            StringBuilder builder = new StringBuilder(transform.name);
            Transform parent = transform.parent;
            while (parent != null)
            {
                builder.Insert(0, '/');
                builder.Insert(0, parent.name);
                parent = parent.parent;
            }

            return builder.ToString();
        }

        private static string CreateCaptureDirectory()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string root = Path.Combine(projectRoot, "Library", "LoogaSoft", "EditorPerformanceCaptures");
            string directory = Path.Combine(root, DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void WriteCaptureFiles(bool profileSaved)
        {
            File.WriteAllText(Path.Combine(_captureDirectory, "report.md"), BuildReport(profileSaved));
            File.WriteAllText(Path.Combine(_captureDirectory, "samples.csv"), BuildSamplesCsv());
            File.WriteAllText(Path.Combine(_captureDirectory, "events.csv"), BuildEventsCsv());
        }

        private static void WriteEditorLogExcerpt()
        {
            if (string.IsNullOrEmpty(_editorLogPath) || !File.Exists(_editorLogPath))
            {
                return;
            }

            try
            {
                using FileStream stream = new FileStream(
                    _editorLogPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);

                if (_editorLogStartPosition > stream.Length)
                {
                    _editorLogStartPosition = 0;
                }

                stream.Position = _editorLogStartPosition;
                using StreamReader reader = new StreamReader(stream, Encoding.UTF8, true);
                File.WriteAllText(
                    Path.Combine(_captureDirectory, "editor-log-excerpt.txt"),
                    reader.ReadToEnd());
            }
            catch (IOException exception)
            {
                Debug.LogWarning($"Looga Toolkit could not copy the Editor log excerpt: {exception.Message}");
            }
        }

        private static long GetFileLength(string path)
        {
            try
            {
                return string.IsNullOrEmpty(path) || !File.Exists(path) ? 0L : new FileInfo(path).Length;
            }
            catch (IOException)
            {
                return 0L;
            }
        }

        private static string BuildReport(bool profileSaved)
        {
            PerformanceSample peakWorkingSet = FindPeak(sample => sample.WorkingSetBytes);
            PerformanceSample peakPrivateMemory = FindPeak(sample => sample.PrivateMemoryBytes);
            PerformanceSample peakUnityAllocated = FindPeak(sample => sample.UnityAllocatedBytes);
            List<PerformanceSample> topStalls = Samples
                .Where(sample => sample.UpdateGapMilliseconds >= StallThresholdSeconds * 1000d)
                .OrderByDescending(sample => sample.UpdateGapMilliseconds)
                .Take(10)
                .ToList();

            StringBuilder builder = new StringBuilder(2048);
            builder.AppendLine("# Unity Editor Performance Capture");
            builder.AppendLine();
            builder.AppendLine($"- Captured: {DateTime.Now:O}");
            builder.AppendLine($"- Duration: {(EditorApplication.timeSinceStartup - _startedAt):F1} seconds");
            builder.AppendLine($"- Unity: {Application.unityVersion}");
            builder.AppendLine($"- Platform: {SystemInfo.operatingSystem}");
            builder.AppendLine($"- CPU: {SystemInfo.processorType} ({SystemInfo.processorCount} logical processors)");
            builder.AppendLine($"- GPU: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})");
            builder.AppendLine($"- System memory: {SystemInfo.systemMemorySize} MB");
            builder.AppendLine($"- Native profiler capture: {(profileSaved ? "unity-profiler.data" : "Save failed")}");
            builder.AppendLine();
            builder.AppendLine("## Summary");
            builder.AppendLine();
            builder.AppendLine($"- Samples: {Samples.Count}");
            builder.AppendLine($"- Events: {Events.Count}");
            builder.AppendLine($"- Editor stalls at or above 100 ms: {StallCount}");
            builder.AppendLine($"- Peak process working set: {FormatBytes(peakWorkingSet.WorkingSetBytes)}");
            builder.AppendLine($"- Peak process private memory: {FormatBytes(peakPrivateMemory.PrivateMemoryBytes)}");
            builder.AppendLine($"- Peak Unity allocated memory: {FormatBytes(peakUnityAllocated.UnityAllocatedBytes)}");
            builder.AppendLine();
            builder.AppendLine("## Longest Editor Stalls");
            builder.AppendLine();

            if (topStalls.Count == 0)
            {
                builder.AppendLine("No update gap reached 100 ms.");
            }
            else
            {
                foreach (PerformanceSample stall in topStalls)
                {
                    builder.AppendLine(
                        $"- {stall.ElapsedMilliseconds:F0} ms: {stall.UpdateGapMilliseconds:F1} ms gap; " +
                        $"selection `{stall.Selection}`; drag `{stall.Drag}`");
                }
            }

            builder.AppendLine();
            builder.AppendLine("## Files");
            builder.AppendLine();
            builder.AppendLine("- `unity-profiler.data`: Load this in Unity's Profiler window.");
            builder.AppendLine("- `samples.csv`: Memory, update gaps, selection, and drag state.");
            builder.AppendLine("- `events.csv`: Selection, asset, hierarchy, compilation, and stall markers.");
            builder.AppendLine("- `editor-log-excerpt.txt`: Unity log output produced during this recording.");
            return builder.ToString();
        }

        private static PerformanceSample FindPeak(Func<PerformanceSample, long> selector)
        {
            PerformanceSample peak = default;
            long peakValue = long.MinValue;
            foreach (PerformanceSample sample in Samples)
            {
                long value = selector(sample);
                if (value <= peakValue)
                {
                    continue;
                }

                peakValue = value;
                peak = sample;
            }

            return peak;
        }

        private static string BuildSamplesCsv()
        {
            StringBuilder builder = new StringBuilder(Samples.Count * 160);
            builder.AppendLine("elapsed_ms,update_gap_ms,working_set_bytes,private_memory_bytes,managed_bytes,unity_allocated_bytes,mono_used_bytes,mono_heap_bytes,selection,drag");
            foreach (PerformanceSample sample in Samples)
            {
                builder.Append(sample.ElapsedMilliseconds.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
                builder.Append(sample.UpdateGapMilliseconds.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
                builder.Append(sample.WorkingSetBytes).Append(',');
                builder.Append(sample.PrivateMemoryBytes).Append(',');
                builder.Append(sample.ManagedBytes).Append(',');
                builder.Append(sample.UnityAllocatedBytes).Append(',');
                builder.Append(sample.MonoUsedBytes).Append(',');
                builder.Append(sample.MonoHeapBytes).Append(',');
                builder.Append(EscapeCsv(sample.Selection)).Append(',');
                builder.AppendLine(EscapeCsv(sample.Drag));
            }

            return builder.ToString();
        }

        private static string BuildEventsCsv()
        {
            StringBuilder builder = new StringBuilder(Events.Count * 100);
            builder.AppendLine("elapsed_ms,category,detail");
            foreach (PerformanceEvent recordedEvent in Events)
            {
                builder.Append(recordedEvent.ElapsedMilliseconds.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
                builder.Append(EscapeCsv(recordedEvent.Category)).Append(',');
                builder.AppendLine(EscapeCsv(recordedEvent.Detail));
            }

            return builder.ToString();
        }

        private static string BuildPathPreview(params string[][] pathGroups)
        {
            IEnumerable<string> paths = pathGroups.SelectMany(group => group).Where(path => !string.IsNullOrEmpty(path)).Take(8);
            return string.Join(" | ", paths);
        }

        private static string EscapeCsv(string value)
        {
            string safeValue = value ?? string.Empty;
            return '"' + safeValue.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + '"';
        }

        private static string FormatBytes(long bytes)
        {
            const double gigabyte = 1024d * 1024d * 1024d;
            return (bytes / gigabyte).ToString("F2", CultureInfo.InvariantCulture) + " GB";
        }

        private readonly struct PerformanceSample
        {
            internal PerformanceSample(
                double elapsedMilliseconds,
                double updateGapMilliseconds,
                long workingSetBytes,
                long privateMemoryBytes,
                long managedBytes,
                long unityAllocatedBytes,
                long monoUsedBytes,
                long monoHeapBytes,
                string selection,
                string drag)
            {
                ElapsedMilliseconds = elapsedMilliseconds;
                UpdateGapMilliseconds = updateGapMilliseconds;
                WorkingSetBytes = workingSetBytes;
                PrivateMemoryBytes = privateMemoryBytes;
                ManagedBytes = managedBytes;
                UnityAllocatedBytes = unityAllocatedBytes;
                MonoUsedBytes = monoUsedBytes;
                MonoHeapBytes = monoHeapBytes;
                Selection = selection;
                Drag = drag;
            }

            internal double ElapsedMilliseconds { get; }
            internal double UpdateGapMilliseconds { get; }
            internal long WorkingSetBytes { get; }
            internal long PrivateMemoryBytes { get; }
            internal long ManagedBytes { get; }
            internal long UnityAllocatedBytes { get; }
            internal long MonoUsedBytes { get; }
            internal long MonoHeapBytes { get; }
            internal string Selection { get; }
            internal string Drag { get; }
        }

        private readonly struct PerformanceEvent
        {
            internal PerformanceEvent(double elapsedMilliseconds, string category, string detail)
            {
                ElapsedMilliseconds = elapsedMilliseconds;
                Category = category;
                Detail = detail;
            }

            internal double ElapsedMilliseconds { get; }
            internal string Category { get; }
            internal string Detail { get; }
        }
    }

    internal sealed class EditorPerformanceAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            EditorPerformanceRecorder.RecordAssetChanges(
                importedAssets,
                deletedAssets,
                movedAssets,
                movedFromAssetPaths);
        }
    }
}
