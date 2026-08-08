using System.Collections.Generic;
using UnityEngine;
using ArgumentException = System.ArgumentException;
using ArgumentNullException = System.ArgumentNullException;
using Convert = System.Convert;
using Enum = System.Enum;
using Math = System.Math;
using Type = System.Type;
using TypeCode = System.TypeCode;

namespace LoogaSoft.Tools.Runtime
{
    /// <summary>
    /// Provides small helpers for common Unity operations.
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
        /// Reverses a list in place.
        /// </summary>
        public static void ReverseInPlace<T>(this IList<T> list)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));

            int end = list.Count - 1;
            for (int start = 0; start < end; start++, end--)
                (list[start], list[end]) = (list[end], list[start]);
        }

        /// <summary>
        /// Sorts a list in ascending order.
        /// </summary>
        public static void SortAscending<T>(this IList<T> list, IComparer<T> comparer = null)
        {
            Sort(list, comparer ?? Comparer<T>.Default, descending: false);
        }

        /// <summary>
        /// Sorts a list in descending order.
        /// </summary>
        public static void SortDescending<T>(this IList<T> list, IComparer<T> comparer = null)
        {
            Sort(list, comparer ?? Comparer<T>.Default, descending: true);
        }

        /// <summary>
        /// Removes null and destroyed Unity object entries from a list.
        /// </summary>
        /// <returns>The number of removed entries.</returns>
        public static int RemoveNullEntries<T>(this IList<T> list)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));

            int removedCount = 0;
            for (int index = list.Count - 1; index >= 0; index--)
            {
                T item = list[index];
                bool isNull = item is null || item is Object unityObject && unityObject == null;
                if (!isNull)
                    continue;

                list.RemoveAt(index);
                removedCount++;
            }

            return removedCount;
        }

        /// <summary>
        /// Keeps the first occurrence of each value and removes later duplicates.
        /// </summary>
        /// <returns>The number of removed entries.</returns>
        public static int RemoveDuplicates<T>(this IList<T> list, IEqualityComparer<T> comparer = null)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));

            HashSet<T> seen = new(comparer ?? EqualityComparer<T>.Default);
            int removedCount = 0;

            for (int index = 0; index < list.Count;)
            {
                if (seen.Add(list[index]))
                {
                    index++;
                    continue;
                }

                list.RemoveAt(index);
                removedCount++;
            }

            return removedCount;
        }

        /// <summary>
        /// Returns the normalized vector, or zero when the vector has no direction.
        /// </summary>
        public static Vector2 Normalized(this Vector2 vector) => vector.normalized;

        /// <summary>
        /// Returns the normalized vector, or zero when the vector has no direction.
        /// </summary>
        public static Vector3 Normalized(this Vector3 vector) => vector.normalized;

        /// <summary>
        /// Returns the normalized vector, or zero when the vector has no direction.
        /// </summary>
        public static Vector4 Normalized(this Vector4 vector) => vector.normalized;

        /// <summary>
        /// Rounds a value to the nearest integer.
        /// </summary>
        public static int RoundToInt(this float value) => Mathf.RoundToInt(value);

        /// <summary>
        /// Rounds a value to the nearest integer.
        /// </summary>
        public static long RoundToInt(this double value) => Convert.ToInt64(Math.Round(value));

        /// <summary>
        /// Rounds a value to one decimal place.
        /// </summary>
        public static float RoundToOneDecimal(this float value) => (float)Math.Round(value, 1);

        /// <summary>
        /// Rounds a value to one decimal place.
        /// </summary>
        public static double RoundToOneDecimal(this double value) => Math.Round(value, 1);

        /// <summary>
        /// Rounds a value to two decimal places.
        /// </summary>
        public static float RoundToTwoDecimals(this float value) => (float)Math.Round(value, 2);

        /// <summary>
        /// Rounds a value to two decimal places.
        /// </summary>
        public static double RoundToTwoDecimals(this double value) => Math.Round(value, 2);

        /// <summary>
        /// Returns a random RGB color with full alpha.
        /// </summary>
        public static Color Randomized(this Color color)
        {
            return new Color(Random.value, Random.value, Random.value, 1f);
        }

        /// <summary>
        /// Returns a random RGBA color.
        /// </summary>
        public static Color RandomizedIncludingAlpha(this Color color)
        {
            return new Color(Random.value, Random.value, Random.value, Random.value);
        }

        /// <summary>
        /// Inverts the flags that are declared by an enum.
        /// </summary>
        public static TEnum InvertFlags<TEnum>(this TEnum value) where TEnum : struct, Enum
        {
            Type enumType = typeof(TEnum);
            ulong declaredMask = 0;

            foreach (object declaredValue in Enum.GetValues(enumType))
                declaredMask |= GetEnumBits(declaredValue, enumType);

            ulong inverted = (~GetEnumBits(value, enumType)) & declaredMask;
            return (TEnum)Enum.ToObject(enumType, inverted);
        }

        /// <summary>
        /// Converts a string to uppercase with culture-independent rules.
        /// </summary>
        public static string Uppercase(this string value) => value?.ToUpperInvariant();

        /// <summary>
        /// Converts a string to lowercase with culture-independent rules.
        /// </summary>
        public static string Lowercase(this string value) => value?.ToLowerInvariant();

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

        private static void Sort<T>(IList<T> list, IComparer<T> comparer, bool descending)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));

            List<T> sorted = new(list);
            sorted.Sort((left, right) => descending
                ? comparer.Compare(right, left)
                : comparer.Compare(left, right));

            for (int index = 0; index < sorted.Count; index++)
                list[index] = sorted[index];
        }

        private static ulong GetEnumBits(object value, Type enumType)
        {
            Type underlyingType = Enum.GetUnderlyingType(enumType);
            return Type.GetTypeCode(underlyingType) switch
            {
                TypeCode.SByte => unchecked((ulong)Convert.ToSByte(value)),
                TypeCode.Int16 => unchecked((ulong)Convert.ToInt16(value)),
                TypeCode.Int32 => unchecked((ulong)Convert.ToInt32(value)),
                TypeCode.Int64 => unchecked((ulong)Convert.ToInt64(value)),
                TypeCode.Byte => Convert.ToByte(value),
                TypeCode.UInt16 => Convert.ToUInt16(value),
                TypeCode.UInt32 => Convert.ToUInt32(value),
                TypeCode.UInt64 => Convert.ToUInt64(value),
                _ => 0
            };
        }
    }
}
