using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Inspector.Editor
{
    /// <summary>
    /// Presents optional Looga package integrations in one workspace. Providers remain in their
    /// owning packages and are discovered through a small reflection contract.
    /// </summary>
    internal sealed class LoogaPackageSupportWindow : EditorWindow
    {
        private const string MenuPath = "LoogaSoft/Package Support";
        private const float ToolbarHeight = 21f;
        private const float CardHeight = 82f;
        private const float UpdateCardHeight = 126f;
        private const float ContentPadding = 12f;

        private readonly List<PackageSupportPage> _pages = new();
        private GUIStyle _availableStatusStyle;
        private GUIStyle _currentUpdateStyle;
        private GUIStyle _enabledStatusStyle;
        private GUIStyle _errorUpdateStyle;
        private GUIStyle _sourceUpdateStyle;
        private GUIStyle _unavailableStatusStyle;
        private GUIStyle _updateAvailableStyle;
        private Vector2 _navigationScroll;
        private Vector2 _contentScroll;
        private int _selectedPage;

        [MenuItem(MenuPath, priority = 0)]
        private static void Open()
        {
            LoogaPackageSupportWindow window = GetWindow<LoogaPackageSupportWindow>();
            window.titleContent = new GUIContent("Looga Package Support");
            window.minSize = new Vector2(720f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            LoogaPackageUpdateService.Changed += OnPackageUpdatesChanged;
            LoogaPackageUpdateService.Initialize();
            RefreshProviders();
            UpdateTitle();
        }

        private void OnDisable()
        {
            LoogaPackageUpdateService.Changed -= OnPackageUpdatesChanged;
        }

        private void OnGUI()
        {
            DrawToolbar();

            Rect bodyRect = new(
                0f,
                ToolbarHeight,
                position.width,
                Mathf.Max(1f, position.height - ToolbarHeight));
            float navigationWidth = Mathf.Min(LoogaSidebarGUI.DefaultWidth, bodyRect.width);
            Rect navigationRect = new(bodyRect.x, bodyRect.y, navigationWidth, bodyRect.height);
            Rect dividerRect = new(
                navigationRect.xMax,
                bodyRect.y,
                LoogaSidebarGUI.DividerWidth,
                bodyRect.height);
            Rect contentRect = new(
                dividerRect.xMax,
                bodyRect.y,
                Mathf.Max(1f, bodyRect.xMax - dividerRect.xMax),
                bodyRect.height);

            DrawNavigation(navigationRect);
            LoogaSidebarGUI.Divider(dividerRect);
            DrawContent(contentRect);

            if (Event.current.type == EventType.MouseMove)
                Repaint();
        }

        private void DrawToolbar()
        {
            Rect toolbarRect = new(0f, 0f, position.width, ToolbarHeight);
            GUILayout.BeginArea(toolbarRect);
            try
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandHeight(true)))
                {
                    if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                    {
                        RefreshProviders();
                        LoogaPackageUpdateService.Refresh(true);
                    }

                    GUILayout.FlexibleSpace();
                    int packageCount = LoogaPackageUpdateService.Packages.Count;
                    int updateCount = LoogaPackageUpdateService.AvailableUpdateCount;
                    string summary = updateCount > 0
                        ? $"{packageCount} package(s), {updateCount} update(s)"
                        : $"{packageCount} package(s)";
                    GUILayout.Label(summary, EditorStyles.miniLabel);
                }
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        private void DrawNavigation(Rect rect)
        {
            _selectedPage = LoogaSidebarGUI.Navigation(
                rect,
                _navigationScroll,
                _selectedPage,
                _pages.Count + 1,
                GetNavigationLabel,
                out _navigationScroll);
        }

        private void DrawContent(Rect rect)
        {
            GUILayout.BeginArea(rect);
            try
            {
                if (_selectedPage == 0)
                {
                    DrawPackageUpdates();
                    return;
                }

                if (_pages.Count == 0)
                {
                    GUILayout.Space(ContentPadding);
                    EditorGUILayout.HelpBox("No optional package integrations were found.", MessageType.Info);
                    return;
                }

                PackageSupportPage page = _pages[Mathf.Clamp(_selectedPage - 1, 0, _pages.Count - 1)];
                _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);
                GUILayout.Space(ContentPadding);
                EditorGUILayout.LabelField(page.Name, LoogaSidebarGUI.HeaderStyle);
                GUILayout.Space(2f);
                EditorGUILayout.LabelField(
                    "Enable only the integrations used by this project.",
                    EditorStyles.wordWrappedMiniLabel);
                GUILayout.Space(10f);

                for (int i = 0; i < page.Providers.Count; i++)
                {
                    DrawProvider(page.Providers[i]);
                    GUILayout.Space(4f);
                }

                GUILayout.Space(ContentPadding);
                EditorGUILayout.EndScrollView();
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        private void DrawPackageUpdates()
        {
            _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);
            GUILayout.Space(ContentPadding);
            EditorGUILayout.LabelField("Package Updates", LoogaSidebarGUI.HeaderStyle);
            GUILayout.Space(2f);
            EditorGUILayout.LabelField(
                "Review installed Looga packages and apply updates through Unity Package Manager.",
                EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(8f);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(
                           LoogaPackageUpdateService.IsChecking ||
                           LoogaPackageUpdateService.IsUpdating))
                {
                    if (GUILayout.Button("Check Now", GUILayout.Width(92f)))
                        LoogaPackageUpdateService.Refresh(true);
                }

                using (new EditorGUI.DisabledScope(
                           LoogaPackageUpdateService.AvailableUpdateCount == 0 ||
                           LoogaPackageUpdateService.IsUpdating))
                {
                    if (GUILayout.Button("Update All", GUILayout.Width(92f)) &&
                        ConfirmUpdateAll())
                    {
                        LoogaPackageUpdateService.UpdateAll();
                    }
                }

                GUILayout.FlexibleSpace();
            }

            if (!string.IsNullOrWhiteSpace(LoogaPackageUpdateService.OperationMessage))
            {
                GUILayout.Space(5f);
                EditorGUILayout.HelpBox(
                    LoogaPackageUpdateService.OperationMessage,
                    LoogaPackageUpdateService.IsChecking ? MessageType.Info : MessageType.None);
            }

            GUILayout.Space(7f);
            IReadOnlyList<LoogaPackageUpdateInfo> packages = LoogaPackageUpdateService.Packages;
            if (packages.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "The project does not contain direct com.loogasoft Git dependencies.",
                    MessageType.Info);
            }

            for (int i = 0; i < packages.Count; i++)
            {
                DrawPackageUpdate(packages[i]);
                GUILayout.Space(4f);
            }

            GUILayout.Space(ContentPadding);
            EditorGUILayout.EndScrollView();
        }

        private void DrawPackageUpdate(LoogaPackageUpdateInfo package)
        {
            Rect cardRect = LoogaEditorStyle.PixelSnap(
                GUILayoutUtility.GetRect(1f, UpdateCardHeight, GUILayout.ExpandWidth(true)));
            EditorGUI.DrawRect(cardRect, LoogaEditorStyle.AlternateBoxColor);

            Rect innerRect = new(
                cardRect.x + 12f,
                cardRect.y + 8f,
                cardRect.width - 24f,
                cardRect.height - 16f);
            Rect titleRect = new(innerRect.x, innerRect.y, innerRect.width - 150f, 19f);
            EditorGUI.LabelField(titleRect, package.DisplayName, EditorStyles.boldLabel);

            Rect statusRect = new(innerRect.xMax - 146f, innerRect.y, 146f, 19f);
            EditorGUI.LabelField(
                statusRect,
                GetUpdateStatusLabel(package.Status),
                GetUpdateStatusStyle(package.Status));

            string installed = FormatRevision(package.InstalledRevision);
            string installedLabel = string.IsNullOrWhiteSpace(package.InstalledVersion)
                ? installed
                : $"{package.InstalledVersion}  {installed}";
            Rect installedRect = new(innerRect.x, innerRect.y + 22f, innerRect.width, 18f);
            EditorGUI.LabelField(installedRect, $"Installed: {installedLabel}", EditorStyles.miniLabel);

            string latestLabel = string.IsNullOrWhiteSpace(package.LatestLabel)
                ? "Not checked"
                : $"{package.LatestLabel}  {FormatRevision(package.LatestRevision)}";
            Rect latestRect = new(innerRect.x, innerRect.y + 39f, innerRect.width, 18f);
            EditorGUI.LabelField(latestRect, $"Latest: {latestLabel}", EditorStyles.miniLabel);

            Rect detailRect = new(innerRect.x, innerRect.y + 58f, innerRect.width, 29f);
            EditorGUI.LabelField(detailRect, package.Detail, EditorStyles.wordWrappedMiniLabel);

            Rect actionsRect = new(innerRect.x, innerRect.yMax - 20f, innerRect.width, 20f);
            DrawPackageActions(actionsRect, package);
        }

        private void DrawPackageActions(Rect rect, LoogaPackageUpdateInfo package)
        {
            const float buttonWidth = 104f;
            Rect updateRect = new(rect.x, rect.y, buttonWidth, rect.height);
            using (new EditorGUI.DisabledScope(
                       !package.CanUpdate || LoogaPackageUpdateService.IsUpdating))
            {
                string updateLabel = package.Status == LoogaPackageUpdateStatus.UnreleasedChanges
                    ? "Install Source"
                    : "Update";
                if (GUI.Button(updateRect, updateLabel) && ConfirmPackageUpdate(package))
                    LoogaPackageUpdateService.UpdatePackage(package);
            }

            Rect changesRect = new(updateRect.xMax + 4f, rect.y, buttonWidth, rect.height);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(package.ChangesUrl)))
            {
                if (GUI.Button(changesRect, "View Changes"))
                    LoogaPackageUpdateService.OpenChanges(package);
            }
        }

        private void DrawProvider(OptionalSupportProvider provider)
        {
            Rect cardRect = LoogaEditorStyle.PixelSnap(
                GUILayoutUtility.GetRect(1f, CardHeight, GUILayout.ExpandWidth(true)));
            EditorGUI.DrawRect(cardRect, LoogaEditorStyle.AlternateBoxColor);

            Rect innerRect = new(
                cardRect.x + 12f,
                cardRect.y + 8f,
                cardRect.width - 24f,
                cardRect.height - 16f);
            bool enabled = provider.Enabled;
            bool available = provider.Available;

            Rect titleRect = new(innerRect.x, innerRect.y, innerRect.width - 100f, 20f);
            EditorGUI.LabelField(titleRect, provider.IntegrationName, EditorStyles.boldLabel);

            Rect toggleRect = new(innerRect.xMax - 88f, innerRect.y, 88f, 20f);
            using (new EditorGUI.DisabledScope(!available && !enabled))
            {
                bool nextEnabled = EditorGUI.ToggleLeft(toggleRect, "Enabled", enabled);
                if (nextEnabled != enabled)
                    SetProviderEnabled(provider, nextEnabled);
            }

            string status = enabled ? "Enabled" : available ? "Available" : "Unavailable";
            Rect statusRect = new(innerRect.x, innerRect.y + 22f, innerRect.width, 18f);
            EditorGUI.LabelField(statusRect, status, GetStatusStyle(enabled, available));

            string detail = available ? provider.Description : provider.UnavailableReason;
            Rect detailRect = new(innerRect.x, innerRect.y + 41f, innerRect.width, 28f);
            EditorGUI.LabelField(detailRect, detail, EditorStyles.wordWrappedMiniLabel);
        }

        private GUIStyle GetStatusStyle(bool enabled, bool available)
        {
            _enabledStatusStyle ??= CreateStatusStyle(new Color(0.45f, 0.78f, 0.48f));
            _availableStatusStyle ??= CreateStatusStyle(LoogaEditorStyle.TextColor);
            _unavailableStatusStyle ??= CreateStatusStyle(new Color(0.88f, 0.58f, 0.30f));

            return enabled
                ? _enabledStatusStyle
                : available ? _availableStatusStyle : _unavailableStatusStyle;
        }

        private GUIStyle GetUpdateStatusStyle(LoogaPackageUpdateStatus status)
        {
            _currentUpdateStyle ??= CreateStatusStyle(new Color(0.45f, 0.78f, 0.48f));
            _updateAvailableStyle ??= CreateStatusStyle(new Color(0.42f, 0.68f, 0.95f));
            _sourceUpdateStyle ??= CreateStatusStyle(new Color(0.88f, 0.68f, 0.30f));
            _errorUpdateStyle ??= CreateStatusStyle(new Color(0.92f, 0.46f, 0.40f));

            return status switch
            {
                LoogaPackageUpdateStatus.Current => _currentUpdateStyle,
                LoogaPackageUpdateStatus.UpdateAvailable => _updateAvailableStyle,
                LoogaPackageUpdateStatus.UnreleasedChanges => _sourceUpdateStyle,
                LoogaPackageUpdateStatus.Unavailable => _errorUpdateStyle,
                _ => EditorStyles.miniLabel
            };
        }

        private static GUIStyle CreateStatusStyle(Color textColor)
        {
            GUIStyle style = new(EditorStyles.miniLabel);
            style.normal.textColor = textColor;
            return style;
        }

        private static string GetUpdateStatusLabel(LoogaPackageUpdateStatus status)
        {
            return status switch
            {
                LoogaPackageUpdateStatus.Checking => "Checking",
                LoogaPackageUpdateStatus.Current => "Current",
                LoogaPackageUpdateStatus.UpdateAvailable => "Update available",
                LoogaPackageUpdateStatus.UnreleasedChanges => "Unreleased changes",
                LoogaPackageUpdateStatus.LocalDevelopment => "Local development",
                LoogaPackageUpdateStatus.Unavailable => "Unavailable",
                _ => status.ToString()
            };
        }

        private static string FormatRevision(string revision)
        {
            if (string.IsNullOrWhiteSpace(revision))
                return "revision unavailable";

            return revision.Length > 8 ? revision.Substring(0, 8) : revision;
        }

        private static bool ConfirmPackageUpdate(LoogaPackageUpdateInfo package)
        {
            bool unreleased = package.Status == LoogaPackageUpdateStatus.UnreleasedChanges;
            string title = unreleased ? "Install Unreleased Source" : "Update Looga Package";
            string detail = unreleased
                ? "This revision is not part of a newer release tag. Install it only when you want the latest repository source."
                : $"Install {package.LatestLabel}?";
            return EditorUtility.DisplayDialog(
                title,
                $"{package.DisplayName}\n\n{detail}",
                unreleased ? "Install Source" : "Update",
                "Cancel");
        }

        private static bool ConfirmUpdateAll()
        {
            int unreleasedCount = LoogaPackageUpdateService.Packages.Count(package =>
                package.Status == LoogaPackageUpdateStatus.UnreleasedChanges);
            string warning = unreleasedCount > 0
                ? $"\n\nThis operation also installs unreleased source for {unreleasedCount} package(s)."
                : string.Empty;
            return EditorUtility.DisplayDialog(
                "Update All Looga Packages",
                $"Update {LoogaPackageUpdateService.AvailableUpdateCount} package(s)?{warning}\n\nLooga Toolkit updates last.",
                "Update All",
                "Cancel");
        }

        private void SetProviderEnabled(OptionalSupportProvider provider, bool enabled)
        {
            try
            {
                provider.SetEnabled(enabled);
                provider.RefreshState();
                Repaint();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Integration Update Failed",
                    $"Looga could not update {provider.IntegrationName} support.\n\n{exception.GetBaseException().Message}",
                    "OK");
            }
        }

        private void RefreshProviders()
        {
            string selectedName = _selectedPage > 0 && _selectedPage - 1 < _pages.Count
                ? _pages[_selectedPage - 1].Name
                : string.Empty;

            _pages.Clear();
            IEnumerable<OptionalSupportProvider> providers = OptionalSupportProvider.Discover();
            foreach (IGrouping<string, OptionalSupportProvider> group in providers.GroupBy(provider => provider.PackageName))
            {
                _pages.Add(new PackageSupportPage(
                    group.Key,
                    group.OrderBy(provider => provider.IntegrationName, StringComparer.Ordinal).ToList()));
            }

            _pages.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
            if (!string.IsNullOrEmpty(selectedName))
            {
                int selectedIndex = _pages.FindIndex(page =>
                    string.Equals(page.Name, selectedName, StringComparison.Ordinal));
                _selectedPage = selectedIndex >= 0 ? selectedIndex + 1 : 0;
            }
            else
            {
                _selectedPage = Mathf.Clamp(_selectedPage, 0, _pages.Count);
            }

            Repaint();
        }

        private string GetNavigationLabel(int index)
        {
            if (index == 0)
            {
                int updateCount = LoogaPackageUpdateService.AvailableUpdateCount;
                return updateCount > 0 ? $"Updates ({updateCount})" : "Updates";
            }

            return _pages[index - 1].Name;
        }

        private void OnPackageUpdatesChanged()
        {
            UpdateTitle();
            Repaint();
        }

        private void UpdateTitle()
        {
            int updateCount = LoogaPackageUpdateService.AvailableUpdateCount;
            titleContent = new GUIContent(updateCount > 0
                ? $"Looga Packages ({updateCount})"
                : "Looga Packages");
        }

        private sealed class PackageSupportPage
        {
            public PackageSupportPage(string name, List<OptionalSupportProvider> providers)
            {
                Name = name;
                Providers = providers;
            }

            public string Name { get; }
            public List<OptionalSupportProvider> Providers { get; }
        }

        private sealed class OptionalSupportProvider
        {
            private const BindingFlags StaticPublic = BindingFlags.Static | BindingFlags.Public;
            private readonly MethodInfo _getUnavailableReason;
            private readonly MethodInfo _isEnabled;
            private readonly MethodInfo _setEnabled;

            private OptionalSupportProvider(Type type)
            {
                ProviderId = ReadString(type, "ProviderId");
                PackageName = ReadString(type, "PackageName");
                IntegrationName = ReadString(type, "IntegrationName");
                Description = ReadString(type, "Description");
                _isEnabled = type.GetMethod("IsEnabled", StaticPublic);
                _getUnavailableReason = type.GetMethod("GetUnavailableReason", StaticPublic);
                _setEnabled = type.GetMethod("SetEnabled", StaticPublic, null, new[] { typeof(bool) }, null);
                RefreshState();
            }

            public string ProviderId { get; }
            public string PackageName { get; }
            public string IntegrationName { get; }
            public string Description { get; }
            public bool Enabled { get; private set; }
            public string UnavailableReason { get; private set; }
            public bool Available => string.IsNullOrEmpty(UnavailableReason);

            public void SetEnabled(bool enabled) => _setEnabled.Invoke(null, new object[] { enabled });

            /// <summary>
            /// Refreshes checks that can scan packages or compiled assemblies. Do not call this from OnGUI.
            /// </summary>
            public void RefreshState()
            {
                Enabled = (bool)_isEnabled.Invoke(null, null);
                UnavailableReason = (string)_getUnavailableReason.Invoke(null, null) ?? string.Empty;
            }

            public static IEnumerable<OptionalSupportProvider> Discover()
            {
                List<OptionalSupportProvider> providers = new();
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (Type type in GetLoadableTypes(assembly))
                    {
                        if (!HasProviderContract(type))
                            continue;

                        providers.Add(new OptionalSupportProvider(type));
                    }
                }

                return providers
                    .Where(provider => !string.IsNullOrWhiteSpace(provider.ProviderId))
                    .GroupBy(provider => provider.ProviderId, StringComparer.Ordinal)
                    .Select(group => group.First());
            }

            private static bool HasProviderContract(Type type)
            {
                return type.GetProperty("ProviderId", StaticPublic)?.PropertyType == typeof(string) &&
                       type.GetProperty("PackageName", StaticPublic)?.PropertyType == typeof(string) &&
                       type.GetProperty("IntegrationName", StaticPublic)?.PropertyType == typeof(string) &&
                       type.GetProperty("Description", StaticPublic)?.PropertyType == typeof(string) &&
                       type.GetMethod("IsEnabled", StaticPublic)?.ReturnType == typeof(bool) &&
                       type.GetMethod("GetUnavailableReason", StaticPublic)?.ReturnType == typeof(string) &&
                       type.GetMethod("SetEnabled", StaticPublic, null, new[] { typeof(bool) }, null) != null;
            }

            private static string ReadString(Type type, string propertyName)
            {
                return type.GetProperty(propertyName, StaticPublic)?.GetValue(null) as string ?? string.Empty;
            }

            private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    return exception.Types.Where(type => type != null);
                }
            }
        }
    }
}
