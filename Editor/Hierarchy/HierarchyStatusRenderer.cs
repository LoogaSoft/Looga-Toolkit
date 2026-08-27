using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    /// <summary>
    /// Draws compact status indicators and routes actionable badges to their editor workflows.
    /// </summary>
    internal static class HierarchyStatusRenderer
    {
        private const float BadgeSize = 14f;
        private const float BadgeSpacing = 1f;
        private const float PrefabOverrideIconSize = 8f;

        private static readonly Rect PrefabOverrideIconUv = new(0.5f, 0f, 0.5f, 0.5f);

        private static readonly GUIContent MissingScriptContent =
            CreateIconContent("console.erroricon.sml", "!", "Missing script - click for actions");
        private static readonly GUIContent PrefabOverrideContent =
            CreateIconContent("PrefabOverlayAdded Icon", "O", "Prefab overrides - click for actions");
        private static readonly GUIContent PrefabOverrideTooltipContent =
            new(string.Empty, "Prefab overrides - click for actions");
        private static readonly GUIContent StaticContent = new("S");
        private static readonly GUIContent EditorOnlyContent = new("E", "EditorOnly GameObject - click for actions");

        private static readonly GUIStyle LetterStyle = new(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 8
        };

        private static readonly GUIStyle StaticLetterStyle = new(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 10
        };

        internal static float GetReservedWidth(GameObject gameObject)
        {
            HierarchyStatus status = HierarchyStatusCache.Get(gameObject);
            int count = 0;

            while (status != HierarchyStatus.None)
            {
                count += (int)status & 1;
                status = (HierarchyStatus)((int)status >> 1);
            }

            return count * (BadgeSize + BadgeSpacing);
        }

        internal static void Draw(GameObject gameObject, Rect rowRect)
        {
            HierarchyStatus status = HierarchyStatusCache.Get(gameObject);
            if (status == HierarchyStatus.None)
            {
                return;
            }

            float right = rowRect.xMax - 1f;
            DrawIfPresent(gameObject, status, HierarchyStatus.MissingScript, MissingScriptContent, rowRect, ref right, null);
            DrawIfPresent(gameObject, status, HierarchyStatus.PrefabOverride, PrefabOverrideContent, rowRect, ref right, null);
            DrawStaticIfPresent(gameObject, status, rowRect, ref right);
            DrawIfPresent(
                gameObject,
                status,
                HierarchyStatus.EditorOnly,
                EditorOnlyContent,
                rowRect,
                ref right,
                new Color(0.72f, 0.52f, 0.88f));
        }

        private static void DrawIfPresent(
            GameObject gameObject,
            HierarchyStatus statuses,
            HierarchyStatus expected,
            GUIContent content,
            Rect rowRect,
            ref float right,
            Color? color)
        {
            if ((statuses & expected) == 0)
            {
                return;
            }

            Rect badgeRect = new(
                right - BadgeSize,
                rowRect.y + Mathf.Floor((rowRect.height - BadgeSize) * 0.5f),
                BadgeSize,
                BadgeSize);

            bool hovered = badgeRect.Contains(Event.current.mousePosition);
            EditorGUIUtility.AddCursorRect(badgeRect, MouseCursor.Link);

            if (Event.current.type == EventType.MouseMove && hovered)
            {
                EditorApplication.RepaintHierarchyWindow();
            }

            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                hovered)
            {
                ShowMenu(gameObject, expected);
                Event.current.Use();
            }

            if (Event.current.type == EventType.Repaint)
            {
                if (color.HasValue)
                {
                    Color background = color.Value;
                    background.a = hovered ? 0.52f : 0.34f;
                    EditorGUI.DrawRect(badgeRect, background);
                    GUI.Label(badgeRect, content, LetterStyle);
                }
                else
                {
                    if (hovered)
                    {
                        EditorGUI.DrawRect(badgeRect, new Color(1f, 1f, 1f, 0.12f));
                    }

                    if (expected == HierarchyStatus.PrefabOverride)
                    {
                        DrawPrefabOverrideIcon(badgeRect, content);
                    }
                    else
                    {
                        GUI.Label(badgeRect, content);
                    }
                }
            }

            right -= BadgeSize + BadgeSpacing;
        }

        private static void DrawPrefabOverrideIcon(Rect badgeRect, GUIContent content)
        {
            if (content.image == null)
            {
                GUI.Label(badgeRect, content, LetterStyle);
                return;
            }

            // Unity stores this glyph in the lower-right quadrant because the Editor normally draws it as an overlay.
            float pixelScale = EditorGUIUtility.pixelsPerPoint;
            float iconX = Mathf.Round((badgeRect.center.x - PrefabOverrideIconSize * 0.5f) * pixelScale) / pixelScale;
            float iconY = Mathf.Round((badgeRect.center.y - PrefabOverrideIconSize * 0.5f) * pixelScale) / pixelScale;
            Rect iconRect = new(iconX, iconY, PrefabOverrideIconSize, PrefabOverrideIconSize);

            GUI.DrawTextureWithTexCoords(iconRect, content.image, PrefabOverrideIconUv, true);
            GUI.Label(badgeRect, PrefabOverrideTooltipContent, GUIStyle.none);
        }

        private static void DrawStaticIfPresent(
            GameObject gameObject,
            HierarchyStatus statuses,
            Rect rowRect,
            ref float right)
        {
            if ((statuses & HierarchyStatus.Static) == 0)
            {
                return;
            }

            Rect badgeRect = new(
                right - BadgeSize,
                rowRect.y + Mathf.Floor((rowRect.height - BadgeSize) * 0.5f),
                BadgeSize,
                BadgeSize);

            if (Event.current.type == EventType.Repaint)
            {
                StaticContent.tooltip = HierarchyStatusCache.GetStaticTooltip(gameObject);
                GUI.Label(badgeRect, StaticContent, StaticLetterStyle);
            }

            right -= BadgeSize + BadgeSpacing;
        }

        private static void ShowMenu(GameObject gameObject, HierarchyStatus status)
        {
            GenericMenu menu = new();
            menu.AddDisabledItem(new GUIContent(GetStatusName(status)));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Select And Ping"), false, () => SelectAndPing(gameObject));

            switch (status)
            {
                case HierarchyStatus.MissingScript:
                    AddMissingScriptActions(menu, gameObject);
                    break;

                case HierarchyStatus.PrefabOverride:
                    AddPrefabActions(menu, gameObject);
                    break;

                case HierarchyStatus.EditorOnly:
                    menu.AddItem(new GUIContent("Set Tag To Untagged"), false, () => ClearEditorOnlyTag(gameObject));
                    break;
            }

            menu.ShowAsContext();
        }

        private static void AddMissingScriptActions(GenericMenu menu, GameObject gameObject)
        {
            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
            menu.AddItem(
                new GUIContent($"Remove {count} Missing Script{(count == 1 ? string.Empty : "s")}..."),
                false,
                () => RemoveMissingScripts(gameObject, count));
        }

        private static void AddPrefabActions(GenericMenu menu, GameObject gameObject)
        {
            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject);
            Object source = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Select Instance Root"), false, () => SelectAndPing(root));

            if (source != null)
            {
                menu.AddItem(new GUIContent("Open Prefab Asset"), false, () => AssetDatabase.OpenAsset(source));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Open Prefab Asset"));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Apply All Instance Overrides..."), false, () => ApplyOverrides(root));
            menu.AddItem(new GUIContent("Revert All Instance Overrides..."), false, () => RevertOverrides(root));
        }

        private static void SelectAndPing(Object target)
        {
            if (target == null)
            {
                return;
            }

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        private static void RemoveMissingScripts(GameObject gameObject, int count)
        {
            if (gameObject == null ||
                !EditorUtility.DisplayDialog(
                    "Remove Missing Scripts",
                    $"Remove {count} missing script reference{(count == 1 ? string.Empty : "s")} from '{gameObject.name}'?",
                    "Remove",
                    "Cancel"))
            {
                return;
            }

            Undo.RegisterCompleteObjectUndo(gameObject, "Remove Missing Scripts");
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(gameObject);
            EditorUtility.SetDirty(gameObject);
            HierarchyStatusCache.Invalidate();
        }

        private static void ClearEditorOnlyTag(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            Undo.RecordObject(gameObject, "Clear EditorOnly Tag");
            gameObject.tag = "Untagged";
            PrefabUtility.RecordPrefabInstancePropertyModifications(gameObject);
            EditorUtility.SetDirty(gameObject);
            HierarchyStatusCache.Invalidate();
        }

        private static void ApplyOverrides(GameObject root)
        {
            if (root == null ||
                !EditorUtility.DisplayDialog(
                    "Apply Prefab Overrides",
                    $"Apply all overrides on '{root.name}' to its prefab asset?",
                    "Apply All",
                    "Cancel"))
            {
                return;
            }

            PrefabUtility.ApplyPrefabInstance(root, InteractionMode.UserAction);
            HierarchyStatusCache.Invalidate();
        }

        private static void RevertOverrides(GameObject root)
        {
            if (root == null ||
                !EditorUtility.DisplayDialog(
                    "Revert Prefab Overrides",
                    $"Revert all overrides on '{root.name}'? This cannot be undone after closing the editor.",
                    "Revert All",
                    "Cancel"))
            {
                return;
            }

            PrefabUtility.RevertPrefabInstance(root, InteractionMode.UserAction);
            HierarchyStatusCache.Invalidate();
        }

        private static string GetStatusName(HierarchyStatus status)
        {
            return status switch
            {
                HierarchyStatus.MissingScript => "Missing Script",
                HierarchyStatus.PrefabOverride => "Prefab Overrides",
                HierarchyStatus.EditorOnly => "EditorOnly GameObject",
                _ => "Hierarchy Status"
            };
        }

        private static GUIContent CreateIconContent(string iconName, string fallback, string tooltip)
        {
            GUIContent content = EditorGUIUtility.IconContent(iconName);
            if (content.image == null)
            {
                content = new GUIContent(fallback);
            }

            content.tooltip = tooltip;
            return content;
        }
    }
}
