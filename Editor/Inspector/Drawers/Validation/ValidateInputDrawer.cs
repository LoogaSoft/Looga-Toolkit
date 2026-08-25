using System.Reflection;
using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Inspector.Editor
{
    [CustomPropertyDrawer(typeof(ValidateInputAttribute))]
    public class ValidateInputDrawer : PropertyDrawerBase
    {
        private const float BOX_HEIGHT = 30f;
        private readonly float _spacing = EditorGUIUtility.standardVerticalSpacing;
        
        protected override float GetPropertyHeight_Internal(SerializedProperty property, GUIContent label)
        {
            if (IsPropertyValid(property))
                return EditorGUI.GetPropertyHeight(property, label) + BOX_HEIGHT + _spacing;
            
            return EditorGUI.GetPropertyHeight(property, label);
        }

        protected override void OnGUI_Internal(Rect position, SerializedProperty property, GUIContent label)
        {
            ValidateInputAttribute attr = attribute as ValidateInputAttribute;
            
            if (IsPropertyValid(property))
            {
                Rect boxRect = new Rect(position.x, position.y, position.width, BOX_HEIGHT);
                EditorGUI.HelpBox(boxRect, attr.message, GetMessageType(attr.messageMode));
                
                Rect propertyRect = new Rect(position.x, position.y + BOX_HEIGHT + _spacing, position.width, position.height - BOX_HEIGHT + _spacing);
                EditorGUI.PropertyField(propertyRect, property, label, true);
            }
            else
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }

        protected override VisualElement CreatePropertyGUI_Internal(
            SerializedProperty property,
            string label)
        {
            ValidateInputAttribute validationAttribute = (ValidateInputAttribute)attribute;
            VisualElement root = new();
            HelpBox message = LoogaPropertyDrawerUi.CreateMessage(
                validationAttribute.message,
                GetHelpBoxMessageType(validationAttribute.messageMode));
            root.Add(message);
            root.Add(LoogaPropertyDrawerUi.CreateSerializedField(property, label, fieldInfo?.FieldType));

            void Refresh(SerializedProperty current)
            {
                message.style.display = IsPropertyValid(current)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }

            Refresh(property);
            LoogaPropertyDrawerUi.Track(root, property, Refresh);
            return root;
        }

        private bool IsPropertyValid(SerializedProperty property)
        {
            ValidateInputAttribute attr = attribute as ValidateInputAttribute;
            return GetCondition(property.serializedObject.targetObject, attr.condition);
        }

        public static bool GetCondition(object target, string boolName)
        {
            if (target == null) 
                return false;
            
            var type = target.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            
            FieldInfo field = type.GetField(boolName, flags);
            if (field != null && field.FieldType == typeof(bool))
                return (bool)field.GetValue(target);
            
            PropertyInfo property = type.GetProperty(boolName, flags);
            if (property != null && property.PropertyType == typeof(bool))
                return (bool)property.GetValue(target);
            
            MethodInfo method = type.GetMethod(boolName, flags);
            if (method != null && method.ReturnType == typeof(bool))
                return (bool)method.Invoke(target, null);
            
            return false;
        }

        public static MessageType GetMessageType(MessageMode mode)
        {
            return mode switch
            {
                MessageMode.Error => MessageType.Error,
                MessageMode.Warning => MessageType.Warning,
                MessageMode.Info => MessageType.Info,
                _ => MessageType.None
            };
        }

        private static HelpBoxMessageType GetHelpBoxMessageType(MessageMode mode)
        {
            return mode switch
            {
                MessageMode.Error => HelpBoxMessageType.Error,
                MessageMode.Warning => HelpBoxMessageType.Warning,
                MessageMode.Info => HelpBoxMessageType.Info,
                _ => HelpBoxMessageType.None
            };
        }
    }
}
