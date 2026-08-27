using System;
using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Inspector.Editor
{
    /// <summary>
    /// Layout-based Looga Inspector controls for custom inspectors and editor windows.
    /// Use this like EditorGUILayout/GUILayout when you do not manually own Rect layout.
    /// </summary>
    public static class LoogaGUILayout
    {
        /// <summary>
        /// Draws a serialized property with Looga Inspector label fitting and tooltip handling.
        /// Use this in custom inspectors instead of EditorGUILayout.PropertyField.
        /// </summary>
        public static void PropertyField(SerializedProperty property, bool includeChildren = false)
        {
            PropertyField(property, PropertyUtils.GetLabel(property), includeChildren);
        }

        /// <summary>
        /// Draws a serialized property with a custom label that fits the current label column.
        /// </summary>
        public static void PropertyField(
            SerializedProperty property,
            GUIContent label,
            bool includeChildren = false)
        {
            EditorGUILayout.PropertyField(
                property,
                PropertyUtils.GetFittedLabel(label ?? PropertyUtils.GetLabel(property)),
                includeChildren);
        }

        public static int Tabs(int selectedIndex, string[] tabNames, string controlId)
        {
            return LoogaEditorTabs.DrawWrappingToolbar(selectedIndex, tabNames, controlId);
        }

        public static int Tabs(
            int selectedIndex,
            string[] tabNames,
            string controlId,
            float rightControlWidth,
            float rightControlGap,
            Action drawRightControl)
        {
            return LoogaEditorTabs.DrawWrappingToolbarWithRightControl(
                selectedIndex,
                tabNames,
                controlId,
                rightControlWidth,
                rightControlGap,
                drawRightControl);
        }

        public static void Foldout(string title, string stateKey, bool defaultExpanded, Action content)
        {
            LoogaEditorFoldouts.LoogaFoldout(title, stateKey, defaultExpanded, content);
        }

        public static bool Foldout(
            GUIContent label,
            bool expanded,
            Action content,
            SerializedProperty property = null)
        {
            return LoogaEditorFoldouts.LoogaFoldout(label, expanded, content, property);
        }

        public static bool Foldout(
            string label,
            bool expanded,
            Action content,
            SerializedProperty property = null)
        {
            return Foldout(new GUIContent(label), expanded, content, property);
        }

        public static bool ToggleFoldout(
            GUIContent label,
            bool enabled,
            bool expanded,
            Action content,
            out bool newEnabled)
        {
            return LoogaEditorFoldouts.LoogaToggleFoldout(
                label,
                enabled,
                expanded,
                content,
                out newEnabled);
        }

        public static bool ToggleFoldout(
            string label,
            bool enabled,
            bool expanded,
            Action content,
            out bool newEnabled)
        {
            return ToggleFoldout(
                new GUIContent(label),
                enabled,
                expanded,
                content,
                out newEnabled);
        }

        public static void BoxLarge(string title, Action content)
        {
            LoogaEditorFoldouts.LoogaBoxLarge(title, content);
        }

        public static void BoxSmall(GUIContent label, Action content)
        {
            LoogaEditorFoldouts.LoogaBoxSmall(label, content);
        }

        public static void BoxSmall(string label, Action content)
        {
            BoxSmall(new GUIContent(label), content);
        }

        public static bool Notice(
            string message,
            LoogaNoticeType type = LoogaNoticeType.Info,
            bool hasAction = false,
            string actionLabel = "",
            string actionTooltip = "Open",
            bool showBackground = true)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, LoogaGUI.GetNoticeHeight(message));
            return LoogaGUI.Notice(rect, message, type, hasAction, actionLabel, actionTooltip, showBackground);
        }
    }
}
