using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Inspector.Editor
{
    [CustomPropertyDrawer(typeof(ExposeScriptableAttribute))]
    public class ExposeScriptableDrawer : PropertyDrawerBase
    {
        private static readonly float LineHeight = EditorGUIUtility.singleLineHeight;
        private const float MinCreateButtonWidth = 58f;
        private const float HeaderFieldGap = 6f;
        private const float CreateButtonPadding = 2f;
        private const float CreateButtonHorizontalInset = 1f;
        
        protected override void OnGUI_Internal(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            ExposeScriptableAttribute exposeAttribute = (ExposeScriptableAttribute)attribute;
            
            bool objectValid = property.objectReferenceValue != null;
            TryGetScriptableObjectType(out Type scriptableObjectType);
            bool canCreateAsset = !objectValid && scriptableObjectType != null;
            float createButtonWidth = GetCreateButtonWidth(exposeAttribute);

            float spacing = EditorGUIUtility.standardVerticalSpacing;
            Rect boxRect = new(
                position.x,
                position.y,
                position.width,
                position.height);
            Rect headerRect = new(
                boxRect.x,
                boxRect.y,
                boxRect.width,
                LoogaEditorFoldouts.GetFoldoutHeaderHeight());
            Rect contentLineRect = LoogaEditorFoldouts.GetFoldoutHeaderContentRect(
                headerRect,
                objectValid);
            Rect arrowRect = objectValid
                ? LoogaEditorFoldouts.GetFoldoutArrowRect(headerRect)
                : default;
            Rect createButtonRect = canCreateAsset
                ? new Rect(
                    boxRect.xMax - createButtonWidth - CreateButtonPadding + CreateButtonHorizontalInset,
                    contentLineRect.y,
                    Mathf.Max(0f, createButtonWidth - CreateButtonHorizontalInset * 2f),
                    LineHeight)
                : default;
            Rect rightLimitRect = canCreateAsset
                ? createButtonRect
                : new Rect(contentLineRect.xMax, headerRect.y, 0f, headerRect.height);
            float labelWidth = Mathf.Clamp(EditorGUIUtility.labelWidth * 0.65f, 90f, contentLineRect.width * 0.5f);
            Rect labelRect = new(contentLineRect.x, contentLineRect.y, labelWidth, contentLineRect.height);
            Rect fieldRect = new(
                labelRect.xMax + HeaderFieldGap,
                contentLineRect.y,
                Mathf.Max(0f, rightLimitRect.x - labelRect.xMax - GetFieldRightGap(canCreateAsset)),
                contentLineRect.height);

            DrawFoldoutBackground(boxRect, headerRect, property.isExpanded);
            EditorGUI.LabelField(labelRect, label);

            Type objectFieldType = scriptableObjectType ?? typeof(ScriptableObject);
            UnityEngine.Object newValue = EditorGUI.ObjectField(
                fieldRect,
                property.objectReferenceValue,
                objectFieldType,
                false);

            if (newValue != property.objectReferenceValue)
                property.objectReferenceValue = newValue;

            if (canCreateAsset && GUI.Button(createButtonRect, exposeAttribute.createButtonLabel))
                ShowCreateMenu(property, scriptableObjectType);

            if (objectValid)
            {
                string expansionTouchedKey = GetExpansionTouchedKey(property);
                if (exposeAttribute.expandedByDefault && !SessionState.GetBool(expansionTouchedKey, false))
                    property.isExpanded = true;

                bool previousExpanded = property.isExpanded;
                property.isExpanded = DrawHeaderFoldout(headerRect, fieldRect, arrowRect, property.isExpanded);
                if (property.isExpanded != previousExpanded)
                    SessionState.SetBool(expansionTouchedKey, true);
            }
            
            if (property.isExpanded && objectValid)
            {
                Rect inlineContentRect = new(
                    boxRect.x + LoogaEditorFoldouts.FoldoutContentPaddingX,
                    headerRect.yMax + spacing,
                    boxRect.width - LoogaEditorFoldouts.FoldoutContentPaddingX * 2f,
                    Mathf.Max(0f, boxRect.yMax - headerRect.yMax - spacing - LoogaEditorFoldouts.FoldoutContentPaddingY));

                DrawInlineScriptableObject(inlineContentRect, property.objectReferenceValue, exposeAttribute);
            }

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// Draws an exposed asset in layout mode and delegates its expanded contents to the owning inspector.
        /// This keeps nested assets inside the same Looga rendering pipeline as top-level targets.
        /// </summary>
        internal static void DrawLayout(
            SerializedProperty property,
            GUIContent label,
            ExposeScriptableAttribute exposeAttribute,
            Type scriptableObjectType,
            Action<UnityEngine.Object, bool> drawInlineObject)
        {
            bool objectValid = property.objectReferenceValue != null;
            bool canCreateAsset = !objectValid && scriptableObjectType != null;
            float createButtonWidth = GetCreateButtonWidth(exposeAttribute);

            if (objectValid)
            {
                string expansionTouchedKey = GetExpansionTouchedKey(property);
                if (exposeAttribute.expandedByDefault && !SessionState.GetBool(expansionTouchedKey, false))
                    property.isExpanded = true;
            }

            using (LoogaEditorFoldouts.BeginFoldoutLayout(
                       property.isExpanded,
                       out Rect headerRect,
                       out Rect clickRect,
                       out Rect backgroundRect))
            {
                Rect contentLineRect = LoogaEditorFoldouts.GetFoldoutHeaderContentRect(
                    headerRect,
                    objectValid);
                Rect arrowRect = objectValid
                    ? LoogaEditorFoldouts.GetFoldoutArrowRect(headerRect)
                    : default;
                Rect createButtonRect = canCreateAsset
                    ? new Rect(
                        headerRect.xMax - createButtonWidth - CreateButtonPadding + CreateButtonHorizontalInset,
                        contentLineRect.y,
                        Mathf.Max(0f, createButtonWidth - CreateButtonHorizontalInset * 2f),
                        LineHeight)
                    : default;
                float labelWidth = Mathf.Clamp(EditorGUIUtility.labelWidth * 0.65f, 90f, contentLineRect.width * 0.5f);
                Rect labelRect = new(contentLineRect.x, contentLineRect.y, labelWidth, contentLineRect.height);
                float fieldRight = canCreateAsset
                    ? createButtonRect.xMin - HeaderFieldGap
                    : contentLineRect.xMax;
                Rect fieldRect = new(
                    labelRect.xMax + HeaderFieldGap,
                    contentLineRect.y,
                    Mathf.Max(0f, fieldRight - labelRect.xMax - HeaderFieldGap),
                    contentLineRect.height);

                Event current = Event.current;
                if (clickRect.Contains(current.mousePosition))
                    LoogaEditorFoldouts.DrawHoverRect(backgroundRect);

                EditorGUI.BeginProperty(headerRect, label, property);
                EditorGUI.LabelField(labelRect, label);
                UnityEngine.Object newValue = EditorGUI.ObjectField(
                    fieldRect,
                    property.objectReferenceValue,
                    scriptableObjectType ?? typeof(ScriptableObject),
                    false);

                if (newValue != property.objectReferenceValue)
                    property.objectReferenceValue = newValue;

                if (canCreateAsset && GUI.Button(createButtonRect, exposeAttribute.createButtonLabel))
                    ShowCreateMenu(property, scriptableObjectType);

                if (objectValid)
                {
                    bool previousExpanded = property.isExpanded;
                    property.isExpanded = DrawHeaderFoldout(clickRect, fieldRect, arrowRect, property.isExpanded);
                    if (property.isExpanded != previousExpanded)
                        SessionState.SetBool(GetExpansionTouchedKey(property), true);
                }

                EditorGUI.EndProperty();

                if (property.isExpanded && property.objectReferenceValue != null)
                {
                    EditorGUILayout.Space(2f);
                    using (LoogaEditorFoldouts.ContainedFoldoutScope())
                        drawInlineObject?.Invoke(property.objectReferenceValue, exposeAttribute.showScriptField);
                    EditorGUILayout.Space(2f);
                }
            }
        }

        private bool TryGetScriptableObjectType(out Type scriptableObjectType)
        {
            return TryGetScriptableObjectType(fieldInfo, out scriptableObjectType);
        }

        internal static bool TryGetScriptableObjectType(FieldInfo inspectedField, out Type scriptableObjectType)
        {
            if (inspectedField == null)
            {
                scriptableObjectType = null;
                return false;
            }

            scriptableObjectType = inspectedField.FieldType;

            if (scriptableObjectType.IsArray)
                scriptableObjectType = scriptableObjectType.GetElementType();
            else if (scriptableObjectType.IsGenericType && scriptableObjectType.GetGenericArguments().Length == 1)
                scriptableObjectType = scriptableObjectType.GetGenericArguments()[0];

            return scriptableObjectType != null
                && typeof(ScriptableObject).IsAssignableFrom(scriptableObjectType);
        }

        private static void DrawFoldoutBackground(Rect boxRect, Rect headerRect, bool expanded)
        {
            GUI.Box(boxRect, GUIContent.none, LoogaEditorFoldouts.FoldoutBoxStyle);

            Rect hoverRect = expanded ? headerRect : boxRect;
            Event current = Event.current;
            if (hoverRect.Contains(current.mousePosition))
                LoogaEditorFoldouts.DrawHoverRect(boxRect);
        }

        private static bool DrawHeaderFoldout(Rect headerRect, Rect fieldRect, Rect arrowRect, bool expanded)
        {
            Event current = Event.current;
            bool newExpanded = expanded;

            bool canToggle = headerRect.Contains(current.mousePosition)
                && !fieldRect.Contains(current.mousePosition);

            if (current.type == EventType.MouseDown && current.button == 0 && canToggle)
            {
                newExpanded = !expanded;
                current.Use();
            }

            if (current.type == EventType.Repaint)
                LoogaEditorStyle.DrawFoldoutTriangle(arrowRect, expanded);

            return newExpanded;
        }

        private static float GetFieldRightGap(bool hasCreateButton)
        {
            return hasCreateButton
                ? HeaderFieldGap
                : HeaderFieldGap * 2f;
        }

        private static float GetCreateButtonWidth(ExposeScriptableAttribute exposeAttribute)
        {
            GUIContent content = new(exposeAttribute.createButtonLabel);
            return Mathf.Max(MinCreateButtonWidth, GUI.skin.button.CalcSize(content).x + CreateButtonPadding * 4f);
        }

        private static string GetExpansionTouchedKey(SerializedProperty property)
        {
            UnityEngine.Object targetObject = property.serializedObject.targetObject;
            int targetId = targetObject != null ? targetObject.GetInstanceID() : 0;
            return $"{targetId}_{property.propertyPath}_ExposeScriptableTouched";
        }

        private static void DrawInlineScriptableObject(
            Rect position,
            UnityEngine.Object scriptableObject,
            ExposeScriptableAttribute exposeAttribute)
        {
            if (scriptableObject == null)
                return;

            SerializedObject serializedObject = new(scriptableObject);
            serializedObject.Update();

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            float y = position.y;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            int oldIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel++;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (!exposeAttribute.showScriptField && iterator.propertyPath == "m_Script")
                    continue;

                float propertyHeight = EditorGUI.GetPropertyHeight(iterator, includeChildren: true);
                Rect propertyRect = new(position.x, y, position.width, propertyHeight);

                using (new EditorGUI.DisabledScope(iterator.propertyPath == "m_Script"))
                    EditorGUI.PropertyField(propertyRect, iterator, includeChildren: true);

                y += propertyHeight + spacing;
            }

            EditorGUI.indentLevel = oldIndent;
            serializedObject.ApplyModifiedProperties();
        }

        private static void ShowCreateMenu(SerializedProperty property, Type scriptableObjectType)
        {
            List<Type> concreteTypes = GetConcreteScriptableObjectTypes(scriptableObjectType);
            if (concreteTypes.Count == 0)
                return;

            if (concreteTypes.Count == 1)
            {
                CreateAndAssignAsset(property, concreteTypes[0]);
                return;
            }

            GenericMenu menu = new();
            foreach (Type concreteType in concreteTypes)
            {
                Type capturedType = concreteType;
                menu.AddItem(
                    new GUIContent(ObjectNames.NicifyVariableName(concreteType.Name)),
                    false,
                    () => CreateAndAssignAsset(property, capturedType));
            }

            menu.ShowAsContext();
        }

        private static List<Type> GetConcreteScriptableObjectTypes(Type scriptableObjectType)
        {
            List<Type> concreteTypes = new();

            if (!scriptableObjectType.IsAbstract && !scriptableObjectType.IsInterface)
                concreteTypes.Add(scriptableObjectType);

            concreteTypes.AddRange(TypeCache.GetTypesDerivedFrom(scriptableObjectType)
                .Where(type => typeof(ScriptableObject).IsAssignableFrom(type)
                    && !type.IsAbstract
                    && !type.IsGenericType));

            return concreteTypes
                .Distinct()
                .OrderBy(type => type.Name)
                .ToList();
        }

        private static void CreateAndAssignAsset(SerializedProperty property, Type scriptableObjectType)
        {
            UnityEngine.Object targetObject = property.serializedObject.targetObject;
            string propertyPath = property.propertyPath;

            EditorApplication.delayCall += () => CreateAndAssignAsset(targetObject, propertyPath, scriptableObjectType);
        }

        private static void CreateAndAssignAsset(UnityEngine.Object targetObject, string propertyPath, Type scriptableObjectType)
        {
            if (targetObject == null || string.IsNullOrWhiteSpace(propertyPath))
                return;

            ScriptableObject asset = ScriptableObject.CreateInstance(scriptableObjectType);
            string assetPath = EditorUtility.SaveFilePanelInProject(
                "Create Scriptable Object",
                GetDefaultAssetName(scriptableObjectType),
                "asset",
                "Choose where to save the new asset.",
                GetDefaultDirectory(targetObject));

            if (string.IsNullOrWhiteSpace(assetPath))
            {
                UnityEngine.Object.DestroyImmediate(asset);
                return;
            }

            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SerializedObject serializedObject = new(targetObject);
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null)
            {
                EditorGUIUtility.PingObject(asset);
                return;
            }

            property.objectReferenceValue = asset;
            serializedObject.ApplyModifiedProperties();

            EditorGUIUtility.PingObject(asset);
        }

        private static string GetDefaultDirectory(UnityEngine.Object targetObject)
        {
            string targetPath = AssetDatabase.GetAssetPath(targetObject);
            string directory = "Assets";

            if (!string.IsNullOrWhiteSpace(targetPath))
            {
                directory = Directory.Exists(targetPath)
                    ? targetPath
                    : Path.GetDirectoryName(targetPath);
            }

            if (string.IsNullOrWhiteSpace(directory))
                directory = "Assets";

            return directory.Replace('\\', '/');
        }

        private static string GetDefaultAssetName(Type scriptableObjectType)
        {
            return ObjectNames.NicifyVariableName(scriptableObjectType.Name);
        }

        protected override UnityEngine.UIElements.VisualElement CreatePropertyGUI_Internal(
            SerializedProperty property,
            string label)
        {
            ExposeScriptableAttribute exposeAttribute = (ExposeScriptableAttribute)attribute;
            TryGetScriptableObjectType(out Type scriptableObjectType);
            SerializedObject owner = property.serializedObject;
            string propertyPath = property.propertyPath;
            string expansionKey = GetExpansionTouchedKey(property);
            bool useDefaultExpansion = exposeAttribute.expandedByDefault
                && !SessionState.GetBool(expansionKey, false);
            UnityEngine.UIElements.Foldout foldout = new()
            {
                text = label,
                value = useDefaultExpansion || property.isExpanded
            };
            LoogaUiToolkitStyle.StyleFoldout(foldout);
            UnityEngine.UIElements.VisualElement header = new();
            header.style.flexDirection = UnityEngine.UIElements.FlexDirection.Row;
            header.style.alignItems = UnityEngine.UIElements.Align.Center;
            UnityEditor.UIElements.ObjectField objectField = new()
            {
                objectType = scriptableObjectType ?? typeof(ScriptableObject),
                allowSceneObjects = false
            };
            objectField.style.flexGrow = 1f;
            objectField.BindProperty(property);
            UnityEngine.UIElements.Button create = new()
            {
                text = exposeAttribute.createButtonLabel
            };
            create.clicked += () =>
            {
                owner.UpdateIfRequiredOrScript();
                SerializedProperty current = owner.FindProperty(propertyPath);
                if (current != null && scriptableObjectType != null)
                    ShowCreateMenu(current, scriptableObjectType);
            };
            header.Add(objectField);
            header.Add(create);
            UnityEngine.UIElements.VisualElement inline = new();
            foldout.Add(header);
            foldout.Add(inline);
            UnityEngine.Object renderedObject = null;

            void Rebuild(SerializedProperty current)
            {
                if (current == null)
                    return;

                UnityEngine.Object value = current.objectReferenceValue;
                create.style.display = value == null && scriptableObjectType != null
                    ? UnityEngine.UIElements.DisplayStyle.Flex
                    : UnityEngine.UIElements.DisplayStyle.None;
                if (value == renderedObject)
                    return;

                renderedObject = value;
                inline.Clear();
                if (value == null)
                    return;

                SerializedObject nestedOwner = new(value);
                SerializedProperty iterator = nestedOwner.GetIterator();
                bool enterChildren = true;
                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (!exposeAttribute.showScriptField && iterator.propertyPath == "m_Script")
                        continue;

                    UnityEditor.UIElements.PropertyField nestedField = new(iterator.Copy());
                    nestedField.SetEnabled(iterator.propertyPath != "m_Script");
                    nestedField.Bind(nestedOwner);
                    inline.Add(nestedField);
                }
            }

            foldout.RegisterValueChangedCallback(evt =>
            {
                SessionState.SetBool(expansionKey, true);
                LoogaPropertyDrawerUi.Commit(owner, propertyPath, current => current.isExpanded = evt.newValue);
            });
            Rebuild(property);
            LoogaPropertyDrawerUi.Track(foldout, property, Rebuild);
            return foldout;
        }

        protected override float GetPropertyHeight_Internal(SerializedProperty property, GUIContent label)
        {
            float height = LoogaEditorFoldouts.GetFoldoutHeaderHeight();

            if (property.isExpanded && property.objectReferenceValue != null)
                height += GetInlineScriptableObjectHeight(property.objectReferenceValue, ((ExposeScriptableAttribute)attribute).showScriptField)
                    + LoogaEditorFoldouts.FoldoutContentPaddingY;

            return height;
        }

        private static float GetInlineScriptableObjectHeight(UnityEngine.Object scriptableObject, bool showScriptField)
        {
            if (scriptableObject == null)
                return 0f;

            SerializedObject serializedObject = new(scriptableObject);
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            float height = EditorGUIUtility.standardVerticalSpacing;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (!showScriptField && iterator.propertyPath == "m_Script")
                    continue;

                height += EditorGUI.GetPropertyHeight(iterator, includeChildren: true)
                    + EditorGUIUtility.standardVerticalSpacing;
            }

            return height;
        }
    }
}





