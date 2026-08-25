using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Inspector.Editor
{
    [CustomPropertyDrawer(typeof(NavMeshAreaAttribute))]
    public sealed class NavMeshAreaDrawer : PropertyDrawerBase
    {
        protected override void OnGUI_Internal(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            string[] areaNames = UnityEngine.AI.NavMesh.GetAreaNames();
            if (areaNames.Length == 0)
            {
                EditorGUI.LabelField(position, label.text, "No NavMesh areas configured");
            }
            else if (property.propertyType == SerializedPropertyType.Integer)
            {
                int currentIndex = FindAreaNameIndex(areaNames, property.intValue);
                int nextIndex = LoogaGUI.Popup(position, label.text, currentIndex, areaNames);
                property.intValue = UnityEngine.AI.NavMesh.GetAreaFromName(areaNames[nextIndex]);
            }
            else if (property.propertyType == SerializedPropertyType.String)
            {
                int currentIndex = Mathf.Max(0, System.Array.IndexOf(areaNames, property.stringValue));
                int nextIndex = LoogaGUI.Popup(position, label.text, currentIndex, areaNames);
                property.stringValue = areaNames[nextIndex];
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "Use NavMeshAreaAttribute with ints or strings only");
            }

            EditorGUI.EndProperty();
        }

        protected override VisualElement CreatePropertyGUI_Internal(SerializedProperty property, string label)
        {
            string[] areaNames = UnityEngine.AI.NavMesh.GetAreaNames();
            if (areaNames.Length == 0)
                return LoogaPropertyDrawerUi.CreateMessage("No NavMesh areas are configured.", HelpBoxMessageType.Info);

            int selected = property.propertyType == SerializedPropertyType.String
                ? Mathf.Max(0, System.Array.IndexOf(areaNames, property.stringValue))
                : FindAreaNameIndex(areaNames, property.intValue);
            return LoogaPropertyDrawerUi.CreatePopup(property, label, areaNames, selected, (current, index) =>
            {
                if (current.propertyType == SerializedPropertyType.String)
                    current.stringValue = areaNames[index];
                else
                    current.intValue = UnityEngine.AI.NavMesh.GetAreaFromName(areaNames[index]);
            });
        }

        private static int FindAreaNameIndex(string[] areaNames, int areaIndex)
        {
            for (int i = 0; i < areaNames.Length; i++)
            {
                if (UnityEngine.AI.NavMesh.GetAreaFromName(areaNames[i]) == areaIndex)
                    return i;
            }

            return 0;
        }
    }
}
