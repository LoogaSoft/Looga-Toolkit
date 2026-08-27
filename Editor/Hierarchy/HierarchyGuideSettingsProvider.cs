using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Hierarchy.Editor
{
    internal sealed class HierarchyGuideSettingsProvider : SettingsProvider
    {
        private SerializedObject _serializedSettings;
        private SerializedProperty _enabled;
        private SerializedProperty _highlightInteractiveBranches;
        private SerializedProperty _showFavorites;
        private SerializedProperty _showPresentation;
        private SerializedProperty _showComponentIcons;
        private SerializedProperty _maximumComponentIcons;
        private SerializedProperty _useCustomColor;
        private SerializedProperty _customColor;
        private SerializedProperty _opacity;
        private SerializedProperty _thickness;

        private HierarchyGuideSettingsProvider()
            : base("Project/LoogaSoft/Toolkit/Hierarchy", SettingsScope.Project)
        {
            label = "Hierarchy";
            keywords = new[] { "Looga", "Hierarchy", "Guides", "Lines", "Tree", "Parents", "Children" };
        }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new HierarchyGuideSettingsProvider();
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            BindSettings();
        }

        public override void OnGUI(string searchContext)
        {
            if (_serializedSettings == null || _serializedSettings.targetObject == null)
            {
                BindSettings();
            }

            _serializedSettings.Update();

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "Looga Hierarchy improves scene organization without changing scenes or adding runtime components.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_enabled, new GUIContent("Hierarchy Guides"));

            using (new EditorGUI.DisabledScope(!_enabled.boolValue))
            {
                EditorGUILayout.PropertyField(_useCustomColor, new GUIContent("Custom Color"));

                using (new EditorGUI.DisabledScope(!_useCustomColor.boolValue))
                {
                    EditorGUILayout.PropertyField(_customColor, new GUIContent("Guide Color"));
                }

                EditorGUILayout.Slider(_opacity, 0.1f, 1f, new GUIContent("Opacity"));
                EditorGUILayout.IntSlider(_thickness, 1, 3, new GUIContent("Thickness", "Physical pixels."));
                EditorGUILayout.PropertyField(
                    _highlightInteractiveBranches,
                    new GUIContent(
                        "Interactive Branch Highlights",
                        "Emphasize the direct parent connector for hovered and selected objects."));
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Organization", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_showFavorites, new GUIContent("Favorites"));
            EditorGUILayout.PropertyField(_showPresentation, new GUIContent("Object Colors"));
            EditorGUILayout.PropertyField(_showComponentIcons, new GUIContent("Component Icons"));

            using (new EditorGUI.DisabledScope(!_showComponentIcons.boolValue))
            {
                EditorGUILayout.IntSlider(
                    _maximumComponentIcons,
                    1,
                    8,
                    new GUIContent(
                        "Maximum Component Icons",
                        "Maximum number of component summary icons shown on each row."));
            }

            if (EditorGUI.EndChangeCheck())
            {
                _serializedSettings.ApplyModifiedProperties();
                HierarchyGuideSettings.instance.SaveSettings();
            }

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Restore Defaults", GUILayout.Width(120f)))
            {
                Undo.RecordObject(HierarchyGuideSettings.instance, "Restore Hierarchy Guide Defaults");
                HierarchyGuideSettings.instance.ResetToDefaults();
                BindSettings();
            }
        }

        private void BindSettings()
        {
            _serializedSettings = new SerializedObject(HierarchyGuideSettings.instance);
            _enabled = _serializedSettings.FindProperty("_enabled");
            _highlightInteractiveBranches = _serializedSettings.FindProperty("_highlightInteractiveBranches");
            _showFavorites = _serializedSettings.FindProperty("_showFavorites");
            _showPresentation = _serializedSettings.FindProperty("_showPresentation");
            _showComponentIcons = _serializedSettings.FindProperty("_showComponentIcons");
            _maximumComponentIcons = _serializedSettings.FindProperty("_maximumComponentIcons");
            _useCustomColor = _serializedSettings.FindProperty("_useCustomColor");
            _customColor = _serializedSettings.FindProperty("_customColor");
            _opacity = _serializedSettings.FindProperty("_opacity");
            _thickness = _serializedSettings.FindProperty("_thickness");
        }
    }
}
