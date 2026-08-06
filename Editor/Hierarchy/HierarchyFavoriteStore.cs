using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    [Serializable]
    internal sealed class HierarchyFavorite
    {
        [SerializeField]
        private string _objectId;

        [SerializeField]
        private string _displayName;

        [SerializeField]
        private string _scenePath;

        internal string ObjectId => _objectId;

        internal string DisplayName => _displayName;

        internal string ScenePath => _scenePath;

        internal HierarchyFavorite(GameObject gameObject)
        {
            _objectId = HierarchyObjectId.Get(gameObject);
            _displayName = gameObject.name;
            _scenePath = gameObject.scene.path;
        }
    }

    /// <summary>
    /// Stores personal navigation shortcuts outside version control and scene data.
    /// </summary>
    [FilePath(SettingsPath, FilePathAttribute.Location.ProjectFolder)]
    internal sealed class HierarchyFavoriteStore : ScriptableSingleton<HierarchyFavoriteStore>
    {
        private const string SettingsPath = "UserSettings/LoogaHierarchyFavorites.asset";

        [SerializeField]
        private List<HierarchyFavorite> _entries = new();

        private readonly HashSet<string> _lookup = new();

        [NonSerialized]
        private bool _lookupDirty = true;

        internal static event Action Changed;

        internal IReadOnlyList<HierarchyFavorite> Entries => _entries;

        [InitializeOnLoadMethod]
        private static void ResetRuntimeStateAfterReload()
        {
            instance.ResetRuntimeState();
        }

        private void OnEnable()
        {
            ResetRuntimeState();
        }

        internal bool Contains(GameObject gameObject)
        {
            EnsureLookup();
            return _lookup.Contains(HierarchyObjectId.Get(gameObject));
        }

        internal void Toggle(GameObject gameObject)
        {
            string objectId = HierarchyObjectId.Get(gameObject);
            int index = FindIndex(objectId);

            if (index >= 0)
            {
                _entries.RemoveAt(index);
            }
            else
            {
                _entries.Add(new HierarchyFavorite(gameObject));
            }

            SaveStore();
        }

        internal void Remove(string objectId)
        {
            int index = FindIndex(objectId);
            if (index < 0)
            {
                return;
            }

            _entries.RemoveAt(index);
            SaveStore();
        }

        private int FindIndex(string objectId)
        {
            for (int index = 0; index < _entries.Count; index++)
            {
                if (_entries[index].ObjectId == objectId)
                {
                    return index;
                }
            }

            return -1;
        }

        private void SaveStore()
        {
            Save(true);
            _lookupDirty = true;
            EditorApplication.RepaintHierarchyWindow();
            Changed?.Invoke();
        }

        private void EnsureLookup()
        {
            if (!_lookupDirty)
            {
                return;
            }

            _lookup.Clear();
            for (int index = 0; index < _entries.Count; index++)
            {
                _lookup.Add(_entries[index].ObjectId);
            }

            _lookupDirty = false;
        }

        private void ResetRuntimeState()
        {
            _lookup.Clear();
            _lookupDirty = true;
        }
    }
}
