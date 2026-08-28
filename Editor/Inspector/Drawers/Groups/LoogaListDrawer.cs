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

        private static string _dragKey = string.Empty;
        private static int _dragIndex = -1;

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
            float y = bodyRect.y + BodyPadding;
            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                float elementHeight = EditorGUI.GetPropertyHeight(element, true);
                float rowHeight = LoogaEditorStyle.PixelCeil(elementHeight + RowPaddingY * 2f);
                Rect rowRect = LoogaEditorStyle.PixelSnap(new Rect(
                    bodyRect.x + BodyPadding,
                    y,
                    Mathf.Max(0f, bodyRect.width - BodyPadding * 2f),
                    rowHeight));

                DrawRow(rowRect, property, element, elementHeight, key, i);
                y += rowHeight + LoogaEditorStyle.Pixels(RowGap);
            }

            if (Event.current.type == EventType.MouseUp && _dragKey == key)
            {
                ClearDrag();
            }
        }

        private static void DrawRow(
            Rect rowRect,
            SerializedProperty property,
            SerializedProperty element,
            float elementHeight,
            string key,
            int index)
        {
            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.Repaint)
            {
                Color rowColor = rowRect.Contains(currentEvent.mousePosition)
                    ? Color.Lerp(LoogaEditorStyle.ListRowColor, LoogaEditorStyle.ListHoverColor, 0.65f)
                    : LoogaEditorStyle.ListRowColor;
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
            EditorGUI.PropertyField(elementRect, element, true);

            if (GUI.Button(deleteRect, new GUIContent("-", "Remove item"), EditorStyles.miniButton))
            {
                DeleteElement(property, index);
                GUI.changed = true;
                return;
            }

            HandleDrag(property, key, index, dragRect, rowRect);
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

        private static void HandleDrag(
            SerializedProperty property,
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
                currentEvent.Use();
                return;
            }

            if (_dragKey != key || _dragIndex < 0)
                return;

            if (currentEvent.type == EventType.MouseDrag && rowRect.Contains(currentEvent.mousePosition))
            {
                if (_dragIndex != index)
                {
                    property.MoveArrayElement(_dragIndex, index);
                    _dragIndex = index;
                    GUI.changed = true;
                }

                currentEvent.Use();
            }
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
        }
    }
}
