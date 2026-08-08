using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Inspector.Editor
{
    public static class PropertyUtils
    {
        private const float IndentWidth = 15f;
        private const float LabelRightPadding = 2f;
        private const int MaximumFittedLabelCacheEntries = 1024;

        private static readonly Dictionary<AttributeLookupKey, Array> AttributeCache = new();
        private static readonly Dictionary<string, GUIContent> LabelCache = new();
        private static readonly Dictionary<LabelContentKey, GUIContent> TooltipLabelCache = new();
        private static readonly Dictionary<FittedLabelKey, GUIContent> FittedLabelCache = new();
        private static readonly GUIContent MeasurementContent = new();

        public static T GetAttribute<T>(SerializedProperty property) where T : class
        {
            if (property == null) 
                return null;
            
            T[] attributes = GetAttributes<T>(property);
            return attributes.Length > 0 ? attributes[0] : null;
        }
        
        public static T[] GetAttributes<T>(SerializedProperty property) where T : class
        {
            if (property == null) 
                return Array.Empty<T>();
            
            FieldInfo field = ReflectionUtils.GetField(GetTargetObjectWithProperty(property), property.name);
            if (field == null)
                return Array.Empty<T>();
            
            return GetCachedAttributes<T>(field);
        }


        private static T[] GetCachedAttributes<T>(FieldInfo field) where T : class
        {
            if (field == null)
                return Array.Empty<T>();

            AttributeLookupKey key = new(field, typeof(T));
            if (AttributeCache.TryGetValue(key, out Array cachedAttributes))
                return (T[])cachedAttributes;

            object[] rawAttributes = field.GetCustomAttributes(typeof(T), true);
            if (rawAttributes.Length == 0)
            {
                T[] emptyAttributes = Array.Empty<T>();
                AttributeCache[key] = emptyAttributes;
                return emptyAttributes;
            }

            T[] typedAttributes = new T[rawAttributes.Length];
            for (int i = 0; i < rawAttributes.Length; i++)
                typedAttributes[i] = rawAttributes[i] as T;

            AttributeCache[key] = typedAttributes;
            return typedAttributes;
        }

        public static GUIContent GetLabel(SerializedProperty property)
        {
            LabelAttribute labelAttribute = GetAttribute<LabelAttribute>(property);
            TooltipAttribute tooltipAttribute = GetAttribute<TooltipAttribute>(property);
            string labelString = labelAttribute == null ? property.displayName : labelAttribute.label;
            return GetContent(labelString, tooltipAttribute?.tooltip);
        }

        public static GUIContent GetContent(string text)
        {
            text ??= string.Empty;
            if (LabelCache.TryGetValue(text, out GUIContent content))
                return content;

            content = new GUIContent(text);
            LabelCache.Add(text, content);
            return content;
        }

        public static GUIContent GetContent(string text, string tooltip)
        {
            text ??= string.Empty;
            tooltip ??= string.Empty;
            if (string.IsNullOrEmpty(tooltip))
                return GetContent(text);

            LabelContentKey key = new(text, tooltip);
            if (TooltipLabelCache.TryGetValue(key, out GUIContent content))
                return content;

            content = new GUIContent(text, tooltip);
            TooltipLabelCache.Add(key, content);
            return content;
        }

        public static GUIContent GetFittedLabel(GUIContent label)
        {
            return GetFittedLabel(label, EditorGUIUtility.labelWidth);
        }

        public static GUIContent GetFittedLabel(GUIContent label, Rect position)
        {
            return GetFittedLabel(label, Mathf.Min(EditorGUIUtility.labelWidth, position.width));
        }

        private static GUIContent GetFittedLabel(GUIContent label, float labelWidth)
        {
            if (label == null || label == GUIContent.none || string.IsNullOrEmpty(label.text))
                return label;

            float availableWidth = Mathf.Max(
                0f,
                labelWidth - EditorGUI.indentLevel * IndentWidth - LabelRightPadding);
            GUIStyle style = EditorStyles.label;
            if (MeasureLabel(style, label.text) <= availableWidth)
                return label;

            int widthInPixels = Mathf.RoundToInt(availableWidth * EditorGUIUtility.pixelsPerPoint);
            FittedLabelKey key = new(label.text, label.tooltip, widthInPixels);
            if (FittedLabelCache.TryGetValue(key, out GUIContent fittedLabel))
                return fittedLabel;

            string shortenedText = TruncateLabel(label.text, availableWidth, style);
            string tooltip = string.IsNullOrWhiteSpace(label.tooltip)
                ? label.text
                : $"{label.text}\n\n{label.tooltip}";

            fittedLabel = new GUIContent(shortenedText, label.image, tooltip);
            if (FittedLabelCache.Count >= MaximumFittedLabelCacheEntries)
                FittedLabelCache.Clear();

            FittedLabelCache.Add(key, fittedLabel);
            return fittedLabel;
        }

        private static string TruncateLabel(string text, float availableWidth, GUIStyle style)
        {
            const string suffix = "...";
            if (MeasureLabel(style, suffix) > availableWidth)
                return suffix;

            int low = 0;
            int high = text.Length;
            while (low < high)
            {
                int midpoint = (low + high + 1) / 2;
                string candidate = text.Substring(0, midpoint) + suffix;
                if (MeasureLabel(style, candidate) <= availableWidth)
                    low = midpoint;
                else
                    high = midpoint - 1;
            }

            return text.Substring(0, low).TrimEnd() + suffix;
        }

        private static float MeasureLabel(GUIStyle style, string text)
        {
            MeasurementContent.text = text;
            MeasurementContent.image = null;
            MeasurementContent.tooltip = string.Empty;
            return style.CalcSize(MeasurementContent).x;
        }

        public static void CallOnFieldChangedCallbacks(SerializedProperty property)
        {
            OnFieldChangedAttribute[] onFieldChangedAttributes = GetAttributes<OnFieldChangedAttribute>(property);
            if (onFieldChangedAttributes.Length == 0) 
                return;

            object target = GetTargetObjectWithProperty(property);
            property.serializedObject.ApplyModifiedProperties();

            foreach (var onFieldChangedAttribute in onFieldChangedAttributes)
            {
                MethodInfo method = ReflectionUtils.GetMethod(target, onFieldChangedAttribute.MethodName);
                if (method != null && method.ReturnType == typeof(void) && method.GetParameters().Length == 0) 
                    method.Invoke(target, null);
                else 
                    Debug.LogWarning(onFieldChangedAttribute.GetType().Name
                                     + "callback is invalid, callback must be void with no parameters"
                                     , property.serializedObject.targetObject); 
            }
        }
        
        public static bool IsEnabled(SerializedProperty property)
        {
            if (property == null) 
                return true;
            
            ReadOnlyAttribute readOnlyAttribute = GetAttribute<ReadOnlyAttribute>(property);
            if (readOnlyAttribute != null)
                return false;

            if (GetAttribute<DisableInPlayModeAttribute>(property) != null && EditorApplication.isPlayingOrWillChangePlaymode)
                return false;

            if (GetAttribute<DisableInEditModeAttribute>(property) != null && !EditorApplication.isPlayingOrWillChangePlaymode)
                return false;
            
            EnableIfAttributeBase enableIfAttribute = GetAttribute<EnableIfAttributeBase>(property);
            if (enableIfAttribute == null)
                return true;
            
            object target = GetTargetObjectWithProperty(property);
            
            bool inverted = enableIfAttribute.inverted;
            bool condition = GetCondition(target, enableIfAttribute.condition);

            return inverted ? !condition : condition;
        }

        public static bool IsVisible(SerializedProperty property)
        {
            if (property == null) 
                return true;
            
            if (GetAttribute<ShowInPlayModeAttribute>(property) != null && !EditorApplication.isPlayingOrWillChangePlaymode)
                return false;

            if (GetAttribute<ShowInEditModeAttribute>(property) != null && EditorApplication.isPlayingOrWillChangePlaymode)
                return false;

            ShowIfAttributeBase showIfAttribute = GetAttribute<ShowIfAttributeBase>(property);
            if (showIfAttribute == null)
                return true;
            
            object target = GetTargetObjectWithProperty(property);

            bool inverted = showIfAttribute.inverted;
            bool condition = showIfAttribute.hasExpectedValue
                ? GetCondition(target, showIfAttribute.condition, showIfAttribute.expectedValue)
                : GetCondition(target, showIfAttribute.condition);
            
            return inverted ? !condition : condition;
        }

        private static bool GetCondition(object target, string conditionName)
        {
            FieldInfo field = ReflectionUtils.GetField(target, conditionName);
            if (field != null && field.FieldType == typeof(bool))
                return (bool)field.GetValue(target);
            
            PropertyInfo property = ReflectionUtils.GetProperty(target, conditionName);
            if (property != null && property.PropertyType == typeof(bool))
                return (bool)property.GetValue(target, null);
            
            MethodInfo method = ReflectionUtils.GetMethod(target, conditionName);
            if (method != null && method.ReturnType == typeof(bool))
                return (bool)method.Invoke(target, null);
            
            Debug.LogWarning($"Could not find condition {conditionName} on {target.GetType().Name}");
            return false;
        }

        public static bool GetConditionValue(object target, string conditionName)
        {
            return GetCondition(target, conditionName);
        }

        private static bool GetCondition(object target, string conditionName, string expectedValue)
        {
            object value = GetConditionObject(target, conditionName);
            if (value == null)
                return string.IsNullOrEmpty(expectedValue);

            if (value is bool boolValue && bool.TryParse(expectedValue, out bool expectedBool))
                return boolValue == expectedBool;

            if (value is int intValue && int.TryParse(expectedValue, out int expectedInt))
                return intValue == expectedInt;

            if (value is float floatValue && float.TryParse(expectedValue, out float expectedFloat))
                return Mathf.Approximately(floatValue, expectedFloat);

            if (value.GetType().IsEnum)
                return string.Equals(value.ToString(), expectedValue, StringComparison.Ordinal);

            return string.Equals(value.ToString(), expectedValue, StringComparison.Ordinal);
        }

        private static object GetConditionObject(object target, string conditionName)
        {
            FieldInfo field = ReflectionUtils.GetField(target, conditionName);
            if (field != null)
                return field.GetValue(target);

            PropertyInfo property = ReflectionUtils.GetProperty(target, conditionName);
            if (property != null)
                return property.GetValue(target, null);

            MethodInfo method = ReflectionUtils.GetMethod(target, conditionName);
            if (method != null && method.GetParameters().Length == 0)
                return method.Invoke(target, null);

            Debug.LogWarning($"Could not find condition {conditionName} on {target.GetType().Name}");
            return null;
        }

        public static object GetTargetObjectWithProperty(SerializedProperty property)
        {
            if (property == null) 
                return null;
            
            object obj = property.serializedObject.targetObject;
            string path = property.propertyPath.Replace(".Array.data[", "[");
            string[] elements = path.Split('.');

            for (int i = 0; i < elements.Length - 1; i++)
            {
                string element = elements[i];
                if (element.Contains("["))
                {
                    string elementName = element.Substring(0, element.IndexOf("[", StringComparison.Ordinal));
                    int index = Convert.ToInt32(element.Substring(element.IndexOf("[", StringComparison.Ordinal)).Replace("[", "").Replace("]", ""));
                    obj = GetValue(obj, elementName, index);
                }
                else
                {
                    obj = GetValue(obj, element);
                }
                
                if (obj == null) return null;
            }
            
            return obj;
        }

        private static object GetValue(object source, string name)
        {
            if (source == null) 
                return null;
            
            Type type = source.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            while (type != null)
            {
                FieldInfo field = type.GetField(name, flags);
                if (field != null) 
                    return field.GetValue(source);
                
                PropertyInfo property = type.GetProperty(name, flags);
                if (property != null) 
                    return property.GetValue(source, null);
                
                type = type.BaseType;
            }
            
            return null;
        }

        private static object GetValue(object source, string name, int index)
        {
            IEnumerable enumerable = GetValue(source, name) as IEnumerable;
            if (enumerable == null) 
                return null;
            
            IEnumerator enumerator = enumerable.GetEnumerator();
            using var enumerator1 = enumerator as IDisposable;

            for (int i = 0; i <= index; i++) 
            {
                if (!enumerator.MoveNext()) 
                    return null;
            }
            
            return enumerator.Current;
        }

        private readonly struct AttributeLookupKey : IEquatable<AttributeLookupKey>
        {
            private readonly FieldInfo _field;
            private readonly Type _attributeType;

            public AttributeLookupKey(FieldInfo field, Type attributeType)
            {
                _field = field;
                _attributeType = attributeType;
            }

            public bool Equals(AttributeLookupKey other)
            {
                return Equals(_field, other._field) && _attributeType == other._attributeType;
            }

            public override bool Equals(object obj)
            {
                return obj is AttributeLookupKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((_field != null ? _field.GetHashCode() : 0) * 397)
                        ^ (_attributeType != null ? _attributeType.GetHashCode() : 0);
                }
            }
        }

        private readonly struct LabelContentKey : IEquatable<LabelContentKey>
        {
            private readonly string _text;
            private readonly string _tooltip;

            public LabelContentKey(string text, string tooltip)
            {
                _text = text;
                _tooltip = tooltip;
            }

            public bool Equals(LabelContentKey other)
            {
                return string.Equals(_text, other._text, StringComparison.Ordinal)
                    && string.Equals(_tooltip, other._tooltip, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is LabelContentKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((_text != null ? _text.GetHashCode() : 0) * 397)
                        ^ (_tooltip != null ? _tooltip.GetHashCode() : 0);
                }
            }
        }

        private readonly struct FittedLabelKey : IEquatable<FittedLabelKey>
        {
            private readonly string _text;
            private readonly string _tooltip;
            private readonly int _widthInPixels;

            public FittedLabelKey(string text, string tooltip, int widthInPixels)
            {
                _text = text;
                _tooltip = tooltip;
                _widthInPixels = widthInPixels;
            }

            public bool Equals(FittedLabelKey other)
            {
                return _widthInPixels == other._widthInPixels
                    && string.Equals(_text, other._text, StringComparison.Ordinal)
                    && string.Equals(_tooltip, other._tooltip, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is FittedLabelKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = _text != null ? _text.GetHashCode() : 0;
                    hashCode = (hashCode * 397) ^ (_tooltip != null ? _tooltip.GetHashCode() : 0);
                    return (hashCode * 397) ^ _widthInPixels;
                }
            }
        }
    }
}

