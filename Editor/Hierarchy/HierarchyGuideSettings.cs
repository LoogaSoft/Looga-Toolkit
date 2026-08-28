using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace LoogaSoft.Hierarchy.Editor
{
    /// <summary>
    /// Stores project-wide hierarchy presentation without adding an asset to the Assets folder.
    /// </summary>
    [FilePath(SettingsPath, FilePathAttribute.Location.ProjectFolder)]
    internal sealed class HierarchyGuideSettings : ScriptableSingleton<HierarchyGuideSettings>
    {
        internal const string SettingsPath = "ProjectSettings/LoogaHierarchySettings.asset";

        internal const bool DefaultEnabled = true;
        internal const bool DefaultHighlightInteractiveBranches = true;
        internal const bool DefaultShowPresentation = true;
        internal const bool DefaultShowComponentIcons = true;
        internal const bool DefaultUseCustomColor = false;
        internal const float DefaultOpacity = 0.52f;
        internal const int DefaultThickness = 1;
        internal const int DefaultMaximumComponentIcons = 5;

        private static readonly Color DefaultCustomColor = new(0.48f, 0.52f, 0.58f, 1f);

        [SerializeField]
        private bool _enabled = DefaultEnabled;

        [FormerlySerializedAs("_highlightHoveredBranch")]
        [SerializeField]
        private bool _highlightInteractiveBranches = DefaultHighlightInteractiveBranches;

        [SerializeField]
        private bool _showPresentation = DefaultShowPresentation;

        [FormerlySerializedAs("_showStatusBadges")]
        [SerializeField]
        private bool _showComponentIcons = DefaultShowComponentIcons;

        [SerializeField, Range(1, 8)]
        private int _maximumComponentIcons = DefaultMaximumComponentIcons;

        [SerializeField]
        private bool _useCustomColor = DefaultUseCustomColor;

        [SerializeField]
        private Color _customColor = DefaultCustomColor;

        [SerializeField, Range(0.1f, 1f)]
        private float _opacity = DefaultOpacity;

        [SerializeField, Range(1, 3)]
        private int _thickness = DefaultThickness;

        internal bool Enabled => _enabled;

        internal bool HighlightInteractiveBranches => _highlightInteractiveBranches;

        internal bool ShowPresentation => _showPresentation;

        internal bool ShowComponentIcons => _showComponentIcons;

        internal int MaximumComponentIcons => _maximumComponentIcons;

        internal bool UseCustomColor => _useCustomColor;

        internal Color CustomColor => _customColor;

        internal float Opacity => _opacity;

        internal int Thickness => _thickness;

        internal Color ResolveColor()
        {
            Color color = _useCustomColor
                ? _customColor
                : EditorGUIUtility.isProSkin
                    ? new Color(0.48f, 0.52f, 0.58f, 1f)
                    : new Color(0.30f, 0.34f, 0.39f, 1f);

            color.a *= _opacity;
            return color;
        }

        internal Color ResolveHoverColor()
        {
            return ResolveEmphasisColor(
                EditorGUIUtility.isProSkin ? 1.4f : 1.2f,
                1.45f);
        }

        internal Color ResolveSelectedColor()
        {
            return ResolveEmphasisColor(
                EditorGUIUtility.isProSkin ? 1.55f : 1.28f,
                1.65f);
        }

        internal void SaveSettings()
        {
            Save(true);
            EditorApplication.RepaintHierarchyWindow();
        }

        internal void ResetToDefaults()
        {
            _enabled = DefaultEnabled;
            _highlightInteractiveBranches = DefaultHighlightInteractiveBranches;
            _showPresentation = DefaultShowPresentation;
            _showComponentIcons = DefaultShowComponentIcons;
            _maximumComponentIcons = DefaultMaximumComponentIcons;
            _useCustomColor = DefaultUseCustomColor;
            _customColor = DefaultCustomColor;
            _opacity = DefaultOpacity;
            _thickness = DefaultThickness;
            SaveSettings();
        }

        private Color ResolveEmphasisColor(float brightness, float opacity)
        {
            Color color = ResolveColor();
            color.r = Mathf.Min(1f, color.r * brightness);
            color.g = Mathf.Min(1f, color.g * brightness);
            color.b = Mathf.Min(1f, color.b * brightness);
            color.a = Mathf.Min(1f, color.a * opacity);
            return color;
        }
    }
}
