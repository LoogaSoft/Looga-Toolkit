using System;
using Object = UnityEngine.Object;

namespace LoogaSoft.Tags.Editor
{
    /// <summary>
    /// Provides dependency-neutral collection operations for Looga Tags editor workflows.
    /// </summary>
    public static class LoogaTagQueryUtility
    {
        public static Func<Object[], Object[]> ValidObjectProvider { private get; set; } = DefaultValidObjects;

        /// <summary>
        /// Returns the valid objects from a saved editor selection.
        /// </summary>
        public static Object[] GetValidObjects(Object[] objects) => ValidObjectProvider(objects);

        private static Object[] DefaultValidObjects(Object[] objects)
        {
            if (objects == null || objects.Length == 0)
                return Array.Empty<Object>();

            int validCount = 0;
            for (int index = 0; index < objects.Length; index++)
            {
                if (objects[index] != null)
                    validCount++;
            }

            if (validCount == objects.Length)
                return objects;

            Object[] validObjects = new Object[validCount];
            int writeIndex = 0;
            for (int index = 0; index < objects.Length; index++)
            {
                if (objects[index] != null)
                    validObjects[writeIndex++] = objects[index];
            }

            return validObjects;
        }
    }
}
