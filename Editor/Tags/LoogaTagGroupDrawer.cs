using System.Collections.Generic;
using LoogaSoft.Tags.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Tags.Editor
{
    [CustomPropertyDrawer(typeof(LoogaTagGroup))]
    public class LoogaTagGroupDrawer : PropertyDrawer
    {
        private const int TagFontSize = 10;
        private const int AddTagFontSize = 14;
        private const int PendingTagTextInset = 5;
        private const float PillHeight = 18f;
        private const float PillHorizontalPadding = 12f;
        private const float PillSpacing = 4f;

        private GUIStyle _labelStyle;
        private GUIStyle _pendingTagFieldStyle;
        private Texture2D _pillTexture;
        private LoogaTagDatabase _cachedDb;
        private readonly HashSet<string> _currentSelection = new();
        private readonly List<EditorWindow> _monitoredWindows = new();
        private UnityEngine.Object _editingTarget;
        private EditorWindow _editingWindow;
        private string _editingPropertyPath;
        private string _pendingTagName = string.Empty;
        private Rect _pendingTagScreenRect;
        private int _editingTargetId;
        private bool _completeTagScheduled;
        private bool _requestTagNameFocus;
        private bool _tagNameHadFocus;

        private readonly float _singleLineHeight = EditorGUIUtility.singleLineHeight;
        private readonly float _verticalSpacing = EditorGUIUtility.standardVerticalSpacing;
        
        private void InitStyles()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = TagFontSize,
                    fontStyle = FontStyle.Normal
                };
            }

            if (_pendingTagFieldStyle == null)
            {
                _pendingTagFieldStyle = new GUIStyle(EditorStyles.textField)
                {
                    alignment = TextAnchor.MiddleLeft,
                    border = new RectOffset(),
                    margin = new RectOffset(),
                    padding = new RectOffset(PendingTagTextInset, 7, 0, 0)
                };

                _pendingTagFieldStyle.normal.background = null;
                _pendingTagFieldStyle.hover.background = null;
                _pendingTagFieldStyle.active.background = null;
                _pendingTagFieldStyle.focused.background = null;
            }

            if (_pillTexture == null)
                _pillTexture = Texture2D.whiteTexture;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            InitStyles();
            
            bool hasLabel = label != null && !string.IsNullOrEmpty(label.text);
            float totalHeight = hasLabel ? _singleLineHeight + _verticalSpacing : 0f;
            totalHeight += DoPillLayout(Rect.zero, property, true);
            
            if (hasLabel)
                totalHeight += _singleLineHeight + _verticalSpacing * 2f;
            
            return totalHeight;
        }
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Event e = Event.current;

            if (e.type == EventType.Layout && EditorWindow.mouseOverWindow != null)
            {
                EditorWindow.mouseOverWindow.wantsMouseMove = true;
            }

            if (e.type == EventType.MouseMove && EditorWindow.mouseOverWindow != null)
            {
                EditorWindow.mouseOverWindow.Repaint();
            }
            
            InitStyles();
            
            label = EditorGUI.BeginProperty(position, label, property);
            bool hasLabel = label != null && !string.IsNullOrEmpty(label.text);
            float currentY = position.y;

            if (hasLabel)
            {
                Rect labelRect = new Rect(position.x, position.y, position.width, _singleLineHeight);
                EditorGUI.LabelField(labelRect, label);
                currentY += _singleLineHeight + _verticalSpacing;
            }

            Rect tagsRect = new Rect(position.x, currentY, position.width, position.height);
            float tagsAreaHeight = DoPillLayout(tagsRect, property, false);

            currentY += tagsAreaHeight + _verticalSpacing * 2f;

            if (hasLabel)
            {
                Rect buttonRect = new Rect(position.x, currentY, position.width, _singleLineHeight);
                SerializedProperty listProp = property.FindPropertyRelative("_selectedTagGuids");

                using (new EditorGUI.DisabledScope(listProp.arraySize == 0))
                {
                    if (GUI.Button(buttonRect, "Clear Tags"))
                    {
                        listProp.ClearArray();
                        property.serializedObject.ApplyModifiedProperties();
                    }
                }
            }
            
            EditorGUI.EndProperty();
        }

        private float DoPillLayout(Rect position, SerializedProperty property, bool calculateHeightOnly)
        {
            if (_cachedDb == null)
                _cachedDb = LoogaTagManager.ValidateDatabase();

            SerializedProperty listProp = property.FindPropertyRelative("_selectedTagGuids");

            float maxWidth = (calculateHeightOnly ? EditorGUIUtility.currentViewWidth - 20f : position.width) - 2f;
            float currentX = 2f;
            float currentY = 0f;
            float totalHeight = 0f;

            _currentSelection.Clear();
            if (listProp != null)
            {
                for (int i = 0; i < listProp.arraySize; i++)
                {
                    _currentSelection.Add(listProp.GetArrayElementAtIndex(i).stringValue);
                }
            }

            if (_cachedDb != null)
            {
                foreach (LoogaTag tag in _cachedDb.Tags)
                {
                    if (string.IsNullOrEmpty(tag.Guid))
                    {
                        continue;
                    }
                    
                    DrawSinglePill(tag.Name, tag.Color, tag.Guid, ref currentX, ref currentY, maxWidth, position, _currentSelection, listProp, calculateHeightOnly, false);
                }
            }
            
            if (IsCreatingTag(listProp))
                DrawPendingTagField(ref currentX, ref currentY, maxWidth, position, listProp, calculateHeightOnly);
            else
                DrawSinglePill("+", Color.gray4, "ADD_BUTTON_ID", ref currentX, ref currentY, maxWidth, position, _currentSelection, listProp, calculateHeightOnly, true);

            totalHeight += currentY + PillHeight;

            return totalHeight;
        }

        private void DrawSinglePill(string name, Color color, string guid, ref float currentX, ref float currentY, float maxWidth, Rect startRect, HashSet<string> selection, SerializedProperty listProp,
            bool calcOnly, bool isAddButton)
        {
            int originalTextSize = _labelStyle.fontSize;
            if (isAddButton) 
                _labelStyle.fontSize = AddTagFontSize;
            
            GUIContent content = new GUIContent(name);
            Vector2 textSize = _labelStyle.CalcSize(content);
            
            float pillWidth = SnapToPixel(textSize.x + PillHorizontalPadding);

            if (isAddButton)
                pillWidth -= 4f;
            
            if (currentX + pillWidth > maxWidth)
            {
                currentX = 2f;
                currentY += PillHeight;
            }

            if (!calcOnly)
            {
                Rect pillRect = SnapToPixelGrid(
                    new Rect(startRect.x + currentX, startRect.y + currentY, pillWidth, PillHeight));
                bool isSelected = !isAddButton && selection.Contains(guid);
                bool isHovered = pillRect.Contains(Event.current.mousePosition);
                float outlineThickness = PillOutlineThickness;
                
                Rect outlineRect = new Rect(
                    pillRect.x - outlineThickness,
                    pillRect.y - outlineThickness,
                    pillRect.width + (outlineThickness * 2f),
                    pillRect.height + (outlineThickness * 2f)
                );
                
                Color outlineColor = Color.gray2;
                if (isSelected)
                    outlineColor = Color.gray8;
                else if (isHovered)
                    outlineColor = Color.gray6;
                
                DrawPillRect(outlineRect, outlineColor);
                DrawPillRect(pillRect, color);

                Color textColor = isAddButton ? Color.white : GetReadableColor(color);

                Rect labelRect = pillRect;
                if (isAddButton)
                    labelRect.y -= 1f;
                
                DrawContrastLabel(labelRect, name, _labelStyle, textColor);

                Event currentEvent = Event.current;
                bool opensContextMenu =
                    currentEvent.type == EventType.ContextClick ||
                    currentEvent.type == EventType.MouseDown && currentEvent.button == 1;
                if (!isAddButton &&
                    opensContextMenu &&
                    pillRect.Contains(currentEvent.mousePosition))
                {
                    ShowTagContextMenu(name, guid, listProp);
                    currentEvent.Use();
                    return;
                }

                if (GUI.Button(pillRect, "", GUIStyle.none))
                {
                    if (isAddButton)
                    {
                        BeginTagCreation(listProp);
                    }
                    else
                    {
                        if (isSelected)
                        {
                            for (int i = 0; i < listProp.arraySize; i++)
                            {
                                if (listProp.GetArrayElementAtIndex(i).stringValue == guid)
                                {
                                    listProp.DeleteArrayElementAtIndex(i);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            listProp.InsertArrayElementAtIndex(listProp.arraySize);
                            listProp.GetArrayElementAtIndex(listProp.arraySize - 1).stringValue = guid;
                        }
                        
                        listProp.serializedObject.ApplyModifiedProperties();
                    }
                }
            }
            
            if (isAddButton) 
                _labelStyle.fontSize = originalTextSize;
            
            currentX += pillWidth + PillSpacing;
        }

        private void DrawPendingTagField(ref float currentX, ref float currentY, float maxWidth,
            Rect startRect, SerializedProperty listProp, bool calculateHeightOnly)
        {
            const float minimumWidth = 60f;
            const float textPadding = 16f;

            float textWidth = _pendingTagFieldStyle.CalcSize(new GUIContent(_pendingTagName)).x;
            float fieldWidth = SnapToPixel(Mathf.Clamp(textWidth + textPadding, minimumWidth, maxWidth - 2f));
            if (currentX + fieldWidth > maxWidth)
            {
                currentX = 2f;
                currentY += PillHeight;
            }

            if (!calculateHeightOnly)
            {
                Rect pillRect = SnapToPixelGrid(
                    new Rect(startRect.x + currentX, startRect.y + currentY, fieldWidth, PillHeight));
                _pendingTagScreenRect = GUIUtility.GUIToScreenRect(pillRect);
                float outlineThickness = PillOutlineThickness;
                Rect outlineRect = new(
                    pillRect.x - outlineThickness,
                    pillRect.y - outlineThickness,
                    pillRect.width + outlineThickness * 2f,
                    pillRect.height + outlineThickness * 2f);

                DrawPillRect(outlineRect, Color.gray6);
                DrawPillRect(pillRect, Color.gray4);

                string controlName = $"LoogaTagName_{_editingTargetId}";
                Event currentEvent = Event.current;
                bool fieldHasFocus = GUI.GetNameOfFocusedControl() == controlName;
                if (currentEvent.type == EventType.KeyDown && GUI.GetNameOfFocusedControl() == controlName)
                {
                    if (currentEvent.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                    {
                        CommitPendingTag(listProp);
                        currentEvent.Use();
                        return;
                    }

                    if (currentEvent.keyCode == KeyCode.Escape)
                    {
                        EndTagCreation();
                        currentEvent.Use();
                        return;
                    }
                }

                else if (currentEvent.type == EventType.MouseDown &&
                         !pillRect.Contains(currentEvent.mousePosition) &&
                         (_tagNameHadFocus || fieldHasFocus))
                {
                    CompleteTagCreation(listProp);
                    return;
                }

                Rect fieldRect = SnapToPixelGrid(new Rect(
                    pillRect.x + 7f,
                    pillRect.y + 1f,
                    pillRect.width - 14f,
                    pillRect.height - 2f));

                GUI.SetNextControlName(controlName);
                _pendingTagName = EditorGUI.TextField(fieldRect, _pendingTagName, _pendingTagFieldStyle);

                if (_requestTagNameFocus)
                {
                    EditorGUI.FocusTextInControl(controlName);
                    _requestTagNameFocus = false;
                }


                fieldHasFocus = GUI.GetNameOfFocusedControl() == controlName;
                if (fieldHasFocus)
                    _tagNameHadFocus = true;
                else if (_tagNameHadFocus && currentEvent.type == EventType.Repaint)
                {
                    CompleteTagCreation(listProp);
                    return;
                }
            }

            currentX += fieldWidth + PillSpacing;
        }

        private void ShowTagContextMenu(string tagName, string tagGuid, SerializedProperty listProp)
        {
            UnityEngine.Object target = listProp.serializedObject.targetObject;
            string propertyPath = listProp.propertyPath;
            GenericMenu menu = new();
            menu.AddItem(
                new GUIContent("Delete Tag"),
                false,
                () => DeleteTag(tagName, tagGuid, target, propertyPath));
            menu.ShowAsContext();
        }

        private void DeleteTag(
            string tagName,
            string tagGuid,
            UnityEngine.Object target,
            string propertyPath)
        {
            if (_cachedDb == null || string.IsNullOrEmpty(tagGuid))
                return;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Delete Looga Tag '{tagName}'");
            Undo.RecordObject(_cachedDb, "Delete Looga Tag");
            _cachedDb.Tags.RemoveAll(tag => tag.Guid == tagGuid);
            EditorUtility.SetDirty(_cachedDb);
            AssetDatabase.SaveAssetIfDirty(_cachedDb);

            if (target != null && !string.IsNullOrEmpty(propertyPath))
            {
                Undo.RecordObject(target, "Remove Looga Tag Reference");
                SerializedObject serializedObject = new(target);
                serializedObject.Update();
                SerializedProperty tags = serializedObject.FindProperty(propertyPath);
                RemoveTagReference(tags, tagGuid);
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorWindow.focusedWindow?.Repaint();
        }

        private static void RemoveTagReference(SerializedProperty tags, string tagGuid)
        {
            if (tags == null || !tags.isArray)
                return;

            for (int i = tags.arraySize - 1; i >= 0; i--)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tagGuid)
                    tags.DeleteArrayElementAtIndex(i);
            }
        }

        private void BeginTagCreation(SerializedProperty listProp)
        {
            _editingTarget = listProp.serializedObject.targetObject;
            _editingTargetId = _editingTarget.GetInstanceID();
            _editingPropertyPath = listProp.propertyPath;
            _pendingTagName = string.Empty;
            _requestTagNameFocus = true;
            _tagNameHadFocus = false;
            BeginEditorFocusMonitoring();
            EditorWindow.focusedWindow?.Repaint();
        }

        private void BeginEditorFocusMonitoring()
        {
            EndEditorFocusMonitoring();
            _editingWindow = EditorWindow.focusedWindow ?? EditorWindow.mouseOverWindow;

            EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                EditorWindow window = windows[i];
                if (window == null || window.rootVisualElement == null)
                    continue;

                window.rootVisualElement.RegisterCallback<PointerDownEvent>(
                    OnEditorPointerDown,
                    TrickleDown.TrickleDown);
                _monitoredWindows.Add(window);
            }

            EditorApplication.update -= MonitorEditorFocus;
            EditorApplication.update += MonitorEditorFocus;
        }

        private void MonitorEditorFocus()
        {
            if (_editingTargetId == 0)
            {
                EndEditorFocusMonitoring();
                return;
            }

            if (_editingWindow != null && EditorWindow.focusedWindow != _editingWindow)
                ScheduleCompleteTagCreation();
        }

        private void OnEditorPointerDown(PointerDownEvent evt)
        {
            if (_editingTargetId == 0 || evt.button != 0)
                return;

            EditorWindow clickedWindow = FindMonitoredWindow(evt.currentTarget as VisualElement);
            if (clickedWindow == _editingWindow)
            {
                Vector2 screenPosition = clickedWindow.position.position + (Vector2)evt.position;
                if (_pendingTagScreenRect.Contains(screenPosition))
                    return;
            }

            ScheduleCompleteTagCreation();
        }

        private EditorWindow FindMonitoredWindow(VisualElement root)
        {
            for (int i = 0; i < _monitoredWindows.Count; i++)
            {
                EditorWindow window = _monitoredWindows[i];
                if (window != null && window.rootVisualElement == root)
                    return window;
            }

            return null;
        }

        private void ScheduleCompleteTagCreation()
        {
            if (_completeTagScheduled)
                return;

            _completeTagScheduled = true;
            EditorApplication.delayCall += CompleteStoredTagCreation;
        }

        private void CompleteStoredTagCreation()
        {
            _completeTagScheduled = false;
            if (_editingTargetId == 0)
                return;

            if (_editingTarget == null || string.IsNullOrEmpty(_editingPropertyPath))
            {
                EndTagCreation();
                return;
            }

            SerializedObject serializedObject = new(_editingTarget);
            serializedObject.Update();
            SerializedProperty listProp = serializedObject.FindProperty(_editingPropertyPath);
            if (listProp == null)
            {
                EndTagCreation();
                return;
            }

            CompleteTagCreation(listProp);
        }

        private void EndEditorFocusMonitoring()
        {
            EditorApplication.update -= MonitorEditorFocus;
            EditorApplication.delayCall -= CompleteStoredTagCreation;
            _completeTagScheduled = false;
            for (int i = 0; i < _monitoredWindows.Count; i++)
            {
                EditorWindow window = _monitoredWindows[i];
                if (window == null || window.rootVisualElement == null)
                    continue;

                window.rootVisualElement.UnregisterCallback<PointerDownEvent>(
                    OnEditorPointerDown,
                    TrickleDown.TrickleDown);
            }

            _monitoredWindows.Clear();
            _editingWindow = null;
        }

        private void CompleteTagCreation(SerializedProperty listProp)
        {
            if (string.IsNullOrWhiteSpace(_pendingTagName))
                EndTagCreation();
            else
                CommitPendingTag(listProp);
        }

        private void CommitPendingTag(SerializedProperty listProp)
        {
            string tagName = _pendingTagName.Trim();
            if (string.IsNullOrEmpty(tagName) || _cachedDb == null)
                return;

            string tagGuid = null;
            foreach (LoogaTag existingTag in _cachedDb.Tags)
            {
                if (!string.IsNullOrEmpty(existingTag.Guid) &&
                    string.Equals(existingTag.Name, tagName, System.StringComparison.OrdinalIgnoreCase))
                {
                    tagGuid = existingTag.Guid;
                    break;
                }
            }

            if (string.IsNullOrEmpty(tagGuid))
            {
                tagGuid = System.Guid.NewGuid().ToString("N");
                Undo.RecordObject(_cachedDb, "Create Looga Tag");
                _cachedDb.Tags.Add(new LoogaTag
                {
                    Name = tagName,
                    Color = Color.gray3,
                    Guid = tagGuid
                });
                EditorUtility.SetDirty(_cachedDb);
                AssetDatabase.SaveAssetIfDirty(_cachedDb);
            }

            for (int i = 0; i < listProp.arraySize; i++)
            {
                if (listProp.GetArrayElementAtIndex(i).stringValue == tagGuid)
                {
                    EndTagCreation();
                    return;
                }
            }

            Undo.RecordObject(listProp.serializedObject.targetObject, "Assign Looga Tag");
            listProp.InsertArrayElementAtIndex(listProp.arraySize);
            listProp.GetArrayElementAtIndex(listProp.arraySize - 1).stringValue = tagGuid;
            listProp.serializedObject.ApplyModifiedProperties();
            EndTagCreation();
        }

        private bool IsCreatingTag(SerializedProperty listProp)
        {
            return listProp != null &&
                   listProp.serializedObject.targetObject != null &&
                   _editingTargetId == listProp.serializedObject.targetObject.GetInstanceID() &&
                   _editingPropertyPath == listProp.propertyPath;
        }

        private void EndTagCreation()
        {
            EndEditorFocusMonitoring();
            _editingTarget = null;
            _editingTargetId = 0;
            _editingPropertyPath = null;
            _pendingTagName = string.Empty;
            _pendingTagScreenRect = default;
            _completeTagScheduled = false;
            _requestTagNameFocus = false;
            _tagNameHadFocus = false;
            GUIUtility.keyboardControl = 0;
            EditorWindow.focusedWindow?.Repaint();
        }

        private Color GetReadableColor(Color color)
        {
            float luminance = (color.r * 0.299f) + (color.g * 0.587f) + (color.b * 0.114f);
            return luminance > 0.5f ? Color.black : Color.white;
        }

        private void DrawContrastLabel(Rect rect, string text, GUIStyle style, Color textColor)
        {
            Color oldColor = style.normal.textColor;
            style.normal.textColor = textColor;
            GUI.Label(rect, text, style);
            style.normal.textColor = oldColor;
        }
        private void DrawPillRect(Rect rect, Color color)
        {
            rect = SnapToPixelGrid(rect);
            Color oldColor = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, 1f);
            GUI.DrawTexture(rect, _pillTexture, ScaleMode.StretchToFill, true, 10f, GUI.color, 0f, rect.height / 2f);
            GUI.color = oldColor;
        }

        private static float PhysicalPixel => 1f / Mathf.Max(1f, EditorGUIUtility.pixelsPerPoint);
        private static float PillOutlineThickness => PhysicalPixel * 2f;

        private static float SnapToPixel(float value)
        {
            float scale = Mathf.Max(1f, EditorGUIUtility.pixelsPerPoint);
            return Mathf.Round(value * scale) / scale;
        }

        private static Rect SnapToPixelGrid(Rect rect)
        {
            float xMin = SnapToPixel(rect.xMin);
            float yMin = SnapToPixel(rect.yMin);
            float xMax = SnapToPixel(rect.xMax);
            float yMax = SnapToPixel(rect.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}
