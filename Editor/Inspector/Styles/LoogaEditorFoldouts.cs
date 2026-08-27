using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Inspector.Editor
{
    public static class LoogaEditorFoldouts
    {
        public const float FoldoutContentPaddingX = 8f;
        public const float FoldoutContentPaddingY = 6f;

        private const string PropertyClipboardPrefix = "LOOGA_SERIALIZED_PROPERTY::";
        private const float FoldoutGap = 2f;
        private const float FoldoutExtraHeight = 4f;
        private const float BoxHorizontalInset = 3f;
        private const float HeaderLeftInset = 6f;
        private const float HeaderTextArrowGap = 6f;
        private const int AccentRailWidth = 0;

        private static GUIStyle _largeHeader;
        private static GUIStyle _smallHeader;
        private static GUIStyle _largeBox;
        private static GUIStyle _foldoutBox;
        private static GUIStyle _alternateLargeBox;
        private static GUIStyle _alternateFoldoutBox;
        private static GUIStyle _smallBox;
        private static GUIStyle _alternateSmallBox;
        private static GUIStyle _smallLayoutBox;
        private static GUIStyle _alternateSmallLayoutBox;
        private static int _boxDepth;
        private static int _containedFoldoutDepth;

        public static GUIStyle SmallBoxStyle
        {
            get
            {
                EnsureStyles();
                return GetSmallBoxStyle();
            }
        }

        public static GUIStyle FoldoutBoxStyle
        {
            get
            {
                EnsureStyles();
                return GetFoldoutBoxStyle();
            }
        }

        /// <summary>
        /// Begins the canonical foldout layout while leaving header contents to the caller.
        /// Use this for headers that contain fields or buttons.
        /// </summary>
        public static IDisposable BeginFoldoutLayout(
            bool expanded,
            out Rect headerRect,
            out Rect clickRect)
        {
            EnsureStyles();

            GUIStyle boxStyle = GetFoldoutBoxStyle();
            EditorGUILayout.BeginVertical(boxStyle);

            Rect baseRect = GetFoldoutBaseRect();
            Rect boxRect = ContentToBoxRect(baseRect, boxStyle);
            headerRect = new Rect(
                boxRect.x,
                boxRect.y,
                boxRect.width,
                baseRect.height + boxStyle.padding.top + 2f);
            clickRect = expanded ? headerRect : boxRect;

            return new FoldoutLayoutScopeInstance();
        }

        public static Rect GetFoldoutHeaderContentRect(Rect headerRect, bool includeArrow)
        {
            EnsureStyles();
            float x = headerRect.x + HeaderLeftInset + AccentRailWidth;
            float right = includeArrow
                ? GetHeaderArrowRect(headerRect, GetFoldoutBoxStyle()).xMin - HeaderTextArrowGap
                : headerRect.xMax - HeaderLeftInset;

            return new Rect(
                x,
                CenterVertically(headerRect, EditorGUIUtility.singleLineHeight).y,
                Mathf.Max(0f, right - x),
                EditorGUIUtility.singleLineHeight);
        }

        public static Rect GetFoldoutArrowRect(Rect headerRect)
        {
            EnsureStyles();
            return GetHeaderArrowRect(headerRect, GetFoldoutBoxStyle());
        }

        public static float GetFoldoutHeaderHeight()
        {
            EnsureStyles();
            return EditorGUIUtility.singleLineHeight
                + _largeHeader.padding.vertical
                + FoldoutExtraHeight
                + GetFoldoutBoxStyle().padding.top
                + 2f;
        }

        public static void LoogaFoldout(string title, string prefKey, bool defaultShow, Action content)
        {
            EnsureStyles();

            bool show = EditorPrefs.GetBool(prefKey, defaultShow);
            GUIStyle boxStyle = GetFoldoutBoxStyle();

            EditorGUILayout.BeginVertical(boxStyle);
            Rect baseRect = GetFoldoutBaseRect();
            Rect boxRect = ContentToBoxRect(baseRect, boxStyle);
            Rect headerRect = new(
                boxRect.x,
                boxRect.y,
                boxRect.width,
                baseRect.height + boxStyle.padding.top + 2f);
            Rect text = GetHeaderTextRect(headerRect, 1f, boxStyle);
            Rect arrow = GetHeaderArrowRect(headerRect, boxStyle);

            Rect hoverRect = show ? headerRect : boxRect;
            bool containsMouse = hoverRect.Contains(Event.current.mousePosition);
            RequestMouseMoveRepaint();

            if (containsMouse)
                DrawHoverRect(hoverRect);

            GUI.Label(text, title, _largeHeader);

            bool newShow = show;
            DrawFoldoutArrow(arrow, show);
            if (Event.current.type == EventType.MouseDown && headerRect.Contains(Event.current.mousePosition) && Event.current.button == 0)
            {
                newShow = !show;
                Event.current.Use();
            }

            if (newShow != show)
            {
                EditorPrefs.SetBool(prefKey, newShow);
                show = newShow;
            }

            if (show)
            {
                PushBoxDepth();
                try
                {
                    EditorGUILayout.Space(2);
                    content?.Invoke();
                    EditorGUILayout.Space(2);
                }
                finally
                {
                    PopBoxDepth();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(FoldoutGap);
        }

        /// <summary>
        /// Draws a foldout for state owned by the caller.
        /// </summary>
        public static bool LoogaFoldout(
            GUIContent label,
            bool expanded,
            Action content,
            SerializedProperty property = null)
        {
            EnsureStyles();

            GUIStyle boxStyle = GetFoldoutBoxStyle();
            EditorGUILayout.BeginVertical(boxStyle);
            Rect baseRect = GetFoldoutBaseRect();
            Rect boxRect = ContentToBoxRect(baseRect, boxStyle);
            Rect headerRect = new(
                boxRect.x,
                boxRect.y,
                boxRect.width,
                baseRect.height + boxStyle.padding.top + 2f);
            Rect hoverRect = expanded ? headerRect : boxRect;
            Rect textRect = GetHeaderTextRect(headerRect, 1f, boxStyle);
            Rect arrowRect = GetHeaderArrowRect(headerRect, boxStyle);
            Event current = Event.current;

            RequestMouseMoveRepaint();
            if (hoverRect.Contains(current.mousePosition))
                DrawHoverRect(hoverRect);

            if (property != null)
                EditorGUI.BeginProperty(hoverRect, label, property);

            GUI.Label(textRect, label, _largeHeader);
            DrawFoldoutArrow(arrowRect, expanded);

            if (property != null && current.type == EventType.ContextClick && hoverRect.Contains(current.mousePosition))
            {
                ShowPropertyContextMenu(property);
                current.Use();
            }
            else if (current.type == EventType.MouseDown
                && hoverRect.Contains(current.mousePosition)
                && current.button == 0)
            {
                expanded = !expanded;
                current.Use();
            }

            if (property != null)
                EditorGUI.EndProperty();

            if (expanded)
            {
                PushBoxDepth();
                try
                {
                    EditorGUILayout.Space(2f);
                    content?.Invoke();
                    EditorGUILayout.Space(2f);
                }
                finally
                {
                    PopBoxDepth();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(FoldoutGap);
            return expanded;
        }

        public static void LoogaBoxLarge(string title, Action content)
        {
            EnsureStyles();

            GUIStyle boxStyle = GetLargeBoxStyle();
            EditorGUILayout.BeginVertical(boxStyle);
            Rect baseRect = GUILayoutUtility.GetRect(GUIContent.none, _largeHeader);
            Rect boxRect = ContentToBoxRect(baseRect, boxStyle);
            Rect headerRect = new(
                boxRect.x,
                boxRect.y,
                boxRect.width,
                baseRect.height + boxStyle.padding.top + 2f);
            Rect text = GetStaticHeaderTextRect(headerRect, 1f);

            GUI.Label(text, title, _largeHeader);
            PushBoxDepth();
            try
            {
                EditorGUILayout.Space(2);
                content?.Invoke();
                EditorGUILayout.Space(2);
            }
            finally
            {
                PopBoxDepth();
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(FoldoutGap);
        }

        public static bool LoogaFoldout(Rect position, GUIContent label, bool expanded, out Rect contentRect, SerializedProperty property = null)
        {
            EnsureStyles();

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            int oldIndent = EditorGUI.indentLevel;
            Rect indentedPosition = ShrinkBoxRect(EditorGUI.IndentedRect(position));
            bool newExpanded;
            GUIStyle boxStyle = GetFoldoutBoxStyle();

            try
            {
                EditorGUI.indentLevel = 0;

                GUI.Box(indentedPosition, GUIContent.none, boxStyle);

                Rect headerRect = new(
                    indentedPosition.x,
                    indentedPosition.y + 2f,
                    indentedPosition.width,
                    lineHeight + boxStyle.padding.top + 2f + FoldoutExtraHeight);
                Rect hoverRect = expanded ? headerRect : indentedPosition;
                Rect textRect = GetHeaderTextRect(headerRect, 1f, boxStyle);
                Rect arrowRect = GetHeaderArrowRect(headerRect, boxStyle);

                if (property != null)
                    EditorGUI.BeginProperty(hoverRect, label, property);

                Event current = Event.current;
                bool containsMouse = hoverRect.Contains(current.mousePosition);
                RequestMouseMoveRepaint();

                if (containsMouse)
                    DrawHoverRect(hoverRect);

                GUI.Label(textRect, label, _largeHeader);

                newExpanded = expanded;
                DrawFoldoutArrow(arrowRect, expanded);
                if (property != null && current.type == EventType.ContextClick && containsMouse)
                {
                    ShowPropertyContextMenu(property);
                    current.Use();
                }
                else if (current.type == EventType.MouseDown && containsMouse && current.button == 0)
                {
                    newExpanded = !expanded;
                    current.Use();
                }

                if (property != null)
                    EditorGUI.EndProperty();

                contentRect = new Rect(
                    indentedPosition.x + boxStyle.padding.left,
                    headerRect.yMax + spacing,
                    indentedPosition.width - boxStyle.padding.horizontal,
                    indentedPosition.height - headerRect.height - boxStyle.padding.vertical - spacing);
            }
            finally
            {
                EditorGUI.indentLevel = oldIndent;
            }

            return newExpanded;
        }

        public static void LoogaBoxSmall(GUIContent label, Action content)
        {
            EnsureStyles();

            EditorGUILayout.Space(1f);
            GUIStyle boxStyle = GetSmallLayoutBoxStyle();
            EditorGUILayout.BeginVertical(boxStyle);

            Rect baseRect = GUILayoutUtility.GetRect(GUIContent.none, _smallHeader);
            Rect boxRect = ContentToBoxRect(baseRect, boxStyle);
            Rect headerRect = new(
                boxRect.x,
                boxRect.y,
                boxRect.width,
                baseRect.height + boxStyle.padding.top + 2f);
            Rect textRect = GetStaticHeaderTextRect(headerRect, 1f);

            GUI.Label(textRect, label, _smallHeader);
            PushBoxDepth();
            try
            {
                EditorGUILayout.Space(2f);
                content?.Invoke();
                EditorGUILayout.Space(2f);
            }
            finally
            {
                PopBoxDepth();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(1f);
        }

        public static bool LoogaFoldoutHeader(Rect headerRect, GUIContent label, bool expanded, SerializedProperty property = null)
        {
            return LoogaFoldoutHeader(headerRect, headerRect, label, expanded, property, GetFoldoutBoxStyle());
        }

        public static IDisposable ContainedFoldoutScope()
        {
            _containedFoldoutDepth++;
            return new ContainedFoldoutScopeInstance();
        }

        private static bool LoogaFoldoutHeader(
            Rect headerRect,
            Rect clickRect,
            GUIContent label,
            bool expanded,
            SerializedProperty property = null,
            GUIStyle boxStyle = null)
        {
            EnsureStyles();

            boxStyle ??= GetFoldoutBoxStyle();
            Rect textRect = GetHeaderTextRect(headerRect, 1f, boxStyle);
            Rect arrowRect = GetHeaderArrowRect(headerRect, boxStyle);

            if (property != null)
                EditorGUI.BeginProperty(clickRect, label, property);

            Event current = Event.current;
            bool containsMouse = clickRect.Contains(current.mousePosition);
            RequestMouseMoveRepaint();

            if (containsMouse)
                DrawHoverRect(clickRect);

            GUI.Label(textRect, label, _largeHeader);

            bool newExpanded = expanded;
            DrawFoldoutArrow(arrowRect, expanded);

            if (property != null && current.type == EventType.ContextClick && containsMouse)
            {
                ShowPropertyContextMenu(property);
                current.Use();
            }
            else if (current.type == EventType.MouseDown && containsMouse && current.button == 0)
            {
                newExpanded = !expanded;
                current.Use();
            }

            if (property != null)
                EditorGUI.EndProperty();

            return newExpanded;
        }

        public static void LoogaToggleFoldout(string title, SerializedProperty toggleProperty, string prefKey, Action content)
        {
            EnsureStyles();

            bool enabled = toggleProperty != null && toggleProperty.propertyType == SerializedPropertyType.Boolean && toggleProperty.boolValue;
            bool show = enabled && EditorPrefs.GetBool(prefKey, false);
            GUIStyle boxStyle = GetFoldoutBoxStyle();

            EditorGUILayout.BeginVertical(boxStyle);
            Rect baseRect = GetFoldoutBaseRect();
            Rect boxRect = ContentToBoxRect(baseRect, boxStyle);
            Rect headerRect = new(
                boxRect.x,
                boxRect.y,
                boxRect.width,
                baseRect.height + boxStyle.padding.top + 2f);
            Rect toggleRect = GetHeaderToggleRect(headerRect);
            Rect arrowRect = GetHeaderArrowRectAfter(headerRect, toggleRect);
            Rect textRect = GetHeaderTextRectAfter(headerRect, toggleRect, 1f);

            Event current = Event.current;
            Rect hoverRect = show ? headerRect : boxRect;
            bool containsMouse = hoverRect.Contains(current.mousePosition);
            RequestMouseMoveRepaint();

            if (containsMouse)
                DrawHoverRect(hoverRect);

            EditorGUI.BeginChangeCheck();
            bool newEnabled = EditorGUI.Toggle(toggleRect, enabled);
            if (EditorGUI.EndChangeCheck() && toggleProperty != null)
            {
                toggleProperty.boolValue = newEnabled;
                enabled = newEnabled;
                show = false;
                EditorPrefs.SetBool(prefKey, false);
            }

            GUI.Label(textRect, title, _largeHeader);

            if (enabled)
            {
                DrawFoldoutArrow(arrowRect, show);
                if (current.type == EventType.MouseDown
                    && hoverRect.Contains(current.mousePosition)
                    && !toggleRect.Contains(current.mousePosition)
                    && current.button == 0)
                {
                    show = !show;
                    EditorPrefs.SetBool(prefKey, show);
                    current.Use();
                }
            }

            if (enabled && show)
            {
                PushBoxDepth();
                try
                {
                    EditorGUILayout.Space(2f);
                    content?.Invoke();
                    EditorGUILayout.Space(2f);
                }
                finally
                {
                    PopBoxDepth();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(FoldoutGap);
        }

        /// <summary>
        /// Draws a toggle foldout for state owned by the caller.
        /// </summary>
        public static bool LoogaToggleFoldout(
            GUIContent label,
            bool enabled,
            bool expanded,
            Action content,
            out bool newEnabled)
        {
            EnsureStyles();

            bool show = enabled && expanded;
            GUIStyle boxStyle = GetFoldoutBoxStyle();
            EditorGUILayout.BeginVertical(boxStyle);
            Rect baseRect = GetFoldoutBaseRect();
            Rect boxRect = ContentToBoxRect(baseRect, boxStyle);
            Rect headerRect = new(
                boxRect.x,
                boxRect.y,
                boxRect.width,
                baseRect.height + boxStyle.padding.top + 2f);
            Rect toggleRect = GetHeaderToggleRect(headerRect);
            Rect arrowRect = GetHeaderArrowRectAfter(headerRect, toggleRect);
            Rect textRect = GetHeaderTextRectAfter(headerRect, toggleRect, 1f);
            Rect hoverRect = show ? headerRect : boxRect;
            Event current = Event.current;

            RequestMouseMoveRepaint();
            if (hoverRect.Contains(current.mousePosition))
                DrawHoverRect(hoverRect);

            EditorGUI.BeginChangeCheck();
            newEnabled = EditorGUI.Toggle(toggleRect, enabled);
            if (EditorGUI.EndChangeCheck())
            {
                enabled = newEnabled;
                show = false;
            }

            GUI.Label(textRect, label, _largeHeader);
            if (enabled)
            {
                DrawFoldoutArrow(arrowRect, show);
                if (current.type == EventType.MouseDown
                    && hoverRect.Contains(current.mousePosition)
                    && !toggleRect.Contains(current.mousePosition)
                    && current.button == 0)
                {
                    show = !show;
                    current.Use();
                }
            }

            if (enabled && show)
            {
                PushBoxDepth();
                try
                {
                    EditorGUILayout.Space(2f);
                    content?.Invoke();
                    EditorGUILayout.Space(2f);
                }
                finally
                {
                    PopBoxDepth();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(FoldoutGap);
            newEnabled = enabled;
            return enabled && show;
        }

        private static void RequestMouseMoveRepaint()
        {
            EditorWindow window = EditorWindow.mouseOverWindow;
            if (window == null)
                return;

            window.wantsMouseMove = true;
            if (Event.current.type == EventType.MouseMove)
            {
                window.Repaint();
            }
        }

        private static Rect GetFoldoutBaseRect()
        {
            float height = EditorGUIUtility.singleLineHeight
                + _largeHeader.padding.vertical
                + FoldoutExtraHeight;

            return GUILayoutUtility.GetRect(
                GUIContent.none,
                _largeHeader,
                GUILayout.Height(height));
        }

        private static Rect ContentToBoxRect(Rect contentRect, GUIStyle boxStyle)
        {
            RectOffset padding = boxStyle.padding;
            return new Rect(
                contentRect.x - padding.left,
                contentRect.y - padding.top,
                contentRect.width + padding.horizontal,
                contentRect.height + padding.vertical);
        }

        private static Rect GetHeaderTextRect(Rect headerRect, float yOffset, GUIStyle boxStyle)
        {
            Rect arrowRect = GetHeaderArrowRect(headerRect, boxStyle);
            float x = headerRect.x + HeaderLeftInset + AccentRailWidth;

            return new Rect(
                x,
                GetHeaderTextY(headerRect, yOffset),
                Mathf.Max(0f, arrowRect.xMin - HeaderTextArrowGap - x),
                EditorGUIUtility.singleLineHeight);
        }

        private static Rect GetStaticHeaderTextRect(Rect headerRect, float yOffset)
        {
            return new Rect(
                headerRect.x + HeaderLeftInset + AccentRailWidth,
                GetHeaderTextY(headerRect, yOffset),
                Mathf.Max(0f, headerRect.width - HeaderLeftInset * 2f - AccentRailWidth),
                EditorGUIUtility.singleLineHeight);
        }

        private static Rect GetHeaderToggleRect(Rect headerRect)
        {
            float size = EditorGUIUtility.singleLineHeight - 2f;
            return new Rect(
                headerRect.x + HeaderLeftInset + AccentRailWidth,
                CenterVertically(headerRect, size).y,
                size,
                size);
        }

        private static Rect GetHeaderArrowRectAfter(Rect headerRect, Rect previousRect)
        {
            return GetHeaderArrowRect(headerRect, null);
        }

        private static Rect GetHeaderTextRectAfter(Rect headerRect, Rect previousRect, float yOffset)
        {
            float x = previousRect.xMax + HeaderTextArrowGap;
            Rect arrowRect = GetHeaderArrowRect(headerRect, null);
            return new Rect(
                x,
                GetHeaderTextY(headerRect, yOffset),
                Mathf.Max(0f, arrowRect.xMin - HeaderTextArrowGap - x),
                EditorGUIUtility.singleLineHeight);
        }

        private static Rect GetHeaderArrowRect(Rect headerRect, GUIStyle boxStyle)
        {
            float rightInset = GetHeaderSideInset(headerRect);
            return new Rect(
                headerRect.xMax - rightInset - LoogaEditorStyle.FoldoutTriangleSize,
                CenterVertically(headerRect, LoogaEditorStyle.FoldoutTriangleSize).y,
                LoogaEditorStyle.FoldoutTriangleSize,
                LoogaEditorStyle.FoldoutTriangleSize);
        }

        private static float GetHeaderTextY(Rect headerRect, float yOffset)
        {
            return CenterVertically(headerRect, EditorGUIUtility.singleLineHeight).y + yOffset;
        }

        private static Rect CenterVertically(Rect container, float height)
        {
            return new Rect(
                container.x,
                SnapToPixel(container.y + (container.height - height) * 0.5f),
                container.width,
                height);
        }

        private static float SnapToPixel(float value)
        {
            float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            return Mathf.Floor(value * pixelsPerPoint + 0.5f) / pixelsPerPoint;
        }

        private static float GetHeaderSideInset(Rect headerRect)
        {
            return Mathf.Max(2f, (headerRect.height - LoogaEditorStyle.FoldoutTriangleSize) * 0.5f);
        }

        private static void DrawFoldoutArrow(Rect arrowRect, bool expanded)
        {
            LoogaEditorStyle.DrawFoldoutTriangle(arrowRect, expanded);
        }

        private sealed class ContainedFoldoutScopeInstance : IDisposable
        {
            public void Dispose()
            {
                _containedFoldoutDepth = Mathf.Max(0, _containedFoldoutDepth - 1);
            }
        }

        private sealed class FoldoutLayoutScopeInstance : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(FoldoutGap);
            }
        }

        private static void ShowPropertyContextMenu(SerializedProperty property)
        {
            GenericMenu menu = new();

            menu.AddItem(new GUIContent("Copy Property Path"), false, () =>
            {
                EditorGUIUtility.systemCopyBuffer = property.propertyPath;
            });
            menu.AddDisabledItem(new GUIContent("Search Same Property Value"));
            menu.AddSeparator(string.Empty);

            menu.AddItem(new GUIContent("Copy"), false, () => CopyProperty(property));

            if (CanPasteProperty(property))
                menu.AddItem(new GUIContent("Paste"), false, () => PasteProperty(property));
            else
                menu.AddDisabledItem(new GUIContent("Paste"));

            menu.ShowAsContext();
        }

        private static void CopyProperty(SerializedProperty property)
        {
            if (!TryGetPropertyPayload(property, out PropertyClipboardPayload payload))
                return;

            EditorGUIUtility.systemCopyBuffer = PropertyClipboardPrefix + JsonUtility.ToJson(payload);
        }

        private static bool CanPasteProperty(SerializedProperty property)
        {
            return TryReadClipboardPayload(out PropertyClipboardPayload payload)
                && IsPasteCompatible(property, payload);
        }

        private static void PasteProperty(SerializedProperty property)
        {
            if (!TryReadClipboardPayload(out PropertyClipboardPayload payload))
                return;

            if (!IsPasteCompatible(property, payload))
                return;

            property.serializedObject.Update();

            for (int i = 0; i < payload.properties.Count; i++)
            {
                SerializedPropertyValue source = payload.properties[i];
                SerializedProperty target = property.FindPropertyRelative(source.relativePath);

                if (target == null || (int)target.propertyType != source.propertyType)
                    continue;

                ApplyPropertyValue(target, source);
            }

            property.serializedObject.ApplyModifiedProperties();
            GUI.changed = true;
        }

        private static bool TryGetPropertyPayload(SerializedProperty property, out PropertyClipboardPayload payload)
        {
            payload = null;

            if (!TryGetTargetType(property, out Type targetType))
                return false;

            List<SerializedPropertyValue> values = new();
            CollectPropertyValues(property, values);

            if (values.Count == 0)
                return false;

            payload = new PropertyClipboardPayload
            {
                typeName = targetType.AssemblyQualifiedName,
                properties = values
            };
            return true;
        }

        private static bool TryReadClipboardPayload(out PropertyClipboardPayload payload)
        {
            payload = null;
            string copyBuffer = EditorGUIUtility.systemCopyBuffer;

            if (string.IsNullOrEmpty(copyBuffer) || !copyBuffer.StartsWith(PropertyClipboardPrefix, StringComparison.Ordinal))
                return false;

            string json = copyBuffer.Substring(PropertyClipboardPrefix.Length);
            payload = JsonUtility.FromJson<PropertyClipboardPayload>(json);

            return payload != null
                && payload.properties != null;
        }

        private static bool TryGetTargetType(SerializedProperty property, out Type targetType)
        {
            targetType = CustomDrawerUtil.GetTargetType(property);
            return targetType != null && property.propertyType == SerializedPropertyType.Generic;
        }

        private static bool IsPasteCompatible(SerializedProperty property, PropertyClipboardPayload payload)
        {
            if (property == null || payload?.properties == null || payload.properties.Count == 0)
                return false;

            if (TryGetTargetType(property, out Type targetType)
                && !string.IsNullOrEmpty(payload.typeName)
                && payload.typeName == targetType.AssemblyQualifiedName)
            {
                return true;
            }

            for (int i = 0; i < payload.properties.Count; i++)
            {
                SerializedPropertyValue source = payload.properties[i];
                SerializedProperty target = property.FindPropertyRelative(source.relativePath);

                if (target == null || (int)target.propertyType != source.propertyType)
                    return false;
            }

            return true;
        }

        private static void CollectPropertyValues(SerializedProperty root, List<SerializedPropertyValue> values)
        {
            SerializedProperty iterator = root.Copy();
            SerializedProperty endProperty = iterator.GetEndProperty();
            int rootDepth = root.depth;
            string rootPath = root.propertyPath;

            if (!iterator.NextVisible(true))
                return;

            do
            {
                if (iterator.depth <= rootDepth || SerializedProperty.EqualContents(iterator, endProperty))
                    break;

                if (iterator.propertyType == SerializedPropertyType.Generic)
                    continue;

                SerializedPropertyValue value = CreatePropertyValue(rootPath, iterator);
                if (value != null)
                    values.Add(value);
            } while (iterator.NextVisible(true));
        }

        private static SerializedPropertyValue CreatePropertyValue(string rootPath, SerializedProperty property)
        {
            SerializedPropertyValue value = new()
            {
                relativePath = GetRelativePath(rootPath, property.propertyPath),
                propertyType = (int)property.propertyType
            };

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Enum:
                    value.longValue = property.longValue;
                    return value;
                case SerializedPropertyType.Boolean:
                    value.boolValue = property.boolValue;
                    return value;
                case SerializedPropertyType.Float:
                    value.doubleValue = property.doubleValue;
                    return value;
                case SerializedPropertyType.String:
                    value.stringValue = property.stringValue;
                    return value;
                case SerializedPropertyType.Color:
                    value.colorValue = property.colorValue;
                    return value;
                case SerializedPropertyType.ObjectReference:
                    value.objectReferenceValue = property.objectReferenceValue;
                    return value;
                case SerializedPropertyType.Vector2:
                    value.vector2Value = property.vector2Value;
                    return value;
                case SerializedPropertyType.Vector3:
                    value.vector3Value = property.vector3Value;
                    return value;
                case SerializedPropertyType.Vector4:
                    value.vector4Value = property.vector4Value;
                    return value;
                case SerializedPropertyType.Rect:
                    value.rectValue = property.rectValue;
                    return value;
                case SerializedPropertyType.AnimationCurve:
                    value.animationCurveValue = new AnimationCurve(property.animationCurveValue.keys);
                    value.animationCurveValue.preWrapMode = property.animationCurveValue.preWrapMode;
                    value.animationCurveValue.postWrapMode = property.animationCurveValue.postWrapMode;
                    return value;
                case SerializedPropertyType.Bounds:
                    value.boundsValue = property.boundsValue;
                    return value;
                case SerializedPropertyType.Quaternion:
                    value.quaternionValue = property.quaternionValue;
                    return value;
                case SerializedPropertyType.Vector2Int:
                    value.vector2IntValue = property.vector2IntValue;
                    return value;
                case SerializedPropertyType.Vector3Int:
                    value.vector3IntValue = property.vector3IntValue;
                    return value;
                case SerializedPropertyType.RectInt:
                    value.rectIntValue = property.rectIntValue;
                    return value;
                case SerializedPropertyType.BoundsInt:
                    value.boundsIntValue = property.boundsIntValue;
                    return value;
                default:
                    return null;
            }
        }

        private static void ApplyPropertyValue(SerializedProperty property, SerializedPropertyValue value)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Enum:
                    property.longValue = value.longValue;
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = value.boolValue;
                    break;
                case SerializedPropertyType.Float:
                    property.doubleValue = value.doubleValue;
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = value.stringValue;
                    break;
                case SerializedPropertyType.Color:
                    property.colorValue = value.colorValue;
                    break;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = value.objectReferenceValue;
                    break;
                case SerializedPropertyType.Vector2:
                    property.vector2Value = value.vector2Value;
                    break;
                case SerializedPropertyType.Vector3:
                    property.vector3Value = value.vector3Value;
                    break;
                case SerializedPropertyType.Vector4:
                    property.vector4Value = value.vector4Value;
                    break;
                case SerializedPropertyType.Rect:
                    property.rectValue = value.rectValue;
                    break;
                case SerializedPropertyType.AnimationCurve:
                    if (value.animationCurveValue != null)
                    {
                        AnimationCurve curve = new(value.animationCurveValue.keys)
                        {
                            preWrapMode = value.animationCurveValue.preWrapMode,
                            postWrapMode = value.animationCurveValue.postWrapMode
                        };
                        property.animationCurveValue = curve;
                    }
                    else
                    {
                        property.animationCurveValue = null;
                    }
                    break;
                case SerializedPropertyType.Bounds:
                    property.boundsValue = value.boundsValue;
                    break;
                case SerializedPropertyType.Quaternion:
                    property.quaternionValue = value.quaternionValue;
                    break;
                case SerializedPropertyType.Vector2Int:
                    property.vector2IntValue = value.vector2IntValue;
                    break;
                case SerializedPropertyType.Vector3Int:
                    property.vector3IntValue = value.vector3IntValue;
                    break;
                case SerializedPropertyType.RectInt:
                    property.rectIntValue = value.rectIntValue;
                    break;
                case SerializedPropertyType.BoundsInt:
                    property.boundsIntValue = value.boundsIntValue;
                    break;
            }
        }

        private static string GetRelativePath(string rootPath, string propertyPath)
        {
            if (propertyPath.Length <= rootPath.Length)
                return string.Empty;

            return propertyPath.Substring(rootPath.Length + 1);
        }

        [Serializable]
        private sealed class PropertyClipboardPayload
        {
            public string typeName;
            public List<SerializedPropertyValue> properties = new();
        }

        [Serializable]
        private sealed class SerializedPropertyValue
        {
            public string relativePath;
            public int propertyType;
            public long longValue;
            public bool boolValue;
            public double doubleValue;
            public string stringValue;
            public Color colorValue;
            public UnityEngine.Object objectReferenceValue;
            public Vector2 vector2Value;
            public Vector3 vector3Value;
            public Vector4 vector4Value;
            public Rect rectValue;
            public AnimationCurve animationCurveValue;
            public Bounds boundsValue;
            public Quaternion quaternionValue;
            public Vector2Int vector2IntValue;
            public Vector3Int vector3IntValue;
            public RectInt rectIntValue;
            public BoundsInt boundsIntValue;
        }

        private static void EnsureStyles()
        {
            if (_largeHeader != null)
                return;

            _largeHeader = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                padding = new RectOffset(0, 0, 0, 4)
            };

            _smallHeader = new GUIStyle(EditorStyles.label)
            {
                //fontSize = 13,
                padding = new RectOffset(0, 0, 0, 2)
            };

            _largeBox = CreateFlatBoxStyle(new RectOffset(8, 8, 4, 2), true, false);
            _foldoutBox = CreateFlatBoxStyle(new RectOffset(8, 8, 4, 2), true, false);
            _alternateLargeBox = CreateFlatBoxStyle(new RectOffset(8, 8, 4, 2), true, true);
            _alternateFoldoutBox = CreateFlatBoxStyle(new RectOffset(8, 8, 4, 2), true, true);
            _smallBox = CreateFlatBoxStyle(new RectOffset(8, 8, 3, 0), true, false);
            _alternateSmallBox = CreateFlatBoxStyle(new RectOffset(8, 8, 3, 0), true, true);
            _smallLayoutBox = CreateFlatBoxStyle(new RectOffset(8, 8, 3, -2), true, false);
            _alternateSmallLayoutBox = CreateFlatBoxStyle(new RectOffset(8, 8, 3, -2), true, true);
        }

        public static void DrawHoverRect(Rect rect)
        {
            Color hoverColor = GetFlatHoverColor();
            hoverColor.a = 0.45f;
            EditorGUI.DrawRect(rect, hoverColor);
        }

        private static Color GetFlatHoverColor()
        {
            return LoogaEditorStyle.HoverColor;
        }

        private static GUIStyle GetLargeBoxStyle()
        {
            return UseAlternateBoxStyle() ? _alternateLargeBox : _largeBox;
        }

        private static GUIStyle GetFoldoutBoxStyle()
        {
            return UseAlternateBoxStyle() ? _alternateFoldoutBox : _foldoutBox;
        }

        private static GUIStyle GetSmallBoxStyle()
        {
            return UseAlternateBoxStyle() ? _alternateSmallBox : _smallBox;
        }

        private static GUIStyle GetSmallLayoutBoxStyle()
        {
            return UseAlternateBoxStyle() ? _alternateSmallLayoutBox : _smallLayoutBox;
        }

        private static bool UseAlternateBoxStyle()
        {
            return ((_boxDepth + _containedFoldoutDepth) & 1) == 1;
        }

        private static void PushBoxDepth()
        {
            _boxDepth++;
        }

        private static void PopBoxDepth()
        {
            _boxDepth = Mathf.Max(0, _boxDepth - 1);
        }

        private static GUIStyle CreateFlatBoxStyle(RectOffset padding, bool includeAccentRail, bool alternate)
        {
            GUIStyle style = new(EditorStyles.helpBox)
            {
                margin = new RectOffset((int)BoxHorizontalInset, (int)BoxHorizontalInset, 0, 0),
                padding = padding,
                overflow = new RectOffset(0, 0, 0, 0)
            };
            return style;
        }

        private static Rect ShrinkBoxRect(Rect rect)
        {
            return new Rect(
                rect.x + BoxHorizontalInset,
                rect.y,
                Mathf.Max(0f, rect.width - BoxHorizontalInset * 2f),
                rect.height);
        }
    }
}




















