using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Inspector.Editor
{
    [CustomPropertyDrawer(typeof(SecureStringAttribute))]
    public class SecureStringDrawer : PropertyDrawerBase
    {
        private const float ButtonWidth = 56f;
        private const float Gap = 4f;

        protected override void OnGUI_Internal(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "SecureString is for strings only");
                EditorGUI.EndProperty();
                return;
            }

            string stateKey = GetStateKey(property);
            bool editing = SessionState.GetBool(stateKey, false);
            string controlName = $"SecureString_{stateKey}";
            SecureStringAttribute secureString = (SecureStringAttribute)attribute;

            Rect fieldArea = EditorGUI.PrefixLabel(position, label);
            Rect buttonRect = new(fieldArea.xMax - ButtonWidth, fieldArea.y, ButtonWidth, fieldArea.height);
            Rect valueRect = new(fieldArea.x, fieldArea.y, fieldArea.width - ButtonWidth - Gap, fieldArea.height);

            GUI.SetNextControlName(controlName);
            using (new EditorGUI.DisabledScope(!editing))
            {
                if (editing)
                {
                    property.stringValue = EditorGUI.TextField(valueRect, property.stringValue);
                }
                else
                {
                    if (secureString.Obscure)
                    {
                        EditorGUI.PasswordField(valueRect, property.stringValue);
                    }
                    else
                    {
                        EditorGUI.TextField(valueRect, property.stringValue);
                    }
                }
            }

            string buttonLabel = editing ? "Done" : "Edit";
            if (GUI.Button(buttonRect, buttonLabel))
            {
                SetEditing(stateKey, controlName, !editing);
            }

            if (editing && Event.current.type == EventType.KeyDown &&
                GUI.GetNameOfFocusedControl() == controlName &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                SetEditing(stateKey, controlName, false);
                Event.current.Use();
            }

            EditorGUI.EndProperty();
        }

        protected override VisualElement CreatePropertyGUI_Internal(SerializedProperty property, string label)
        {
            if (property.propertyType != SerializedPropertyType.String)
                return LoogaPropertyDrawerUi.CreateMessage("SecureString is for strings only.", HelpBoxMessageType.Warning);

            SecureStringAttribute secureString = (SecureStringAttribute)attribute;
            string stateKey = GetStateKey(property);
            bool editing = SessionState.GetBool(stateKey, false);
            TextField field = new(label)
            {
                value = property.stringValue,
                isPasswordField = secureString.Obscure && !editing
            };
            Button button = new() { text = editing ? "Done" : "Edit" };
            SerializedObject owner = property.serializedObject;
            string path = property.propertyPath;

            void ApplyEditingState(bool nextEditing)
            {
                editing = nextEditing;
                SessionState.SetBool(stateKey, editing);
                field.SetEnabled(editing);
                field.isPasswordField = secureString.Obscure && !editing;
                button.text = editing ? "Done" : "Edit";
                if (editing)
                    field.Focus();
            }

            field.RegisterValueChangedCallback(evt =>
            {
                if (editing)
                    LoogaPropertyDrawerUi.Commit(owner, path, current => current.stringValue = evt.newValue);
            });
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (editing && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter))
                {
                    ApplyEditingState(false);
                    evt.StopPropagation();
                }
            });
            button.clicked += () => ApplyEditingState(!editing);
            ApplyEditingState(editing);
            return LoogaPropertyDrawerUi.CreateFieldWithButtons(field, button);
        }

        private static string GetStateKey(SerializedProperty property)
        {
            return $"{property.serializedObject.targetObject.GetInstanceID()}_{property.propertyPath}";
        }

        private static void SetEditing(string stateKey, string controlName, bool editing)
        {
            SessionState.SetBool(stateKey, editing);

            if (editing)
            {
                EditorGUI.FocusTextInControl(controlName);
            }
            else
            {
                GUI.FocusControl(null);
            }
        }
    }
}
