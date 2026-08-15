using System.Collections.Generic;
using LoogaSoft.Tags.Runtime;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Tags.Editor
{
    [InitializeOnLoad]
    internal static class LoogaTagsOverlay
    {
        private const float ControlRowHeight = 20f;
        private const string TagGroupPropertyName = "_tagGroup";
        private const string SelectedTagsPropertyName = "_selectedTagGuids";

        private static readonly HashSet<LoogaTags> PendingRemovals = new();

        private static EmptyTagState _emptyTagState;
        private static SerializedObject _emptyTagSerializedObject;
        private static bool _materializationScheduled;
        private static Object[] _materializationTargets;
        private static string[] _materializationTagGuids;
        private static bool _removalScheduled;

        static LoogaTagsOverlay()
        {
            UnityEditor.Editor.finishedDefaultHeaderGUI -= OnPostHeaderGUI;
            UnityEditor.Editor.finishedDefaultHeaderGUI += OnPostHeaderGUI;
        }

        private static void OnPostHeaderGUI(UnityEditor.Editor editor)
        {
            if (editor.target is not GameObject)
                return;

            Object[] targets = editor.targets;
            LoogaTags tagComponent = FindFirstTagComponent(targets);
            DrawControlRow(targets);

            if (tagComponent == null)
                DrawEmptyTagPicker(targets);
            else
                DrawTagPicker(tagComponent, targets);

            EditorGUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
        }

        private static void DrawControlRow(Object[] targets)
        {
            Rect rowRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.Height(ControlRowHeight),
                GUILayout.ExpandWidth(true));

            using (new EditorGUI.DisabledScope(!HasAnyTags(targets)))
            {
                if (!GUI.Button(rowRect, "Clear Tags"))
                    return;
            }

            foreach (Object target in targets)
            {
                if (target is not GameObject gameObject ||
                    !gameObject.TryGetComponent(out LoogaTags tags))
                {
                    continue;
                }

                Undo.RecordObject(tags, "Clear Looga Tags");
                tags.ClearTags();
                EditorUtility.SetDirty(tags);
                ScheduleRemoval(tags);
            }
        }

        private static void DrawEmptyTagPicker(Object[] targets)
        {
            EnsureEmptyTagState();
            _emptyTagSerializedObject.Update();

            SerializedProperty tagGroup = _emptyTagSerializedObject.FindProperty(TagGroupPropertyName);
            EditorGUILayout.PropertyField(tagGroup, GUIContent.none, true);
            _emptyTagSerializedObject.ApplyModifiedProperties();
            _emptyTagSerializedObject.Update();

            SerializedProperty selectedTags = tagGroup.FindPropertyRelative(SelectedTagsPropertyName);
            if (selectedTags.arraySize > 0)
                ScheduleMaterialization(targets, ReadTagGuids(selectedTags));
        }

        private static void DrawTagPicker(LoogaTags tagComponent, Object[] targets)
        {
            SerializedObject serializedTags = new(tagComponent);
            serializedTags.Update();

            SerializedProperty tagGroup = serializedTags.FindProperty(TagGroupPropertyName);
            SerializedProperty selectedTags = tagGroup.FindPropertyRelative(SelectedTagsPropertyName);
            int selectedTagCountBefore = selectedTags.arraySize;

            EditorGUILayout.PropertyField(tagGroup, GUIContent.none, true);
            serializedTags.ApplyModifiedProperties();
            serializedTags.Update();

            selectedTags = serializedTags
                .FindProperty(TagGroupPropertyName)
                .FindPropertyRelative(SelectedTagsPropertyName);

            if (selectedTagCountBefore > 0 && selectedTags.arraySize == 0)
            {
                foreach (Object target in targets)
                {
                    if (target is GameObject gameObject &&
                        gameObject.TryGetComponent(out LoogaTags tags) &&
                        tags.TagGroup.SelectedTagGuids is not { Count: > 0 })
                    {
                        ScheduleRemoval(tags);
                    }
                }
            }
        }

        private static void EnsureEmptyTagState()
        {
            if (_emptyTagState != null && _emptyTagSerializedObject != null)
                return;

            _emptyTagState = ScriptableObject.CreateInstance<EmptyTagState>();
            _emptyTagState.hideFlags = HideFlags.HideAndDontSave;
            _emptyTagSerializedObject = new SerializedObject(_emptyTagState);
        }

        private static void ScheduleMaterialization(Object[] targets, string[] tagGuids)
        {
            if (_materializationScheduled || tagGuids.Length == 0)
                return;

            _materializationScheduled = true;
            _materializationTargets = (Object[])targets.Clone();
            _materializationTagGuids = tagGuids;
            EditorApplication.delayCall += MaterializeTagComponents;
        }

        private static void MaterializeTagComponents()
        {
            EditorApplication.delayCall -= MaterializeTagComponents;
            _materializationScheduled = false;

            Object[] targets = _materializationTargets;
            string[] tagGuids = _materializationTagGuids;
            _materializationTargets = null;
            _materializationTagGuids = null;

            if (targets == null || tagGuids == null || tagGuids.Length == 0)
                return;

            foreach (Object target in targets)
            {
                if (target is not GameObject gameObject)
                    continue;

                LoogaTags tags = gameObject.GetComponent<LoogaTags>();
                if (tags == null)
                    tags = Undo.AddComponent<LoogaTags>(gameObject);

                SerializedObject serializedTags = new(tags);
                serializedTags.Update();
                SerializedProperty selectedTags = serializedTags
                    .FindProperty(TagGroupPropertyName)
                    .FindPropertyRelative(SelectedTagsPropertyName);

                selectedTags.arraySize = tagGuids.Length;
                for (int index = 0; index < tagGuids.Length; index++)
                    selectedTags.GetArrayElementAtIndex(index).stringValue = tagGuids[index];

                serializedTags.ApplyModifiedProperties();
                EditorUtility.SetDirty(tags);
            }

            ClearEmptyTagState();
            RepaintEditorViews();
        }

        private static void ClearEmptyTagState()
        {
            if (_emptyTagSerializedObject == null)
                return;

            _emptyTagSerializedObject.Update();
            SerializedProperty selectedTags = _emptyTagSerializedObject
                .FindProperty(TagGroupPropertyName)
                .FindPropertyRelative(SelectedTagsPropertyName);
            selectedTags.ClearArray();
            _emptyTagSerializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ScheduleRemoval(LoogaTags tags)
        {
            if (tags == null)
                return;

            PendingRemovals.Add(tags);
            if (_removalScheduled)
                return;

            _removalScheduled = true;
            EditorApplication.delayCall += RemoveEmptyTagComponents;
        }

        private static void RemoveEmptyTagComponents()
        {
            EditorApplication.delayCall -= RemoveEmptyTagComponents;
            _removalScheduled = false;

            foreach (LoogaTags tags in PendingRemovals)
            {
                if (tags == null || tags.TagGroup.SelectedTagGuids is { Count: > 0 })
                {
                    continue;
                }

                Undo.DestroyObjectImmediate(tags);
            }

            PendingRemovals.Clear();
            RepaintEditorViews();
        }

        private static LoogaTags FindFirstTagComponent(Object[] targets)
        {
            foreach (Object target in targets)
            {
                if (target is GameObject gameObject && gameObject.TryGetComponent(out LoogaTags tags))
                    return tags;
            }

            return null;
        }

        private static bool HasAnyTags(Object[] targets)
        {
            foreach (Object target in targets)
            {
                if (target is GameObject gameObject &&
                    gameObject.TryGetComponent(out LoogaTags tags) &&
                    tags.TagGroup.SelectedTagGuids is { Count: > 0 })
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] ReadTagGuids(SerializedProperty selectedTags)
        {
            string[] tagGuids = new string[selectedTags.arraySize];
            for (int index = 0; index < selectedTags.arraySize; index++)
                tagGuids[index] = selectedTags.GetArrayElementAtIndex(index).stringValue;

            return tagGuids;
        }

        private static void RepaintEditorViews()
        {
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                window.Repaint();
        }

        private sealed class EmptyTagState : ScriptableObject
        {
            [SerializeField]
            private LoogaTagGroup _tagGroup;
        }
    }
}
