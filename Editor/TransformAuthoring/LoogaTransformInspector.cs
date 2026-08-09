using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LoogaSoft.Toolkit.TransformAuthoring
{
    [CustomEditor(typeof(Transform))]
    [CanEditMultipleObjects]
    internal sealed class LoogaTransformInspector : UnityEditor.Editor
    {
        private enum TransformProperty
        {
            Position,
            Rotation,
            Scale
        }

        private const string WorldSpaceSessionKey = "LoogaSoft.TransformAuthoring.WorldSpace";
        private const string CopyIconPath =
            "Packages/com.loogasoft.loogatoolkit/Editor/Inspector/Icons/Remix/copy.png";
        private const string PasteIconPath =
            "Packages/com.loogasoft.loogatoolkit/Editor/Inspector/Icons/Remix/clipboard-paste.png";
        private const float ActionButtonSize = 18f;
        private const float ActionButtonGap = 1f;
        private const float FieldActionGap = 3f;
        private const float ActionIconSize = 16f;

        private static readonly string[] SpaceOptions = { "Local", "World" };
        private static readonly Color XAxisColor = new(0.95f, 0.3f, 0.3f);
        private static readonly Color YAxisColor = new(0.35f, 0.85f, 0.4f);
        private static readonly Color ZAxisColor = new(0.3f, 0.65f, 1f);

        private static GUIStyle _xLabelStyle;
        private static GUIStyle _yLabelStyle;
        private static GUIStyle _zLabelStyle;

        private Texture2D _copyIcon;
        private Texture2D _pasteIcon;
        private GUIContent _resetIcon;
        private bool _worldSpace;

        private void OnEnable()
        {
            _worldSpace = SessionState.GetBool(WorldSpaceSessionKey, false);
            _copyIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(CopyIconPath);
            _pasteIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(PasteIconPath);
            _resetIcon = EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_Refresh" : "Refresh");
        }

        public override void OnInspectorGUI()
        {
            Transform first = GetFirstTransform();
            if (first == null)
                return;

            DrawSpaceSelector();
            DrawTransformRow("Position", TransformProperty.Position, first);
            DrawTransformRow("Rotation", TransformProperty.Rotation, first);
            DrawTransformRow("Scale", TransformProperty.Scale, first);
            DrawSizeRow(first);
        }

        private void OnSceneGUI()
        {
            if (target is not Transform activeTransform ||
                !TransformBoundsUtility.TryCalculate(activeTransform, out TransformBounds bounds))
            {
                return;
            }

            DrawBounds(activeTransform, bounds);
        }

        private void DrawSpaceSelector()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Space", EditorStyles.label);
                GUILayout.FlexibleSpace();
                int selected = GUILayout.Toolbar(
                    _worldSpace ? 1 : 0,
                    SpaceOptions,
                    EditorStyles.toolbarButton,
                    GUILayout.Width(120f));
                bool useWorldSpace = selected == 1;
                if (useWorldSpace == _worldSpace)
                    return;

                _worldSpace = useWorldSpace;
                SessionState.SetBool(WorldSpaceSessionKey, _worldSpace);
                Repaint();
                SceneView.RepaintAll();
            }
        }

        private void DrawTransformRow(string label, TransformProperty property, Transform first)
        {
            Rect rowRect = EditorGUILayout.GetControlRect();
            float actionsWidth = (ActionButtonSize * 3f) + (ActionButtonGap * 2f);
            Rect fieldRect = rowRect;
            fieldRect.width -= actionsWidth + FieldActionGap;

            Vector3 currentValue = ReadValue(first, property);
            EditorGUI.showMixedValue = HasMixedValue(currentValue, property);
            EditorGUI.BeginChangeCheck();
            Vector3 nextValue = EditorGUI.Vector3Field(fieldRect, label, currentValue);
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;

            if (changed)
                ApplyValue(property, nextValue, $"Change Transform {label}");

            Rect buttonRect = new(
                fieldRect.xMax + FieldActionGap,
                rowRect.y + Mathf.Floor((rowRect.height - ActionButtonSize) * 0.5f),
                ActionButtonSize,
                ActionButtonSize);

            DrawCopyButton(buttonRect, currentValue, label);
            buttonRect.x += ActionButtonSize + ActionButtonGap;
            DrawPasteButton(buttonRect, property, label);
            buttonRect.x += ActionButtonSize + ActionButtonGap;
            DrawResetButton(buttonRect, property, label);
        }

        private void DrawSizeRow(Transform first)
        {
            Vector3 size = Vector3.zero;
            bool hasBounds = TransformBoundsUtility.TryCalculate(first, out TransformBounds firstBounds);
            if (hasBounds)
                size = firstBounds.WorldSize;

            bool mixed = false;
            for (int i = 1; i < targets.Length; i++)
            {
                if (targets[i] is not Transform transform)
                    continue;

                bool targetHasBounds = TransformBoundsUtility.TryCalculate(transform, out TransformBounds targetBounds);
                if (targetHasBounds != hasBounds ||
                    targetHasBounds && !Approximately(size, targetBounds.WorldSize))
                {
                    mixed = true;
                    break;
                }
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.showMixedValue = mixed;
                EditorGUILayout.Vector3Field(
                    new GUIContent(
                        "Size",
                        "World-space size of rendered geometry. Collider bounds are used when no rendered geometry exists."),
                    size);
                EditorGUI.showMixedValue = false;
            }
        }

        private void DrawCopyButton(Rect rect, Vector3 value, string label)
        {
            if (DrawActionButton(rect, _copyIcon, $"Copy {label}"))
                TransformVectorClipboard.Copy(value);
        }

        private void DrawPasteButton(Rect rect, TransformProperty property, string label)
        {
            bool canPaste = TransformVectorClipboard.TryRead(out Vector3 value);
            using (new EditorGUI.DisabledScope(!canPaste))
            {
                if (DrawActionButton(rect, _pasteIcon, $"Paste {label}"))
                    ApplyValue(property, value, $"Paste Transform {label}");
            }
        }

        private void DrawResetButton(Rect rect, TransformProperty property, string label)
        {
            Vector3 resetValue = property == TransformProperty.Scale ? Vector3.one : Vector3.zero;
            if (DrawActionButton(rect, _resetIcon.image, $"Reset {label}"))
                ApplyValue(property, resetValue, $"Reset Transform {label}");
        }

        private static bool DrawActionButton(Rect rect, Texture icon, string tooltip)
        {
            bool clicked = GUI.Button(rect, new GUIContent(string.Empty, tooltip), EditorStyles.miniButton);
            if (Event.current.type != EventType.Repaint || icon == null)
                return clicked;

            Rect iconRect = new(
                rect.x + Mathf.Floor((rect.width - ActionIconSize) * 0.5f),
                rect.y + Mathf.Floor((rect.height - ActionIconSize) * 0.5f),
                ActionIconSize,
                ActionIconSize);
            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
            return clicked;
        }

        private Vector3 ReadValue(Transform transform, TransformProperty property)
        {
            return property switch
            {
                TransformProperty.Position => _worldSpace ? transform.position : transform.localPosition,
                TransformProperty.Rotation => _worldSpace ? transform.eulerAngles : transform.localEulerAngles,
                TransformProperty.Scale => _worldSpace ? transform.lossyScale : transform.localScale,
                _ => Vector3.zero
            };
        }

        private bool HasMixedValue(Vector3 firstValue, TransformProperty property)
        {
            for (int i = 1; i < targets.Length; i++)
            {
                if (targets[i] is Transform transform && !Approximately(firstValue, ReadValue(transform, property)))
                    return true;
            }

            return false;
        }

        private void ApplyValue(TransformProperty property, Vector3 value, string undoName)
        {
            Object[] undoTargets = new Object[targets.Length];
            for (int i = 0; i < targets.Length; i++)
                undoTargets[i] = targets[i];

            Undo.RecordObjects(undoTargets, undoName);
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] is not Transform transform)
                    continue;

                switch (property)
                {
                    case TransformProperty.Position:
                        if (_worldSpace)
                            transform.position = value;
                        else
                            transform.localPosition = value;
                        break;

                    case TransformProperty.Rotation:
                        if (_worldSpace)
                            transform.eulerAngles = value;
                        else
                            transform.localEulerAngles = value;
                        break;

                    case TransformProperty.Scale:
                        transform.localScale = _worldSpace ? CalculateLocalScale(transform, value) : value;
                        break;
                }

                PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
                EditorUtility.SetDirty(transform);
            }

            SceneView.RepaintAll();
        }

        private static Vector3 CalculateLocalScale(Transform transform, Vector3 desiredWorldScale)
        {
            if (transform.parent == null)
                return desiredWorldScale;

            Vector3 currentLocalScale = transform.localScale;
            Vector3 currentWorldScale = transform.lossyScale;
            Vector3 parentWorldScale = transform.parent.lossyScale;
            return new Vector3(
                ConvertWorldScaleAxis(currentLocalScale.x, currentWorldScale.x, parentWorldScale.x, desiredWorldScale.x),
                ConvertWorldScaleAxis(currentLocalScale.y, currentWorldScale.y, parentWorldScale.y, desiredWorldScale.y),
                ConvertWorldScaleAxis(currentLocalScale.z, currentWorldScale.z, parentWorldScale.z, desiredWorldScale.z));
        }

        private static float ConvertWorldScaleAxis(float local, float world, float parentWorld, float desiredWorld)
        {
            if (Mathf.Abs(world) > Mathf.Epsilon)
                return local * (desiredWorld / world);

            if (Mathf.Abs(parentWorld) > Mathf.Epsilon)
                return desiredWorld / parentWorld;

            return Mathf.Approximately(desiredWorld, 0f) ? 0f : local;
        }

        private Transform GetFirstTransform()
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] is Transform transform)
                    return transform;
            }

            return null;
        }

        private static bool Approximately(Vector3 left, Vector3 right)
        {
            return Mathf.Approximately(left.x, right.x) &&
                   Mathf.Approximately(left.y, right.y) &&
                   Mathf.Approximately(left.z, right.z);
        }

        private static void DrawBounds(Transform transform, TransformBounds bounds)
        {
            EnsureLabelStyles();
            Bounds localBounds = bounds.LocalBounds;
            Color previousColor = Handles.color;
            CompareFunction previousZTest = Handles.zTest;
            Matrix4x4 previousMatrix = Handles.matrix;

            Handles.zTest = CompareFunction.LessEqual;
            Handles.matrix = transform.localToWorldMatrix;
            Handles.color = new Color(1f, 1f, 1f, 0.55f);
            Handles.DrawWireCube(localBounds.center, localBounds.size);
            Handles.matrix = Matrix4x4.identity;

            Vector3 min = localBounds.min;
            Vector3 max = localBounds.max;
            DrawDimension(
                transform.TransformPoint(new Vector3(min.x, min.y, min.z)),
                transform.TransformPoint(new Vector3(max.x, min.y, min.z)),
                XAxisColor,
                "X",
                bounds.WorldSize.x,
                _xLabelStyle);
            DrawDimension(
                transform.TransformPoint(new Vector3(min.x, min.y, min.z)),
                transform.TransformPoint(new Vector3(min.x, max.y, min.z)),
                YAxisColor,
                "Y",
                bounds.WorldSize.y,
                _yLabelStyle);
            DrawDimension(
                transform.TransformPoint(new Vector3(min.x, min.y, min.z)),
                transform.TransformPoint(new Vector3(min.x, min.y, max.z)),
                ZAxisColor,
                "Z",
                bounds.WorldSize.z,
                _zLabelStyle);

            Handles.matrix = previousMatrix;
            Handles.color = previousColor;
            Handles.zTest = previousZTest;
        }

        private static void DrawDimension(
            Vector3 start,
            Vector3 end,
            Color color,
            string axis,
            float length,
            GUIStyle style)
        {
            Handles.color = color;
            Handles.DrawAAPolyLine(2f, start, end);

            Vector3 midpoint = Vector3.Lerp(start, end, 0.5f);
            float offset = HandleUtility.GetHandleSize(midpoint) * 0.025f;
            Camera camera = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera : null;
            Vector3 labelOffset = camera != null ? camera.transform.up * offset : Vector3.up * offset;
            Handles.Label(midpoint + labelOffset, $"{axis}: {length:0.###} m", style);
        }

        private static void EnsureLabelStyles()
        {
            _xLabelStyle ??= CreateLabelStyle(XAxisColor);
            _yLabelStyle ??= CreateLabelStyle(YAxisColor);
            _zLabelStyle ??= CreateLabelStyle(ZAxisColor);
        }

        private static GUIStyle CreateLabelStyle(Color color)
        {
            GUIStyle style = new(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = EditorStyles.miniLabel.fontSize,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(3, 3, 1, 1)
            };
            style.normal.textColor = color;
            return style;
        }
    }
}
