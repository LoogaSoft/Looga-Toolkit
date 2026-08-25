using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Inspector.Editor
{
    public abstract class PropertyDrawerBase : PropertyDrawer
    {
        public sealed override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            bool visible = PropertyUtils.IsVisible(property);
            if (!visible)
                return;
            
            EditorGUI.BeginChangeCheck();
            
            bool enabled = PropertyUtils.IsEnabled(property);

            GUIContent resolvedLabel = GetResolvedLabel(property, label);

            using (new EditorGUI.DisabledScope(disabled: !enabled))
                OnGUI_Internal(position, property, PropertyUtils.GetFittedLabel(resolvedLabel, position));
            
            if (EditorGUI.EndChangeCheck())
                PropertyUtils.CallOnFieldChangedCallbacks(property);
        }
        protected abstract void OnGUI_Internal(Rect position, SerializedProperty property, GUIContent label);

        public sealed override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            GUIContent label = GetResolvedLabel(property, null);
            VisualElement content = CreatePropertyGUI_Internal(property, label.text)
                ?? CreateLegacyPropertyGUI(property, label);
            VisualElement root = LoogaPropertyDrawerUi.CreateRoot(content, label.tooltip);
            SerializedObject owner = property.serializedObject;
            string propertyPath = property.propertyPath;

            void RefreshState()
            {
                SerializedProperty current = owner?.FindProperty(propertyPath);
                if (current == null)
                    return;

                root.style.display = PropertyUtils.IsVisible(current)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                root.SetEnabled(PropertyUtils.IsEnabled(current));
            }

            RefreshState();
            root.TrackSerializedObjectValue(owner, _ => RefreshState());
            root.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
            {
                if (evt.changedProperty?.propertyPath == propertyPath)
                    PropertyUtils.CallOnFieldChangedCallbacks(evt.changedProperty);
            });
            return root;
        }

        protected virtual VisualElement CreatePropertyGUI_Internal(
            SerializedProperty property,
            string label)
        {
            return null;
        }

        public sealed override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            bool visible = PropertyUtils.IsVisible(property);
            if (!visible)
                return 0f;
            
            return GetPropertyHeight_Internal(property, label);
        }

        protected virtual float GetPropertyHeight_Internal(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, includeChildren: true);
        }

        private VisualElement CreateLegacyPropertyGUI(
            SerializedProperty property,
            GUIContent label)
        {
            SerializedObject owner = property.serializedObject;
            string propertyPath = property.propertyPath;
            return new IMGUIContainer(() =>
            {
                owner.UpdateIfRequiredOrScript();
                SerializedProperty current = owner.FindProperty(propertyPath);
                if (current == null)
                    return;

                float height = GetPropertyHeight_Internal(current, label);
                Rect position = EditorGUILayout.GetControlRect(true, height);
                EditorGUI.BeginChangeCheck();
                OnGUI_Internal(position, current, PropertyUtils.GetFittedLabel(label, position));
                if (!EditorGUI.EndChangeCheck())
                    return;

                owner.ApplyModifiedProperties();
                PropertyUtils.CallOnFieldChangedCallbacks(current);
            });
        }

        private static GUIContent GetResolvedLabel(SerializedProperty property, GUIContent label)
        {
            if (label != null && label != GUIContent.none && !string.IsNullOrWhiteSpace(label.text))
                return label;

            return PropertyUtils.GetLabel(property);
        }
    }
}
