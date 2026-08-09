using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Toolkit.TransformAuthoring
{
    internal static class TransformVectorClipboard
    {
        private const string Prefix = "LOOGA_VECTOR3|";

        public static void Copy(Vector3 value)
        {
            EditorGUIUtility.systemCopyBuffer = string.Concat(
                Prefix,
                value.x.ToString("R", CultureInfo.InvariantCulture), "|",
                value.y.ToString("R", CultureInfo.InvariantCulture), "|",
                value.z.ToString("R", CultureInfo.InvariantCulture));
        }

        public static bool TryRead(out Vector3 value)
        {
            value = default;
            string text = EditorGUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(text) || !text.StartsWith(Prefix))
                return false;

            string[] parts = text.Split('|');
            if (parts.Length != 4 ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                return false;
            }

            value = new Vector3(x, y, z);
            return true;
        }
    }
}
