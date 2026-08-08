using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LoogaSoft.Inspector.Editor
{
    /// <summary>
    /// Adds safe, type-specific commands to serialized property context menus.
    /// </summary>
    [InitializeOnLoad]
    internal static class LoogaPropertyContextMenu
    {
        private enum ListSortMode
        {
            NumericAscending,
            NumericDescending,
            NameAscending,
            NameDescending,
            LengthAscending,
            LengthDescending
        }

        private enum ListElementKind
        {
            Unsupported,
            Numeric,
            String,
            ObjectReference
        }

        static LoogaPropertyContextMenu()
        {
            EditorApplication.contextualPropertyMenu -= PopulatePropertyMenu;
            EditorApplication.contextualPropertyMenu += PopulatePropertyMenu;
        }

        internal static void ShowListMenu(
            Object[] targets,
            string propertyPath,
            string displayName,
            bool enabled,
            Action completed = null)
        {
            GenericMenu menu = new();
            AddListCommands(menu, targets, propertyPath, displayName, enabled, completed);
            menu.ShowAsContext();
        }

        private static void PopulatePropertyMenu(GenericMenu menu, SerializedProperty property)
        {
            if (property == null)
                return;

            Object[] targets = property.serializedObject.targetObjects;
            string propertyPath = property.propertyPath;
            string displayName = property.displayName;

            if (IsList(property))
            {
                menu.AddSeparator(string.Empty);
                AddListCommands(menu, targets, propertyPath, displayName, property.editable, completed: null);
                return;
            }

            Type valueType = ResolvePropertyType(targets, propertyPath);
            switch (property.propertyType)
            {
                case SerializedPropertyType.Vector2:
                case SerializedPropertyType.Vector3:
                case SerializedPropertyType.Vector4:
                    menu.AddSeparator(string.Empty);
                    AddPropertyCommand(menu, "Normalize", property.editable, targets, propertyPath, displayName, NormalizeVector);
                    break;

                case SerializedPropertyType.Float:
                    menu.AddSeparator(string.Empty);
                    AddPropertyCommand(menu, "Round/Round to Int", property.editable, targets, propertyPath, displayName, value => RoundNumber(value, 0));
                    AddPropertyCommand(menu, "Round/Round to One Decimal", property.editable, targets, propertyPath, displayName, value => RoundNumber(value, 1));
                    AddPropertyCommand(menu, "Round/Round to Two Decimals", property.editable, targets, propertyPath, displayName, value => RoundNumber(value, 2));
                    break;

                case SerializedPropertyType.Color:
                    menu.AddSeparator(string.Empty);
                    AddPropertyCommand(menu, "Randomize", property.editable, targets, propertyPath, displayName, value => RandomizeColor(value, includeAlpha: false));
                    AddPropertyCommand(menu, "Randomize Including Alpha", property.editable, targets, propertyPath, displayName, value => RandomizeColor(value, includeAlpha: true));
                    break;

                case SerializedPropertyType.Enum when IsFlagsEnum(valueType):
                    menu.AddSeparator(string.Empty);
                    AddPropertyCommand(menu, "Invert", property.editable, targets, propertyPath, displayName, value => InvertFlags(value, valueType));
                    break;

                case SerializedPropertyType.String:
                    menu.AddSeparator(string.Empty);
                    AddPropertyCommand(menu, "Uppercase", property.editable, targets, propertyPath, displayName, value => value.stringValue = value.stringValue.ToUpperInvariant());
                    AddPropertyCommand(menu, "Lowercase", property.editable, targets, propertyPath, displayName, value => value.stringValue = value.stringValue.ToLowerInvariant());
                    break;
            }
        }

        private static void AddListCommands(
            GenericMenu menu,
            Object[] targets,
            string propertyPath,
            string displayName,
            bool enabled,
            Action completed)
        {
            bool canReorder = enabled && AnyListHasAtLeast(targets, propertyPath, 2);
            AddMenuItem(menu, "Shuffle", canReorder, () =>
            {
                ReorderLists(targets, propertyPath, displayName, "Shuffle", ShuffleOrder);
                completed?.Invoke();
            });
            AddMenuItem(menu, "Reverse", canReorder, () =>
            {
                ReorderLists(targets, propertyPath, displayName, "Reverse", ReverseOrder);
                completed?.Invoke();
            });

            ListElementKind elementKind = GetListElementKind(targets, propertyPath);
            switch (elementKind)
            {
                case ListElementKind.Numeric:
                    AddSortCommand(menu, "Sort/Ascending", targets, propertyPath, displayName, enabled, ListSortMode.NumericAscending, completed);
                    AddSortCommand(menu, "Sort/Descending", targets, propertyPath, displayName, enabled, ListSortMode.NumericDescending, completed);
                    break;

                case ListElementKind.String:
                    AddSortCommand(menu, "Sort/Name Ascending", targets, propertyPath, displayName, enabled, ListSortMode.NameAscending, completed);
                    AddSortCommand(menu, "Sort/Name Descending", targets, propertyPath, displayName, enabled, ListSortMode.NameDescending, completed);
                    AddSortCommand(menu, "Sort/Length Ascending", targets, propertyPath, displayName, enabled, ListSortMode.LengthAscending, completed);
                    AddSortCommand(menu, "Sort/Length Descending", targets, propertyPath, displayName, enabled, ListSortMode.LengthDescending, completed);
                    break;

                case ListElementKind.ObjectReference:
                    AddSortCommand(menu, "Sort/Name Ascending", targets, propertyPath, displayName, enabled, ListSortMode.NameAscending, completed);
                    AddSortCommand(menu, "Sort/Name Descending", targets, propertyPath, displayName, enabled, ListSortMode.NameDescending, completed);
                    break;
            }

            Type elementType = ResolveListElementType(targets, propertyPath);
            bool canRemoveNulls = enabled && CanContainSerializedNull(targets, propertyPath, elementType);
            AddMenuItem(menu, "Cleanup/Remove Null Entries", canRemoveNulls, () =>
            {
                RemoveListEntries(targets, propertyPath, displayName, "Remove Null Entries", IsNullEntry);
                completed?.Invoke();
            });

            bool canRemoveDuplicates = enabled && CanCompareListElements(elementType) &&
                AnyListHasAtLeast(targets, propertyPath, 2);
            AddMenuItem(menu, "Cleanup/Remove Duplicates", canRemoveDuplicates, () =>
            {
                RemoveDuplicateEntries(targets, propertyPath, displayName);
                completed?.Invoke();
            });
        }

        private static void AddSortCommand(
            GenericMenu menu,
            string path,
            Object[] targets,
            string propertyPath,
            string displayName,
            bool enabled,
            ListSortMode mode,
            Action completed)
        {
            bool canSort = enabled && AnyListHasAtLeast(targets, propertyPath, 2);
            AddMenuItem(menu, path, canSort, () =>
            {
                SortLists(targets, propertyPath, displayName, mode);
                completed?.Invoke();
            });
        }

        private static void AddPropertyCommand(
            GenericMenu menu,
            string path,
            bool enabled,
            Object[] targets,
            string propertyPath,
            string displayName,
            Action<SerializedProperty> operation)
        {
            AddMenuItem(menu, path, enabled, () => ModifyProperties(targets, propertyPath, displayName, path, operation));
        }

        private static void AddMenuItem(GenericMenu menu, string path, bool enabled, GenericMenu.MenuFunction action)
        {
            GUIContent content = new(path);
            if (enabled)
                menu.AddItem(content, false, action);
            else
                menu.AddDisabledItem(content);
        }

        private static void ModifyProperties(
            Object[] targets,
            string propertyPath,
            string displayName,
            string operationName,
            Action<SerializedProperty> operation)
        {
            Object[] validTargets = GetValidTargets(targets);
            if (validTargets.Length == 0)
                return;

            Undo.RecordObjects(validTargets, $"{operationName} {displayName}");
            for (int index = 0; index < validTargets.Length; index++)
            {
                Object target = validTargets[index];
                SerializedObject owner = new(target);
                owner.Update();
                SerializedProperty property = owner.FindProperty(propertyPath);
                if (property == null)
                    continue;

                operation(property);
                owner.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(target);
            }
        }

        private static void ReorderLists(
            Object[] targets,
            string propertyPath,
            string displayName,
            string operationName,
            Action<List<int>> reorder)
        {
            ModifyLists(targets, propertyPath, displayName, operationName, list =>
            {
                List<int> desiredOrder = CreateIdentityOrder(list.arraySize);
                reorder(desiredOrder);
                ApplyOrder(list, desiredOrder);
            });
        }

        private static void SortLists(Object[] targets, string propertyPath, string displayName, ListSortMode mode)
        {
            ModifyLists(targets, propertyPath, displayName, "Sort", list =>
            {
                List<int> desiredOrder = CreateIdentityOrder(list.arraySize);
                desiredOrder.Sort((left, right) => CompareElements(list, left, right, mode));
                ApplyOrder(list, desiredOrder);
            });
        }

        private static void RemoveListEntries(
            Object[] targets,
            string propertyPath,
            string displayName,
            string operationName,
            Func<SerializedProperty, bool> shouldRemove)
        {
            ModifyLists(targets, propertyPath, displayName, operationName, list =>
            {
                for (int index = list.arraySize - 1; index >= 0; index--)
                {
                    if (shouldRemove(list.GetArrayElementAtIndex(index)))
                        DeleteArrayElement(list, index);
                }
            });
        }

        private static void RemoveDuplicateEntries(Object[] targets, string propertyPath, string displayName)
        {
            ModifyLists(targets, propertyPath, displayName, "Remove Duplicates", list =>
            {
                HashSet<object> seen = new();
                for (int index = 0; index < list.arraySize;)
                {
                    object key = GetComparableValue(list.GetArrayElementAtIndex(index));
                    if (seen.Add(key))
                    {
                        index++;
                        continue;
                    }

                    DeleteArrayElement(list, index);
                }
            });
        }

        private static void ModifyLists(
            Object[] targets,
            string propertyPath,
            string displayName,
            string operationName,
            Action<SerializedProperty> operation)
        {
            Object[] validTargets = GetValidTargets(targets);
            if (validTargets.Length == 0)
                return;

            Undo.RecordObjects(validTargets, $"{operationName} {displayName}");
            for (int targetIndex = 0; targetIndex < validTargets.Length; targetIndex++)
            {
                Object target = validTargets[targetIndex];
                SerializedObject owner = new(target);
                owner.Update();
                SerializedProperty list = owner.FindProperty(propertyPath);
                if (!IsList(list))
                    continue;

                operation(list);
                owner.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(target);
            }
        }

        private static int CompareElements(SerializedProperty list, int leftIndex, int rightIndex, ListSortMode mode)
        {
            SerializedProperty left = list.GetArrayElementAtIndex(leftIndex);
            SerializedProperty right = list.GetArrayElementAtIndex(rightIndex);
            int comparison;

            switch (mode)
            {
                case ListSortMode.NumericAscending:
                case ListSortMode.NumericDescending:
                    comparison = left.propertyType == SerializedPropertyType.Integer
                        ? left.longValue.CompareTo(right.longValue)
                        : left.doubleValue.CompareTo(right.doubleValue);
                    if (mode == ListSortMode.NumericDescending)
                        comparison = -comparison;
                    break;

                case ListSortMode.LengthAscending:
                case ListSortMode.LengthDescending:
                    comparison = left.stringValue.Length.CompareTo(right.stringValue.Length);
                    if (mode == ListSortMode.LengthDescending)
                        comparison = -comparison;
                    if (comparison == 0)
                        comparison = CompareNames(left.stringValue, right.stringValue);
                    break;

                default:
                    comparison = CompareNames(GetElementName(left), GetElementName(right));
                    if (mode == ListSortMode.NameDescending)
                        comparison = -comparison;
                    break;
            }

            return comparison != 0 ? comparison : leftIndex.CompareTo(rightIndex);
        }

        private static int CompareNames(string left, string right)
        {
            return EditorUtility.NaturalCompare(left ?? string.Empty, right ?? string.Empty);
        }

        private static string GetElementName(SerializedProperty element)
        {
            if (element.propertyType == SerializedPropertyType.String)
                return element.stringValue;

            Object value = element.objectReferenceValue;
            return value != null ? value.name : string.Empty;
        }

        private static List<int> CreateIdentityOrder(int count)
        {
            List<int> order = new(count);
            for (int index = 0; index < count; index++)
                order.Add(index);
            return order;
        }

        private static void ShuffleOrder(List<int> order)
        {
            System.Random random = new();
            for (int index = order.Count - 1; index > 0; index--)
            {
                int destination = random.Next(index + 1);
                (order[index], order[destination]) = (order[destination], order[index]);
            }
        }

        private static void ReverseOrder(List<int> order)
        {
            order.Reverse();
        }

        private static void ApplyOrder(SerializedProperty list, IReadOnlyList<int> desiredOrder)
        {
            List<int> currentOrder = CreateIdentityOrder(list.arraySize);
            for (int destination = 0; destination < desiredOrder.Count; destination++)
            {
                int source = currentOrder.IndexOf(desiredOrder[destination]);
                if (source == destination)
                    continue;

                list.MoveArrayElement(source, destination);
                int moved = currentOrder[source];
                currentOrder.RemoveAt(source);
                currentOrder.Insert(destination, moved);
            }
        }

        private static void DeleteArrayElement(SerializedProperty list, int index)
        {
            int previousSize = list.arraySize;
            SerializedProperty element = list.GetArrayElementAtIndex(index);
            bool objectReference = element.propertyType == SerializedPropertyType.ObjectReference;

            list.DeleteArrayElementAtIndex(index);
            if (list.arraySize < previousSize)
                return;

            if (!objectReference)
            {
                list.arraySize = Mathf.Max(0, previousSize - 1);
                return;
            }

            for (int shiftIndex = index; shiftIndex < previousSize - 1; shiftIndex++)
            {
                SerializedProperty current = list.GetArrayElementAtIndex(shiftIndex);
                SerializedProperty next = list.GetArrayElementAtIndex(shiftIndex + 1);
                current.objectReferenceValue = next.objectReferenceValue;
            }

            list.arraySize = Mathf.Max(0, previousSize - 1);
        }

        private static bool IsNullEntry(SerializedProperty element)
        {
            return element.propertyType switch
            {
                SerializedPropertyType.ObjectReference => element.objectReferenceValue == null,
                SerializedPropertyType.ManagedReference => element.managedReferenceValue == null,
                _ => false
            };
        }

        private static object GetComparableValue(SerializedProperty element)
        {
            return element.propertyType switch
            {
                SerializedPropertyType.Integer => (element.propertyType, element.longValue),
                SerializedPropertyType.Boolean => (element.propertyType, element.boolValue),
                SerializedPropertyType.Float => (element.propertyType, element.doubleValue),
                SerializedPropertyType.String => (element.propertyType, element.stringValue),
                SerializedPropertyType.Color => (element.propertyType, element.colorValue),
                SerializedPropertyType.ObjectReference => (element.propertyType, element.objectReferenceInstanceIDValue),
                SerializedPropertyType.LayerMask => (element.propertyType, element.intValue),
                SerializedPropertyType.Enum => (element.propertyType, element.intValue),
                SerializedPropertyType.Vector2 => (element.propertyType, element.vector2Value),
                SerializedPropertyType.Vector3 => (element.propertyType, element.vector3Value),
                SerializedPropertyType.Vector4 => (element.propertyType, element.vector4Value),
                SerializedPropertyType.Rect => (element.propertyType, element.rectValue),
                SerializedPropertyType.Character => (element.propertyType, element.intValue),
                SerializedPropertyType.Bounds => (element.propertyType, element.boundsValue),
                SerializedPropertyType.Quaternion => (element.propertyType, element.quaternionValue),
                SerializedPropertyType.Vector2Int => (element.propertyType, element.vector2IntValue),
                SerializedPropertyType.Vector3Int => (element.propertyType, element.vector3IntValue),
                SerializedPropertyType.RectInt => (element.propertyType, element.rectIntValue),
                SerializedPropertyType.BoundsInt => (element.propertyType, element.boundsIntValue),
                SerializedPropertyType.Hash128 => (element.propertyType, element.hash128Value),
                _ => null
            };
        }

        private static void NormalizeVector(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Vector2:
                    property.vector2Value = property.vector2Value.normalized;
                    break;
                case SerializedPropertyType.Vector3:
                    property.vector3Value = property.vector3Value.normalized;
                    break;
                case SerializedPropertyType.Vector4:
                    property.vector4Value = property.vector4Value.normalized;
                    break;
            }
        }

        private static void RoundNumber(SerializedProperty property, int decimals)
        {
            property.doubleValue = Math.Round(property.doubleValue, decimals);
        }

        private static void RandomizeColor(SerializedProperty property, bool includeAlpha)
        {
            property.colorValue = new Color(
                UnityEngine.Random.value,
                UnityEngine.Random.value,
                UnityEngine.Random.value,
                includeAlpha ? UnityEngine.Random.value : 1f);
        }

        private static void InvertFlags(SerializedProperty property, Type enumType)
        {
            int declaredMask = 0;
            foreach (object value in Enum.GetValues(enumType))
                declaredMask |= Convert.ToInt32(value);

            property.intValue = (~property.intValue) & declaredMask;
        }

        private static bool AnyListHasAtLeast(Object[] targets, string propertyPath, int minimumSize)
        {
            for (int index = 0; index < targets.Length; index++)
            {
                Object target = targets[index];
                if (target == null)
                    continue;

                SerializedObject owner = new(target);
                SerializedProperty list = owner.FindProperty(propertyPath);
                if (IsList(list) && list.arraySize >= minimumSize)
                    return true;
            }

            return false;
        }

        private static bool IsList(SerializedProperty property)
        {
            return property != null && property.isArray && property.propertyType != SerializedPropertyType.String;
        }

        private static ListElementKind GetListElementKind(Object[] targets, string propertyPath)
        {
            Type elementType = ResolveListElementType(targets, propertyPath);
            if (elementType == typeof(string))
                return ListElementKind.String;
            if (elementType != null && typeof(Object).IsAssignableFrom(elementType))
                return ListElementKind.ObjectReference;
            if (IsNumericType(elementType))
                return ListElementKind.Numeric;
            return ListElementKind.Unsupported;
        }

        private static bool IsNumericType(Type type)
        {
            if (type == null || type.IsEnum)
                return false;

            TypeCode code = Type.GetTypeCode(type);
            return code is TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16
                or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64
                or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;
        }

        private static bool CanContainSerializedNull(Object[] targets, string propertyPath, Type elementType)
        {
            if (elementType != null && typeof(Object).IsAssignableFrom(elementType))
                return true;

            for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
            {
                Object target = targets[targetIndex];
                if (target == null)
                    continue;

                SerializedObject owner = new(target);
                SerializedProperty list = owner.FindProperty(propertyPath);
                if (IsList(list) && list.arraySize > 0 &&
                    list.GetArrayElementAtIndex(0).propertyType == SerializedPropertyType.ManagedReference)
                    return true;
            }

            return false;
        }

        private static bool CanCompareListElements(Type elementType)
        {
            return elementType != null &&
                (elementType.IsPrimitive || elementType.IsEnum || elementType == typeof(string) ||
                 typeof(Object).IsAssignableFrom(elementType) || elementType == typeof(Color) ||
                 elementType == typeof(Vector2) || elementType == typeof(Vector3) ||
                 elementType == typeof(Vector4) || elementType == typeof(Rect) ||
                 elementType == typeof(Bounds) || elementType == typeof(Quaternion) ||
                 elementType == typeof(Vector2Int) || elementType == typeof(Vector3Int) ||
                 elementType == typeof(RectInt) || elementType == typeof(BoundsInt) ||
                 elementType == typeof(Hash128));
        }

        private static bool IsFlagsEnum(Type type)
        {
            return type != null && type.IsEnum && type.IsDefined(typeof(FlagsAttribute), inherit: false);
        }

        private static Type ResolveListElementType(Object[] targets, string propertyPath)
        {
            Type listType = ResolvePropertyType(targets, propertyPath);
            if (listType == null)
                return null;
            if (listType.IsArray)
                return listType.GetElementType();

            for (Type current = listType; current != null; current = current.BaseType)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(List<>))
                    return current.GetGenericArguments()[0];
            }

            foreach (Type interfaceType in listType.GetInterfaces())
            {
                if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IList<>))
                    return interfaceType.GetGenericArguments()[0];
            }

            return null;
        }

        private static Type ResolvePropertyType(Object[] targets, string propertyPath)
        {
            for (int index = 0; index < targets.Length; index++)
            {
                Object target = targets[index];
                if (target != null)
                    return ResolvePropertyType(target.GetType(), propertyPath);
            }

            return null;
        }

        private static Type ResolvePropertyType(Type rootType, string propertyPath)
        {
            string normalizedPath = propertyPath.Replace(".Array.data[", "[");
            string[] segments = normalizedPath.Split('.');
            Type currentType = rootType;

            for (int index = 0; index < segments.Length && currentType != null; index++)
            {
                string segment = segments[index];
                int arrayMarker = segment.IndexOf('[');
                string fieldName = arrayMarker >= 0 ? segment.Substring(0, arrayMarker) : segment;
                FieldInfo field = FindField(currentType, fieldName);
                if (field == null)
                    return null;

                currentType = field.FieldType;
                if (arrayMarker >= 0)
                    currentType = GetCollectionElementType(currentType);
            }

            return currentType;
        }

        private static FieldInfo FindField(Type type, string fieldName)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(fieldName, Flags);
                if (field != null)
                    return field;
            }

            return null;
        }

        private static Type GetCollectionElementType(Type collectionType)
        {
            if (collectionType.IsArray)
                return collectionType.GetElementType();
            if (collectionType.IsGenericType)
                return collectionType.GetGenericArguments()[0];
            return null;
        }

        private static Object[] GetValidTargets(Object[] targets)
        {
            List<Object> valid = new(targets.Length);
            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] != null)
                    valid.Add(targets[index]);
            }

            return valid.ToArray();
        }
    }
}
