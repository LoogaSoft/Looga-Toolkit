using System;
using System.Collections.Generic;
using LoogaSoft.Inspector.Editor;
using LoogaSoft.PrefabBrowser.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.PrefabBrowser.Editor
{
    /// <summary>
    /// Browses the generated prefab index through a virtualized UI Toolkit grid.
    /// </summary>
    public sealed class PrefabBrowser : EditorWindow
    {
        private const float TileHeight = 118f;
        private const float TileMinimumWidth = 100f;
        private const float TileSpacing = 2f;
        private const double PreviewPollInterval = 0.2d;

        private readonly List<PrefabData> _displayedPrefabs = new();
        private readonly List<int> _finishedThumbnailIds = new();
        private readonly List<PrefabData> _filteredPrefabs = new();
        private readonly HashSet<int> _pendingThumbnailIds = new();
        private readonly Dictionary<int, Texture2D> _prefabThumbnails = new();
        private readonly List<RowData> _rows = new();
        private readonly List<PrefabData> _selectedPrefabs = new();

        private PrefabBrowserConfig _browserConfig;
        private PrefabBrowserDatabase _prefabDatabase;
        private VisualElement _categoryBar;
        private int _columnCount = 1;
        private string _currentMainCategory = "All";
        private string _currentSubCategory = "All";
        private bool _includeBroken = true;
        private bool _includePackages;
        private bool _includeUi;
        private int _lastSelectedPrefabIndex = -1;
        private ListView _prefabGrid;
        private string _searchText = string.Empty;
        private ToolbarSearchField _searchField;
        private VisualElement _subcategoryBar;
        private double _nextPreviewPollTime;

        [MenuItem("Window/LoogaSoft/Prefab Browser/Browser Window")]
        public static void ShowWindow()
        {
            GetWindow<PrefabBrowser>("Prefab Browser");
        }

        private void OnEnable()
        {
            LoadProjectData();
            RefreshFilter();
            EditorApplication.projectChanged -= HandleProjectChanged;
            EditorApplication.projectChanged += HandleProjectChanged;
            EditorApplication.update -= PollPendingPreviews;
            EditorApplication.update += PollPendingPreviews;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= HandleProjectChanged;
            EditorApplication.update -= PollPendingPreviews;
        }

        private void OnDestroy()
        {
            _prefabThumbnails.Clear();
            _pendingThumbnailIds.Clear();
            _finishedThumbnailIds.Clear();
            _displayedPrefabs.Clear();
            _filteredPrefabs.Clear();
            _selectedPrefabs.Clear();
        }

        private void OnLostFocus()
        {
            ResetSelection();
        }

        public void CreateGUI()
        {
            LoadProjectData();

            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Column;

            root.Add(CreateCommandToolbar());
            root.Add(CreateFilterToolbar());

            _categoryBar = CreateNavigationBar();
            _categoryBar.style.minHeight = 36f;
            root.Add(_categoryBar);

            _subcategoryBar = CreateNavigationBar();
            root.Add(_subcategoryBar);

            _prefabGrid = new ListView
            {
                itemsSource = _rows,
                fixedItemHeight = TileHeight + TileSpacing,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.None,
                makeItem = () => new PrefabRowElement(this),
                bindItem = (element, index) =>
                    ((PrefabRowElement)element).Bind(_rows[index].StartIndex, _columnCount)
            };
            LoogaUiToolkitStyle.DisableCollectionRowHover(_prefabGrid);
            _prefabGrid.style.flexGrow = 1f;
            _prefabGrid.RegisterCallback<GeometryChangedEvent>(HandleGridGeometryChanged);
            root.Add(_prefabGrid);

            RefreshNavigation();
            RefreshFilter();
        }

        private Toolbar CreateCommandToolbar()
        {
            Toolbar toolbar = new();
            toolbar.Add(CreateIconButton(
                "d_SettingsIcon",
                "Open category configuration",
                PrefabBrowserConfigWindow.ShowWindow));
            toolbar.Add(CreateIconButton(
                "d_Refresh",
                "Rebuild the prefab index",
                () =>
                {
                    PrefabBrowserPostprocessor.RebuildDatabase();
                    LoadProjectData();
                    RefreshFilter();
                }));

            _searchField = new ToolbarSearchField();
            _searchField.style.flexGrow = 1f;
            _searchField.SetValueWithoutNotify(_searchText);
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _searchText = evt.newValue ?? string.Empty;
                UpdateFilterWithSearch();
            });
            toolbar.Add(_searchField);
            return toolbar;
        }

        private Toolbar CreateFilterToolbar()
        {
            Toolbar toolbar = new();
            toolbar.Add(CreateFilterToggle("Include Broken", _includeBroken, value => _includeBroken = value));
            toolbar.Add(CreateFilterToggle("Include Packages", _includePackages, value => _includePackages = value));
            toolbar.Add(CreateFilterToggle("Include UI", _includeUi, value => _includeUi = value));
            return toolbar;
        }

        private ToolbarToggle CreateFilterToggle(string text, bool value, Action<bool> assign)
        {
            ToolbarToggle toggle = new() { text = text };
            toggle.SetValueWithoutNotify(value);
            toggle.RegisterValueChangedCallback(evt =>
            {
                assign(evt.newValue);
                RefreshFilter();
            });
            return toggle;
        }

        private static ToolbarButton CreateIconButton(string iconName, string tooltip, Action action)
        {
            ToolbarButton button = new(action) { tooltip = tooltip };
            button.style.width = 28f;
            UnityEngine.UIElements.Image icon = new()
            {
                image = EditorGUIUtility.IconContent(iconName).image,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            icon.style.width = 16f;
            icon.style.height = 16f;
            button.Add(icon);
            return button;
        }

        private static VisualElement CreateNavigationBar()
        {
            VisualElement bar = new();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.flexWrap = Wrap.Wrap;
            bar.style.paddingLeft = 2f;
            bar.style.paddingRight = 2f;
            bar.style.paddingTop = 2f;
            bar.style.paddingBottom = 2f;
            return bar;
        }

        private void RefreshNavigation()
        {
            if (_categoryBar == null || _subcategoryBar == null)
                return;

            _categoryBar.Clear();
            _categoryBar.Add(CreateCategoryButton("All", true));
            if (_browserConfig != null)
            {
                foreach (BrowserCategory category in _browserConfig.Categories)
                {
                    if (category != null && !string.IsNullOrWhiteSpace(category.Name))
                        _categoryBar.Add(CreateCategoryButton(category.Name, true));
                }
            }

            _subcategoryBar.Clear();
            BrowserCategory activeCategory = _browserConfig?.Categories.Find(
                category => category.Name == _currentMainCategory);
            if (_currentMainCategory == "All" || activeCategory == null)
            {
                _subcategoryBar.style.display = DisplayStyle.None;
                if (_currentMainCategory != "All")
                {
                    _currentMainCategory = "All";
                    _currentSubCategory = "All";
                }

                return;
            }

            _subcategoryBar.style.display = DisplayStyle.Flex;
            _subcategoryBar.Add(CreateCategoryButton("All", false));
            foreach (string subcategory in activeCategory.SubCategories)
            {
                if (!string.IsNullOrWhiteSpace(subcategory))
                    _subcategoryBar.Add(CreateCategoryButton(subcategory, false));
            }
        }

        private Button CreateCategoryButton(string category, bool main)
        {
            Button button = new(() => SelectCategory(category, main)) { text = category };
            button.style.height = main ? 34f : 26f;
            button.style.minWidth = main ? 72f : 56f;
            button.style.marginLeft = 1f;
            button.style.marginRight = 1f;
            button.style.marginTop = 1f;
            button.style.marginBottom = 1f;
            bool selected = main
                ? _currentMainCategory == category
                : _currentSubCategory == category;
            if (selected)
            {
                button.style.backgroundColor = EditorGUIUtility.isProSkin
                    ? new Color(0.24f, 0.45f, 0.68f)
                    : new Color(0.35f, 0.62f, 0.88f);
            }

            return button;
        }

        private void SelectCategory(string category, bool main)
        {
            if (main)
            {
                if (_currentMainCategory == category)
                    return;

                _currentMainCategory = category;
                _currentSubCategory = "All";
            }
            else
            {
                if (_currentSubCategory == category)
                    return;

                _currentSubCategory = category;
            }

            RefreshNavigation();
            RefreshFilter();
        }

        private void LoadProjectData()
        {
            _browserConfig = PrefabBrowserConfig.GetOrCreateConfig();
            _prefabDatabase = PrefabBrowserDatabase.GetOrCreateDatabase();
        }

        private void HandleProjectChanged()
        {
            LoadProjectData();
            RefreshNavigation();
            RefreshFilter();
        }

        private void RefreshFilter()
        {
            _filteredPrefabs.Clear();
            _prefabThumbnails.Clear();
            _pendingThumbnailIds.Clear();

            if (_prefabDatabase == null)
                _prefabDatabase = PrefabBrowserDatabase.GetOrCreateDatabase();

            foreach (PrefabData data in _prefabDatabase.Prefabs)
            {
                if (data == null || string.IsNullOrWhiteSpace(data.Path))
                    continue;

                if (!_includePackages && data.Path.StartsWith("Packages/", StringComparison.Ordinal))
                    continue;

                if (!_includeUi && data.IsUi)
                    continue;

                if (!_includeBroken && data.IsBroken)
                    continue;

                if (_currentMainCategory != "All" && !data.Labels.Contains(_currentMainCategory))
                    continue;

                bool matchesSubcategory = _currentSubCategory == "All"
                    || data.Labels.Exists(label => label.Equals(
                        _currentSubCategory,
                        StringComparison.OrdinalIgnoreCase));
                if (matchesSubcategory)
                    _filteredPrefabs.Add(data);
            }

            UpdateFilterWithSearch();
        }

        private void UpdateFilterWithSearch()
        {
            _displayedPrefabs.Clear();
            if (string.IsNullOrWhiteSpace(_searchText))
            {
                _displayedPrefabs.AddRange(_filteredPrefabs);
            }
            else
            {
                foreach (PrefabData prefab in _filteredPrefabs)
                {
                    int fileNameIndex = prefab.Path.LastIndexOf('/') + 1;
                    if (prefab.Path.IndexOf(
                            _searchText,
                            fileNameIndex,
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _displayedPrefabs.Add(prefab);
                    }
                }
            }

            _displayedPrefabs.Sort((left, right) => string.CompareOrdinal(left.Path, right.Path));
            ResetSelection();
            RebuildRows();
        }

        private void HandleGridGeometryChanged(GeometryChangedEvent evt)
        {
            float availableWidth = Mathf.Max(TileMinimumWidth, evt.newRect.width - 14f);
            int columnCount = Mathf.Max(
                1,
                Mathf.FloorToInt((availableWidth + TileSpacing) / (TileMinimumWidth + TileSpacing)));
            if (columnCount == _columnCount)
                return;

            _columnCount = columnCount;
            RebuildRows();
        }

        private void RebuildRows()
        {
            _rows.Clear();
            for (int index = 0; index < _displayedPrefabs.Count; index += _columnCount)
                _rows.Add(new RowData(index));

            if (_prefabGrid == null)
                return;

            _prefabGrid.itemsSource = _rows;
            _prefabGrid.Rebuild();
        }

        private Texture2D GetThumbnail(GameObject prefab)
        {
            if (prefab == null)
                return null;

            int id = prefab.GetInstanceID();
            if (_prefabThumbnails.TryGetValue(id, out Texture2D cached) && cached != null)
                return cached;

            Texture2D thumbnail = AssetPreview.GetAssetPreview(prefab);
            if (thumbnail != null)
            {
                _prefabThumbnails[id] = thumbnail;
                _pendingThumbnailIds.Remove(id);
                return thumbnail;
            }

            if (AssetPreview.IsLoadingAssetPreview(id))
                _pendingThumbnailIds.Add(id);

            thumbnail = AssetPreview.GetMiniThumbnail(prefab);
            if (thumbnail != null && !_pendingThumbnailIds.Contains(id))
                _prefabThumbnails[id] = thumbnail;
            return thumbnail;
        }

        private void PollPendingPreviews()
        {
            if (_pendingThumbnailIds.Count == 0
                || EditorApplication.timeSinceStartup < _nextPreviewPollTime)
            {
                return;
            }

            _nextPreviewPollTime = EditorApplication.timeSinceStartup + PreviewPollInterval;
            _finishedThumbnailIds.Clear();
            foreach (int id in _pendingThumbnailIds)
            {
                if (!AssetPreview.IsLoadingAssetPreview(id))
                    _finishedThumbnailIds.Add(id);
            }

            foreach (int id in _finishedThumbnailIds)
                _pendingThumbnailIds.Remove(id);

            _prefabGrid?.RefreshItems();
        }

        private void HandleSelection(PrefabData data, int currentIndex, bool actionKey, bool shift)
        {
            if (actionKey)
            {
                if (!_selectedPrefabs.Remove(data))
                    _selectedPrefabs.Add(data);
                _lastSelectedPrefabIndex = currentIndex;
            }
            else if (shift && _lastSelectedPrefabIndex >= 0)
            {
                _selectedPrefabs.Clear();
                int start = Mathf.Min(_lastSelectedPrefabIndex, currentIndex);
                int end = Mathf.Max(_lastSelectedPrefabIndex, currentIndex);
                for (int index = start; index <= end; index++)
                    _selectedPrefabs.Add(_displayedPrefabs[index]);
            }
            else
            {
                _selectedPrefabs.Clear();
                _selectedPrefabs.Add(data);
                _lastSelectedPrefabIndex = currentIndex;
            }

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(data.Path);
            _prefabGrid?.RefreshItems();
        }

        private void ResetSelection()
        {
            if (_selectedPrefabs.Count == 0 && _lastSelectedPrefabIndex < 0)
                return;

            _selectedPrefabs.Clear();
            _lastSelectedPrefabIndex = -1;
            _prefabGrid?.RefreshItems();
        }

        private void StartDrag(PrefabData data)
        {
            if (!_selectedPrefabs.Contains(data))
            {
                _selectedPrefabs.Clear();
                _selectedPrefabs.Add(data);
            }

            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = PrefabBrowserQueryUtility.GetPrefabObjects(_selectedPrefabs);
            DragAndDrop.StartDrag($"{_selectedPrefabs.Count} Prefab(s)");
        }

        private sealed class PrefabRowElement : VisualElement
        {
            private readonly PrefabBrowser _owner;
            private readonly List<PrefabTileElement> _tiles = new();

            public PrefabRowElement(PrefabBrowser owner)
            {
                _owner = owner;
                style.flexDirection = FlexDirection.Row;
                style.height = TileHeight;
                style.paddingLeft = 2f;
                style.paddingRight = 2f;
            }

            public void Bind(int startIndex, int columnCount)
            {
                EnsureTileCount(columnCount);
                for (int column = 0; column < _tiles.Count; column++)
                {
                    int dataIndex = startIndex + column;
                    if (column < columnCount && dataIndex < _owner._displayedPrefabs.Count)
                        _tiles[column].Bind(_owner._displayedPrefabs[dataIndex], dataIndex);
                    else
                        _tiles[column].Unbind();
                }
            }

            private void EnsureTileCount(int count)
            {
                while (_tiles.Count < count)
                {
                    PrefabTileElement tile = new(_owner);
                    _tiles.Add(tile);
                    Add(tile);
                }

                while (_tiles.Count > count)
                {
                    int last = _tiles.Count - 1;
                    _tiles[last].RemoveFromHierarchy();
                    _tiles.RemoveAt(last);
                }
            }
        }

        private sealed class PrefabTileElement : VisualElement
        {
            private readonly UnityEngine.UIElements.Image _image;
            private readonly Label _name;
            private readonly PrefabBrowser _owner;
            private PrefabData _data;
            private int _index;
            private Vector2 _pointerDownPosition;
            private bool _pointerPressed;

            public PrefabTileElement(PrefabBrowser owner)
            {
                _owner = owner;
                style.flexBasis = 0f;
                style.flexGrow = 1f;
                style.height = TileHeight;
                style.marginLeft = 1f;
                style.marginRight = 1f;
                style.paddingLeft = 4f;
                style.paddingRight = 4f;
                style.paddingTop = 4f;
                style.paddingBottom = 2f;
                style.borderBottomWidth = 1f;
                style.borderLeftWidth = 1f;
                style.borderRightWidth = 1f;
                style.borderTopWidth = 1f;

                _image = new UnityEngine.UIElements.Image
                {
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore
                };
                _image.style.flexGrow = 1f;
                Add(_image);

                _name = new Label
                {
                    pickingMode = PickingMode.Ignore
                };
                _name.style.height = 18f;
                _name.style.unityTextAlign = TextAnchor.MiddleCenter;
                _name.style.whiteSpace = WhiteSpace.NoWrap;
                _name.style.overflow = Overflow.Hidden;
                _name.style.textOverflow = TextOverflow.Ellipsis;
                Add(_name);

                RegisterCallback<PointerEnterEvent>(_ => SetHover(true));
                RegisterCallback<PointerLeaveEvent>(_ => SetHover(false));
                RegisterCallback<PointerDownEvent>(HandlePointerDown);
                RegisterCallback<PointerMoveEvent>(HandlePointerMove);
                RegisterCallback<PointerUpEvent>(HandlePointerUp);
                RegisterCallback<PointerCaptureOutEvent>(_ => _pointerPressed = false);
            }

            public void Bind(PrefabData data, int index)
            {
                _data = data;
                _index = index;
                style.display = DisplayStyle.Flex;
                tooltip = data.Path;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(data.Path);
                _image.image = _owner.GetThumbnail(prefab);
                _name.text = prefab != null ? prefab.name : data.Path;
                RefreshSelection();
            }

            public void Unbind()
            {
                _data = null;
                _image.image = null;
                _name.text = string.Empty;
                style.display = DisplayStyle.None;
            }

            private void HandlePointerDown(PointerDownEvent evt)
            {
                if (_data == null)
                    return;

                if (evt.button == 1)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_data.Path);
                    GenericMenu menu = new();
                    menu.AddItem(new GUIContent("Open Prefab"), false, () => AssetDatabase.OpenAsset(prefab));
                    menu.AddItem(new GUIContent("Copy Prefab Path"), false,
                        () => EditorGUIUtility.systemCopyBuffer = _data.Path);
                    menu.ShowAsContext();
                    evt.StopPropagation();
                    return;
                }

                if (evt.button != 0)
                    return;

                _pointerPressed = true;
                _pointerDownPosition = evt.position;
                _owner.HandleSelection(_data, _index, evt.actionKey, evt.shiftKey);
                this.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            }

            private void HandlePointerMove(PointerMoveEvent evt)
            {
                if (!_pointerPressed || _data == null)
                    return;

                if (Vector2.Distance(_pointerDownPosition, evt.position) < 5f)
                    return;

                _pointerPressed = false;
                if (this.HasPointerCapture(evt.pointerId))
                    this.ReleasePointer(evt.pointerId);
                _owner.StartDrag(_data);
                evt.StopPropagation();
            }

            private void HandlePointerUp(PointerUpEvent evt)
            {
                _pointerPressed = false;
                if (this.HasPointerCapture(evt.pointerId))
                    this.ReleasePointer(evt.pointerId);
            }

            private void RefreshSelection()
            {
                bool selected = _data != null && _owner._selectedPrefabs.Contains(_data);
                Color border = selected
                    ? new Color(0.24f, 0.62f, 0.94f)
                    : EditorGUIUtility.isProSkin
                        ? new Color(0.19f, 0.19f, 0.19f)
                        : new Color(0.62f, 0.62f, 0.62f);
                style.borderBottomColor = border;
                style.borderLeftColor = border;
                style.borderRightColor = border;
                style.borderTopColor = border;
                style.backgroundColor = selected
                    ? new Color(0.18f, 0.36f, 0.54f, 0.55f)
                    : Color.clear;
            }

            private void SetHover(bool hovering)
            {
                if (_data == null || _owner._selectedPrefabs.Contains(_data))
                    return;

                style.backgroundColor = hovering
                    ? new Color(0.5f, 0.5f, 0.5f, 0.16f)
                    : Color.clear;
            }
        }

        private readonly struct RowData
        {
            public RowData(int startIndex)
            {
                StartIndex = startIndex;
            }

            public int StartIndex { get; }
        }
    }
}
