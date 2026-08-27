using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LoogaSoft.Hierarchy.Editor
{
    [Serializable]
    internal sealed class HierarchyPresentation
    {
        [SerializeField]
        private string _objectId;

        [SerializeField]
        private string _objectLocator;

        [SerializeField]
        private bool _hasLabelColor;

        [SerializeField]
        private Color _labelColor;

        [SerializeField]
        private string _iconName;

        // Keep these fields until all pre-0.7.0 folder records are migrated.
        [SerializeField, HideInInspector]
        private bool _isSection;

        [SerializeField, HideInInspector]
        private bool _hasOriginalHideFlags;

        [SerializeField, HideInInspector]
        private HideFlags _originalGameObjectHideFlags;

        [SerializeField, HideInInspector]
        private HideFlags _originalTransformHideFlags;

        internal string ObjectId => _objectId;

        internal string ObjectLocator => _objectLocator;

        internal bool HasLabelColor => _hasLabelColor;

        internal Color LabelColor => _labelColor;

        internal bool HasIcon => !string.IsNullOrEmpty(_iconName);

        internal string IconName => _iconName;

        internal HierarchyPresentation(GameObject gameObject)
        {
            _objectId = HierarchyObjectId.Get(gameObject);
            _objectLocator = HierarchyObjectId.GetLocator(gameObject);
            _labelColor = HierarchyPresentationStore.DefaultLabelColor;
        }

        internal bool RefreshIdentity(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            string objectId = HierarchyObjectId.Get(gameObject);
            string objectLocator = HierarchyObjectId.GetLocator(gameObject);
            if (_objectId == objectId && _objectLocator == objectLocator)
            {
                return false;
            }

            _objectId = objectId;
            _objectLocator = objectLocator;
            return true;
        }

        internal void SetLabelColor(Color color)
        {
            _hasLabelColor = true;
            _labelColor = color;
        }

        internal void ClearLabelColor()
        {
            _hasLabelColor = false;
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
            return !_hasLabelColor && !HasIcon && !_isSection && !_hasOriginalHideFlags;
        }

        internal bool MigrateLegacyFolder(GameObject gameObject)
        {
            if ((!_isSection && !_hasOriginalHideFlags) || gameObject == null)
            {
                return false;
            }

            const HideFlags gameObjectMask = HideFlags.NotEditable;
            const HideFlags transformMask = HideFlags.NotEditable | HideFlags.HideInInspector;

            if (_hasOriginalHideFlags)
            {
                gameObject.hideFlags =
                    (gameObject.hideFlags & ~gameObjectMask) |
                    (_originalGameObjectHideFlags & gameObjectMask);

                gameObject.transform.hideFlags =
                    (gameObject.transform.hideFlags & ~transformMask) |
                    (_originalTransformHideFlags & transformMask);
            }
            else
            {
                gameObject.hideFlags &= ~gameObjectMask;
                gameObject.transform.hideFlags &= ~transformMask;
            }

            _isSection = false;
            _hasOriginalHideFlags = false;
            EditorUtility.SetDirty(gameObject);
            EditorUtility.SetDirty(gameObject.transform);
            return true;
        }
    }

    /// <summary>
    /// Stores project-shared row color metadata without modifying scene components.
    /// </summary>
    [FilePath(SettingsPath, FilePathAttribute.Location.ProjectFolder)]
    internal sealed class HierarchyPresentationStore : ScriptableSingleton<HierarchyPresentationStore>
    {
        private const string SettingsPath = "ProjectSettings/LoogaHierarchyPresentation.asset";

        internal static readonly Color DefaultLabelColor = new(0.22f, 0.52f, 0.82f, 1f);

        [SerializeField]
        private List<HierarchyPresentation> _entries = new();

        private readonly Dictionary<string, HierarchyPresentation> _lookup = new();
        private readonly Dictionary<string, HierarchyPresentation> _locatorLookup = new();

        [NonSerialized]
        private bool _lookupDirty = true;

        [NonSerialized]
        private bool _saveScheduled;

        internal bool TryGet(GameObject gameObject, out HierarchyPresentation presentation)
        {
            if (gameObject == null)
            {
                presentation = null;
                return false;
            }

            EnsureLookup();

            bool found = _lookup.TryGetValue(HierarchyObjectId.Get(gameObject), out presentation) ||
                _locatorLookup.TryGetValue(HierarchyObjectId.GetLocator(gameObject), out presentation);

            if (found && presentation.RefreshIdentity(gameObject))
            {
                _lookupDirty = true;
                ScheduleSave();
            }

            return found;
        }

        internal void SetLabelColor(GameObject gameObject, Color color)
        {
            GetOrCreate(gameObject).SetLabelColor(color);
            SaveStore();
        }

        internal void ClearLabelColor(GameObject gameObject)
        {
            if (!TryGet(gameObject, out HierarchyPresentation presentation))
            {
                return;
            }

            presentation.ClearLabelColor();
            RemoveIfEmpty(presentation);
            SaveStore();
        }

        internal void SetIcon(GameObject gameObject, string iconName)
        {
            GetOrCreate(gameObject).SetIcon(iconName);
            SaveStore();
        }

        internal void ClearIcon(GameObject gameObject)
        {
            if (!TryGet(gameObject, out HierarchyPresentation presentation))
            {
                return;
            }

            presentation.ClearIcon();
            RemoveIfEmpty(presentation);
            SaveStore();
        }

        [InitializeOnLoadMethod]
        private static void ScheduleLegacyFolderMigration()
        {
            instance.ResetRuntimeState();
            EditorSceneManager.sceneOpened -= HandleSceneOpened;
            EditorSceneManager.sceneOpened += HandleSceneOpened;
            EditorApplication.delayCall -= instance.MigrateLegacyFolders;
            EditorApplication.delayCall += instance.MigrateLegacyFolders;
        }

        private static void HandleSceneOpened(Scene scene, OpenSceneMode mode)
        {
            EditorApplication.delayCall -= instance.MigrateLegacyFolders;
            EditorApplication.delayCall += instance.MigrateLegacyFolders;
        }

        private void OnEnable()
        {
            ResetRuntimeState();
        }

        private void MigrateLegacyFolders()
        {
            bool metadataChanged = false;

            for (int index = _entries.Count - 1; index >= 0; index--)
            {
                HierarchyPresentation presentation = _entries[index];
                GameObject gameObject = HierarchyObjectId.Resolve(presentation.ObjectId);
                gameObject ??= HierarchyObjectId.ResolveLocator(presentation.ObjectLocator);

                metadataChanged |= presentation.RefreshIdentity(gameObject);
                metadataChanged |= presentation.MigrateLegacyFolder(gameObject);

                if (presentation.IsEmpty())
                {
                    _entries.RemoveAt(index);
                    metadataChanged = true;
                }
            }

            if (metadataChanged)
            {
                SaveStore();
            }
        }

        private HierarchyPresentation GetOrCreate(GameObject gameObject)
        {
            if (TryGet(gameObject, out HierarchyPresentation presentation))
            {
                return presentation;
            }

            presentation = new HierarchyPresentation(gameObject);
            _entries.Add(presentation);
            _lookup[presentation.ObjectId] = presentation;
            _locatorLookup[presentation.ObjectLocator] = presentation;
            return presentation;
        }

        private void RemoveIfEmpty(HierarchyPresentation presentation)
        {
            if (presentation.IsEmpty())
            {
                _entries.Remove(presentation);
            }
        }

        private void EnsureLookup()
        {
            if (!_lookupDirty)
            {
                return;
            }

            _lookup.Clear();
            _locatorLookup.Clear();
            bool removedDuplicate = false;

            // Iterate backward so the newest serialized record wins.
            for (int index = _entries.Count - 1; index >= 0; index--)
            {
                HierarchyPresentation presentation = _entries[index];
                if (presentation == null || IsDuplicate(presentation))
                {
                    _entries.RemoveAt(index);
                    removedDuplicate = true;
                    continue;
                }

                if (!string.IsNullOrEmpty(presentation.ObjectId))
                {
                    _lookup[presentation.ObjectId] = presentation;
                }

                if (!string.IsNullOrEmpty(presentation.ObjectLocator))
                {
                    _locatorLookup[presentation.ObjectLocator] = presentation;
                }
            }

            _lookupDirty = false;
            if (removedDuplicate)
            {
                ScheduleSave();
            }
        }

        private bool IsDuplicate(HierarchyPresentation presentation)
        {
            return !string.IsNullOrEmpty(presentation.ObjectId) && _lookup.ContainsKey(presentation.ObjectId) ||
                   !string.IsNullOrEmpty(presentation.ObjectLocator) && _locatorLookup.ContainsKey(presentation.ObjectLocator);
        }

        private void ResetRuntimeState()
        {
            _lookup.Clear();
            _locatorLookup.Clear();
            _lookupDirty = true;
            _saveScheduled = false;
        }

        private void SaveStore()
        {
            _lookupDirty = true;
            Save(true);
            EditorApplication.RepaintHierarchyWindow();
        }

        private void ScheduleSave()
        {
            if (_saveScheduled)
            {
                return;
            }

            _saveScheduled = true;
            EditorApplication.delayCall += SaveScheduledChanges;
        }

        private void SaveScheduledChanges()
        {
            _saveScheduled = false;
            SaveStore();
        }
    }
}
