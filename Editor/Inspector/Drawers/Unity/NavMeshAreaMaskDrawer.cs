using LoogaSoft.Inspector.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.Inspector.Editor
{
    [CustomPropertyDrawer(typeof(NavMeshAreaMaskAttribute))]
    public sealed class NavMeshAreaMaskDrawer : PropertyDrawerBase
    {
        protected override void OnGUI_Internal(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType == SerializedPropertyType.Integer)
            {
                string[] areaNames = UnityEngine.AI.NavMesh.GetAreaNames();

                if (areaNames.Length == 0)
                {
                    EditorGUI.LabelField(position, label.text, "No NavMesh areas configured");
                }
                else
                {
                    property.intValue = NamedBitMaskFieldUtility.DrawMaskField(
                        position,
                        label,
                        property.intValue,
                        areaNames,
                        GetAreaIndices(areaNames));
                }
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "Use NavMeshAreaMaskAttribute with ints only");
            }

            EditorGUI.EndProperty();
        }

        protected override VisualElement CreatePropertyGUI_Internal(SerializedProperty property, string label)
        {
            if (property.propertyType != SerializedPropertyType.Integer)
                return LoogaPropertyDrawerUi.CreateMessage("NavMeshAreaMask is for integer fields only.", HelpBoxMessageType.Warning);

            string[] names = UnityEngine.AI.NavMesh.GetAreaNames();
            int[] indices = GetAreaIndices(names);
            int displayed = LoogaPropertyDrawerUi.ToDisplayedMask(property.intValue, indices);
            return LoogaPropertyDrawerUi.CreateMaskField(
                property,
                label,
                names,
                displayed,
                value => LoogaPropertyDrawerUi.ToActualMask(value, indices));
        }

        private static int[] GetAreaIndices(string[] areaNames)
        {
            int[] areaIndices = new int[areaNames.Length];

            for (int i = 0; i < areaNames.Length; i++)
                areaIndices[i] = UnityEngine.AI.NavMesh.GetAreaFromName(areaNames[i]);

            return areaIndices;
        }
    }
}
