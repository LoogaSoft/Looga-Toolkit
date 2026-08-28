using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace LoogaSoft.Navigation.Editor
{
    /// <summary>Adds a searchable window picker after the tabs in each Unity dock area.</summary>
    [InitializeOnLoad]
    internal static class EditorWindowTabMenu
    {
        private const double DockScanInterval = 1d;
        private const double LayoutRefreshInterval = 0.1d;

        private static readonly Dictionary<Object, DockTabButton> Buttons = new();
        private static double _nextDockScan;
        private static double _nextLayoutRefresh;

        static EditorWindowTabMenu()
        {
            EditorApplication.delayCall += Initialize;
        }

        private static void Initialize()
        {
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            ScanDockAreas();
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            double now = EditorApplication.timeSinceStartup;
            if (now >= _nextDockScan)
            {
                _nextDockScan = now + DockScanInterval;
                ScanDockAreas();
            }

            if (now < _nextLayoutRefresh)
                return;

            _nextLayoutRefresh = now + LayoutRefreshInterval;
            foreach (DockTabButton button in Buttons.Values)
                button.RefreshLayout();
        }

        private static void ScanDockAreas()
        {
            if (!DockAreaBridge.IsAvailable)
                return;

            Object[] dockAreas = Resources.FindObjectsOfTypeAll(DockAreaBridge.DockAreaType);
            HashSet<Object> found = new();
            for (int i = 0; i < dockAreas.Length; i++)
            {
                Object dockArea = dockAreas[i];
                if (dockArea == null || !found.Add(dockArea))
                    continue;

                if (!Buttons.TryGetValue(dockArea, out DockTabButton button))
                {
                    button = new DockTabButton(dockArea);
                    Buttons.Add(dockArea, button);
                }

                button.Attach();
            }

            List<Object> removed = new();
            foreach (KeyValuePair<Object, DockTabButton> pair in Buttons)
            {
                if (pair.Key == null || !found.Contains(pair.Key))
                    removed.Add(pair.Key);
            }

            for (int i = 0; i < removed.Count; i++)
            {
                Object dockArea = removed[i];
                if (Buttons.TryGetValue(dockArea, out DockTabButton button))
                    button.Dispose();

                Buttons.Remove(dockArea);
            }
        }

        private static void Dispose()
        {
            EditorApplication.update -= Update;
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;

            foreach (DockTabButton button in Buttons.Values)
                button.Dispose();

            Buttons.Clear();
        }
    }

    internal sealed class DockTabButton : IDisposable
    {
        private const string ButtonName = "Looga Add Editor Window Tab";
        private const float ButtonSize = 22f;
        private const float TabGap = 1f;
        private const float PlusHalfSize = 5f;
        private const float PlusLineWidth = 2.25f;

        private readonly Object _dockArea;
        private readonly ToolbarButton _button;
        private bool _isHovered;
        private bool _isPressed;

        public DockTabButton(Object dockArea)
        {
            _dockArea = dockArea;
            _button = new ToolbarButton(OpenWindowMenu)
            {
                name = ButtonName,
                text = string.Empty,
                tooltip = "Add Window"
            };
            _button.generateVisualContent += DrawPlus;
            _button.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            _button.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            _button.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _button.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _button.style.position = Position.Absolute;
            _button.style.width = ButtonSize;
            _button.style.minWidth = ButtonSize;
            _button.style.maxWidth = ButtonSize;
            _button.style.height = ButtonSize;
            _button.style.minHeight = ButtonSize;
            _button.style.maxHeight = ButtonSize;
            _button.style.marginLeft = 0f;
            _button.style.marginRight = 0f;
            _button.style.marginTop = 0f;
            _button.style.marginBottom = 0f;
            _button.style.paddingLeft = 0f;
            _button.style.paddingRight = 0f;
            _button.style.paddingTop = 0f;
            _button.style.paddingBottom = 0f;
            _button.style.backgroundColor = Color.clear;
            _button.style.backgroundImage = StyleKeyword.None;
            _button.style.borderLeftWidth = 0f;
            _button.style.borderRightWidth = 0f;
            _button.style.borderTopWidth = 0f;
            _button.style.borderBottomWidth = 0f;
        }

        public void Attach()
        {
            VisualElement root = DockAreaBridge.GetVisualTree(_dockArea);
            if (root == null)
                return;

            VisualElement duplicate = root.Q<VisualElement>(ButtonName);
            if (duplicate != null && duplicate != _button)
                duplicate.RemoveFromHierarchy();

            if (_button.parent != root)
                root.Add(_button);

            RefreshLayout();
        }

        public void RefreshLayout()
        {
            if (_dockArea == null || _button.parent == null)
                return;

            if (!DockAreaBridge.TryGetTabLayout(
                    _dockArea,
                    out Rect tabArea,
                    out float totalTabWidth,
                    out float scrollOffset))
            {
                _button.style.display = DisplayStyle.None;
                return;
            }

            float maximumLeft = tabArea.xMax - ButtonSize;
            float tabRight = tabArea.xMin + totalTabWidth - scrollOffset + TabGap;
            float left = Mathf.Clamp(tabRight, tabArea.xMin, maximumLeft);
            float top = tabArea.yMin + Mathf.Max(0f, (tabArea.height - ButtonSize) * 0.5f);

            _button.style.left = Mathf.Round(left);
            _button.style.top = Mathf.Round(top);
            _button.style.display = DisplayStyle.Flex;
            _button.BringToFront();
        }

        public void Dispose()
        {
            _button.RemoveFromHierarchy();
        }

        private void OpenWindowMenu()
        {
            List<EditorWindowEntry> entries = EditorWindowCatalog.GetEntries(_dockArea);
            if (entries.Count == 0)
                return;

            EditorWindowDropdown dropdown = new(
                new AdvancedDropdownState(),
                entries,
                AddWindow);
            dropdown.Show(GetDropdownAnchor());
        }

        private Rect GetDropdownAnchor()
        {
            Rect worldBounds = _button.worldBound;
            return new Rect(worldBounds.position, worldBounds.size);
        }

        private void DrawPlus(MeshGenerationContext context)
        {
            Rect bounds = _button.contentRect;
            Vector2 center = bounds.center;
            Color baseColor = EditorGUIUtility.isProSkin
                ? new Color(0.72f, 0.72f, 0.72f)
                : new Color(0.28f, 0.28f, 0.28f);

            if (_isPressed)
            {
                baseColor *= 0.75f;
            }
            else if (_isHovered)
            {
                baseColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;
            }

            Painter2D painter = context.painter2D;
            painter.strokeColor = baseColor;
            painter.lineWidth = PlusLineWidth;
            painter.BeginPath();
            painter.MoveTo(new Vector2(center.x - PlusHalfSize, center.y));
            painter.LineTo(new Vector2(center.x + PlusHalfSize, center.y));
            painter.MoveTo(new Vector2(center.x, center.y - PlusHalfSize));
            painter.LineTo(new Vector2(center.x, center.y + PlusHalfSize));
            painter.Stroke();
        }

        #region Pointer
        private void OnPointerEnter(PointerEnterEvent current)
        {
            _isHovered = true;
            _button.MarkDirtyRepaint();
        }

        private void OnPointerLeave(PointerLeaveEvent current)
        {
            _isHovered = false;
            _isPressed = false;
            _button.MarkDirtyRepaint();
        }

        private void OnPointerDown(PointerDownEvent current)
        {
            _isPressed = true;
            _button.MarkDirtyRepaint();
        }

        private void OnPointerUp(PointerUpEvent current)
        {
            _isPressed = false;
            _button.MarkDirtyRepaint();
        }
        #endregion

        private void AddWindow(Type windowType)
        {
            if (windowType == null)
                return;

            EditorWindow window = ScriptableObject.CreateInstance(windowType) as EditorWindow;
            if (window == null)
                return;

            if (DockAreaBridge.AddTab(_dockArea, window))
                return;

            Object.DestroyImmediate(window);
        }
    }

    internal sealed class EditorWindowDropdown : AdvancedDropdown
    {
        private readonly IReadOnlyList<EditorWindowEntry> _entries;
        private readonly Action<Type> _selected;

        public EditorWindowDropdown(
            AdvancedDropdownState state,
            IReadOnlyList<EditorWindowEntry> entries,
            Action<Type> selected)
            : base(state)
        {
            _entries = entries;
            _selected = selected;
            minimumSize = new Vector2(270f, 320f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            AdvancedDropdownItem root = new("Add Window");
            Dictionary<string, AdvancedDropdownItem> categories = new(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _entries.Count; i++)
            {
                EditorWindowEntry entry = _entries[i];
                if (!categories.TryGetValue(entry.Category, out AdvancedDropdownItem category))
                {
                    category = new AdvancedDropdownItem(entry.Category);
                    categories.Add(entry.Category, category);
                    root.AddChild(category);
                }

                EditorWindowDropdownItem item = new(entry.Name, entry.WindowType)
                {
                    icon = entry.Icon
                };
                category.AddChild(item);
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is EditorWindowDropdownItem windowItem)
                _selected?.Invoke(windowItem.WindowType);
        }
    }

    internal sealed class EditorWindowDropdownItem : AdvancedDropdownItem
    {
        public EditorWindowDropdownItem(string name, Type windowType)
            : base(name)
        {
            WindowType = windowType;
        }

        public Type WindowType { get; }
    }

    internal readonly struct EditorWindowEntry
    {
        public EditorWindowEntry(string category, string name, Type windowType, Texture2D icon)
        {
            Category = category;
            Name = name;
            WindowType = windowType;
            Icon = icon;
        }

        public string Category { get; }
        public string Name { get; }
        public Type WindowType { get; }
        public Texture2D Icon { get; }
    }

    internal readonly struct EditorWindowRegistration
    {
        public EditorWindowRegistration(Type windowType, string category)
        {
            WindowType = windowType;
            Category = category;
        }

        public Type WindowType { get; }
        public string Category { get; }
    }

    internal static class EditorWindowCatalog
    {
        private const string UnityCategory = "Unity";
        private const string UnityAdvancedCategory = "Unity Advanced";
        private const string TextMeshProCategory = "TextMesh Pro";
        private const string LoogaSoftCategory = "LoogaSoft";
        private const string KuberaCategory = "Kubera";
        private const string ProjectCategory = "Project";
        private const string OtherPackagesCategory = "Other Packages";

        private static readonly HashSet<string> AdditionalBuiltInWindowTypes = new()
        {
            "UnityEditor.AudioMixerWindow",
            "UnityEditor.Build.Profile.BuildProfileWindow",
            "UnityEditor.BuildPlayerWindow",
            "UnityEditor.ConsoleWindow",
            "UnityEditor.PackageManager.UI.PackageManagerWindow",
            "UnityEditor.PreferenceSettingsWindow",
            "UnityEditor.ProjectSettingsWindow",
            "UnityEditor.ShortcutManagement.ShortcutManagerWindow"
        };

        private static readonly HashSet<string> MainUnityWindowNames = new()
        {
            "AnimationWindow",
            "AnimatorControllerTool",
            "AudioMixerWindow",
            "ConsoleWindow",
            "GameView",
            "HierarchyWindow",
            "InspectorWindow",
            "LightingExplorerWindow",
            "LightingWindow",
            "PackageManagerWindow",
            "PreferenceSettingsWindow",
            "ProfilerWindow",
            "ProjectBrowser",
            "ProjectSettingsWindow",
            "SceneHierarchyWindow",
            "SceneView"
        };

        private static readonly HashSet<string> FunctionalMenuGroups = new(StringComparer.OrdinalIgnoreCase)
        {
            "2D",
            "Accessibility",
            "AI",
            "Analysis",
            "Animation",
            "Assets",
            "Audio",
            "Build",
            "CONTEXT",
            "GameObject",
            "General",
            "Help",
            "Navigation",
            "Package Management",
            "Rendering",
            "Search",
            "Sequencing",
            "Settings",
            "Text",
            "Tools",
            "UI Toolkit",
            "Window"
        };

        private static readonly HashSet<string> NativeOnlyWindowTypes = new()
        {
            "Unity.Hierarchy.Editor.HierarchyWindow",
            "UnityEditor.SceneHierarchyWindow"
        };

        private static readonly string[] TransientWindowNameParts =
        {
            "Blackboard",
            "BlockEditor",
            "ColumnEditor",
            "Dropdown",
            "Minimap",
            "OverlayPreset",
            "Picker",
            "Popup",
            "Preview",
            "Selector",
            "Tooltip"
        };

        private static readonly Type WindowTitleAttributeType =
            typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.EditorWindowTitleAttribute");
        private static readonly MethodInfo WindowTitleMethod = typeof(EditorWindow).GetMethod(
            "GetLocalizedTitleContentFromType",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        private static IReadOnlyList<EditorWindowRegistration> _discoverableWindows;

        public static List<EditorWindowEntry> GetEntries(Object dockArea)
        {
            HashSet<Type> nativeWindowTypes = new();
            DockAreaBridge.AddPaneTypes(dockArea, nativeWindowTypes);

            Dictionary<Type, string> windowCategories = new();
            foreach (Type windowType in nativeWindowTypes)
            {
                windowCategories.Add(windowType, GetDefaultCategory(windowType));
            }

            IReadOnlyList<EditorWindowRegistration> discoverableWindows = GetDiscoverableWindows();
            for (int i = 0; i < discoverableWindows.Count; i++)
            {
                EditorWindowRegistration registration = discoverableWindows[i];
                windowCategories[registration.WindowType] = registration.Category;
            }

            List<EditorWindowEntry> entries = new(windowCategories.Count);
            foreach (KeyValuePair<Type, string> pair in windowCategories)
            {
                GUIContent title = GetWindowTitle(pair.Key);
                entries.Add(new EditorWindowEntry(
                    pair.Value,
                    title.text,
                    pair.Key,
                    title.image as Texture2D));
            }

            entries.Sort(CompareWindowEntries);
            return entries;
        }

        private static IReadOnlyList<EditorWindowRegistration> GetDiscoverableWindows()
        {
            if (_discoverableWindows != null)
                return _discoverableWindows;

            Dictionary<Type, string> menuWindowCategories = GetMenuWindowCategories();
            List<EditorWindowRegistration> windows = new();
            foreach (Type windowType in TypeCache.GetTypesDerivedFrom<EditorWindow>())
            {
                if (!IsUsableWindowType(windowType))
                {
                    continue;
                }

                bool hasWindowMenu = menuWindowCategories.TryGetValue(
                    windowType,
                    out string menuCategory);
                bool isAdditionalBuiltIn = AdditionalBuiltInWindowTypes.Contains(windowType.FullName);
                bool hasWindowTitle = HasWindowTitle(windowType) &&
                                      !NativeOnlyWindowTypes.Contains(windowType.FullName) &&
                                      !LooksTransient(windowType);
                if (hasWindowMenu || isAdditionalBuiltIn || hasWindowTitle)
                {
                    string category = hasWindowMenu
                        ? menuCategory
                        : GetDefaultCategory(windowType);
                    windows.Add(new EditorWindowRegistration(windowType, category));
                }
            }

            _discoverableWindows = windows;
            return _discoverableWindows;
        }

        private static Dictionary<Type, string> GetMenuWindowCategories()
        {
            Dictionary<Type, MenuCategoryCandidate> candidates = new();
            foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<MenuItem>())
            {
                Type windowType = method.DeclaringType;
                if (!IsUsableWindowType(windowType))
                {
                    continue;
                }

                foreach (MenuItem menuItem in method.GetCustomAttributes<MenuItem>(false))
                {
                    if (!menuItem.validate &&
                        !string.IsNullOrEmpty(menuItem.menuItem))
                    {
                        MenuCategoryCandidate candidate = GetCreatorCategory(
                            windowType,
                            menuItem.menuItem);
                        if (!candidates.TryGetValue(windowType, out MenuCategoryCandidate current) ||
                            candidate.Priority > current.Priority ||
                            candidate.Priority == current.Priority &&
                            string.Compare(
                                candidate.Category,
                                current.Category,
                                StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            candidates[windowType] = candidate;
                        }
                    }
                }
            }

            Dictionary<Type, string> categories = new(candidates.Count);
            foreach (KeyValuePair<Type, MenuCategoryCandidate> pair in candidates)
            {
                categories.Add(pair.Key, pair.Value.Category);
            }

            return categories;
        }

        #region Categories
        private static MenuCategoryCandidate GetCreatorCategory(Type windowType, string menuPath)
        {
            string menuCreator = GetMenuCreator(menuPath);
            if (string.Equals(menuCreator, KuberaCategory, StringComparison.OrdinalIgnoreCase))
                return new MenuCategoryCandidate(KuberaCategory, 5);

            string knownCategory = GetKnownCreatorCategory(windowType);
            if (!string.IsNullOrEmpty(knownCategory))
                return new MenuCategoryCandidate(knownCategory, 4);

            if (!string.IsNullOrEmpty(menuCreator))
                return new MenuCategoryCandidate(menuCreator, 3);

            return new MenuCategoryCandidate(GetAssemblyCreator(windowType), 2);
        }

        private static string GetDefaultCategory(Type windowType)
        {
            string knownCategory = GetKnownCreatorCategory(windowType);
            return !string.IsNullOrEmpty(knownCategory)
                ? knownCategory
                : GetAssemblyCreator(windowType);
        }

        private static string GetKnownCreatorCategory(Type windowType)
        {
            string fullName = windowType.FullName ?? windowType.Name;
            string assemblyName = windowType.Assembly.GetName().Name;
            if (ContainsAny(fullName, "TextCore", "TextMeshPro", "TMPro") ||
                ContainsAny(assemblyName, "TextCore", "TextMeshPro", "TMPro"))
            {
                return TextMeshProCategory;
            }

            if (ContainsAny(fullName, "Kubera") || ContainsAny(assemblyName, "Kubera"))
                return KuberaCategory;

            if (fullName.StartsWith("LoogaSoft.", StringComparison.Ordinal) ||
                assemblyName.StartsWith("LoogaSoft.", StringComparison.Ordinal))
            {
                return LoogaSoftCategory;
            }

            if (fullName.StartsWith("Unity.", StringComparison.Ordinal) ||
                fullName.StartsWith("UnityEditor.", StringComparison.Ordinal) ||
                assemblyName.StartsWith("Unity", StringComparison.Ordinal))
            {
                return MainUnityWindowNames.Contains(windowType.Name)
                    ? UnityCategory
                    : UnityAdvancedCategory;
            }

            return null;
        }

        private static string GetMenuCreator(string menuPath)
        {
            string[] parts = menuPath.Split('/');
            string root = parts[0].Trim();
            string creator = root;
            if (string.Equals(root, "Window", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(root, "Tools", StringComparison.OrdinalIgnoreCase))
            {
                creator = parts.Length >= 3 ? parts[1].Trim() : string.Empty;
            }

            if (string.IsNullOrWhiteSpace(creator) || FunctionalMenuGroups.Contains(creator))
                return null;

            if (ContainsAny(creator, "LoogaSoft"))
                return LoogaSoftCategory;

            if (ContainsAny(creator, "TextMesh", "TextMesh Pro"))
                return TextMeshProCategory;

            if (ContainsAny(creator, "Kubera"))
                return KuberaCategory;

            if (string.Equals(creator, "FPS ANIMATOR", StringComparison.OrdinalIgnoreCase))
                return "KINEMATION";

            return creator;
        }

        private static string GetAssemblyCreator(Type windowType)
        {
            string assemblyName = windowType.Assembly.GetName().Name;
            if (assemblyName.StartsWith("Assembly-CSharp", StringComparison.OrdinalIgnoreCase))
                return ProjectCategory;

            string[] nameParts = assemblyName.Split('.');
            string creator = nameParts.Length > 0 ? nameParts[0] : string.Empty;
            if (string.IsNullOrWhiteSpace(creator) ||
                string.Equals(creator, "Editor", StringComparison.OrdinalIgnoreCase))
            {
                return OtherPackagesCategory;
            }

            return ObjectNames.NicifyVariableName(creator);
        }

        private static bool ContainsAny(string value, params string[] terms)
        {
            for (int i = 0; i < terms.Length; i++)
            {
                if (value.IndexOf(terms[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
        #endregion

        private static bool IsUsableWindowType(Type windowType)
        {
            return windowType != null &&
                   !windowType.IsAbstract &&
                   !windowType.ContainsGenericParameters &&
                   typeof(EditorWindow).IsAssignableFrom(windowType);
        }

        private static bool HasWindowTitle(Type windowType)
        {
            return WindowTitleAttributeType != null &&
                   windowType.IsDefined(WindowTitleAttributeType, true);
        }

        private static bool LooksTransient(Type windowType)
        {
            string name = windowType.Name;
            for (int i = 0; i < TransientWindowNameParts.Length; i++)
            {
                if (name.IndexOf(TransientWindowNameParts[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static GUIContent GetWindowTitle(Type windowType)
        {
            if (WindowTitleMethod != null)
            {
                try
                {
                    if (WindowTitleMethod.Invoke(null, new object[] { windowType }) is GUIContent title &&
                        !string.IsNullOrWhiteSpace(title.text))
                    {
                        return new GUIContent(title);
                    }
                }
                catch (Exception)
                {
                    // Use the type name when Unity cannot provide localized window content.
                }
            }

            string name = ObjectNames.NicifyVariableName(windowType.Name);
            if (name.EndsWith(" Window", StringComparison.Ordinal))
            {
                name = name[..^7];
            }

            return new GUIContent(name, EditorGUIUtility.ObjectContent(null, windowType).image);
        }

        private static int CompareWindowEntries(EditorWindowEntry left, EditorWindowEntry right)
        {
            int categoryComparison = CompareCategories(left.Category, right.Category);
            return categoryComparison != 0
                ? categoryComparison
                : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareCategories(string left, string right)
        {
            int orderComparison = GetCategoryOrder(left).CompareTo(GetCategoryOrder(right));
            if (orderComparison != 0)
                return orderComparison;

            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static int GetCategoryOrder(string category)
        {
            if (string.Equals(category, UnityCategory, StringComparison.OrdinalIgnoreCase))
                return 0;

            if (string.Equals(category, UnityAdvancedCategory, StringComparison.OrdinalIgnoreCase))
                return 1;

            if (string.Equals(category, TextMeshProCategory, StringComparison.OrdinalIgnoreCase))
                return 2;

            if (string.Equals(category, LoogaSoftCategory, StringComparison.OrdinalIgnoreCase))
                return 3;

            if (string.Equals(category, KuberaCategory, StringComparison.OrdinalIgnoreCase))
                return 4;

            if (string.Equals(category, ProjectCategory, StringComparison.OrdinalIgnoreCase))
                return 5;

            if (string.Equals(category, OtherPackagesCategory, StringComparison.OrdinalIgnoreCase))
                return 7;

            return 6;
        }

        private readonly struct MenuCategoryCandidate
        {
            public MenuCategoryCandidate(string category, int priority)
            {
                Category = category;
                Priority = priority;
            }

            public string Category { get; }
            public int Priority { get; }
        }
    }

    internal static class DockAreaBridge
    {
        private const BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static readonly Type DockAreaType =
            typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.DockArea");

        private static readonly FieldInfo TabAreaRectField =
            DockAreaType?.GetField("m_TabAreaRect", InstanceFlags);
        private static readonly FieldInfo TotalTabWidthField =
            DockAreaType?.GetField("m_TotalTabWidth", InstanceFlags);
        private static readonly FieldInfo ScrollOffsetField =
            DockAreaType?.GetField("m_ScrollOffset", InstanceFlags);
        private static readonly MethodInfo GetPaneTypesMethod =
            DockAreaType?.GetMethod("GetPaneTypes", InstanceFlags);
        private static readonly MethodInfo AddTabMethod = DockAreaType?.GetMethod(
            "AddTab",
            InstanceFlags,
            null,
            new[] { typeof(EditorWindow), typeof(bool) },
            null);
        private static readonly PropertyInfo VisualTreeProperty = FindProperty(
            DockAreaType,
            "visualTree");
        private static bool _warningLogged;

        public static bool IsAvailable =>
            DockAreaType != null &&
            TabAreaRectField != null &&
            TotalTabWidthField != null &&
            ScrollOffsetField != null &&
            GetPaneTypesMethod != null &&
            AddTabMethod != null &&
            VisualTreeProperty != null;

        public static VisualElement GetVisualTree(Object dockArea)
        {
            return GetValue<VisualElement>(VisualTreeProperty, dockArea);
        }

        public static bool TryGetTabLayout(
            Object dockArea,
            out Rect tabArea,
            out float totalTabWidth,
            out float scrollOffset)
        {
            tabArea = GetValue<Rect>(TabAreaRectField, dockArea);
            totalTabWidth = GetValue<float>(TotalTabWidthField, dockArea);
            scrollOffset = GetValue<float>(ScrollOffsetField, dockArea);
            return tabArea.width > 0f && tabArea.height > 0f && totalTabWidth > 0f;
        }

        public static void AddPaneTypes(Object dockArea, HashSet<Type> windowTypes)
        {
            if (!IsAvailable || dockArea == null || windowTypes == null)
                return;

            try
            {
                if (GetPaneTypesMethod.Invoke(dockArea, null) is not IEnumerable paneTypes)
                    return;

                foreach (object value in paneTypes)
                {
                    if (value is Type windowType)
                    {
                        windowTypes.Add(windowType);
                    }
                }
            }
            catch (Exception exception)
            {
                LogWarningOnce(exception);
            }
        }

        public static bool AddTab(Object dockArea, EditorWindow window)
        {
            if (!IsAvailable || dockArea == null || window == null)
                return false;

            try
            {
                AddTabMethod.Invoke(dockArea, new object[] { window, true });
                window.Focus();
                return true;
            }
            catch (Exception exception)
            {
                LogWarningOnce(exception);
                return false;
            }
        }

        private static T GetValue<T>(MemberInfo member, Object target)
        {
            if (member == null || target == null)
                return default;

            try
            {
                object value = member switch
                {
                    FieldInfo field => field.GetValue(target),
                    PropertyInfo property => property.GetValue(target),
                    _ => null
                };
                return value is T result ? result : default;
            }
            catch (Exception exception)
            {
                LogWarningOnce(exception);
                return default;
            }
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo property = current.GetProperty(name, InstanceFlags | BindingFlags.DeclaredOnly);
                if (property != null)
                    return property;
            }

            return null;
        }

        private static void LogWarningOnce(Exception exception)
        {
            if (_warningLogged)
                return;

            _warningLogged = true;
            Debug.LogWarning(
                "Looga Toolkit could not access Unity's dock-tab API. " +
                $"The add-window button is disabled for this editor version. {exception.GetBaseException().Message}");
        }
    }
}
