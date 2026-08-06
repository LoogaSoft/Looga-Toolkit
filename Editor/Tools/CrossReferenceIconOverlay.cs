using System;
using System.Collections.Generic;
using LoogaSoft.Tools.Runtime;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Tools.Editor
{
    [InitializeOnLoad]
    public static class CrossReferenceIconOverlay
    {
        private static Dictionary<string, bool> _isCrossReferenceCache = new();

        static CrossReferenceIconOverlay()
        {
            EditorApplication.projectWindowItemOnGUI += DrawLinkOverlay;
        }

        private static void DrawLinkOverlay(string guid, Rect selectionRect)
        {
            if (Event.current.type != EventType.Repaint) return;

            if (!_isCrossReferenceCache.TryGetValue(guid, out bool isReference))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Type assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
                
                isReference = assetType == typeof(CrossReference);
                
                //limit cache size to save memory
                if (_isCrossReferenceCache.Count > 100)
                    _isCrossReferenceCache.Clear();
                
                _isCrossReferenceCache[guid] = isReference;
            }

            if (isReference)
            {
                Texture linkIcon = EditorGUIUtility.IconContent("Linked").image;
                if (linkIcon == null) return;

                bool isGrid = selectionRect.height > 20f;
                Rect bgRect;
                Rect iconRect;
                float padding;

                if (isGrid)
                {
                    float bgSize = 24f;
                    padding = 2f;
                    bgRect = new Rect(selectionRect.x + 4f, selectionRect.y + 4f, bgSize, bgSize);
                    iconRect = new Rect(bgRect.x + padding, bgRect.y + padding, bgSize - padding * 2f, bgSize - padding * 2f);
                }
                else
                {
                    float bgSize = 8f;
                    padding = 1f;
                    bgRect = new Rect(selectionRect.x, selectionRect.y + 1f, bgSize, bgSize);
                    iconRect = new Rect(bgRect.x + padding, bgRect.y + padding, bgSize - padding * 2f, bgSize - padding * 2f);
                }
                
                EditorGUI.DrawRect(bgRect, new Color(0.1f, 0.1f, 0.1f, 0.8f));;
                GUI.DrawTexture(iconRect, linkIcon);
            }
        }
    }
}