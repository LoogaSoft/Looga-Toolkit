using System;
using System.Collections.Generic;
using LoogaSoft.Tools.Runtime;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Tools.Editor
{
    [InitializeOnLoad]
    internal static class CrossReferenceIconOverlay
    {
        private static readonly Dictionary<string, bool> IsCrossReferenceByGuid = new();
        private static readonly Texture LinkIcon = EditorGUIUtility.IconContent("Linked").image;

        static CrossReferenceIconOverlay()
        {
            EditorApplication.projectChanged -= ClearCache;
            EditorApplication.projectChanged += ClearCache;
            EditorApplication.projectWindowItemOnGUI -= DrawLinkOverlay;
            EditorApplication.projectWindowItemOnGUI += DrawLinkOverlay;
        }

        private static void DrawLinkOverlay(string guid, Rect selectionRect)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (!IsCrossReferenceByGuid.TryGetValue(guid, out bool isReference))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Type assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
                isReference = assetType == typeof(CrossReference);
                IsCrossReferenceByGuid[guid] = isReference;
            }

            if (!isReference || LinkIcon == null)
            {
                return;
            }

            bool isGrid = selectionRect.height > 20f;
            Rect backgroundRect;
            Rect iconRect;
            float padding;

            if (isGrid)
            {
                const float backgroundSize = 24f;
                padding = 2f;
                backgroundRect = new Rect(selectionRect.x + 4f, selectionRect.y + 4f, backgroundSize, backgroundSize);
                iconRect = new Rect(
                    backgroundRect.x + padding,
                    backgroundRect.y + padding,
                    backgroundSize - padding * 2f,
                    backgroundSize - padding * 2f);
            }
            else
            {
                const float backgroundSize = 8f;
                padding = 1f;
                backgroundRect = new Rect(selectionRect.x, selectionRect.y + 1f, backgroundSize, backgroundSize);
                iconRect = new Rect(
                    backgroundRect.x + padding,
                    backgroundRect.y + padding,
                    backgroundSize - padding * 2f,
                    backgroundSize - padding * 2f);
            }

            EditorGUI.DrawRect(backgroundRect, new Color(0.1f, 0.1f, 0.1f, 0.8f));
            GUI.DrawTexture(iconRect, LinkIcon);
        }

        private static void ClearCache()
        {
            IsCrossReferenceByGuid.Clear();
        }
    }
}
