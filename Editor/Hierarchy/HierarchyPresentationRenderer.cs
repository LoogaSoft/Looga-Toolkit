using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    internal static class HierarchyPresentationRenderer
    {
        private const int GradientResolution = 64;
        private const float GradientOpacity = 0.20f;

        private static readonly Dictionary<Color32, Texture2D> Gradients = new();

        static HierarchyPresentationRenderer()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseGradients;
        }

        internal static bool Draw(GameObject gameObject, Rect rowRect)
        {
            if (!HierarchyPresentationStore.instance.TryGet(gameObject, out HierarchyPresentation presentation))
            {
                return false;
            }

            Color color = presentation.HasLabelColor
                ? presentation.LabelColor
                : HierarchyPresentationStore.DefaultLabelColor;

            if (Event.current.type == EventType.Repaint)
            {
                float decorationX = rowRect.x -
                    HierarchyHeaderStyle.AccentWidth -
                    HierarchyHeaderStyle.ContentSpacing;

                Rect gradientRect = new(
                    decorationX,
                    rowRect.y,
                    rowRect.xMax - decorationX,
                    rowRect.height);

                Color previousColor = GUI.color;
                GUI.color = Color.white;
                GUI.DrawTexture(gradientRect, GetGradient(color), ScaleMode.StretchToFill, true);
                GUI.color = previousColor;

                Color accent = color;
                accent.a = 0.9f;
                // Keep Unity's native icon, label, selection, and rename geometry intact. Drawing the
                // rail before the shared content gap creates separation without replacing its UI.
                EditorGUI.DrawRect(
                    new Rect(
                        decorationX,
                        rowRect.y,
                        HierarchyHeaderStyle.AccentWidth,
                        rowRect.height),
                    accent);
            }

            return false;
        }

        private static Texture2D GetGradient(Color color)
        {
            Color32 key = color;
            if (Gradients.TryGetValue(key, out Texture2D texture) && texture != null)
            {
                return texture;
            }

            texture = new Texture2D(GradientResolution, 1, TextureFormat.RGBA32, false)
            {
                name = "Looga Hierarchy Row Gradient",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[GradientResolution];
            for (int index = 0; index < GradientResolution; index++)
            {
                float normalized = index / (GradientResolution - 1f);
                float fade = 1f - Mathf.SmoothStep(0f, 1f, normalized);
                pixels[index] = new Color(color.r, color.g, color.b, color.a * GradientOpacity * fade);
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Gradients[key] = texture;
            return texture;
        }

        private static void ReleaseGradients()
        {
            foreach (Texture2D texture in Gradients.Values)
            {
                if (texture != null)
                {
                    Object.DestroyImmediate(texture);
                }
            }

            Gradients.Clear();
        }
    }

    /// <summary>
    /// Shared flat header treatment used by favorites and other synthetic hierarchy rows.
    /// </summary>
    internal static class HierarchyHeaderStyle
    {
        internal const float AccentWidth = 3f;
        internal const float ContentSpacing = 4f;

        private const float IconSize = 14f;

        private static GUIStyle _labelStyle;

        internal static void Draw(
            Rect rowRect,
            GUIContent icon,
            string label,
            Color accent,
            bool hovered,
            bool selected,
            bool drawAccentRail = true,
            Color? backgroundOverride = null)
        {
            EnsureStyle();

            Color background = backgroundOverride ?? ResolveBackground(hovered);
            if (selected)
            {
                Color selectedTint = accent;
                selectedTint.a = 1f;
                background = Color.Lerp(background, selectedTint, 0.24f);
            }

            EditorGUI.DrawRect(rowRect, background);

            Rect contentRect = rowRect;
            if (drawAccentRail)
            {
                Color railColor = accent;
                railColor.a = 0.95f;
                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y, AccentWidth, rowRect.height), railColor);
                contentRect.xMin += AccentWidth + ContentSpacing;
            }
            else
            {
                contentRect.xMin += ContentSpacing;
            }

            if (icon.image != null)
            {
                Rect iconRect = new(
                    contentRect.x,
                    contentRect.y + Mathf.Floor((contentRect.height - IconSize) * 0.5f),
                    IconSize,
                    IconSize);

                Color previousColor = GUI.color;
                GUI.color = accent;
                GUI.DrawTexture(iconRect, icon.image, ScaleMode.ScaleToFit, true);
                GUI.color = previousColor;
                contentRect.xMin = iconRect.xMax + ContentSpacing;
            }

            GUI.Label(contentRect, label, _labelStyle);
        }

        private static Color ResolveBackground(bool hovered)
        {
            if (EditorGUIUtility.isProSkin)
            {
                return hovered
                    ? new Color(0.175f, 0.175f, 0.175f, 1f)
                    : new Color(0.135f, 0.135f, 0.135f, 1f);
            }

            return hovered
                ? new Color(0.70f, 0.70f, 0.70f, 1f)
                : new Color(0.76f, 0.76f, 0.76f, 1f);
        }

        private static void EnsureStyle()
        {
            if (_labelStyle != null)
            {
                return;
            }

            _labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };

            _labelStyle.normal.textColor = EditorStyles.label.normal.textColor;
        }
    }
}
