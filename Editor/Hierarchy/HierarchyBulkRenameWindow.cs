using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    internal enum HierarchyNumberPlacement
    {
        Suffix,
        Prefix
    }

    /// <summary>
    /// Provides a preview-first, Undo-safe rename workflow for hierarchy selections.
    /// </summary>
    internal sealed class HierarchyBulkRenameWindow : EditorWindow
    {
        private const float MinimumWidth = 540f;
        private const float MinimumHeight = 440f;
        private const int MaximumPadding = 8;

        private static readonly GUIStyle ArrowStyle = new(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter
        };

        [SerializeField]
        private int[] _selectionIds = Array.Empty<int>();

        [SerializeField]
        private int[] _targetIds = Array.Empty<int>();

        [SerializeField]
        private bool _includeDescendants;

        [SerializeField]
        private bool _useSharedBaseName;

        [SerializeField]
        private string _sharedBaseName = "GameObject";

        [SerializeField]
        private string _find = string.Empty;

        [SerializeField]
        private string _replace = string.Empty;

        [SerializeField]
        private string _prefix = string.Empty;

        [SerializeField]
        private string _suffix = string.Empty;

        [SerializeField]
        private bool _addNumbering;

        [SerializeField]
        private HierarchyNumberPlacement _numberPlacement = HierarchyNumberPlacement.Suffix;

        [SerializeField]
        private int _numberStart = 1;

        [SerializeField]
        private int _numberStep = 1;

        [SerializeField]
        private int _numberPadding = 2;

        [SerializeField]
        private string _numberSeparator = " ";

        [SerializeField]
        private Vector2 _previewScroll;

        internal static void Open(GameObject[] selection)
        {
            HierarchyBulkRenameWindow window = GetWindow<HierarchyBulkRenameWindow>(true, "Bulk Rename");
            window.minSize = new Vector2(MinimumWidth, MinimumHeight);
            window.CaptureSelection(selection);
            window.Show();
        }

        private void OnGUI()
        {
            List<GameObject> targets = ResolveObjects(_targetIds);

            EditorGUILayout.Space(6f);
            DrawScope(targets.Count);
            EditorGUILayout.Space(8f);
            DrawNamingOptions();
            EditorGUILayout.Space(8f);
            DrawNumberingOptions();
            EditorGUILayout.Space(8f);

            List<string> names = BuildNames(targets);
            DrawPreview(targets, names);
            DrawFooter(targets, names);
        }

        private void DrawScope(int targetCount)
        {
            EditorGUILayout.LabelField("Scope", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    $"{targetCount} object{(targetCount == 1 ? string.Empty : "s")} in hierarchy order",
                    EditorStyles.miniLabel);

                if (GUILayout.Button("Use Current Selection", EditorStyles.miniButton, GUILayout.Width(140f)))
                {
                    CaptureSelection(Selection.gameObjects);
                    GUI.FocusControl(null);
                }
            }

            EditorGUI.BeginChangeCheck();
            _includeDescendants = EditorGUILayout.ToggleLeft("Include Descendants", _includeDescendants);
            if (EditorGUI.EndChangeCheck())
            {
                RebuildTargets();
            }
        }

        private void DrawNamingOptions()
        {
            EditorGUILayout.LabelField("Naming", EditorStyles.boldLabel);
            _useSharedBaseName = EditorGUILayout.ToggleLeft("Replace Existing Names", _useSharedBaseName);

            using (new EditorGUI.DisabledScope(!_useSharedBaseName))
            {
                _sharedBaseName = EditorGUILayout.TextField("Base Name", _sharedBaseName);
            }

            _find = EditorGUILayout.TextField("Find", _find);
            _replace = EditorGUILayout.TextField("Replace With", _replace);
            _prefix = EditorGUILayout.TextField("Prefix", _prefix);
            _suffix = EditorGUILayout.TextField("Suffix", _suffix);
        }

        private void DrawNumberingOptions()
        {
            EditorGUILayout.LabelField("Numbering", EditorStyles.boldLabel);
            _addNumbering = EditorGUILayout.ToggleLeft("Add Sequential Number", _addNumbering);

            using (new EditorGUI.DisabledScope(!_addNumbering))
            {
                _numberPlacement = (HierarchyNumberPlacement)EditorGUILayout.EnumPopup("Placement", _numberPlacement);
                _numberStart = EditorGUILayout.IntField("Start", _numberStart);
                _numberStep = EditorGUILayout.IntField("Step", _numberStep);
                _numberPadding = EditorGUILayout.IntSlider("Padding", _numberPadding, 1, MaximumPadding);
                _numberSeparator = EditorGUILayout.TextField("Separator", _numberSeparator);
            }
        }

        private void DrawPreview(List<GameObject> targets, List<string> names)
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            Rect headerRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            float halfWidth = (headerRect.width - 24f) * 0.5f;

            GUI.Label(new Rect(headerRect.x, headerRect.y, halfWidth, headerRect.height), "Current", EditorStyles.miniBoldLabel);
            GUI.Label(
                new Rect(headerRect.x + halfWidth + 24f, headerRect.y, halfWidth, headerRect.height),
                "Result",
                EditorStyles.miniBoldLabel);

            _previewScroll = EditorGUILayout.BeginScrollView(
                _previewScroll,
                GUILayout.ExpandHeight(true));
            for (int index = 0; index < targets.Count; index++)
            {
                GameObject target = targets[index];
                Rect rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 2f);
                float rowHalfWidth = (rowRect.width - 24f) * 0.5f;

                GUI.Label(
                    new Rect(rowRect.x + 4f, rowRect.y, rowHalfWidth - 4f, rowRect.height),
                    new GUIContent(target.name, GetHierarchyPath(target)),
                    EditorStyles.label);
                GUI.Label(
                    new Rect(rowRect.x + rowHalfWidth, rowRect.y, 24f, rowRect.height),
                    "→",
                    ArrowStyle);
                GUI.Label(
                    new Rect(rowRect.x + rowHalfWidth + 24f, rowRect.y, rowHalfWidth - 4f, rowRect.height),
                    names[index],
                    EditorStyles.label);
            }

            if (targets.Count == 0)
            {
                EditorGUILayout.LabelField("No editable GameObjects selected.", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawFooter(List<GameObject> targets, List<string> names)
        {
            bool hasInvalidName = false;
            bool hasChanges = false;

            for (int index = 0; index < targets.Count; index++)
            {
                hasInvalidName |= string.IsNullOrWhiteSpace(names[index]);
                hasChanges |= targets[index].name != names[index];
            }

            if (hasInvalidName)
            {
                EditorGUILayout.HelpBox("Resulting names cannot be empty.", MessageType.Error);
            }

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Close", GUILayout.Width(90f), GUILayout.Height(24f)))
                {
                    Close();
                }

                using (new EditorGUI.DisabledScope(targets.Count == 0 || hasInvalidName || !hasChanges))
                {
                    if (GUILayout.Button("Apply Rename", GUILayout.Width(110f), GUILayout.Height(24f)))
                    {
                        ApplyNames(targets, names);
                    }
                }
            }
        }

        private List<string> BuildNames(List<GameObject> targets)
        {
            List<string> names = new(targets.Count);
            int number = _numberStart;

            for (int index = 0; index < targets.Count; index++)
            {
                string name = _useSharedBaseName ? _sharedBaseName : targets[index].name;
                if (!string.IsNullOrEmpty(_find))
                {
                    name = name.Replace(_find, _replace);
                }

                name = $"{_prefix}{name}{_suffix}";
                if (_addNumbering)
                {
                    string formattedNumber = number.ToString($"D{Mathf.Clamp(_numberPadding, 1, MaximumPadding)}");
                    name = _numberPlacement == HierarchyNumberPlacement.Prefix
                        ? $"{formattedNumber}{_numberSeparator}{name}"
                        : $"{name}{_numberSeparator}{formattedNumber}";
                }

                names.Add(name);
                number += _numberStep;
            }

            return names;
        }

        private void ApplyNames(List<GameObject> targets, List<string> names)
        {
            Undo.RecordObjects(targets.ToArray(), "Bulk Rename GameObjects");

            for (int index = 0; index < targets.Count; index++)
            {
                GameObject target = targets[index];
                target.name = names[index];
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                EditorUtility.SetDirty(target);
            }

            EditorApplication.RepaintHierarchyWindow();
            ShowNotification(new GUIContent($"Renamed {targets.Count} object{(targets.Count == 1 ? string.Empty : "s")}."));
        }

        private void CaptureSelection(GameObject[] selection)
        {
            List<int> selectionIds = new(selection.Length);
            for (int index = 0; index < selection.Length; index++)
            {
                GameObject gameObject = selection[index];
                if (gameObject != null)
                {
                    selectionIds.Add(gameObject.GetInstanceID());
                }
            }

            _selectionIds = selectionIds.ToArray();
            RebuildTargets();
        }

        private void RebuildTargets()
        {
            List<GameObject> selection = ResolveObjects(_selectionIds);
            List<GameObject> targets = new();
            HashSet<int> included = new();

            for (int index = 0; index < selection.Count; index++)
            {
                AddTarget(selection[index], targets, included);
                if (_includeDescendants)
                {
                    AddDescendants(selection[index].transform, targets, included);
                }
            }

            targets.Sort((left, right) => string.CompareOrdinal(GetHierarchyKey(left), GetHierarchyKey(right)));
            _targetIds = GetInstanceIds(targets);
            Repaint();
        }

        private static void AddDescendants(Transform parent, List<GameObject> targets, HashSet<int> included)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                AddTarget(child.gameObject, targets, included);
                AddDescendants(child, targets, included);
            }
        }

        private static void AddTarget(GameObject gameObject, List<GameObject> targets, HashSet<int> included)
        {
            if (gameObject != null && included.Add(gameObject.GetInstanceID()))
            {
                targets.Add(gameObject);
            }
        }

        private static List<GameObject> ResolveObjects(int[] instanceIds)
        {
            List<GameObject> objects = new(instanceIds.Length);
            for (int index = 0; index < instanceIds.Length; index++)
            {
#pragma warning disable CS0618
                GameObject gameObject = EditorUtility.InstanceIDToObject(instanceIds[index]) as GameObject;
#pragma warning restore CS0618
                if (gameObject != null)
                {
                    objects.Add(gameObject);
                }
            }

            return objects;
        }

        private static int[] GetInstanceIds(List<GameObject> gameObjects)
        {
            int[] instanceIds = new int[gameObjects.Count];
            for (int index = 0; index < gameObjects.Count; index++)
            {
                instanceIds[index] = gameObjects[index].GetInstanceID();
            }

            return instanceIds;
        }

        private static string GetHierarchyKey(GameObject gameObject)
        {
            string key = string.IsNullOrEmpty(gameObject.scene.path)
                ? gameObject.scene.name
                : gameObject.scene.path;

            List<int> indices = new();
            Transform current = gameObject.transform;
            while (current != null)
            {
                indices.Add(current.GetSiblingIndex());
                current = current.parent;
            }

            for (int index = indices.Count - 1; index >= 0; index--)
            {
                key += $"/{indices[index]:D8}";
            }

            return key;
        }

        private static string GetHierarchyPath(GameObject gameObject)
        {
            string path = gameObject.name;
            Transform current = gameObject.transform.parent;

            while (current != null)
            {
                path = $"{current.name}/{path}";
                current = current.parent;
            }

            return $"{gameObject.scene.name}/{path}";
        }
    }
}
