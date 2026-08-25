using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace LoogaSoft.Inspector.Editor
{
    internal static class LoogaPropertyDrawerUi
    {
        private const float ControlGap = 4f;
        private const float CollectionButtonWidth = 24f;

        public static VisualElement CreateRoot(VisualElement content, string tooltip = null)
        {
            VisualElement root = new();
            LoogaUiToolkitStyle.AddSharedStyleSheet(root);
            root.style.flexGrow = 1f;
            root.style.marginBottom = 2f;
            root.tooltip = tooltip ?? string.Empty;
            if (content != null)
                root.Add(content);
            return root;
        }

        public static HelpBox CreateMessage(string message, HelpBoxMessageType type)
        {
            return new HelpBox(message, type);
        }

        public static VisualElement CreateDefaultField(
            SerializedProperty property,
            string label,
            Type declaredType = null)
        {
            VisualElement field = property.propertyType switch
            {
                SerializedPropertyType.Integer when declaredType == typeof(long) || declaredType == typeof(ulong)
                    => new LongField(label),
                SerializedPropertyType.Integer => new IntegerField(label),
                SerializedPropertyType.Boolean => new Toggle(label),
                SerializedPropertyType.Float when declaredType == typeof(double) => new DoubleField(label),
                SerializedPropertyType.Float => new FloatField(label),
                SerializedPropertyType.String => new TextField(label),
                SerializedPropertyType.Color => new ColorField(label),
                SerializedPropertyType.ObjectReference => new ObjectField(label)
                {
                    objectType = declaredType != null && typeof(Object).IsAssignableFrom(declaredType)
                        ? declaredType
                        : typeof(Object),
                    allowSceneObjects = true
                },
                SerializedPropertyType.Enum when property.enumDisplayNames.Length > 0 => new PopupField<string>(
                    label,
                    new List<string>(property.enumDisplayNames),
                    Mathf.Clamp(property.enumValueIndex, 0, property.enumDisplayNames.Length - 1)),
                SerializedPropertyType.Vector2 => new Vector2Field(label),
                SerializedPropertyType.Vector3 => new Vector3Field(label),
                SerializedPropertyType.Vector4 => new Vector4Field(label),
                SerializedPropertyType.Rect => new RectField(label),
                SerializedPropertyType.Bounds => new BoundsField(label),
                SerializedPropertyType.Vector2Int => new Vector2IntField(label),
                SerializedPropertyType.Vector3Int => new Vector3IntField(label),
                SerializedPropertyType.RectInt => new RectIntField(label),
                SerializedPropertyType.BoundsInt => new BoundsIntField(label),
                SerializedPropertyType.AnimationCurve => new CurveField(label),
                SerializedPropertyType.Gradient => new GradientField(label),
                _ => null
            };

            if (field is BindableElement bindable)
                bindable.BindProperty(property);

            return field;
        }

        public static VisualElement CreateSerializedField(
            SerializedProperty property,
            string label,
            Type declaredType = null,
            bool allowArrayResize = true)
        {
            if (property == null)
                return null;

            if (property.isArray && property.propertyType != SerializedPropertyType.String)
                return CreateArrayField(property, label, allowArrayResize);

            if (property.propertyType == SerializedPropertyType.Generic && property.hasVisibleChildren)
                return CreateCompositeField(property, label);

            return CreateDefaultField(property, label, declaredType)
                ?? CreateUnsupportedField(property, label);
        }

        public static VisualElement CreateArrayField(
            SerializedProperty property,
            string label,
            bool allowResize = true)
        {
            SerializedObject owner = property.serializedObject;
            string propertyPath = property.propertyPath;
            Foldout foldout = new()
            {
                text = label,
                value = property.isExpanded
            };
            VisualElement rows = new();
            int renderedSize = -1;

            void Rebuild(SerializedProperty current)
            {
                if (current == null || renderedSize == current.arraySize)
                    return;

                renderedSize = current.arraySize;
                rows.Clear();
                for (int i = 0; i < current.arraySize; i++)
                {
                    SerializedProperty element = current.GetArrayElementAtIndex(i);
                    PropertyField field = new(element, $"Element {i}");
                    field.Bind(owner);
                    rows.Add(field);
                }

                if (!allowResize)
                    return;

                Button add = new(() => Commit(owner, propertyPath, value => value.arraySize++))
                {
                    text = "+",
                    tooltip = "Add entry"
                };
                Button remove = new(() => Commit(owner, propertyPath, value =>
                {
                    if (value.arraySize > 0)
                        value.arraySize--;
                }))
                {
                    text = "-",
                    tooltip = "Remove last entry"
                };
                add.style.width = CollectionButtonWidth;
                remove.style.width = CollectionButtonWidth;
                remove.SetEnabled(current.arraySize > 0);

                VisualElement buttons = new();
                buttons.style.flexDirection = FlexDirection.Row;
                buttons.style.justifyContent = Justify.FlexEnd;
                buttons.Add(add);
                buttons.Add(remove);
                rows.Add(buttons);
            }

            foldout.RegisterValueChangedCallback(evt =>
                Commit(owner, propertyPath, current => current.isExpanded = evt.newValue));
            foldout.Add(rows);
            Rebuild(property);
            Track(foldout, property, Rebuild);
            return foldout;
        }

        public static VisualElement CreateCompositeField(SerializedProperty property, string label)
        {
            SerializedObject owner = property.serializedObject;
            string propertyPath = property.propertyPath;
            Foldout foldout = new()
            {
                text = label,
                value = property.isExpanded
            };

            foreach (SerializedProperty child in EnumerateVisibleChildren(property))
            {
                PropertyField field = new(child.Copy());
                field.Bind(owner);
                foldout.Add(field);
            }

            foldout.RegisterValueChangedCallback(evt =>
                Commit(owner, propertyPath, current => current.isExpanded = evt.newValue));
            return foldout;
        }

        public static IEnumerable<SerializedProperty> EnumerateVisibleChildren(SerializedProperty property)
        {
            SerializedProperty iterator = property.Copy();
            SerializedProperty end = iterator.GetEndProperty();
            int parentDepth = iterator.depth;
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                if (iterator.depth == parentDepth + 1)
                    yield return iterator.Copy();
            }
        }

        public static PopupField<string> CreatePopup(
            SerializedProperty property,
            string label,
            IReadOnlyList<string> choices,
            int selectedIndex,
            Action<SerializedProperty, int> applySelection)
        {
            List<string> options = choices == null
                ? new List<string>()
                : new List<string>(choices);
            if (options.Count == 0)
                options.Add("None");

            PopupField<string> popup = new(
                label,
                options,
                Mathf.Clamp(selectedIndex, 0, options.Count - 1));
            SerializedObject owner = property.serializedObject;
            string propertyPath = property.propertyPath;
            popup.RegisterValueChangedCallback(evt =>
            {
                int index = popup.choices.IndexOf(evt.newValue);
                Commit(owner, propertyPath, current => applySelection(current, index));
            });
            return popup;
        }

        public static PopupField<string> CreateTrackedPopup(
            SerializedProperty property,
            string label,
            Func<SerializedProperty, IReadOnlyList<string>> getChoices,
            Func<SerializedProperty, IReadOnlyList<string>, int> getSelectedIndex,
            Action<SerializedProperty, IReadOnlyList<string>, int> applySelection)
        {
            IReadOnlyList<string> initialChoices = getChoices(property);
            List<string> options = ToUsableChoices(initialChoices);
            PopupField<string> popup = new(
                label,
                options,
                Mathf.Clamp(getSelectedIndex(property, options), 0, options.Count - 1));
            SerializedObject owner = property.serializedObject;
            string propertyPath = property.propertyPath;
            bool refreshing = false;

            popup.RegisterValueChangedCallback(evt =>
            {
                if (refreshing)
                    return;

                int index = popup.choices.IndexOf(evt.newValue);
                Commit(owner, propertyPath, current => applySelection(current, popup.choices, index));
            });

            Track(popup, property, current =>
            {
                refreshing = true;
                List<string> currentChoices = ToUsableChoices(getChoices(current));
                popup.choices = currentChoices;
                int index = Mathf.Clamp(getSelectedIndex(current, currentChoices), 0, currentChoices.Count - 1);
                popup.SetValueWithoutNotify(currentChoices[index]);
                refreshing = false;
            });
            return popup;
        }

        public static MaskField CreateMaskField(
            SerializedProperty property,
            string label,
            IReadOnlyList<string> choices,
            int displayedMask,
            Func<int, int> toActualMask)
        {
            MaskField field = new(label, new List<string>(choices), displayedMask);
            SerializedObject owner = property.serializedObject;
            string propertyPath = property.propertyPath;
            field.RegisterValueChangedCallback(evt =>
                Commit(owner, propertyPath, current => current.intValue = toActualMask(evt.newValue)));
            return field;
        }

        public static VisualElement CreateFieldWithButtons(
            VisualElement field,
            params Button[] buttons)
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            field.style.flexGrow = 1f;
            field.style.flexShrink = 1f;
            row.Add(field);

            foreach (Button button in buttons)
            {
                button.style.marginLeft = ControlGap;
                row.Add(button);
            }

            return row;
        }

        public static void Commit(
            SerializedObject owner,
            string propertyPath,
            Action<SerializedProperty> mutation)
        {
            if (owner == null || mutation == null)
                return;

            owner.UpdateIfRequiredOrScript();
            SerializedProperty property = owner.FindProperty(propertyPath);
            if (property == null)
                return;

            mutation(property);
            owner.ApplyModifiedProperties();
            PropertyUtils.CallOnFieldChangedCallbacks(property);
        }

        public static void Track(
            VisualElement element,
            SerializedProperty property,
            Action<SerializedProperty> refresh)
        {
            SerializedObject owner = property.serializedObject;
            string propertyPath = property.propertyPath;
            element.TrackSerializedObjectValue(owner, _ =>
            {
                SerializedProperty current = owner.FindProperty(propertyPath);
                if (current != null)
                    refresh(current);
            });
        }

        public static int ToDisplayedMask(int actualMask, IReadOnlyList<int> bitIndices)
        {
            int displayedMask = 0;
            for (int i = 0; i < bitIndices.Count; i++)
            {
                if ((actualMask & (1 << bitIndices[i])) != 0)
                    displayedMask |= 1 << i;
            }

            return displayedMask;
        }

        public static int ToActualMask(int displayedMask, IReadOnlyList<int> bitIndices)
        {
            int actualMask = 0;
            for (int i = 0; i < bitIndices.Count; i++)
            {
                if ((displayedMask & (1 << i)) != 0)
                    actualMask |= 1 << bitIndices[i];
            }

            return actualMask;
        }

        private static List<string> ToUsableChoices(IReadOnlyList<string> choices)
        {
            List<string> options = choices == null
                ? new List<string>()
                : new List<string>(choices);
            if (options.Count == 0)
                options.Add("None");

            return options;
        }

        private static VisualElement CreateUnsupportedField(SerializedProperty property, string label)
        {
            VisualElement root = new();
            Label name = new(label);
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(name);
            root.Add(CreateMessage(
                $"UI Toolkit does not expose a bindable control for {property.propertyType}.",
                HelpBoxMessageType.Warning));
            return root;
        }
    }
}
