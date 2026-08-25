using System;
using System.Collections.Generic;
using LoogaSoft.Tags.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Tags.Editor
{
    /// <summary>
    /// Supplies the retained-mode Looga Tags panel used by the shared GameObject inspector toolbar.
    /// The runtime component exists only while the object has at least one selected tag.
    /// </summary>
    public static class LoogaTagsOverlay
    {
        private const string TagGroupPropertyName = "_tagGroup";
        private const string SelectedTagsPropertyName = "_selectedTagGuids";

        private static readonly HashSet<LoogaTags> PendingRemovals = new();
        private static bool _removalScheduled;

        public static VisualElement CreateToolbarPanel()
        {
            return new TagPanelState();
        }

        public static void BindToolbarPanel(VisualElement panel, GameObject gameObject)
        {
            if (panel is TagPanelState state)
                state.Bind(gameObject);
        }

        private static List<string> ReadAssignedTags(GameObject gameObject)
        {
            if (gameObject == null || !gameObject.TryGetComponent(out LoogaTags tags))
                return new List<string>();

            IReadOnlyList<string> assignedTags = tags.TagGroup.SelectedTagGuids;
            if (assignedTags == null || assignedTags.Count == 0)
                return new List<string>();

            List<string> result = new(assignedTags.Count);
            for (int index = 0; index < assignedTags.Count; index++)
            {
                if (!string.IsNullOrEmpty(assignedTags[index]))
                    result.Add(assignedTags[index]);
            }

            return result;
        }

        private static void WriteAssignedTags(GameObject gameObject, IReadOnlyCollection<string> tagGuids)
        {
            if (gameObject == null)
                return;

            LoogaTags tags = gameObject.GetComponent<LoogaTags>();
            if (tagGuids.Count == 0)
            {
                if (tags != null)
                {
                    Undo.RecordObject(tags, "Clear Looga Tags");

                    SerializedObject clearedTags = new(tags);
                    clearedTags.Update();
                    SerializedProperty clearedSelection = clearedTags
                        .FindProperty(TagGroupPropertyName)
                        .FindPropertyRelative(SelectedTagsPropertyName);

                    clearedSelection.arraySize = 0;
                    clearedTags.ApplyModifiedProperties();
                    EditorUtility.SetDirty(tags);
                    ScheduleRemoval(tags);
                }

                return;
            }

            if (tags == null)
                tags = Undo.AddComponent<LoogaTags>(gameObject);
            else
                Undo.RecordObject(tags, "Change Looga Tags");

            SerializedObject serializedTags = new(tags);
            serializedTags.Update();
            SerializedProperty selectedTags = serializedTags
                .FindProperty(TagGroupPropertyName)
                .FindPropertyRelative(SelectedTagsPropertyName);

            selectedTags.arraySize = tagGuids.Count;
            int index = 0;
            foreach (string tagGuid in tagGuids)
                selectedTags.GetArrayElementAtIndex(index++).stringValue = tagGuid;

            serializedTags.ApplyModifiedProperties();
            EditorUtility.SetDirty(tags);
        }

        private static void ScheduleRemoval(LoogaTags tags)
        {
            if (tags == null)
                return;

            PendingRemovals.Add(tags);
            if (_removalScheduled)
                return;

            _removalScheduled = true;
            EditorApplication.delayCall += RemoveEmptyTagComponents;
        }

        private static void RemoveEmptyTagComponents()
        {
            EditorApplication.delayCall -= RemoveEmptyTagComponents;
            _removalScheduled = false;

            foreach (LoogaTags tags in PendingRemovals)
            {
                if (tags == null || tags.TagGroup.SelectedTagGuids is { Count: > 0 })
                    continue;

                Undo.DestroyObjectImmediate(tags);
            }

            PendingRemovals.Clear();
        }

        private sealed class TagPanelState : VisualElement
        {
            private const float PillHeight = 18f;
            private const float PillSpacing = 4f;
            private const float PillHorizontalPadding = 6f;
            private const float MinimumNameFieldWidth = 60f;
            private const float MaximumNameFieldWidth = 220f;

            private readonly HashSet<string> _selection = new(StringComparer.Ordinal);
            private GameObject _target;
            private TextField _pendingNameField;
            private bool _creationHadFocus;
            private bool _endingCreation;
            private bool _eventsRegistered;

            public TagPanelState()
            {
                name = "Looga Tags Panel";
                pickingMode = PickingMode.Position;
                style.flexDirection = FlexDirection.Row;
                style.flexWrap = Wrap.Wrap;
                style.flexGrow = 1f;
                style.flexShrink = 0f;
                style.marginLeft = 0f;
                style.marginRight = 0f;
                style.marginTop = 0f;
                style.marginBottom = 0f;
                style.paddingLeft = 0f;
                style.paddingRight = 0f;
                style.paddingTop = 0f;
                style.paddingBottom = 0f;

                RegisterCallback<AttachToPanelEvent>(_ => RegisterEditorEvents());
                RegisterCallback<DetachFromPanelEvent>(_ => UnregisterEditorEvents());
            }

            public void Bind(GameObject gameObject)
            {
                if (_target == gameObject)
                    return;

                CompleteTagCreation();
                _target = gameObject;
                Rebuild();
            }

            private void RegisterEditorEvents()
            {
                if (_eventsRegistered)
                    return;

                _eventsRegistered = true;
                Undo.undoRedoPerformed += ScheduleRebuild;
                EditorApplication.projectChanged += ScheduleRebuild;
                EditorApplication.hierarchyChanged += ScheduleRebuild;
            }

            private void UnregisterEditorEvents()
            {
                if (!_eventsRegistered)
                    return;

                _eventsRegistered = false;
                Undo.undoRedoPerformed -= ScheduleRebuild;
                EditorApplication.projectChanged -= ScheduleRebuild;
                EditorApplication.hierarchyChanged -= ScheduleRebuild;
                StopFocusMonitoring();
            }

            private void ScheduleRebuild()
            {
                if (panel == null)
                    return;

                schedule.Execute(Rebuild).ExecuteLater(0);
            }

            private void Rebuild()
            {
                if (_pendingNameField != null || _endingCreation)
                    return;

                Clear();
                _selection.Clear();
                List<string> assignedTags = ReadAssignedTags(_target);
                for (int index = 0; index < assignedTags.Count; index++)
                    _selection.Add(assignedTags[index]);

                LoogaTagDatabase database = LoogaTagManager.ValidateDatabase();
                if (database != null)
                {
                    for (int index = 0; index < database.Tags.Count; index++)
                    {
                        LoogaTag tag = database.Tags[index];
                        if (!string.IsNullOrEmpty(tag.Guid))
                            Add(CreateTagPill(tag));
                    }
                }

                Add(CreateUtilityGroup());
            }

            private Button CreateTagPill(LoogaTag tag)
            {
                bool selected = _selection.Contains(tag.Guid);
                Button pill = new(() => ToggleTag(tag.Guid))
                {
                    text = string.IsNullOrWhiteSpace(tag.Name) ? "Unnamed" : tag.Name,
                    tooltip = tag.Name
                };

                StylePill(pill, tag.Color, ReadableTextColor(tag.Color));
                ApplyPillBorder(pill, selected, false);
                pill.RegisterCallback<MouseEnterEvent>(_ => ApplyPillBorder(pill, selected, true));
                pill.RegisterCallback<MouseLeaveEvent>(_ => ApplyPillBorder(pill, selected, false));
                pill.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    evt.menu.AppendAction(
                        "Delete Tag",
                        _ => DeleteTag(tag.Guid),
                        DropdownMenuAction.AlwaysEnabled);
                }));
                return pill;
            }

            private VisualElement CreateUtilityGroup()
            {
                VisualElement group = new();
                group.style.flexDirection = FlexDirection.Row;
                group.style.height = PillHeight;
                group.style.marginRight = PillSpacing;
                group.style.marginBottom = 0f;

                Button clearButton = new(ClearTags)
                {
                    text = "Clear",
                    tooltip = "Clear all Looga Tags"
                };
                StyleUtilityButton(clearButton);
                clearButton.SetEnabled(_selection.Count > 0);

                Button addButton = new(BeginTagCreation)
                {
                    text = "+",
                    tooltip = "Create and assign a Looga Tag"
                };
                StyleUtilityButton(addButton);
                addButton.style.marginLeft = PillSpacing;
                addButton.style.fontSize = 14f;
                addButton.style.width = 24f;

                group.Add(clearButton);
                group.Add(addButton);
                return group;
            }

            private static void StyleUtilityButton(Button button)
            {
                button.style.height = PillHeight;
                button.style.marginLeft = 0f;
                button.style.marginRight = 0f;
                button.style.marginTop = 0f;
                button.style.marginBottom = 0f;
                button.style.paddingLeft = 6f;
                button.style.paddingRight = 6f;
                button.style.paddingTop = 0f;
                button.style.paddingBottom = 0f;
                button.style.unityTextAlign = TextAnchor.MiddleCenter;
                button.style.fontSize = 10f;
            }

            private static void StylePill(Button pill, Color backgroundColor, Color textColor)
            {
                pill.style.height = PillHeight;
                pill.style.marginLeft = 0f;
                pill.style.marginRight = PillSpacing;
                pill.style.marginTop = 0f;
                pill.style.marginBottom = 0f;
                pill.style.paddingLeft = PillHorizontalPadding;
                pill.style.paddingRight = PillHorizontalPadding;
                pill.style.paddingTop = 0f;
                pill.style.paddingBottom = 0f;
                pill.style.borderTopLeftRadius = PillHeight * 0.5f;
                pill.style.borderTopRightRadius = PillHeight * 0.5f;
                pill.style.borderBottomLeftRadius = PillHeight * 0.5f;
                pill.style.borderBottomRightRadius = PillHeight * 0.5f;
                pill.style.backgroundColor = backgroundColor;
                pill.style.color = textColor;
                pill.style.fontSize = 10f;
                pill.style.unityTextAlign = TextAnchor.MiddleCenter;
                pill.style.flexShrink = 0f;
            }

            private static void ApplyPillBorder(VisualElement pill, bool selected, bool hovered)
            {
                float borderWidth = Mathf.Max(1f, 2f / EditorGUIUtility.pixelsPerPoint);
                Color borderColor = selected
                    ? new Color(0.78f, 0.78f, 0.78f, 1f)
                    : hovered
                        ? new Color(0.56f, 0.56f, 0.56f, 1f)
                        : new Color(0.22f, 0.22f, 0.22f, 1f);

                pill.style.borderLeftWidth = borderWidth;
                pill.style.borderRightWidth = borderWidth;
                pill.style.borderTopWidth = borderWidth;
                pill.style.borderBottomWidth = borderWidth;
                pill.style.borderLeftColor = borderColor;
                pill.style.borderRightColor = borderColor;
                pill.style.borderTopColor = borderColor;
                pill.style.borderBottomColor = borderColor;
            }

            private void ToggleTag(string tagGuid)
            {
                List<string> assignedTags = ReadAssignedTags(_target);
                int existingIndex = assignedTags.IndexOf(tagGuid);
                if (existingIndex >= 0)
                    assignedTags.RemoveAt(existingIndex);
                else
                    assignedTags.Add(tagGuid);

                WriteAssignedTags(_target, assignedTags);
                Rebuild();
            }

            private void ClearTags()
            {
                WriteAssignedTags(_target, Array.Empty<string>());
                Rebuild();
            }

            private void DeleteTag(string tagGuid)
            {
                LoogaTagDatabase database = LoogaTagManager.ValidateDatabase();
                if (database == null)
                    return;

                Undo.RecordObject(database, "Delete Looga Tag");
                database.Tags.RemoveAll(tag => tag.Guid == tagGuid);
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssetIfDirty(database);

                List<string> assignedTags = ReadAssignedTags(_target);
                assignedTags.RemoveAll(guid => guid == tagGuid);
                WriteAssignedTags(_target, assignedTags);
                Rebuild();
            }

            private void BeginTagCreation()
            {
                if (_pendingNameField != null)
                    return;

                VisualElement utilityGroup = this[childCount - 1];
                utilityGroup.RemoveFromHierarchy();

                _pendingNameField = new TextField
                {
                    name = "New Looga Tag Name",
                    tooltip = "Enter a tag name"
                };
                _pendingNameField.style.height = PillHeight;
                _pendingNameField.style.width = MinimumNameFieldWidth;
                _pendingNameField.style.marginLeft = 0f;
                _pendingNameField.style.marginRight = PillSpacing;
                _pendingNameField.style.marginTop = 0f;
                _pendingNameField.style.marginBottom = 0f;
                _pendingNameField.style.flexShrink = 0f;

                VisualElement input = _pendingNameField.Q(className: TextField.inputUssClassName);
                if (input != null)
                {
                    input.style.paddingLeft = 4f;
                    input.style.paddingRight = 4f;
                    input.style.paddingTop = 0f;
                    input.style.paddingBottom = 0f;
                    input.style.borderTopLeftRadius = PillHeight * 0.5f;
                    input.style.borderTopRightRadius = PillHeight * 0.5f;
                    input.style.borderBottomLeftRadius = PillHeight * 0.5f;
                    input.style.borderBottomRightRadius = PillHeight * 0.5f;
                }

                _pendingNameField.RegisterValueChangedCallback(evt => ResizePendingNameField(evt.newValue));
                _pendingNameField.RegisterCallback<FocusInEvent>(_ => _creationHadFocus = true);
                _pendingNameField.RegisterCallback<FocusOutEvent>(_ => ScheduleCompleteTagCreation());
                _pendingNameField.RegisterCallback<KeyDownEvent>(OnPendingNameKeyDown);
                Add(_pendingNameField);

                _creationHadFocus = false;
                EditorApplication.update -= MonitorPendingNameFocus;
                EditorApplication.update += MonitorPendingNameFocus;
                _pendingNameField.schedule.Execute(_pendingNameField.Focus).ExecuteLater(0);
            }

            private void ResizePendingNameField(string value)
            {
                float textWidth = EditorStyles.textField.CalcSize(new GUIContent(value ?? string.Empty)).x;
                _pendingNameField.style.width = Mathf.Clamp(textWidth + 18f, MinimumNameFieldWidth, MaximumNameFieldWidth);
            }

            private void OnPendingNameKeyDown(KeyDownEvent evt)
            {
                if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                {
                    CommitTagCreation();
                    evt.StopImmediatePropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    CancelTagCreation();
                    evt.StopImmediatePropagation();
                }
            }

            private void ScheduleCompleteTagCreation()
            {
                schedule.Execute(() =>
                {
                    if (_pendingNameField != null && _pendingNameField.panel?.focusController.focusedElement != _pendingNameField)
                        CompleteTagCreation();
                }).ExecuteLater(0);
            }

            private void MonitorPendingNameFocus()
            {
                if (_pendingNameField == null)
                {
                    StopFocusMonitoring();
                    return;
                }

                if (_creationHadFocus && _pendingNameField.panel?.focusController.focusedElement != _pendingNameField)
                    CompleteTagCreation();
            }

            private void CompleteTagCreation()
            {
                if (_pendingNameField == null || _endingCreation)
                    return;

                if (string.IsNullOrWhiteSpace(_pendingNameField.value))
                    CancelTagCreation();
                else
                    CommitTagCreation();
            }

            private void CommitTagCreation()
            {
                if (_pendingNameField == null || _endingCreation)
                    return;

                string tagName = _pendingNameField.value?.Trim();
                if (string.IsNullOrEmpty(tagName))
                {
                    CancelTagCreation();
                    return;
                }

                LoogaTagDatabase database = LoogaTagManager.ValidateDatabase();
                if (database == null)
                {
                    CancelTagCreation();
                    return;
                }

                string tagGuid = null;
                for (int index = 0; index < database.Tags.Count; index++)
                {
                    if (string.Equals(database.Tags[index].Name, tagName, StringComparison.OrdinalIgnoreCase))
                    {
                        tagGuid = database.Tags[index].Guid;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(tagGuid))
                {
                    tagGuid = GUID.Generate().ToString();
                    Undo.RecordObject(database, "Create Looga Tag");
                    database.Tags.Add(new LoogaTag
                    {
                        Name = tagName,
                        Color = Color.gray3,
                        Guid = tagGuid
                    });
                    EditorUtility.SetDirty(database);
                    AssetDatabase.SaveAssetIfDirty(database);
                }

                List<string> assignedTags = ReadAssignedTags(_target);
                if (!assignedTags.Contains(tagGuid))
                    assignedTags.Add(tagGuid);

                EndTagCreation();
                WriteAssignedTags(_target, assignedTags);
                Rebuild();
            }

            private void CancelTagCreation()
            {
                if (_pendingNameField == null || _endingCreation)
                    return;

                EndTagCreation();
                Rebuild();
            }

            private void EndTagCreation()
            {
                _endingCreation = true;
                StopFocusMonitoring();
                _pendingNameField?.RemoveFromHierarchy();
                _pendingNameField = null;
                _creationHadFocus = false;
                _endingCreation = false;
            }

            private static Color ReadableTextColor(Color color)
            {
                float luminance = 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
                return luminance > 0.5f ? Color.black : Color.white;
            }

            private void StopFocusMonitoring()
            {
                EditorApplication.update -= MonitorPendingNameFocus;
            }
        }
    }
}
