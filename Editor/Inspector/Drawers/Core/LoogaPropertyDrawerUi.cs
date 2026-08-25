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
                _ => null
            };

            if (field is BindableElement bindable)
                bindable.BindProperty(property);

            return field;
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
    }
}
