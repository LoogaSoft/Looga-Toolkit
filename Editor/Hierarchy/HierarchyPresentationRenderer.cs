using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Hierarchy.Editor
{
    internal static class HierarchyPresentationRenderer
    {
        private const int GradientResolution = 64;
        private const float GradientOpacity = 0.20f;
        private const float AccentWidth = 3f;
        private const float ContentSpacing = 4f;

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
                    AccentWidth -
                    ContentSpacing;

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
                        AccentWidth,
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

}
