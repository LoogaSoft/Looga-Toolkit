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

        private readonly Object _dockArea;
        private readonly ToolbarButton _button;

        public DockTabButton(Object dockArea)
        {
            _dockArea = dockArea;
            _button = new ToolbarButton(OpenWindowMenu)
            {
                name = ButtonName,
                text = "+",
                tooltip = "Add Window"
            };
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
            _button.style.paddingBottom = 2f;
            _button.style.fontSize = 18f;
            _button.style.unityTextAlign = TextAnchor.MiddleCenter;
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
            List<EditorWindowEntry> entries = DockAreaBridge.GetWindowEntries(_dockArea);
            if (entries.Count == 0)
                return;

            EditorWindowDropdown dropdown = new(
                new AdvancedDropdownState(),
                entries,
                AddWindow);
            dropdown.Show(GetScreenAnchor());
        }

        private Rect GetScreenAnchor()
        {
            Rect dockScreenRect = DockAreaBridge.GetScreenPosition(_dockArea);
            float left = _button.resolvedStyle.left;
            float top = _button.resolvedStyle.top;
            return new Rect(
                dockScreenRect.x + left,
                dockScreenRect.y + top,
                ButtonSize,
                ButtonSize);
        }

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
            for (int i = 0; i < _entries.Count; i++)
            {
                EditorWindowEntry entry = _entries[i];
                EditorWindowDropdownItem item = new(entry.Name, entry.WindowType)
                {
                    icon = entry.Icon
                };
                root.AddChild(item);
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
        public EditorWindowEntry(string name, Type windowType, Texture2D icon)
        {
            Name = name;
            WindowType = windowType;
            Icon = icon;
        }

        public string Name { get; }
        public Type WindowType { get; }
        public Texture2D Icon { get; }
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
        private static readonly PropertyInfo ScreenPositionProperty = FindProperty(
            DockAreaType,
            "screenPosition");
        private static readonly MethodInfo WindowTitleMethod = typeof(EditorWindow).GetMethod(
            "GetLocalizedTitleContentFromType",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        private static bool _warningLogged;

        public static bool IsAvailable =>
            DockAreaType != null &&
            TabAreaRectField != null &&
            TotalTabWidthField != null &&
            ScrollOffsetField != null &&
            GetPaneTypesMethod != null &&
            AddTabMethod != null &&
            VisualTreeProperty != null &&
            ScreenPositionProperty != null;

        public static VisualElement GetVisualTree(Object dockArea)
        {
            return GetValue<VisualElement>(VisualTreeProperty, dockArea);
        }

        public static Rect GetScreenPosition(Object dockArea)
        {
            return GetValue<Rect>(ScreenPositionProperty, dockArea);
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

        public static List<EditorWindowEntry> GetWindowEntries(Object dockArea)
        {
            List<EditorWindowEntry> entries = new();
            if (!IsAvailable || dockArea == null)
                return entries;

            try
            {
                if (GetPaneTypesMethod.Invoke(dockArea, null) is not IEnumerable paneTypes)
                    return entries;

                HashSet<Type> addedTypes = new();
                foreach (object value in paneTypes)
                {
                    if (value is not Type windowType ||
                        windowType.IsAbstract ||
                        !typeof(EditorWindow).IsAssignableFrom(windowType) ||
                        !addedTypes.Add(windowType))
                    {
                        continue;
                    }

                    GUIContent title = GetWindowTitle(windowType);
                    entries.Add(new EditorWindowEntry(
                        title.text,
                        windowType,
                        title.image as Texture2D));
                }

                entries.Sort(CompareWindowEntries);
            }
            catch (Exception exception)
            {
                LogWarningOnce(exception);
            }

            return entries;
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
                catch (Exception exception)
                {
                    LogWarningOnce(exception);
                }
            }

            string name = ObjectNames.NicifyVariableName(windowType.Name);
            if (name.EndsWith(" Window", StringComparison.Ordinal))
                name = name[..^7];

            return new GUIContent(name, EditorGUIUtility.ObjectContent(null, windowType).image);
        }

        private static int CompareWindowEntries(EditorWindowEntry left, EditorWindowEntry right)
        {
            return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
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
