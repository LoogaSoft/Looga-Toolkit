using System.Collections.Generic;
using UnityEngine;
using ArgumentException = System.ArgumentException;
using ArgumentNullException = System.ArgumentNullException;

namespace LoogaSoft.Tools.Runtime
{
    /// <summary>
    /// Provides small, allocation-free helpers for common Unity operations.
    /// </summary>
    public static class LoogaExtensions
    {
        /// <summary>
        /// Gets a component or adds it when the GameObject does not contain one.
        /// </summary>
        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            if (gameObject == null)
            {
                throw new ArgumentNullException(nameof(gameObject));
            }

            T component = gameObject.GetComponent<T>();
            if (component == null)
            {
                component = gameObject.AddComponent<T>();
            }

            return component;
        }

        /// <summary>
        /// Gets a sibling component or adds it when the GameObject does not contain one.
        /// </summary>
        public static T GetOrAdd<T>(this Component component) where T : Component
        {
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            return component.gameObject.GetOrAddComponent<T>();
        }

        /// <summary>
        /// Returns a Vector2 with only the specified components replaced.
        /// </summary>
        public static Vector2 With(this Vector2 vector, float? x = null, float? y = null)
        {
            return new Vector2(x ?? vector.x, y ?? vector.y);
        }

        /// <summary>
        /// Returns a Vector3 with only the specified components replaced.
        /// </summary>
        public static Vector3 With(this Vector3 vector, float? x = null, float? y = null, float? z = null)
        {
            return new Vector3(x ?? vector.x, y ?? vector.y, z ?? vector.z);
        }

        /// <summary>
        /// Returns a Vector4 with only the specified components replaced.
        /// </summary>
        public static Vector4 With(this Vector4 vector, float? x = null, float? y = null, float? z = null, float? w = null)
        {
            return new Vector4(x ?? vector.x, y ?? vector.y, z ?? vector.z, w ?? vector.w);
        }

        /// <summary>
        /// Returns a color with only the specified channels replaced.
        /// </summary>
        public static Color With(this Color color, float? r = null, float? g = null, float? b = null, float? a = null)
        {
            return new Color(r ?? color.r, g ?? color.g, b ?? color.b, a ?? color.a);
        }

        /// <summary>
        /// Resets the local position, rotation, and scale of a Transform.
        /// </summary>
        public static void Reset(this Transform transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        /// <summary>
        /// Resets the anchored position, local rotation, and scale of a RectTransform.
        /// </summary>
        public static void Reset(this RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                throw new ArgumentNullException(nameof(rectTransform));
            }

            rectTransform.anchoredPosition3D = Vector3.zero;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
        }

        /// <summary>
        /// Returns one random element from a non-empty list.
        /// </summary>
        /// <exception cref="ArgumentException">The list is empty.</exception>
        public static T GetRandom<T>(this IList<T> list)
        {
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            if (list.Count == 0)
            {
                throw new ArgumentException("The list must contain at least one element.", nameof(list));
            }

            return list[Random.Range(0, list.Count)];
        }

        /// <summary>
        /// Shuffles a list in place with the Fisher-Yates algorithm.
        /// </summary>
        public static void Shuffle<T>(this IList<T> list)
        {
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            for (int index = list.Count - 1; index > 0; index--)
            {
                int randomIndex = Random.Range(0, index + 1);
                (list[randomIndex], list[index]) = (list[index], list[randomIndex]);
            }
        }

        /// <summary>
        /// Returns a Vector3 projected onto the XZ plane.
        /// </summary>
        public static Vector3 Flat(this Vector3 vector)
        {
            return new Vector3(vector.x, 0, vector.z);
        }

        /// <summary>
        /// Returns the distance between two points on the XZ plane.
        /// </summary>
        public static float FlatDistance(this Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return Mathf.Sqrt((x * x) + (z * z));
        }

        /// <summary>
        /// Schedules every direct child GameObject for destruction.
        /// </summary>
        public static void DestroyChildren(this Transform transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            for (int index = transform.childCount - 1; index >= 0; index--)
            {
                Object.Destroy(transform.GetChild(index).gameObject);
            }
        }

        /// <summary>
        /// Returns true when the GameObject layer exists in the mask.
        /// </summary>
        public static bool IsInLayer(this GameObject gameObject, LayerMask layerMask)
        {
            if (gameObject == null)
            {
                throw new ArgumentNullException(nameof(gameObject));
            }

            return (layerMask.value & (1 << gameObject.layer)) != 0;
        }
    }
}
