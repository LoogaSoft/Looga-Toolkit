using System;
using UnityEditor;
using LoogaSoft.Tools.Runtime;
using UnityEngine;

namespace LoogaSoft.Tools.Editor
{
    [CustomEditor(typeof(CrossReference))]
    public sealed class CrossReferenceEditor : UnityEditor.Editor
    {
        private UnityEditor.Editor _referenceEditor;

        private void OnEnable()
        {
            UpdateReferenceEditor();
        }

        private void OnDisable()
        {
            if (_referenceEditor != null)
            {
                DestroyImmediate(_referenceEditor);
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_reference"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                UpdateReferenceEditor();
                UpdateReferenceIcon();
            }

            if (_referenceEditor != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Reference Editor", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                _referenceEditor.OnInspectorGUI();
                EditorGUILayout.EndVertical();
            }
        }

        private void UpdateReferenceEditor()
        {
            CrossReference crossReference = target as CrossReference;
            if (_referenceEditor != null)
            {
                DestroyImmediate(_referenceEditor);
                _referenceEditor = null;
            }
            if (crossReference?.Reference != null)
            {
                _referenceEditor = CreateEditor(crossReference.Reference);
            }
        }

        private void UpdateReferenceIcon()
        {
            CrossReference crossReference = target as CrossReference;
            Texture2D icon = null;

            if (crossReference?.Reference != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(crossReference.Reference);
                icon = AssetDatabase.GetCachedIcon(assetPath) as Texture2D;
            }

            EditorGUIUtility.SetIconForObject(crossReference, icon);

            EditorUtility.SetDirty(crossReference);
            AssetDatabase.SaveAssets();
        }
    }
}
