using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    [Serializable]
    internal sealed class ProjectFolderPresentation
    {
        [SerializeField]
        private string _guid;

        [SerializeField]
        private bool _hasColor;

        [SerializeField]
        private Color _color;

        [SerializeField]
        private string _iconName;

        internal string Guid => _guid;

        internal bool HasColor => _hasColor;

        internal Color Color => _color;

        internal bool HasIcon => !string.IsNullOrEmpty(_iconName);

        internal string IconName => _iconName;

        internal ProjectFolderPresentation(string guid)
        {
            _guid = guid;
            _color = HierarchyPresentationStore.DefaultLabelColor;
        }

        internal void SetColor(Color color)
        {
            _hasColor = true;
            _color = color;
        }

        internal void ClearColor()
        {
            _hasColor = false;
        }

        internal void SetIcon(string iconName)
        {
            _iconName = iconName;
        }

        internal void ClearIcon()
        {
            _iconName = string.Empty;
        }

        internal bool IsEmpty()
        {
            return !_hasColor && !HasIcon;
        }
    }

    [FilePath(SettingsPath, FilePathAttribute.Location.ProjectFolder)]
    internal sealed class ProjectFolderPresentationStore :
        ScriptableSingleton<ProjectFolderPresentationStore>
    {
        private const string SettingsPath =
            "ProjectSettings/LoogaProjectFolderPresentation.asset";

        [SerializeField]
        private List<ProjectFolderPresentation> _entries = new();

        private readonly Dictionary<string, ProjectFolderPresentation> _lookup = new();

        [NonSerialized]
        private bool _lookupDirty = true;

        internal bool TryGet(string guid, out ProjectFolderPresentation presentation)
        {
            if (string.IsNullOrEmpty(guid))
            {
                presentation = null;
                return false;
            }

            EnsureLookup();
            return _lookup.TryGetValue(guid, out presentation);
        }

        internal void SetColor(string guid, Color color)
        {
            GetOrCreate(guid).SetColor(color);
            SaveStore();
        }

        internal void ClearColor(string guid)
        {
            if (!TryGet(guid, out ProjectFolderPresentation presentation))
            {
                return;
            }

            presentation.ClearColor();
            RemoveIfEmpty(presentation);
            SaveStore();
        }

        internal void SetIcon(string guid, string iconName)
        {
            GetOrCreate(guid).SetIcon(iconName);
            SaveStore();
        }

        internal void ClearIcon(string guid)
        {
            if (!TryGet(guid, out ProjectFolderPresentation presentation))
            {
                return;
            }

            presentation.ClearIcon();
            RemoveIfEmpty(presentation);
            SaveStore();
        }

        private void OnEnable()
        {
            _lookup.Clear();
            _lookupDirty = true;
        }

        private ProjectFolderPresentation GetOrCreate(string guid)
        {
            if (TryGet(guid, out ProjectFolderPresentation presentation))
            {
                return presentation;
            }

            presentation = new ProjectFolderPresentation(guid);
            _entries.Add(presentation);
            _lookup[guid] = presentation;
            return presentation;
        }

        private void RemoveIfEmpty(ProjectFolderPresentation presentation)
        {
            if (presentation.IsEmpty())
            {
                _entries.Remove(presentation);
                _lookup.Remove(presentation.Guid);
            }
        }

        private void EnsureLookup()
        {
            if (!_lookupDirty)
            {
                return;
            }

            _lookup.Clear();
            for (int index = _entries.Count - 1; index >= 0; index--)
            {
                ProjectFolderPresentation presentation = _entries[index];
                if (presentation == null ||
                    string.IsNullOrEmpty(presentation.Guid) ||
                    _lookup.ContainsKey(presentation.Guid))
                {
                    _entries.RemoveAt(index);
                    continue;
                }

                _lookup[presentation.Guid] = presentation;
            }

            _lookupDirty = false;
        }

        private void SaveStore()
        {
            Save(true);
            EditorApplication.RepaintProjectWindow();
        }
    }
}
