using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace LoogaSoft.PrefabBrowser.Editor
{
    /// <summary>
    /// Applies or removes project labels through a retained, searchable label list.
    /// </summary>
    public sealed class AssetLabeler : EditorWindow
    {
        private readonly List<string> _filteredLabels = new();
        private readonly List<string> _labels = new();

        private Button _applyButton;
        private Label _labelSummary;
        private ListView _labelList;
        private Button _removeButton;
        private string _label = string.Empty;
        private string _searchPath = "Assets";

        [MenuItem("Window/LoogaSoft/Prefab Browser/Asset Labeler")]
        public static void ShowWindow()
        {
            GetWindow<AssetLabeler>("Asset Labeler");
        }

        private void OnEnable()
        {
            EditorApplication.projectChanged -= RefreshLabels;
            EditorApplication.projectChanged += RefreshLabels;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= RefreshLabels;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            Label title = new("Asset Labeler");
            title.style.fontSize = 14f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 6f;
            root.Add(title);

            VisualElement pathRow = new();
            pathRow.style.flexDirection = FlexDirection.Row;
            pathRow.style.marginBottom = 5f;

            TextField pathField = new("Search Path") { name = "search-path", value = _searchPath };
            pathField.style.flexGrow = 1f;
            pathField.RegisterValueChangedCallback(evt => _searchPath = evt.newValue);
            pathRow.Add(pathField);

            Button browseButton = new(BrowseFolder) { text = "...", tooltip = "Select a folder under Assets." };
            browseButton.style.width = 30f;
            browseButton.style.marginLeft = 4f;
            pathRow.Add(browseButton);
            root.Add(pathRow);

            ToolbarSearchField searchField = new();
            searchField.tooltip = "Filter project labels.";
            searchField.style.marginBottom = 4f;
            searchField.RegisterValueChangedCallback(evt => FilterLabels(evt.newValue));
            root.Add(searchField);

            _labelList = new ListView
            {
                itemsSource = _filteredLabels,
                fixedItemHeight = 22f,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.Single,
                makeItem = () => new Label { style = { unityTextAlign = TextAnchor.MiddleLeft } },
                bindItem = (element, index) => ((Label)element).text = _filteredLabels[index]
            };
            _labelList.style.flexGrow = 1f;
            _labelList.style.minHeight = 120f;
            _labelList.selectionChanged += HandleSelectionChanged;
            root.Add(_labelList);

            _labelSummary = new Label("No label selected");
            _labelSummary.style.marginTop = 5f;
            _labelSummary.style.marginBottom = 5f;
            _labelSummary.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(_labelSummary);

            VisualElement commandRow = new();
            commandRow.style.flexDirection = FlexDirection.Row;

            _applyButton = new Button(() => ApplyLabels(false)) { text = "Apply Label" };
            _applyButton.style.flexGrow = 1f;
            _applyButton.style.height = 32f;
            commandRow.Add(_applyButton);

            _removeButton = new Button(() => ApplyLabels(true)) { text = "Remove Label" };
            _removeButton.style.flexGrow = 1f;
            _removeButton.style.height = 32f;
            _removeButton.style.marginLeft = 4f;
            commandRow.Add(_removeButton);
            root.Add(commandRow);

            RefreshLabels();
            RefreshCommandState();
        }

        private void RefreshLabels()
        {
            _labels.Clear();
            HashSet<string> discoveredLabels = new(StringComparer.OrdinalIgnoreCase);
            foreach (string assetPath in AssetDatabase.GetAllAssetPaths())
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (asset == null)
                    continue;

                foreach (string label in AssetDatabase.GetLabels(asset))
                    discoveredLabels.Add(label);
            }

            _labels.AddRange(discoveredLabels);
            _labels.Sort(StringComparer.OrdinalIgnoreCase);
            FilterLabels(string.Empty);
        }

        private void FilterLabels(string query)
        {
            _filteredLabels.Clear();
            foreach (string label in _labels)
            {
                if (string.IsNullOrWhiteSpace(query)
                    || label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _filteredLabels.Add(label);
                }
            }

            _labelList?.Rebuild();
        }

        private void HandleSelectionChanged(IEnumerable<object> selection)
        {
            _label = string.Empty;
            foreach (object selected in selection)
            {
                _label = selected as string ?? string.Empty;
                break;
            }

            _labelSummary.text = string.IsNullOrEmpty(_label)
                ? "No label selected"
                : $"Selected: {_label}";
            RefreshCommandState();
        }

        private void RefreshCommandState()
        {
            bool canApply = !string.IsNullOrWhiteSpace(_label);
            _applyButton?.SetEnabled(canApply);
            _removeButton?.SetEnabled(canApply);
        }

        private void BrowseFolder()
        {
            string path = EditorUtility.OpenFolderPanel("Select Folder", Application.dataPath, string.Empty);
            if (string.IsNullOrEmpty(path))
                return;

            if (!path.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Invalid Folder", "Select a folder inside Assets.", "OK");
                return;
            }

            _searchPath = "Assets" + path.Substring(Application.dataPath.Length).Replace('\\', '/');
            rootVisualElement.Q<TextField>("search-path")?.SetValueWithoutNotify(_searchPath);
        }

        private void ApplyLabels(bool removeLabels)
        {
            if (string.IsNullOrWhiteSpace(_label))
                return;

            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { _searchPath });
            int changedCount = 0;

            try
            {
                AssetDatabase.StartAssetEditing();
                for (int index = 0; index < guids.Length; index++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                    Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                    if (asset != null && TryUpdateLabel(asset, removeLabels))
                        changedCount++;

                    if (index % 50 == 0)
                    {
                        string action = removeLabels ? "Removing" : "Applying";
                        float progress = guids.Length == 0 ? 1f : (index + 1f) / guids.Length;
                        EditorUtility.DisplayProgressBar(
                            $"{action} Labels",
                            $"Processed {index + 1} of {guids.Length} assets",
                            progress);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            string completedAction = removeLabels ? "Removed" : "Applied";
            Debug.Log($"{completedAction} '{_label}' on {changedCount} asset(s).");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private bool TryUpdateLabel(Object asset, bool removeLabel)
        {
            string[] currentLabels = AssetDatabase.GetLabels(asset);
            bool containsLabel = Array.IndexOf(currentLabels, _label) >= 0;
            if (containsLabel == !removeLabel)
                return false;

            List<string> updatedLabels = new(currentLabels);
            if (removeLabel)
                updatedLabels.Remove(_label);
            else
                updatedLabels.Add(_label);

            AssetDatabase.SetLabels(asset, updatedLabels.ToArray());
            return true;
        }
    }
}
