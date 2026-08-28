using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LoogaSoft.Navigation.Editor
{
    internal sealed class LinearNavigationHistory<T>
    {
        private readonly List<T> _entries = new();
        private readonly Func<T, T, bool> _equals;
        private readonly Func<T, bool> _isValid;
        private readonly int _capacity;
        private int _cursor = -1;

        public LinearNavigationHistory(
            int capacity,
            Func<T, T, bool> equals,
            Func<T, bool> isValid)
        {
            _capacity = Mathf.Max(2, capacity);
            _equals = equals;
            _isValid = isValid;
        }

        public IReadOnlyList<T> Entries => _entries;
        public int Cursor => _cursor;
        public bool CanMoveBack => _cursor > 0;
        public bool CanMoveForward => _cursor >= 0 && _cursor < _entries.Count - 1;

        public bool Observe(T entry)
        {
            PruneInvalidEntries();
            if (!_isValid(entry))
                return false;

            if (_cursor >= 0 && _equals(_entries[_cursor], entry))
                return false;

            if (_cursor < _entries.Count - 1)
                _entries.RemoveRange(_cursor + 1, _entries.Count - _cursor - 1);

            _entries.Add(entry);
            _cursor = _entries.Count - 1;

            if (_entries.Count > _capacity)
            {
                int overflow = _entries.Count - _capacity;
                _entries.RemoveRange(0, overflow);
                _cursor -= overflow;
            }

            return true;
        }

        public bool TryGetBack(out int index, out T entry)
        {
            PruneInvalidEntries();
            index = _cursor - 1;
            return TryGet(index, out entry);
        }

        public bool TryGetForward(out int index, out T entry)
        {
            PruneInvalidEntries();
            index = _cursor + 1;
            return TryGet(index, out entry);
        }

        public bool TryGet(int index, out T entry)
        {
            PruneInvalidEntries();
            if (index < 0 || index >= _entries.Count)
            {
                entry = default;
                return false;
            }

            entry = _entries[index];
            return true;
        }

        public void SetCursor(int index)
        {
            _cursor = Mathf.Clamp(index, 0, _entries.Count - 1);
        }

        public void PruneInvalidEntries()
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (_isValid(_entries[i]))
                    continue;

                _entries.RemoveAt(i);
                if (i < _cursor)
                    _cursor--;
                else if (i == _cursor && _cursor >= _entries.Count)
                    _cursor = _entries.Count - 1;
            }

            if (_entries.Count == 0)
                _cursor = -1;
        }
    }

    internal sealed class InspectorSelectionState
    {
        public InspectorSelectionState(Object[] objects)
        {
            Objects = objects ?? Array.Empty<Object>();
        }

        public Object[] Objects { get; }

        public Object PrimaryObject
        {
            get
            {
                for (int i = 0; i < Objects.Length; i++)
                {
                    if (Objects[i] != null)
                        return Objects[i];
                }

                return null;
            }
        }

        public bool IsValid => PrimaryObject != null;

        public Object[] ValidObjects()
        {
            int validCount = 0;
            for (int i = 0; i < Objects.Length; i++)
            {
                if (Objects[i] != null)
                    validCount++;
            }

            if (validCount == Objects.Length)
                return Objects;

            Object[] validObjects = new Object[validCount];
            int targetIndex = 0;
            for (int i = 0; i < Objects.Length; i++)
            {
                if (Objects[i] != null)
                    validObjects[targetIndex++] = Objects[i];
            }

            return validObjects;
        }

        public bool Matches(InspectorSelectionState other)
        {
            if (other == null || Objects.Length != other.Objects.Length)
                return false;

            for (int i = 0; i < Objects.Length; i++)
            {
                bool found = false;
                for (int j = 0; j < other.Objects.Length; j++)
                {
                    if (Objects[i] != other.Objects[j])
                        continue;

                    found = true;
                    break;
                }

                if (!found)
                    return false;
            }

            return true;
        }
    }

    internal static class InspectorSelectionHistory
    {
        private const int Capacity = 40;

        private static readonly LinearNavigationHistory<InspectorSelectionState> History =
            new(Capacity, (left, right) => left.Matches(right), state => state?.IsValid == true);

        private static InspectorSelectionState _pendingNavigation;

        public static event Action Changed;

        public static IReadOnlyList<InspectorSelectionState> Entries => History.Entries;
        public static int Cursor => History.Cursor;
        public static bool CanMoveBack => History.CanMoveBack;
        public static bool CanMoveForward => History.CanMoveForward;

        public static void Initialize()
        {
            History.Observe(CurrentSelection());
        }

        public static void ObserveCurrentSelection()
        {
            InspectorSelectionState current = CurrentSelection();
            if (_pendingNavigation != null)
            {
                if (_pendingNavigation.Matches(current))
                {
                    _pendingNavigation = null;
                    return;
                }

                _pendingNavigation = null;
            }

            if (History.Observe(current))
                Changed?.Invoke();
        }

        public static void MoveBack()
        {
            if (History.TryGetBack(out int index, out InspectorSelectionState state))
                NavigateTo(index, state);
        }

        public static void MoveForward()
        {
            if (History.TryGetForward(out int index, out InspectorSelectionState state))
                NavigateTo(index, state);
        }

        public static void NavigateTo(int index)
        {
            if (History.TryGet(index, out InspectorSelectionState state))
                NavigateTo(index, state);
        }

        private static InspectorSelectionState CurrentSelection()
        {
            Object activeObject = Selection.activeObject;
            Object[] selectedObjects = Selection.objects;
            if (activeObject == null || selectedObjects.Length <= 1 || selectedObjects[0] == activeObject)
                return new InspectorSelectionState(selectedObjects);

            Object[] orderedObjects = new Object[selectedObjects.Length];
            orderedObjects[0] = activeObject;
            int targetIndex = 1;
            for (int i = 0; i < selectedObjects.Length; i++)
            {
                if (selectedObjects[i] != activeObject)
                    orderedObjects[targetIndex++] = selectedObjects[i];
            }

            return new InspectorSelectionState(orderedObjects);
        }

        private static void NavigateTo(int index, InspectorSelectionState state)
        {
            Object[] objects = state.ValidObjects();
            if (objects.Length == 0)
                return;

            History.SetCursor(index);
            _pendingNavigation = state;
            Selection.objects = objects;
            Changed?.Invoke();

            EditorApplication.delayCall += ClearPendingNavigation;
        }

        private static void ClearPendingNavigation()
        {
            _pendingNavigation = null;
        }
    }

    internal sealed class ProjectFolderHistory
    {
        private const int Capacity = 40;
        private const double PendingNavigationTimeout = 1d;

        private readonly LinearNavigationHistory<string> _history =
            new(Capacity, string.Equals, AssetDatabase.IsValidFolder);
        private string _pendingPath;
        private double _pendingUntil;

        public event Action Changed;

        public IReadOnlyList<string> Entries => _history.Entries;
        public int Cursor => _history.Cursor;
        public bool CanMoveBack => _history.CanMoveBack;
        public bool CanMoveForward => _history.CanMoveForward;

        public void Observe(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            if (!string.IsNullOrEmpty(_pendingPath))
            {
                if (string.Equals(path, _pendingPath, StringComparison.Ordinal))
                {
                    _pendingPath = null;
                    Changed?.Invoke();
                    return;
                }

                if (EditorApplication.timeSinceStartup < _pendingUntil)
                    return;

                _pendingPath = null;
            }

            if (_history.Observe(path))
                Changed?.Invoke();
        }

        public void MoveBack(Func<string, bool> openFolder)
        {
            if (_history.TryGetBack(out int index, out string path))
                NavigateTo(index, path, openFolder);
        }

        public void MoveForward(Func<string, bool> openFolder)
        {
            if (_history.TryGetForward(out int index, out string path))
                NavigateTo(index, path, openFolder);
        }

        public void NavigateTo(int index, Func<string, bool> openFolder)
        {
            if (_history.TryGet(index, out string path))
                NavigateTo(index, path, openFolder);
        }

        private void NavigateTo(int index, string path, Func<string, bool> openFolder)
        {
            if (!openFolder(path))
                return;

            _history.SetCursor(index);
            _pendingPath = path;
            _pendingUntil = EditorApplication.timeSinceStartup + PendingNavigationTimeout;
            Changed?.Invoke();
        }
    }
}
