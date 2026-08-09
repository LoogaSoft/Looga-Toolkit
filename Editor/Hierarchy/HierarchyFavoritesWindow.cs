using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    /// <summary>
    /// Lists personal hierarchy shortcuts without adding synthetic objects to open scenes.
    /// </summary>
    internal sealed class HierarchyFavoritesWindow : EditorWindow
    {
        private const float RowHeight = 24f;
        private const float RemoveButtonWidth = 24f;

        private Vector2 _scrollPosition;

        [MenuItem("LoogaSoft/Toolkit/Hierarchy Favorites", false, 120)]
        private static void Open()
        {
            HierarchyFavoritesWindow window = GetWindow<HierarchyFavoritesWindow>();
            window.titleContent = new GUIContent("Favorites", EditorGUIUtility.IconContent("Favorite").image);
            window.minSize = new Vector2(280f, 160f);
            window.Show();
        }

        private void OnEnable()
        {
            HierarchyFavoriteStore.Changed += Repaint;
            EditorApplication.hierarchyChanged += Repaint;
        }

        private void OnDisable()
        {
            HierarchyFavoriteStore.Changed -= Repaint;
            EditorApplication.hierarchyChanged -= Repaint;
        }

        private void OnGUI()
        {
            IReadOnlyList<HierarchyFavorite> favorites = HierarchyFavoriteStore.instance.Entries;
            if (favorites.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Favorite a GameObject from its hierarchy context menu or star button.",
                    MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            for (int index = 0; index < favorites.Count; index++)
                DrawFavorite(favorites[index]);
            EditorGUILayout.EndScrollView();
        }

        private static void DrawFavorite(HierarchyFavorite favorite)
        {
            GameObject gameObject = HierarchyObjectId.Resolve(favorite.ObjectId);
            Rect rowRect = EditorGUILayout.GetControlRect(false, RowHeight);
            Rect removeRect = new(
                rowRect.xMax - RemoveButtonWidth,
                rowRect.y,
                RemoveButtonWidth,
                rowRect.height);
            Rect contentRect = new(
                rowRect.x,
                rowRect.y,
                Mathf.Max(0f, removeRect.x - rowRect.x - 2f),
                rowRect.height);

            string name = gameObject != null ? gameObject.name : favorite.DisplayName;
            string scene = string.IsNullOrEmpty(favorite.ScenePath)
                ? "Unsaved Scene"
                : System.IO.Path.GetFileNameWithoutExtension(favorite.ScenePath);
            GUIContent content = new(
                $"{name}  ({scene})",
                gameObject != null ? AssetPreview.GetMiniThumbnail(gameObject) : null,
                gameObject != null
                    ? "Select and ping this GameObject."
                    : "This GameObject is not available in the loaded scenes.");

            using (new EditorGUI.DisabledScope(gameObject == null))
            {
                if (GUI.Button(contentRect, content, EditorStyles.miniButton))
                {
                    Selection.activeGameObject = gameObject;
                    EditorGUIUtility.PingObject(gameObject);
                }
            }

            if (GUI.Button(removeRect, EditorGUIUtility.IconContent("TreeEditor.Trash"), EditorStyles.miniButton))
                HierarchyFavoriteStore.instance.Remove(favorite.ObjectId);
        }
    }
}
