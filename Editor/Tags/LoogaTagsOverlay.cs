using System.Collections.Generic;
using LoogaSoft.Tags.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Tags.Editor
{
    /// <summary>
    /// Supplies the Looga Tags panel used by the shared GameObject inspector toolbar.
    /// The runtime component exists only while the object has at least one selected tag.
    /// </summary>
    public static class LoogaTagsOverlay
    {
        private const string TagGroupPropertyName = "_tagGroup";
        private const string SelectedTagsPropertyName = "_selectedTagGuids";

        private static readonly HashSet<LoogaTags> PendingRemovals = new();

        private static EmptyTagState _emptyTagState;
        private static SerializedObject _emptyTagSerializedObject;
        private static bool _materializationScheduled;
        private static Object[] _materializationTargets;
        private static string[] _materializationTagGuids;
        private static bool _removalScheduled;

        public static VisualElement CreateToolbarPanel()
        {
            TagPanelState state = new();
            IMGUIContainer container = new(() => DrawToolbarPanel(state))
            {
                userData = state
            };
            container.style.flexGrow = 1f;
            container.style.flexShrink = 0f;
            container.style.marginLeft = 0f;
            container.style.marginRight = 0f;
            container.style.marginTop = 0f;
            container.style.marginBottom = 0f;
            return container;
        }

        public static void BindToolbarPanel(VisualElement panel, GameObject gameObject)
        {
            if (panel is not IMGUIContainer container || container.userData is not TagPanelState state)
                return;

            if (state.Target == gameObject)
                return;

            state.Target = gameObject;
            ClearEmptyTagState();
            container.MarkDirtyRepaint();
        }

        private static void DrawToolbarPanel(TagPanelState state)
        {
            GameObject gameObject = state.Target;
            if (gameObject == null)
                return;

            Object[] targets = { gameObject };
            LoogaTags tagComponent = gameObject.GetComponent<LoogaTags>();

            if (tagComponent == null)
                DrawEmptyTagPicker(targets);
            else
                DrawTagPicker(tagComponent, targets);
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

        private sealed class TagPanelState
        {
            public GameObject Target;
        }
    }
}
