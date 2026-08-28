using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace LoogaSoft.Navigation.Editor
{
    internal abstract class EditorNavigationBar : IDisposable
    {
        private const string BarName = "Looga Editor Navigation Bar";
        private const string SpacerName = "Looga Editor Navigation Spacer";
        private const float ArrowButtonWidth = 28f;
        protected const float InspectorBarHeight = 26f;
        protected const float ProjectBarHeight = 20f;
        private const float HistoryIconSize = 18f;

        private readonly VisualElement _historyContainer;
        private readonly ToolbarButton _backButton;
        private readonly ToolbarButton _forwardButton;
        private readonly Toolbar _bar;
        private readonly VisualElement _spacer;
        private int _lastWidth;

        protected EditorNavigationBar(EditorWindow window, float barHeight, bool showCreateAssetButton)
        {
            Window = window;

            _backButton = CreateArrowButton(true, MoveBack);
            _forwardButton = CreateArrowButton(false, MoveForward);

            VisualElement flexibleSpace = new();
            flexibleSpace.style.flexGrow = 1f;
            flexibleSpace.style.flexShrink = 1f;

            _historyContainer = new VisualElement
            {
                pickingMode = PickingMode.Position
            };
            _historyContainer.style.flexDirection = FlexDirection.Row;
            _historyContainer.style.flexShrink = 0f;
            _historyContainer.style.alignItems = Align.Center;
            _historyContainer.style.height = barHeight;
            _historyContainer.style.overflow = Overflow.Hidden;

            _bar = new Toolbar
            {
                name = BarName
            };
            _bar.style.position = Position.Absolute;
            _bar.style.left = 0f;
            _bar.style.right = 0f;
            _bar.style.top = 0f;
            _bar.style.height = barHeight;
            _bar.style.minHeight = barHeight;
            _bar.style.maxHeight = barHeight;
            _bar.style.flexDirection = FlexDirection.Row;
            _bar.style.alignItems = Align.Center;
            _bar.style.paddingLeft = 3f;
            _bar.style.paddingRight = 4f;
            _bar.style.borderBottomWidth = 0f;
            _bar.Add(_backButton);
            _bar.Add(_forwardButton);
            _bar.Add(flexibleSpace);
            _bar.Add(_historyContainer);
            if (showCreateAssetButton)
                _bar.Add(CreateAssetButton());

            _bar.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            _spacer = new VisualElement
            {
                name = SpacerName,
                pickingMode = PickingMode.Ignore
            };
            _spacer.style.height = barHeight;
            _spacer.style.minHeight = barHeight;
            _spacer.style.maxHeight = barHeight;
            _spacer.style.flexShrink = 0f;
        }

        protected EditorWindow Window { get; }
        protected float AvailableWidth => _bar.contentRect.width > 0f
            ? _bar.contentRect.width
            : Window.position.width;

        public bool IsValid => Window != null;
        public bool IsAttached => _bar.parent != null && _spacer.parent != null;

        public void Attach()
        {
            if (Window == null || Window.rootVisualElement == null)
                return;

            VisualElement root = Window.rootVisualElement;
            RemoveForeignDuplicates(root);

            if (_spacer.parent != root)
                root.Insert(0, _spacer);

            if (_bar.parent != root)
                root.Add(_bar);

            Refresh();
        }

        public void Dispose()
        {
            _bar.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _bar.RemoveFromHierarchy();
            _spacer.RemoveFromHierarchy();
            OnDisposed();
        }

        public void Refresh()
        {
            if (!IsValid)
                return;

            _backButton.SetEnabled(CanMoveBack);
            _forwardButton.SetEnabled(CanMoveForward);
            _historyContainer.Clear();
            BuildHistoryButtons(_historyContainer);
        }

        protected abstract bool CanMoveBack { get; }
        protected abstract bool CanMoveForward { get; }
        protected abstract void MoveBack();
        protected abstract void MoveForward();
        protected abstract void BuildHistoryButtons(VisualElement container);
        protected virtual void OnDisposed() { }

        protected static ToolbarButton CreateHistoryButton(
            Texture icon,
            string label,
            string tooltip,
            bool selected,
            Action clicked,
            bool showLabel)
        {
            ToolbarButton button = new(clicked)
            {
                tooltip = tooltip
            };
            float buttonHeight = showLabel ? ProjectBarHeight : 22f;
            button.style.height = buttonHeight;
            button.style.minHeight = buttonHeight;
            button.style.maxHeight = buttonHeight;
            button.style.marginLeft = 1f;
            button.style.marginRight = 1f;
            button.style.marginTop = 0f;
            button.style.marginBottom = 0f;
            button.style.paddingLeft = showLabel ? 5f : 3f;
            button.style.paddingRight = showLabel ? 6f : 3f;
            button.style.paddingTop = 0f;
            button.style.paddingBottom = 0f;
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            RemoveButtonBorders(button);

            if (selected)
                button.style.backgroundColor = SelectionColor;

            Image image = new()
            {
                image = icon,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            image.style.width = HistoryIconSize;
            image.style.height = HistoryIconSize;
            image.style.minWidth = HistoryIconSize;
            image.style.flexShrink = 0f;
            image.style.alignSelf = Align.Center;
            button.Add(image);

            if (showLabel)
            {
                Label text = new(label)
                {
                    pickingMode = PickingMode.Ignore
                };
                text.style.maxWidth = 76f;
                text.style.marginLeft = 4f;
                text.style.unityTextAlign = TextAnchor.MiddleLeft;
                text.style.textOverflow = TextOverflow.Ellipsis;
                text.style.whiteSpace = WhiteSpace.NoWrap;
                button.Add(text);
                button.style.width = 100f;
                button.style.minWidth = 100f;
                button.style.maxWidth = 100f;
            }
            else
            {
                button.style.width = 28f;
                button.style.minWidth = 28f;
                button.style.maxWidth = 28f;
            }

            return button;
        }

        protected static Texture ObjectIcon(Object target)
        {
            if (target == null)
                return EditorGUIUtility.IconContent("DefaultAsset Icon").image;

            Texture icon = EditorGUIUtility.ObjectContent(target, target.GetType()).image;
            if (icon != null)
                return icon;

            icon = AssetPreview.GetMiniThumbnail(target);
            return icon != null
                ? icon
                : EditorGUIUtility.IconContent("DefaultAsset Icon").image;
        }

        protected static Texture FolderIcon(string path)
        {
            Texture icon = AssetDatabase.GetCachedIcon(path);
            return icon != null
                ? icon
                : EditorGUIUtility.IconContent("Folder Icon").image;
        }

        private static Color SelectionColor => EditorGUIUtility.isProSkin
            ? new Color(0.17f, 0.36f, 0.54f, 1f)
            : new Color(0.24f, 0.48f, 0.73f, 1f);

        private static ToolbarButton CreateArrowButton(bool pointsLeft, Action clicked)
        {
            ToolbarButton button = new(clicked)
            {
                tooltip = pointsLeft ? "Back" : "Forward"
            };
            button.style.width = ArrowButtonWidth;
            button.style.minWidth = ArrowButtonWidth;
            button.style.maxWidth = ArrowButtonWidth;
            button.style.height = 20f;
            button.style.marginLeft = 0f;
            button.style.marginRight = 0f;
            button.style.marginTop = 0f;
            button.style.marginBottom = 0f;
            button.style.paddingLeft = 0f;
            button.style.paddingRight = 0f;
            button.style.paddingTop = 0f;
            button.style.paddingBottom = 0f;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            RemoveButtonBorders(button);
            button.Add(new NavigationArrowGlyph(pointsLeft));
            return button;
        }

        private ToolbarButton CreateAssetButton()
        {
            ToolbarButton button = null;
            button = new ToolbarButton(() => ShowCreateAssetMenu(button))
            {
                text = "+",
                tooltip = "Create asset"
            };
            button.style.width = 28f;
            button.style.minWidth = 28f;
            button.style.maxWidth = 28f;
            button.style.height = ProjectBarHeight;
            button.style.marginLeft = 2f;
            button.style.marginRight = 0f;
            button.style.marginTop = 0f;
            button.style.marginBottom = 0f;
            button.style.paddingLeft = 0f;
            button.style.paddingRight = 0f;
            button.style.paddingTop = 0f;
            button.style.paddingBottom = 1f;
            button.style.fontSize = 19f;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            RemoveButtonBorders(button);
            return button;
        }

        private void ShowCreateAssetMenu(VisualElement button)
        {
            Rect buttonBounds = button.worldBound;
            Rect screenBounds = new(
                Window.position.x + buttonBounds.x,
                Window.position.y + buttonBounds.yMax,
                buttonBounds.width,
                1f);
            EditorUtility.DisplayPopupMenu(screenBounds, "Assets/Create", null);
        }

        private static void RemoveButtonBorders(VisualElement button)
        {
            button.style.borderLeftWidth = 0f;
            button.style.borderRightWidth = 0f;
            button.style.borderTopWidth = 0f;
            button.style.borderBottomWidth = 0f;
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            int width = Mathf.RoundToInt(evt.newRect.width);
            if (width == _lastWidth)
                return;

            _lastWidth = width;
            Refresh();
        }

        private void RemoveForeignDuplicates(VisualElement root)
        {
            VisualElement existingBar = root.Q<VisualElement>(BarName);
            if (existingBar != null && existingBar != _bar)
                existingBar.RemoveFromHierarchy();

            VisualElement existingSpacer = root.Q<VisualElement>(SpacerName);
            if (existingSpacer != null && existingSpacer != _spacer)
                existingSpacer.RemoveFromHierarchy();
        }
    }

    internal sealed class InspectorNavigationBar : EditorNavigationBar
    {
        private const int MaximumHistoryButtons = 9;

        public InspectorNavigationBar(EditorWindow window)
            : base(window, InspectorBarHeight, false)
        {
            InspectorSelectionHistory.Changed += Refresh;
        }

        protected override bool CanMoveBack => InspectorSelectionHistory.CanMoveBack;
        protected override bool CanMoveForward => InspectorSelectionHistory.CanMoveForward;

        protected override void MoveBack()
        {
            InspectorSelectionHistory.MoveBack();
        }

        protected override void MoveForward()
        {
            InspectorSelectionHistory.MoveForward();
        }

        protected override void BuildHistoryButtons(VisualElement container)
        {
            IReadOnlyList<InspectorSelectionState> entries = InspectorSelectionHistory.Entries;
            int maximumButtons = Mathf.Clamp(
                Mathf.FloorToInt((AvailableWidth - 76f) / 30f),
                0,
                MaximumHistoryButtons);
            if (maximumButtons == 0)
                return;

            List<int> indices = RecentUniqueSelectionIndices(entries, maximumButtons);
            for (int i = 0; i < indices.Count; i++)
            {
                int historyIndex = indices[i];
                InspectorSelectionState state = entries[historyIndex];
                Object target = state.PrimaryObject;
                if (target == null)
                    continue;

                int selectedCount = state.ValidObjects().Length;
                string tooltip = selectedCount > 1
                    ? $"{target.name} (+{selectedCount - 1} selected)"
                    : target.name;
                int capturedIndex = historyIndex;
                container.Add(CreateHistoryButton(
                    ObjectIcon(target),
                    target.name,
                    tooltip,
                    historyIndex == InspectorSelectionHistory.Cursor,
                    () => InspectorSelectionHistory.NavigateTo(capturedIndex),
                    false));
            }
        }

        protected override void OnDisposed()
        {
            InspectorSelectionHistory.Changed -= Refresh;
        }

        private static List<int> RecentUniqueSelectionIndices(
            IReadOnlyList<InspectorSelectionState> entries,
            int maximumCount)
        {
            List<int> indices = new(maximumCount);
            HashSet<int> objectIds = new();
            int cursor = InspectorSelectionHistory.Cursor;
            if (cursor >= 0 && cursor < entries.Count)
            {
                Object current = entries[cursor].PrimaryObject;
                if (current != null)
                {
                    indices.Add(cursor);
                    objectIds.Add(current.GetInstanceID());
                }
            }

            for (int i = entries.Count - 1; i >= 0 && indices.Count < maximumCount; i--)
            {
                Object target = entries[i].PrimaryObject;
                if (target != null && objectIds.Add(target.GetInstanceID()))
                    indices.Add(i);
            }

            indices.Sort();
            return indices;
        }
    }

    internal sealed class ProjectNavigationBar : EditorNavigationBar
    {
        private const int MaximumHistoryButtons = 6;

        private readonly ProjectFolderHistory _history;

        public ProjectNavigationBar(EditorWindow window, ProjectFolderHistory history)
            : base(window, ProjectBarHeight, true)
        {
            _history = history;
            _history.Changed += Refresh;
        }

        protected override bool CanMoveBack => _history.CanMoveBack;
        protected override bool CanMoveForward => _history.CanMoveForward;

        protected override void MoveBack()
        {
            _history.MoveBack(OpenFolder);
        }

        protected override void MoveForward()
        {
            _history.MoveForward(OpenFolder);
        }

        protected override void BuildHistoryButtons(VisualElement container)
        {
            IReadOnlyList<string> entries = _history.Entries;
            int maximumButtons = Mathf.Clamp(
                Mathf.FloorToInt((AvailableWidth - 108f) / 104f),
                0,
                MaximumHistoryButtons);
            if (maximumButtons == 0)
                return;

            List<int> indices = RecentUniqueFolderIndices(entries, _history.Cursor, maximumButtons);
            for (int i = 0; i < indices.Count; i++)
            {
                int historyIndex = indices[i];
                string path = entries[historyIndex];
                int capturedIndex = historyIndex;
                container.Add(CreateHistoryButton(
                    FolderIcon(path),
                    Path.GetFileName(path),
                    path,
                    historyIndex == _history.Cursor,
                    () => _history.NavigateTo(capturedIndex, OpenFolder),
                    true));
            }
        }

        protected override void OnDisposed()
        {
            _history.Changed -= Refresh;
        }

        private bool OpenFolder(string path)
        {
            return ProjectBrowserBridge.OpenFolder(Window, path);
        }

        private static List<int> RecentUniqueFolderIndices(
            IReadOnlyList<string> entries,
            int cursor,
            int maximumCount)
        {
            List<int> indices = new(maximumCount);
            HashSet<string> paths = new(StringComparer.Ordinal);
            if (cursor >= 0 && cursor < entries.Count)
            {
                indices.Add(cursor);
                paths.Add(entries[cursor]);
            }

            for (int i = entries.Count - 1; i >= 0 && indices.Count < maximumCount; i--)
            {
                if (paths.Add(entries[i]))
                    indices.Add(i);
            }

            indices.Sort();
            return indices;
        }
    }

    internal sealed class NavigationArrowGlyph : VisualElement
    {
        private readonly bool _pointsLeft;

        public NavigationArrowGlyph(bool pointsLeft)
        {
            _pointsLeft = pointsLeft;
            pickingMode = PickingMode.Ignore;
            style.width = 12f;
            style.height = 15f;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            painter.strokeColor = EditorStyles.label.normal.textColor;
            painter.lineWidth = 2.25f;
            painter.BeginPath();
            if (_pointsLeft)
            {
                painter.MoveTo(new Vector2(8.5f, 2f));
                painter.LineTo(new Vector2(3.5f, 7.5f));
                painter.LineTo(new Vector2(8.5f, 13f));
            }
            else
            {
                painter.MoveTo(new Vector2(3.5f, 2f));
                painter.LineTo(new Vector2(8.5f, 7.5f));
                painter.LineTo(new Vector2(3.5f, 13f));
            }

            painter.Stroke();
        }
    }
}
