using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    internal static class HierarchyFavoriteRenderer
    {
        private const float ButtonSize = 16f;
        private static readonly GUIContent FavoriteContent = CreateFavoriteContent();

        internal static void Draw(GameObject gameObject, Rect rowRect, HierarchyGuideSettings settings)
        {
            bool favorite = HierarchyFavoriteStore.instance.Contains(gameObject);
            bool hovered = rowRect.Contains(Event.current.mousePosition);
            if (!favorite && !hovered)
            {
                return;
            }

            Rect buttonRect = new(
                rowRect.xMax -
                (settings.ShowComponentIcons
                    ? HierarchyComponentRenderer.GetReservedWidth(
                        gameObject,
                        rowRect,
                        settings.MaximumComponentIcons)
                    : 0f) -
                ButtonSize,
                rowRect.y + Mathf.Floor((rowRect.height - ButtonSize) * 0.5f),
                ButtonSize,
                ButtonSize);

            Color previousColor = GUI.color;
            GUI.color = favorite ? new Color(1f, 0.78f, 0.24f, 1f) : new Color(1f, 1f, 1f, 0.55f);

            if (GUI.Button(buttonRect, FavoriteContent, GUIStyle.none))
            {
                HierarchyFavoriteStore.instance.Toggle(gameObject);
                Event.current.Use();
            }

            GUI.color = previousColor;
        }

        private static GUIContent CreateFavoriteContent()
        {
            GUIContent content = EditorGUIUtility.IconContent("Favorite");
            if (content.image == null)
            {
                content = new GUIContent("*");
            }

            content.tooltip = "Toggle hierarchy favorite";
            return content;
        }
    }
}
