using System;
using System.Collections.Generic;
using System.Linq;
using LoogaSoft.PrefabBrowser.Runtime;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.PrefabBrowser.Editor
{
    public class PrefabBrowser : EditorWindow
    {
        private PrefabBrowserConfig _browserConfig;

        private string _currentMainCategory = "All";
        private string _currentSubCategory = "All";

        private bool _includeBroken = true;
        private bool _includePackages = false;
        private bool _includeUI = false;

        private readonly List<PrefabData> _filteredPrefabs = new();
        private List<PrefabData> _displayedPrefabs = new();
        private readonly Dictionary<int, Texture2D> _prefabThumbnails = new();
        private readonly List<PrefabData> _selectedPrefabs = new();
        private int _lastSelectedPrefabIndex = -1;

        private PrefabBrowserDatabase _prefabDatabase;

        private GUIStyle _noMarginLabelStyle;
        private GUIStyle _mainCategoryButtonStyle;
        private GUIStyle _subCategoryButtonStyle;

        private string _searchText = "";
        
        
        private Vector2 _scrollPos;
        private bool _mouseDown;
        
        private const float Spacing = 8f;
        private const float TileSize = 100f;

        [MenuItem("Window/LoogaSoft/Prefab Browser/Browser Window")]
        public static void ShowWindow()
        {
            GetWindow<PrefabBrowser>("Prefab Browser");
        }

        private void OnEnable()
        {
            _browserConfig = PrefabBrowserConfig.GetOrCreateConfig();
            _prefabDatabase = PrefabBrowserDatabase.GetOrCreateDatabase();
            
            RefreshFilter();
            AssetPreview.SetPreviewTextureCacheSize(Mathf.Max(100, _filteredPrefabs.Count * 2));
            wantsMouseMove = true;
        }
        private void OnDestroy()
        {
            // Flush the thumbnail cache so we don't leak texture memory when the window closes
            _prefabThumbnails.Clear();
            _displayedPrefabs.Clear();
            _filteredPrefabs.Clear();
            _selectedPrefabs.Clear();
        }

        private void OnLostFocus()
        {
            ResetSelection();
        }

        private void OnGUI()
        {
            if (_browserConfig == null)
                _browserConfig = PrefabBrowserConfig.GetOrCreateConfig();
            
            Event e = Event.current;
            if (e.type == EventType.MouseUp)
            {
                _mouseDown = false;
                Repaint();
            }

            DrawSearchBar();
            DrawSettingsToggles();
            DrawNavigation(position.width - 12f);
            DrawPrefabScrollView();

            if (e.type == EventType.MouseDown && e.button == 0 && _selectedPrefabs.Count > 0)
                ResetSelection();
        }

        private void DrawSearchBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            GUIContent configContent = EditorGUIUtility.IconContent("d_SettingsIcon");
            configContent.tooltip = "Open Category Config";
            if (GUILayout.Button(configContent, EditorStyles.toolbarButton, GUILayout.Width(28f)))
            {
                PrefabBrowserConfigWindow.ShowWindow();
            }
            
            GUIContent refreshContent = EditorGUIUtility.IconContent("d_Refresh"); // Uses Unity's built-in refresh icon
            refreshContent.tooltip = "Rebuild Database";
            if (GUILayout.Button(refreshContent, EditorStyles.toolbarButton, GUILayout.Width(28f)))
            {
                PrefabBrowserPostprocessor.RebuildDatabase();
                RefreshFilter(); // Force the browser to immediately read the newly rebuilt database
            }
            
            string newSearchText = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField);
            if (newSearchText != _searchText)
            {
                _searchText = newSearchText;
                UpdateFilterWithSearch();
            }

            if (!string.IsNullOrEmpty(_searchText))
            {
                GUIStyle cancelButtonStyle = GUI.skin.FindStyle("ToolbarSeachCancelButton");
                
                if (cancelButtonStyle == null)
                    cancelButtonStyle = GUI.skin.FindStyle("ToolbarSearchCancelButton");
                if (cancelButtonStyle == null)
                    cancelButtonStyle = EditorStyles.miniButton;

                if (GUILayout.Button("", cancelButtonStyle))
                {
                    _searchText = "";
                    UpdateFilterWithSearch();
                    GUI.FocusControl(null);
                    GUIUtility.keyboardControl = 0;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSettingsToggles()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            EditorGUI.BeginChangeCheck();
            
            _includeBroken = GUILayout.Toggle(_includeBroken, "Include Broken", EditorStyles.toolbarButton);
            _includePackages = GUILayout.Toggle(_includePackages, "Include Packages", EditorStyles.toolbarButton);
            _includeUI = GUILayout.Toggle(_includeUI, "Include UI", EditorStyles.toolbarButton);
            
            if (EditorGUI.EndChangeCheck())
                RefreshFilter();
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawNavigation(float windowWidth)
        {
            EditorGUILayout.Space(2f);
            
            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal();
            
            float currentWidth = 0f;
            
            // THE FIX: Changed base style from EditorStyles.miniButton to "Button"
            if (_mainCategoryButtonStyle == null)
            {
                _mainCategoryButtonStyle = new GUIStyle("Button")
                {
                    fixedHeight = 40f,
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(20, 20, 0, 0)
                };
            }

            bool isAllSelected = _currentMainCategory == "All";
            if (DrawNavButton("All", _mainCategoryButtonStyle, ref currentWidth, windowWidth, isAllSelected))
            {
                if (!isAllSelected)
                {
                    _currentMainCategory = "All";
                    _currentSubCategory = "All";
                    RefreshFilter();
                }
            }
            
            foreach (BrowserCategory category in _browserConfig.Categories)
            {
                bool isSelected = _currentMainCategory == category.Name;
                if (DrawNavButton(category.Name, _mainCategoryButtonStyle, ref currentWidth, windowWidth, isSelected))
                {
                    if (!isSelected)
                    {
                        _currentMainCategory = category.Name;
                        _currentSubCategory = "All";
                        RefreshFilter();
                    }
                }
            }
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            BrowserCategory activeCategory = _browserConfig.Categories.Find(category => category.Name == _currentMainCategory);
            
            if (_currentMainCategory != "All" && activeCategory != null)
            {
                DrawDividerLine();
                EditorGUILayout.BeginHorizontal();
                
                currentWidth = 0f;
                
                // THE FIX: Changed base style from EditorStyles.miniButton to "Button"
                if (_subCategoryButtonStyle == null)
                {
                    _subCategoryButtonStyle = new GUIStyle("miniButton")
                    {
                        fixedHeight = 30f,
                        padding = new RectOffset(10, 10, 0, 0),
                        alignment = TextAnchor.MiddleCenter
                    };
                }

                List<string> subCategoryOptions = new List<string> { "All" };
                subCategoryOptions.AddRange(activeCategory.SubCategories);

                foreach (var subCategory in subCategoryOptions)
                {
                    bool isSubSelected = _currentSubCategory == subCategory;   
                    if (DrawNavButton(subCategory, _subCategoryButtonStyle, ref currentWidth, windowWidth, isSubSelected))
                    {
                        if (!isSubSelected)
                        {
                            _currentSubCategory = subCategory;
                            RefreshFilter();
                        }
                    }
                }
                
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            else if (_currentMainCategory != "All" && activeCategory == null)
            {
                _currentMainCategory = "All";
                _currentSubCategory = "All";
                RefreshFilter();
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(2f);
        }

        private bool DrawNavButton(string label, GUIStyle style, ref float currentWidth, float windowWidth, bool isSelected)
        {
            GUIContent content = new GUIContent(label);
            float buttonWidth = style.CalcSize(content).x + 10f;

            if (currentWidth + buttonWidth > windowWidth)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                currentWidth = 0;
            }
            
            currentWidth += buttonWidth;
            return GUILayout.Toggle(isSelected, label, style, GUILayout.ExpandWidth(true));
        }
        private void DrawDividerLine()
        {
            GUILayout.Space(2f);
            Rect rect = EditorGUILayout.GetControlRect(false, 1f);
            rect.width -= 2f;
            EditorGUI.DrawRect(rect, Color.gray);
            GUILayout.Space(2f);
        }

        private void RefreshFilter()
        {
            _filteredPrefabs.Clear();
            _prefabThumbnails.Clear();

            if (_prefabDatabase == null)
                _prefabDatabase = PrefabBrowserDatabase.GetOrCreateDatabase();

            foreach (PrefabData data in _prefabDatabase.Prefabs)
            {
                // 1. Packages Check
                if (!_includePackages && data.Path.StartsWith("Packages/", StringComparison.Ordinal))
                    continue;

                // 2. UI Check
                if (!_includeUI && data.IsUi)
                    continue;

                // 3. Broken Check
                if (!_includeBroken && data.IsBroken)
                    continue;

                // 4. Main Category Check
                if (!string.IsNullOrEmpty(_currentMainCategory) && _currentMainCategory != "All")
                {
                    if (!data.Labels.Contains(_currentMainCategory))
                        continue;
                }

                // 5. SubCategory Check
                bool matchesSubCategory = _currentSubCategory == "All" || 
                                          data.Labels.Exists(label => label.Equals(_currentSubCategory, StringComparison.OrdinalIgnoreCase));
        
                if (matchesSubCategory)
                    _filteredPrefabs.Add(data);
            }
    
            UpdateFilterWithSearch();
        }

        private void UpdateFilterWithSearch()
        {
            if (string.IsNullOrEmpty(_searchText))
            {
                _displayedPrefabs.Clear();
                _displayedPrefabs.AddRange(_filteredPrefabs);
            }
            else
            {
                _displayedPrefabs.Clear();
                for (int index = 0; index < _filteredPrefabs.Count; index++)
                {
                    PrefabData prefab = _filteredPrefabs[index];
                    int fileNameIndex = prefab.Path.LastIndexOf('/') + 1;
                    if (prefab.Path.IndexOf(_searchText, fileNameIndex, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _displayedPrefabs.Add(prefab);
                    }
                }
            }

            _displayedPrefabs.Sort((left, right) => string.CompareOrdinal(left.Path, right.Path));
        }

        private void DrawPrefabScrollView()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, false, true, GUIStyle.none, GUI.skin.verticalScrollbar, GUI.skin.scrollView);

            // 15f for the scrollbar + 2f for our hardcoded left margin = 16f
            float usableWidth = EditorGUIUtility.currentViewWidth - 16f; 
    
            float gap = 2f; 

            int columnCount = Mathf.Max(1, Mathf.FloorToInt((usableWidth + gap) / (TileSize + gap)));
            float totalGapSpace = (columnCount - 1) * gap;
            float dynamicTileWidth = Mathf.Floor((usableWidth - totalGapSpace) / columnCount);

            EditorGUILayout.BeginVertical(GUIStyle.none);

            for (int i = 0; i < _displayedPrefabs.Count; i += columnCount)
            {
                EditorGUILayout.BeginHorizontal(GUIStyle.none);
        
                // THE FIX: Explicitly shove the entire row to the right by 2 pixels. 
                // IMGUI cannot ignore this!
                GUILayout.Space(2f); 
        
                for (int j = 0; j < columnCount; j++)
                {
                    int index = i + j;
                    if (index < _displayedPrefabs.Count)
                    {
                        DrawPrefab(_displayedPrefabs[index], dynamicTileWidth);
                    }
                    else
                    {
                        GUILayout.Space(dynamicTileWidth);
                    }

                    if (j < columnCount - 1)
                        GUILayout.Space(gap);
                }
        
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
        
                EditorGUILayout.Space(gap); 
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawPrefab(PrefabData data, float size)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(data.Path);
            if (prefab == null) return;
            
            int id = prefab.GetInstanceID();
            float extraHeight = 16f;
            
            Rect prefabRect = EditorGUILayout.BeginVertical(GUIStyle.none, GUILayout.Width(size), GUILayout.Height(size + extraHeight));

            GUIStyle boxStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fixedHeight = 0f,
                stretchHeight = true
            };

            Event e = Event.current;
            bool hovering = prefabRect.Contains(e.mousePosition);
            bool selected = _selectedPrefabs.Contains(data);
            
            if (hovering)
            {
                if (e.type == EventType.MouseMove) Repaint();
                if (e.type == EventType.MouseDown)
                {
                    _mouseDown = true;
                    HandleSelection(data, _displayedPrefabs.IndexOf(data));
                }
            }

            if (e.type == EventType.Repaint)
                boxStyle.Draw(prefabRect, hovering, _mouseDown && hovering, selected, false);

            var thumbnail = LoadPrefabThumbnail(prefab, id);
            Rect thumbnailRect = GUILayoutUtility.GetRect(size, size);

            float inset = 4f;
            thumbnailRect.x += inset;
            thumbnailRect.y += inset;
            thumbnailRect.width -= inset * 2f;
            thumbnailRect.height -= inset * 2f;
            
            GUI.DrawTexture(thumbnailRect, thumbnail, ScaleMode.ScaleToFit, true);

            string displayName = prefab.name.Length > 24 ? prefab.name.Substring(0, 21) + "..." : prefab.name;

            // Cache the stripped style to avoid OnGUI memory allocations
            if (_noMarginLabelStyle == null)
            {
                _noMarginLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    margin = new RectOffset(0, 0, 0, 0),
                    padding = new RectOffset(0, 0, 0, 0),
                    alignment = TextAnchor.UpperCenter // Keeps the text looking tidy without borders
                };
            }

            // Use the stripped style here!
            GUILayout.Label(displayName, _noMarginLabelStyle, GUILayout.Width(size), GUILayout.Height(extraHeight - 2f));
            
            // ... Context Menu & Drag logic remains exactly the same ...
            if (prefabRect.Contains(e.mousePosition))
            {
                if (e.type == EventType.ContextClick)
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Open Prefab"), false, () => AssetDatabase.OpenAsset(prefab));
                    menu.AddItem(new GUIContent("Copy Prefab"), false, () => EditorGUIUtility.systemCopyBuffer = AssetDatabase.GetAssetPath(prefab));
                    menu.ShowAsContext();
                    e.Use();
                }
                else if (e.type == EventType.MouseDown)
                {
                    Selection.activeObject = prefab;
                    e.Use();
                }
                else if (e.type == EventType.MouseDrag)
                {
                    _mouseDown = false;
                    if (!_selectedPrefabs.Contains(data)) // Pass data
                    {
                        _selectedPrefabs.Clear();
                        _selectedPrefabs.Add(data); // Pass data
                    }
                    DragAndDrop.PrepareStartDrag();
            
                    // Convert our selected lightweight data back into real GameObjects for Unity's drag system
                    DragAndDrop.objectReferences = _selectedPrefabs
                        .Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p.Path))
                        .ToArray();
                
                    DragAndDrop.StartDrag($"{_selectedPrefabs.Count} Prefabs");
                    e.Use();
                }
            }
            
            EditorGUILayout.EndVertical();
        }

        private void ResetSelection()
        {
            _selectedPrefabs.Clear();
            _lastSelectedPrefabIndex = -1;
            Repaint();
        }

        private void HandleSelection(PrefabData data, int currentIndex)
        {
            Event e = Event.current;

            if (e.control || e.command)
            {
                if (_selectedPrefabs.Contains(data))
                    _selectedPrefabs.Remove(data);
                else
                    _selectedPrefabs.Add(data);
                
                _lastSelectedPrefabIndex = currentIndex;
            }
            else if (e.shift && _lastSelectedPrefabIndex != -1)
            {
                _selectedPrefabs.Clear();
                int startIndex = Mathf.Min(_lastSelectedPrefabIndex, currentIndex);
                int endIndex = Mathf.Max(_lastSelectedPrefabIndex, currentIndex);
                
                _selectedPrefabs.Clear();
                _selectedPrefabs.AddRange(_displayedPrefabs.GetRange(startIndex, endIndex - startIndex + 1));
            }
            else
            {
                _selectedPrefabs.Clear();
                _selectedPrefabs.Add(data);
                _lastSelectedPrefabIndex = currentIndex;
            }
        }
        private Texture2D LoadPrefabThumbnail(GameObject prefab, int id)
        {
            if (!_prefabThumbnails.TryGetValue(id, out Texture2D thumbnail) || thumbnail == null)
            {
                thumbnail = AssetPreview.GetAssetPreview(prefab);

                if (thumbnail != null)
                {
                    _prefabThumbnails[id] = thumbnail;
                }
                else
                {
                    thumbnail = AssetPreview.GetMiniThumbnail(prefab);

                    if (AssetPreview.IsLoadingAssetPreview(id))
                    {
                        if (Time.realtimeSinceStartup % 0.5f < 0.25f)
                            Repaint();
                    }
                }
            }

            return thumbnail;
        }
    }
}
