using System;
using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Inspector.Editor
{
    /// <summary>
    /// Draws Looga lists when a nested property drawer owns the surrounding layout.
    /// </summary>
    [CustomPropertyDrawer(typeof(LoogaListAttribute))]
    public sealed class LoogaListDrawer : PropertyDrawer
    {
        private const float HeaderHeight = 23f;
        private const float HeaderInset = 6f;
        private const float HeaderButtonSize = 18f;
        private const float HeaderGap = 2f;
        private const float SizeFieldWidth = 48f;
        private const float BodyPadding = 5f;
        private const float RowPaddingX = 7f;
        private const float RowPaddingY = 3f;
        private const float RowGap = 1f;
        private const float DragHandleWidth = 16f;
        private const float DeleteButtonWidth = 20f;
        private const float ReorderAnimationSeconds = 0.08f;

        private static string _dragKey = string.Empty;
        private static int _dragIndex = -1;
        private static int _dropIndex = -1;
        private static int _previousDropIndex = -1;
        private static float _dragMouseOffsetY;
        private static double _dropAnimationStartTime;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!property.isArray || property.propertyType == SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            int previousIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            position = EditorGUI.IndentedRect(position);

            LoogaListAttribute listAttribute = GetListAttribute();
            bool alwaysExpanded = listAttribute.Mode == LoogaListMode.AlwaysExpanded;
            if (alwaysExpanded)
            {
                property.isExpanded = true;
            }

            Rect headerRect = LoogaEditorStyle.PixelSnap(
                new Rect(position.x, position.y, position.width, HeaderHeight));
            DrawHeader(headerRect, property, label, alwaysExpanded);

            if (alwaysExpanded || property.isExpanded)
            {
                Rect bodyRect = new(
                    position.x,
                    headerRect.yMax,
                    position.width,
                    GetBodyHeight(property));
                DrawBody(LoogaEditorStyle.PixelSnap(bodyRect), property);
            }
            else if (_dragKey == GetKey(property))
            {
                ClearDrag();
            }

            EditorGUI.indentLevel = previousIndent;
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isArray || property.propertyType == SerializedPropertyType.String)
                return EditorGUI.GetPropertyHeight(property, label, true);

            LoogaListAttribute listAttribute = GetListAttribute();
            bool expanded = listAttribute.Mode == LoogaListMode.AlwaysExpanded || property.isExpanded;
            return HeaderHeight + (expanded ? GetBodyHeight(property) : 0f);
        }

        private LoogaListAttribute GetListAttribute()
        {
            return fieldInfo != null
                ? Attribute.GetCustomAttribute(fieldInfo, typeof(LoogaListAttribute)) as LoogaListAttribute
                    ?? new LoogaListAttribute()
                : new LoogaListAttribute();
        }

        private static void DrawHeader(
            Rect headerRect,
            SerializedProperty property,
            GUIContent label,
            bool alwaysExpanded)
        {
            Event currentEvent = Event.current;
            GUI.Box(headerRect, GUIContent.none, LoogaEditorFoldouts.FoldoutBoxStyle);

            float controlY = LoogaEditorStyle.PixelSnapValue(
                headerRect.y + (headerRect.height - EditorGUIUtility.singleLineHeight) * 0.5f);
            Rect removeRect = new(
                headerRect.xMax - HeaderGap - HeaderButtonSize,
                controlY,
                HeaderButtonSize,
                EditorGUIUtility.singleLineHeight);
            Rect addRect = new(
                removeRect.x - HeaderGap - HeaderButtonSize,
                controlY,
                HeaderButtonSize,
                EditorGUIUtility.singleLineHeight);
            Rect sizeRect = new(
                addRect.x - HeaderGap - SizeFieldWidth,
                controlY,
                SizeFieldWidth,
                EditorGUIUtility.singleLineHeight);
            Rect toggleRect = new(
                headerRect.x,
                headerRect.y,
                Mathf.Max(0f, sizeRect.x - headerRect.x - HeaderGap),
                headerRect.height);

            if (!alwaysExpanded && toggleRect.Contains(currentEvent.mousePosition))
            {
                LoogaEditorFoldouts.DrawHoverRect(headerRect);
                if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
                {
                    property.isExpanded = !property.isExpanded;
                    currentEvent.Use();
                }
            }

            float labelX = headerRect.x + HeaderInset;
            if (!alwaysExpanded)
            {
                Rect arrowRect = new(
                    labelX,
                    headerRect.y + (headerRect.height - LoogaEditorStyle.FoldoutTriangleSize) * 0.5f,
                    LoogaEditorStyle.FoldoutTriangleSize,
                    LoogaEditorStyle.FoldoutTriangleSize);
                LoogaEditorStyle.DrawFoldoutTriangle(arrowRect, property.isExpanded);
                labelX = arrowRect.xMax + HeaderInset;
            }

            Rect labelRect = new(
                labelX,
                headerRect.y + 1f,
                Mathf.Max(0f, toggleRect.xMax - labelX - HeaderInset),
                headerRect.height);
            EditorGUI.LabelField(labelRect, label, EditorStyles.label);

            EditorGUI.BeginChangeCheck();
            int newSize = Mathf.Max(0, EditorGUI.DelayedIntField(sizeRect, property.arraySize));
            if (EditorGUI.EndChangeCheck())
            {
                property.arraySize = newSize;
            }

            if (GUI.Button(addRect, new GUIContent("+", "Add item"), EditorStyles.miniButton))
            {
                property.arraySize++;
                property.isExpanded = true;
                GUI.changed = true;
            }

            using (new EditorGUI.DisabledScope(property.arraySize == 0))
            {
                if (GUI.Button(removeRect, new GUIContent("-", "Remove last item"), EditorStyles.miniButton))
                {
                    DeleteElement(property, property.arraySize - 1);
                    GUI.changed = true;
                }
            }
        }

        private static void DrawBody(Rect bodyRect, SerializedProperty property)
        {
            GUI.Box(bodyRect, GUIContent.none, LoogaEditorFoldouts.SmallBoxStyle);
            if (property.arraySize == 0)
            {
                EditorGUI.LabelField(bodyRect, "Empty", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            string key = GetKey(property);
            Rect contentRect = LoogaEditorStyle.PixelSnap(new Rect(
                bodyRect.x + BodyPadding,
                bodyRect.y + BodyPadding,
                Mathf.Max(0f, bodyRect.width - BodyPadding * 2f),
                Mathf.Max(0f, bodyRect.height - BodyPadding * 2f)));
            HandleDragOver(property, key, contentRect);

            bool dragging = _dragKey == key &&
                _dragIndex >= 0 &&
                _dragIndex < property.arraySize;
            int dropIndex = dragging
                ? Mathf.Clamp(_dropIndex, 0, property.arraySize)
                : -1;
            int previousDropIndex = dragging
                ? Mathf.Clamp(_previousDropIndex, 0, property.arraySize)
                : dropIndex;
            float animationProgress = dragging ? GetReorderAnimationProgress() : 1f;
            float draggedRowHeight = dragging ? GetRowHeight(property, _dragIndex) : 0f;
            SerializedProperty draggedElement = null;
            float draggedElementHeight = 0f;
            float y = contentRect.y;

            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                float elementHeight = EditorGUI.GetPropertyHeight(element, true);
                float rowHeight = LoogaEditorStyle.PixelCeil(elementHeight + RowPaddingY * 2f);

                if (dragging && i == _dragIndex)
                {
                    draggedElement = element;
                    draggedElementHeight = elementHeight;
                    continue;
                }

                float rowY;
                if (dragging)
                {
                    float previousY = GetVisualRowY(
                        property,
                        contentRect,
                        i,
                        _dragIndex,
                        previousDropIndex,
                        draggedRowHeight);
                    float targetY = GetVisualRowY(
                        property,
                        contentRect,
                        i,
                        _dragIndex,
                        dropIndex,
                        draggedRowHeight);
                    rowY = Mathf.Lerp(previousY, targetY, animationProgress);
                }
                else
                {
                    rowY = y;
                    y += rowHeight + LoogaEditorStyle.Pixels(RowGap);
                }

                Rect rowRect = LoogaEditorStyle.PixelSnap(new Rect(
                    contentRect.x,
                    rowY,
                    contentRect.width,
                    rowHeight));

                DrawRow(rowRect, property, element, elementHeight, key, i, false);
            }

            if (dragging && draggedElement != null)
            {
                float draggedY = GetClampedDraggedRowY(
                    contentRect,
                    draggedRowHeight,
                    Event.current.mousePosition.y);
                Rect draggedRowRect = LoogaEditorStyle.PixelSnap(new Rect(
                    contentRect.x,
                    draggedY,
                    contentRect.width,
                    draggedRowHeight));
                DrawRow(
                    draggedRowRect,
                    property,
                    draggedElement,
                    draggedElementHeight,
                    key,
                    _dragIndex,
                    true);
            }
        }

        private static void DrawRow(
            Rect rowRect,
            SerializedProperty property,
            SerializedProperty element,
            float elementHeight,
            string key,
            int index,
            bool dragging)
        {
            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.Repaint)
            {
                Color rowColor = rowRect.Contains(currentEvent.mousePosition)
                    ? Color.Lerp(LoogaEditorStyle.ListRowColor, LoogaEditorStyle.ListHoverColor, 0.65f)
                    : LoogaEditorStyle.ListRowColor;
                if (dragging)
                {
                    rowColor = Color.Lerp(rowColor, LoogaEditorStyle.SelectionColor, 0.55f);
                }

                EditorGUI.DrawRect(rowRect, rowColor);
            }

            Rect dragRect = new(
                rowRect.x + RowPaddingX,
                rowRect.y,
                DragHandleWidth,
                rowRect.height);
            Rect deleteRect = new(
                rowRect.xMax - RowPaddingX - DeleteButtonWidth,
                rowRect.y + (rowRect.height - EditorGUIUtility.singleLineHeight) * 0.5f,
                DeleteButtonWidth,
                EditorGUIUtility.singleLineHeight);
            Rect elementRect = new(
                dragRect.xMax + RowPaddingX,
                rowRect.y + RowPaddingY,
                Mathf.Max(0f, deleteRect.x - dragRect.xMax - RowPaddingX * 2f),
                elementHeight);

            DrawDragHandle(dragRect);
            EditorGUIUtility.AddCursorRect(dragRect, MouseCursor.MoveArrow);
            EditorGUI.PropertyField(elementRect, element, true);

            if (GUI.Button(deleteRect, new GUIContent("-", "Remove item"), EditorStyles.miniButton))
            {
                if (_dragKey == key)
                {
                    ClearDrag();
                }

                DeleteElement(property, index);
                GUI.changed = true;
                return;
            }

            if (!dragging)
            {
                BeginDrag(key, index, dragRect, rowRect);
            }
        }

        private static void DrawDragHandle(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            float centerX = LoogaEditorStyle.PixelSnapValue(rect.center.x);
            float startY = LoogaEditorStyle.PixelSnapValue(rect.center.y - LoogaEditorStyle.Pixels(3f));
            for (int i = 0; i < 3; i++)
            {
                Rect lineRect = new(
                    centerX - LoogaEditorStyle.Pixels(4f),
                    startY + LoogaEditorStyle.Pixels(i * 3f),
                    LoogaEditorStyle.Pixels(8f),
                    LoogaEditorStyle.Pixels(1f));
                EditorGUI.DrawRect(LoogaEditorStyle.PixelSnap(lineRect), LoogaEditorStyle.DragHandleColor);
            }
        }

        private static void BeginDrag(
            string key,
            int index,
            Rect dragRect,
            Rect rowRect)
        {
            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown &&
                currentEvent.button == 0 &&
                dragRect.Contains(currentEvent.mousePosition))
            {
                _dragKey = key;
                _dragIndex = index;
                _dropIndex = index;
                _previousDropIndex = index;
                _dragMouseOffsetY = currentEvent.mousePosition.y - rowRect.y;
                _dropAnimationStartTime = EditorApplication.timeSinceStartup;
                currentEvent.Use();
            }
        }

        private static void HandleDragOver(
            SerializedProperty property,
            string key,
            Rect contentRect)
        {
            Event currentEvent = Event.current;
            if (_dragKey != key || _dragIndex < 0)
                return;

            if (currentEvent.type == EventType.MouseDrag)
            {
                int newDropIndex = GetDropIndex(
                    property,
                    contentRect,
                    currentEvent.mousePosition.y,
                    _dragIndex);
                if (newDropIndex != _dropIndex)
                {
                    _previousDropIndex = _dropIndex;
                    _dropIndex = newDropIndex;
                    _dropAnimationStartTime = EditorApplication.timeSinceStartup;
                }

                GUI.changed = true;
                RepaintInspector();
                currentEvent.Use();
                return;
            }

            if (currentEvent.type != EventType.MouseUp)
                return;

            CommitDrag(property);
            currentEvent.Use();
        }

        private static float GetBodyHeight(SerializedProperty property)
        {
            if (property.arraySize == 0)
                return BodyPadding * 2f + EditorGUIUtility.singleLineHeight;

            float height = BodyPadding * 2f;
            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                height += LoogaEditorStyle.PixelCeil(
                    EditorGUI.GetPropertyHeight(element, true) + RowPaddingY * 2f);
                if (i < property.arraySize - 1)
                {
                    height += LoogaEditorStyle.Pixels(RowGap);
                }
            }

            return height;
        }

        private static float GetRowHeight(SerializedProperty property, int index)
        {
            if (index < 0 || index >= property.arraySize)
                return 0f;

            SerializedProperty element = property.GetArrayElementAtIndex(index);
            float elementHeight = EditorGUI.GetPropertyHeight(element, true);
            return LoogaEditorStyle.PixelCeil(elementHeight + RowPaddingY * 2f);
        }

        private static float GetReorderAnimationProgress()
        {
            double elapsed = EditorApplication.timeSinceStartup - _dropAnimationStartTime;
            float progress = Mathf.Clamp01((float)(elapsed / ReorderAnimationSeconds));
            if (progress < 1f)
            {
                RepaintInspector();
            }

            return progress * progress * (3f - 2f * progress);
        }

        private static float GetClampedDraggedRowY(
            Rect contentRect,
            float draggedRowHeight,
            float mouseY)
        {
            return Mathf.Clamp(
                mouseY - _dragMouseOffsetY,
                contentRect.y,
                Mathf.Max(contentRect.y, contentRect.yMax - draggedRowHeight));
        }

        private static float GetVisualRowY(
            SerializedProperty property,
            Rect contentRect,
            int rowIndex,
            int sourceIndex,
            int dropIndex,
            float draggedRowHeight)
        {
            float y = contentRect.y;
            int clampedDropIndex = Mathf.Clamp(dropIndex, 0, property.arraySize);

            for (int i = 0; i < property.arraySize; i++)
            {
                if (i == clampedDropIndex)
                {
                    y += draggedRowHeight + LoogaEditorStyle.Pixels(RowGap);
                }

                if (i == sourceIndex)
                    continue;

                if (i == rowIndex)
                    return y;

                y += GetRowHeight(property, i) + LoogaEditorStyle.Pixels(RowGap);
            }

            return y;
        }

        private static int GetDropIndex(
            SerializedProperty property,
            Rect contentRect,
            float mouseY,
            int sourceIndex)
        {
            if (property.arraySize == 0 || sourceIndex < 0 || sourceIndex >= property.arraySize)
                return 0;

            float clampedMouseY = Mathf.Clamp(mouseY, contentRect.y, contentRect.yMax);
            int dropIndex = sourceIndex;

            for (int i = sourceIndex + 1; i < property.arraySize; i++)
            {
                float lowerRowTop = GetOriginalRowTop(property, contentRect, i);
                if (clampedMouseY <= lowerRowTop)
                    break;

                dropIndex = i + 1;
            }

            for (int i = sourceIndex - 1; i >= 0; i--)
            {
                float upperRowBottom = GetOriginalRowTop(property, contentRect, i) +
                    GetRowHeight(property, i);
                if (clampedMouseY >= upperRowBottom)
                    break;

                dropIndex = i;
            }

            return Mathf.Clamp(dropIndex, 0, property.arraySize);
        }

        private static float GetOriginalRowTop(
            SerializedProperty property,
            Rect contentRect,
            int rowIndex)
        {
            float y = contentRect.y;
            int count = Mathf.Clamp(rowIndex, 0, property.arraySize);
            for (int i = 0; i < count; i++)
            {
                y += GetRowHeight(property, i) + LoogaEditorStyle.Pixels(RowGap);
            }

            return y;
        }

        private static void CommitDrag(SerializedProperty property)
        {
            int sourceIndex = _dragIndex;
            int dropIndex = Mathf.Clamp(_dropIndex, 0, property.arraySize);
            ClearDrag();

            if (sourceIndex < 0 || sourceIndex >= property.arraySize)
                return;

            if (dropIndex == sourceIndex || dropIndex == sourceIndex + 1)
                return;

            int targetIndex = dropIndex > sourceIndex ? dropIndex - 1 : dropIndex;
            property.MoveArrayElement(sourceIndex, targetIndex);
            GUI.changed = true;
            RepaintInspector();
        }

        private static void RepaintInspector()
        {
            EditorWindow window = EditorWindow.mouseOverWindow ?? EditorWindow.focusedWindow;
            window?.Repaint();
        }

        private static void DeleteElement(SerializedProperty property, int index)
        {
            if (index < 0 || index >= property.arraySize)
                return;

            int previousSize = property.arraySize;
            bool objectReference = property.GetArrayElementAtIndex(index).propertyType ==
                SerializedPropertyType.ObjectReference;
            property.DeleteArrayElementAtIndex(index);

            if (!objectReference || property.arraySize < previousSize)
                return;

            for (int i = index; i < previousSize - 1; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue =
                    property.GetArrayElementAtIndex(i + 1).objectReferenceValue;
            }

            property.arraySize = previousSize - 1;
        }

        private static string GetKey(SerializedProperty property)
        {
            return $"{property.serializedObject.targetObject.GetInstanceID()}:{property.propertyPath}";
        }

        private static void ClearDrag()
        {
            _dragKey = string.Empty;
            _dragIndex = -1;
            _dropIndex = -1;
            _previousDropIndex = -1;
            _dragMouseOffsetY = 0f;
            _dropAnimationStartTime = 0d;
        }
    }
}
