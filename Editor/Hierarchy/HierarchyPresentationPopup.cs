using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    internal sealed class HierarchyPresentationPopup : PopupWindowContent
    {
        private const int ColumnCount = 12;
        private const float CellSize = 20f;
        private const float CellSpacing = 3f;
        private const float Padding = 8f;
        private const float SectionSpacing = 8f;

        private static readonly Color[] Colors =
        {
            new(0.78f, 0.28f, 0.34f, 1f),
            new(0.91f, 0.47f, 0.23f, 1f),
            new(0.92f, 0.68f, 0.22f, 1f),
            new(0.56f, 0.72f, 0.28f, 1f),
            new(0.30f, 0.68f, 0.40f, 1f),
            new(0.20f, 0.67f, 0.65f, 1f),
            new(0.25f, 0.56f, 0.84f, 1f),
            new(0.42f, 0.42f, 0.82f, 1f),
            new(0.66f, 0.38f, 0.82f, 1f),
            new(0.82f, 0.34f, 0.67f, 1f)
        };

        private readonly int[] _targetIds = Array.Empty<int>();
        private readonly string[] _folderGuids = Array.Empty<string>();
        private static GUIStyle _selectionStyle;

        private bool _hasColor;
        private Color _selectedColor;
        private string _selectedIconName;

        private HierarchyPresentationPopup(GameObject[] targets)
        {
            _targetIds = new int[targets.Length];
            for (int index = 0; index < targets.Length; index++)
            {
                _targetIds[index] = targets[index].GetInstanceID();
            }

            ReadCurrentPresentation(targets[0]);
        }

        private HierarchyPresentationPopup(string[] folderGuids)
        {
            _folderGuids = folderGuids;
            ReadCurrentFolderPresentation(folderGuids[0]);
        }

        internal static void Open(Rect anchor, GameObject[] targets)
        {
            if (targets == null || targets.Length == 0)
            {
                return;
            }

            PopupWindow.Show(anchor, new HierarchyPresentationPopup(targets));
        }

        internal static void OpenProjectFolders(Rect anchor, string[] folderGuids)
        {
            if (folderGuids == null || folderGuids.Length == 0)
            {
                return;
            }

            PopupWindow.Show(anchor, new HierarchyPresentationPopup(folderGuids));
        }

        public override Vector2 GetWindowSize()
        {
            int iconCount = HierarchyIconCatalog.All.Count + 1;
            int iconRows = Mathf.CeilToInt(iconCount / (float)ColumnCount);
            float width = Padding * 2f + ColumnCount * CellSize + (ColumnCount - 1) * CellSpacing;
            float height = Padding * 2f + CellSize + SectionSpacing +
                iconRows * CellSize + (iconRows - 1) * CellSpacing;
            return new Vector2(width, height);
        }

        public override void OnOpen()
        {
            editorWindow.wantsMouseMove = true;
            editorWindow.wantsMouseEnterLeaveWindow = true;
            BeginPreview();
        }

        public override void OnClose()
        {
            EndPreview();
        }

        public override void OnGUI(Rect rect)
        {
            Event current = Event.current;
            if (current.type == EventType.MouseMove ||
                current.type == EventType.MouseEnterWindow ||
                current.type == EventType.MouseLeaveWindow)
            {
                editorWindow.Repaint();
            }

            UpdatePreview(current.mousePosition, current.type == EventType.MouseLeaveWindow);

            float y = Padding;
            DrawColorOptions(ref y);
            y += SectionSpacing;
            DrawIconOptions(y);
        }

        private void UpdatePreview(Vector2 mousePosition, bool mouseLeftWindow)
        {
            if (mouseLeftWindow)
            {
                ClearPreview();
                return;
            }

            float colorY = Padding;
            if (GetCellRect(0, 0, colorY).Contains(mousePosition))
            {
                PreviewColor(false, default);
                return;
            }

            for (int index = 0; index < Colors.Length; index++)
            {
                if (GetCellRect(index + 1, 0, colorY).Contains(mousePosition))
                {
                    PreviewColor(true, Colors[index]);
                    return;
                }
            }

            float iconY = Padding + CellSize + SectionSpacing;
            if (GetCellRect(0, 0, iconY).Contains(mousePosition))
            {
                PreviewIcon(false, string.Empty);
                return;
            }

            IReadOnlyList<HierarchyIconOption> options = HierarchyIconCatalog.All;
            for (int index = 0; index < options.Count; index++)
            {
                int itemIndex = index + 1;
                int column = itemIndex % ColumnCount;
                int row = itemIndex / ColumnCount;
                Rect iconRect = GetCellRect(column, row, iconY);
                if (iconRect.Contains(mousePosition))
                {
                    HierarchyIconOption option = options[index];
                    if (HierarchyIconCatalog.GetTexture(option.IconName) != null)
                    {
                        PreviewIcon(true, option.IconName);
                    }

                    return;
                }
            }
        }

        private void DrawColorOptions(ref float y)
        {
            int column = 0;
            Rect clearRect = GetCellRect(column++, 0, y);
            DrawClearButton(clearRect, "Clear color", !_hasColor, ClearColor);

            for (int index = 0; index < Colors.Length; index++)
            {
                Color color = Colors[index];
                Rect colorRect = GetCellRect(column++, 0, y);
                DrawColorButton(colorRect, color, _hasColor && ColorsMatch(color, _selectedColor));
            }

            Rect customRect = GetCellRect(column, 0, y);
            DrawAddButton(customRect, "Choose a custom color", OpenCustomColorWindow);
            y += CellSize;
        }

        private void DrawIconOptions(float y)
        {
            int itemIndex = 0;
            Rect clearRect = GetCellRect(0, 0, y);
            DrawClearButton(
                clearRect,
                "Clear icon",
                string.IsNullOrEmpty(_selectedIconName),
                ClearIcon);
            itemIndex++;

            IReadOnlyList<HierarchyIconOption> options = HierarchyIconCatalog.All;
            for (int index = 0; index < options.Count; index++)
            {
                HierarchyIconOption option = options[index];
                int column = itemIndex % ColumnCount;
                int row = itemIndex / ColumnCount;
                Rect iconRect = GetCellRect(column, row, y);
                DrawIconButton(iconRect, option, option.IconName == _selectedIconName);
                itemIndex++;
            }
        }

        private void DrawColorButton(Rect rect, Color color, bool selected)
        {
            DrawOptionBackground(rect, selected);
            Rect swatchRect = new(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f);
            EditorGUI.DrawRect(swatchRect, color);

            if (GUI.Button(rect, new GUIContent(string.Empty, "Set color"), GUIStyle.none))
            {
                SetColor(color);
                _hasColor = true;
                _selectedColor = color;
            }
        }

        private void DrawIconButton(Rect rect, HierarchyIconOption option, bool selected)
        {
            Texture icon = HierarchyIconCatalog.GetTexture(option.IconName);
            if (icon == null)
            {
                return;
            }

            DrawOptionBackground(rect, selected);
            GUI.DrawTexture(
                new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f),
                icon,
                ScaleMode.ScaleToFit,
                true);

            if (GUI.Button(rect, new GUIContent(string.Empty, option.Name), GUIStyle.none))
            {
                SetIcon(option.IconName);
                _selectedIconName = option.IconName;
                CommitIcon(option.IconName);
            }
        }

        private void DrawClearButton(Rect rect, string tooltip, bool selected, Action action)
        {
            DrawOptionBackground(rect, selected);
            DrawClearGlyph(rect);
            if (GUI.Button(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none))
            {
                action();
            }
        }

        private void DrawAddButton(Rect rect, string tooltip, Action action)
        {
            DrawOptionBackground(rect, false);
            DrawAddGlyph(rect);
            if (GUI.Button(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none))
            {
                action();
            }
        }

        private static void DrawOptionBackground(Rect rect, bool selected)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (selected)
            {
                SelectionStyle.Draw(rect, false, false, true, true);
                return;
            }

            if (rect.Contains(Event.current.mousePosition))
            {
                Color hoverColor = EditorGUIUtility.isProSkin
                    ? new Color(1f, 1f, 1f, 0.12f)
                    : new Color(0f, 0f, 0f, 0.10f);
                EditorGUI.DrawRect(rect, hoverColor);
            }
        }

        private static void DrawClearGlyph(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            float pixelSize = 1f / pixelsPerPoint;
            float outlineWidth = pixelSize;
            Rect outlineRect = SnapToPixelGrid(
                new Rect(
                    rect.x + 4f,
                    rect.y + 4f,
                    rect.width - 8f,
                    rect.height - 8f),
                pixelsPerPoint);

            Color outlineColor = EditorGUIUtility.isProSkin
                ? new Color(0.58f, 0.58f, 0.58f, 1f)
                : new Color(0.38f, 0.38f, 0.38f, 1f);
            DrawOutline(outlineRect, outlineWidth, outlineColor);

            Color crossColor = EditorGUIUtility.isProSkin
                ? new Color(0.82f, 0.82f, 0.82f, 1f)
                : new Color(0.55f, 0.55f, 0.55f, 1f);
            float crossInset = 2f;
            float crossWidth = 1.5f / pixelsPerPoint;
            Vector3 topLeft = SnapToPixelGrid(
                new Vector3(
                    outlineRect.x + crossInset,
                    outlineRect.y + crossInset,
                    0f),
                pixelsPerPoint);
            Vector3 topRight = SnapToPixelGrid(
                new Vector3(
                    outlineRect.xMax - crossInset,
                    outlineRect.y + crossInset,
                    0f),
                pixelsPerPoint);
            Vector3 bottomLeft = SnapToPixelGrid(
                new Vector3(
                    outlineRect.x + crossInset,
                    outlineRect.yMax - crossInset,
                    0f),
                pixelsPerPoint);
            Vector3 bottomRight = SnapToPixelGrid(
                new Vector3(
                    outlineRect.xMax - crossInset,
                    outlineRect.yMax - crossInset,
                    0f),
                pixelsPerPoint);

            Color previousColor = Handles.color;
            Handles.color = crossColor;
            Handles.DrawAAPolyLine(crossWidth, topLeft, bottomRight);
            Handles.DrawAAPolyLine(crossWidth, topRight, bottomLeft);
            Handles.color = previousColor;
        }

        private static void DrawAddGlyph(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            Color glyphColor = EditorGUIUtility.isProSkin
                ? new Color(0.78f, 0.78f, 0.78f, 1f)
                : new Color(0.42f, 0.42f, 0.42f, 1f);
            float thickness = 2f;
            float length = 12f;

            EditorGUI.DrawRect(
                new Rect(
                    rect.center.x - length * 0.5f,
                    rect.center.y - thickness * 0.5f,
                    length,
                    thickness),
                glyphColor);
            EditorGUI.DrawRect(
                new Rect(
                    rect.center.x - thickness * 0.5f,
                    rect.center.y - length * 0.5f,
                    thickness,
                    length),
                glyphColor);
        }

        private static void DrawOutline(Rect rect, float thickness, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(
                new Rect(rect.x, rect.yMax - thickness, rect.width, thickness),
                color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(
                new Rect(rect.xMax - thickness, rect.y, thickness, rect.height),
                color);
        }

        private static Rect SnapToPixelGrid(Rect rect, float pixelsPerPoint)
        {
            float xMin = SnapToPixelGrid(rect.xMin, pixelsPerPoint);
            float yMin = SnapToPixelGrid(rect.yMin, pixelsPerPoint);
            float xMax = SnapToPixelGrid(rect.xMax, pixelsPerPoint);
            float yMax = SnapToPixelGrid(rect.yMax, pixelsPerPoint);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static Vector3 SnapToPixelGrid(Vector3 point, float pixelsPerPoint)
        {
            point.x = SnapToPixelGrid(point.x, pixelsPerPoint);
            point.y = SnapToPixelGrid(point.y, pixelsPerPoint);
            return point;
        }

        private static float SnapToPixelGrid(float value, float pixelsPerPoint)
        {
            return Mathf.Round(value * pixelsPerPoint) / pixelsPerPoint;
        }

        private void ClearColor()
        {
            if (IsProjectFolderMode)
            {
                ApplyToFolders(ProjectFolderPresentationStore.instance.ClearColor);
            }
            else
            {
                Apply(HierarchyPresentationStore.instance.ClearLabelColor);
            }

            _hasColor = false;
        }

        private void ClearIcon()
        {
            if (IsProjectFolderMode)
            {
                ApplyToFolders(ProjectFolderPresentationStore.instance.ClearIcon);
            }
            else
            {
                Apply(HierarchyPresentationStore.instance.ClearIcon);
            }

            _selectedIconName = string.Empty;
            if (IsProjectFolderMode)
            {
                ProjectFolderPresentationPreview.SetIcon(false, string.Empty);
            }
            else
            {
                HierarchyPresentationPreview.CommitIcon(false, string.Empty);
            }
        }

        private void OpenCustomColorWindow()
        {
            editorWindow.Close();
            if (IsProjectFolderMode)
            {
                string[] folderGuids = _folderGuids;
                EditorApplication.delayCall += () =>
                    ProjectFolderColorWindow.Open(folderGuids);
            }
            else
            {
                GameObject[] targets = ResolveTargets();
                EditorApplication.delayCall += () => HierarchyColorWindow.Open(targets);
            }
        }

        private void ReadCurrentPresentation(GameObject target)
        {
            if (!HierarchyPresentationStore.instance.TryGet(target, out HierarchyPresentation presentation))
            {
                return;
            }

            _hasColor = presentation.HasLabelColor;
            _selectedColor = presentation.LabelColor;
            _selectedIconName = presentation.IconName;
        }

        private void ReadCurrentFolderPresentation(string folderGuid)
        {
            if (!ProjectFolderPresentationStore.instance.TryGet(
                    folderGuid,
                    out ProjectFolderPresentation presentation))
            {
                return;
            }

            _hasColor = presentation.HasColor;
            _selectedColor = presentation.Color;
            _selectedIconName = presentation.IconName;
        }

        private bool IsProjectFolderMode => _folderGuids.Length > 0;

        private void BeginPreview()
        {
            if (IsProjectFolderMode)
            {
                ProjectFolderPresentationPreview.Begin(_folderGuids);
            }
            else
            {
                HierarchyPresentationPreview.Begin(_targetIds);
            }
        }

        private void EndPreview()
        {
            if (IsProjectFolderMode)
            {
                ProjectFolderPresentationPreview.End();
            }
            else
            {
                HierarchyPresentationPreview.End();
            }
        }

        private void ClearPreview()
        {
            if (IsProjectFolderMode)
            {
                ProjectFolderPresentationPreview.ClearOption();
            }
            else
            {
                HierarchyPresentationPreview.ClearOption();
            }
        }

        private void PreviewColor(bool hasColor, Color color)
        {
            if (IsProjectFolderMode)
            {
                ProjectFolderPresentationPreview.SetColor(hasColor, color);
            }
            else
            {
                HierarchyPresentationPreview.SetColor(hasColor, color);
            }
        }

        private void PreviewIcon(bool hasIcon, string iconName)
        {
            if (IsProjectFolderMode)
            {
                ProjectFolderPresentationPreview.SetIcon(hasIcon, iconName);
            }
            else
            {
                HierarchyPresentationPreview.SetIcon(hasIcon, iconName);
            }
        }

        private void SetColor(Color color)
        {
            if (IsProjectFolderMode)
            {
                ApplyToFolders(guid =>
                    ProjectFolderPresentationStore.instance.SetColor(guid, color));
            }
            else
            {
                Apply(target =>
                    HierarchyPresentationStore.instance.SetLabelColor(target, color));
            }
        }

        private void SetIcon(string iconName)
        {
            if (IsProjectFolderMode)
            {
                ApplyToFolders(guid =>
                    ProjectFolderPresentationStore.instance.SetIcon(guid, iconName));
            }
            else
            {
                Apply(target =>
                    HierarchyPresentationStore.instance.SetIcon(target, iconName));
            }
        }

        private void CommitIcon(string iconName)
        {
            if (IsProjectFolderMode)
            {
                ProjectFolderPresentationPreview.SetIcon(true, iconName);
            }
            else
            {
                HierarchyPresentationPreview.CommitIcon(true, iconName);
            }
        }

        private void ApplyToFolders(Action<string> action)
        {
            for (int index = 0; index < _folderGuids.Length; index++)
            {
                action(_folderGuids[index]);
            }
        }

        private void Apply(Action<GameObject> action)
        {
            GameObject[] targets = ResolveTargets();
            for (int index = 0; index < targets.Length; index++)
            {
                action(targets[index]);
            }
        }

        private GameObject[] ResolveTargets()
        {
            List<GameObject> targets = new(_targetIds.Length);
            for (int index = 0; index < _targetIds.Length; index++)
            {
#pragma warning disable CS0618
                GameObject target = EditorUtility.InstanceIDToObject(_targetIds[index]) as GameObject;
#pragma warning restore CS0618
                if (target != null)
                {
                    targets.Add(target);
                }
            }

            return targets.ToArray();
        }

        private static Rect GetCellRect(int column, int row, float y)
        {
            return new Rect(
                Padding + column * (CellSize + CellSpacing),
                y + row * (CellSize + CellSpacing),
                CellSize,
                CellSize);
        }

        private static bool ColorsMatch(Color left, Color right)
        {
            const float tolerance = 0.001f;
            return Mathf.Abs(left.r - right.r) < tolerance &&
                   Mathf.Abs(left.g - right.g) < tolerance &&
                   Mathf.Abs(left.b - right.b) < tolerance &&
                   Mathf.Abs(left.a - right.a) < tolerance;
        }

        private static GUIStyle SelectionStyle
        {
            get
            {
                _selectionStyle ??= GUI.skin.GetStyle("TV Selection");
                return _selectionStyle;
            }
        }
    }

    internal sealed class HierarchyColorWindow : EditorWindow
    {
        private const float Width = 280f;
        private const float Height = 88f;

        private int[] _targetIds = Array.Empty<int>();
        private Color _color = HierarchyPresentationStore.DefaultLabelColor;

        internal static void Open(GameObject[] targets)
        {
            if (targets == null || targets.Length == 0)
            {
                return;
            }

            HierarchyColorWindow window = CreateInstance<HierarchyColorWindow>();
            window.titleContent = new GUIContent("Object Color");
            window._targetIds = new int[targets.Length];

            for (int index = 0; index < targets.Length; index++)
            {
                window._targetIds[index] = targets[index].GetInstanceID();
            }

            if (HierarchyPresentationStore.instance.TryGet(targets[0], out HierarchyPresentation presentation) &&
                presentation.HasLabelColor)
            {
                window._color = presentation.LabelColor;
            }

            window.minSize = new Vector2(Width, Height);
            window.maxSize = window.minSize;
            window.ShowAuxWindow();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            _color = EditorGUILayout.ColorField("Color", _color);
            EditorGUILayout.Space(6f);

            using (new EditorGUI.DisabledScope(_targetIds.Length == 0))
            {
                if (!GUILayout.Button("Apply", GUILayout.Height(22f)))
                {
                    return;
                }
            }

            for (int index = 0; index < _targetIds.Length; index++)
            {
#pragma warning disable CS0618
                GameObject target = EditorUtility.InstanceIDToObject(_targetIds[index]) as GameObject;
#pragma warning restore CS0618
                if (target != null)
                {
                    HierarchyPresentationStore.instance.SetLabelColor(target, _color);
                }
            }

            Close();
        }
    }

    internal sealed class ProjectFolderColorWindow : EditorWindow
    {
        private const float Width = 280f;
        private const float Height = 88f;

        [SerializeField]
        private string[] _folderGuids = Array.Empty<string>();

        [SerializeField]
        private Color _color = HierarchyPresentationStore.DefaultLabelColor;

        internal static void Open(string[] folderGuids)
        {
            if (folderGuids == null || folderGuids.Length == 0)
            {
                return;
            }

            ProjectFolderColorWindow window = CreateInstance<ProjectFolderColorWindow>();
            window.titleContent = new GUIContent("Folder Color");
            window._folderGuids = folderGuids;

            if (ProjectFolderPresentationStore.instance.TryGet(
                    folderGuids[0],
                    out ProjectFolderPresentation presentation) &&
                presentation.HasColor)
            {
                window._color = presentation.Color;
            }

            window.minSize = new Vector2(Width, Height);
            window.maxSize = window.minSize;
            window.ShowAuxWindow();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            _color = EditorGUILayout.ColorField("Color", _color);
            EditorGUILayout.Space(6f);

            using (new EditorGUI.DisabledScope(_folderGuids.Length == 0))
            {
                if (!GUILayout.Button("Apply", GUILayout.Height(22f)))
                {
                    return;
                }
            }

            for (int index = 0; index < _folderGuids.Length; index++)
            {
                ProjectFolderPresentationStore.instance.SetColor(
                    _folderGuids[index],
                    _color);
            }

            Close();
        }
    }
}
