using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class CurveDrawerWindow : EditorWindow
{
    [SerializeField] private AnimationCurve _generatedCurve = new AnimationCurve();
    [SerializeField] private List<Vector2> _normalizedPoints = new List<Vector2>();
    
    [SerializeField] private float _smoothing = 2.0f;
    private Rect _drawArea;
    private static AnimationCurve _curveClipboard;

    private const float GridStep = 30f; 
    
    // Timer variables
    private double _lastCopyTime = -100;
    private const double CopyFeedbackDuration = 1.0;

    [MenuItem("LoogaSoft/Toolkit/Curve Sketcher")]
    public static void ShowWindow() => GetWindow<CurveDrawerWindow>("Curve Sketcher");

    private void OnGUI()
    {
        EditorGUILayout.Space(10);

        // 1. SMOOTHING SLIDER
        EditorGUI.BeginChangeCheck();
        float newSmoothing = EditorGUILayout.Slider("Smoothing Amount", _smoothing, 0.1f, 20f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(this, "Adjust Smoothing");
            _smoothing = newSmoothing;
            ProcessPointsToCurve();
        }
        
        EditorGUILayout.Space(5);

        // 2. SKETCH AREA
        _drawArea = GUILayoutUtility.GetRect(10, 1000, 10, 1000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(_drawArea, new Color(0.12f, 0.12f, 0.12f));
        
        DrawSquareGrid(_drawArea); 
        GUI.Label(_drawArea, " [Sketch Area] ", EditorStyles.centeredGreyMiniLabel);

        HandleDrawingInput();
        DrawPreview();

        EditorGUILayout.Space(10);

        // 3. TALLER OUTPUT VIEW
        Rect totalRect = EditorGUILayout.GetControlRect(false, 60); 
        float labelHeight = 18f;
        Rect labelRect = new Rect(totalRect.x, totalRect.y + (totalRect.height - labelHeight) / 2f, EditorGUIUtility.labelWidth, labelHeight);
        Rect fieldRect = new Rect(totalRect.x + EditorGUIUtility.labelWidth, totalRect.y, totalRect.width - EditorGUIUtility.labelWidth, totalRect.height);

        EditorGUI.LabelField(labelRect, "Output Curve");
        
        EditorGUI.BeginChangeCheck();
        _generatedCurve = EditorGUI.CurveField(fieldRect, _generatedCurve);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(this, "Manual Curve Edit");
        }

        DrawSquareGrid(fieldRect);
        HandleCurveFieldContextMenu(fieldRect);

        EditorGUILayout.Space(10);

        // 4. DUAL BUTTONS (Stabilized Width)
        EditorGUILayout.BeginHorizontal();
        
        // Calculate a fixed width for both buttons so they don't shift when text changes
        // Subtracting ~25 pixels to account for margins and the gap between buttons
        float stabilizedButtonWidth = (position.width - 10f) / 2f;

        // Clear Button
        if (GUILayout.Button("Clear Canvas", GUILayout.Height(30), GUILayout.Width(stabilizedButtonWidth)))
        {
            Undo.RecordObject(this, "Clear Sketch");
            _normalizedPoints.Clear();
            _generatedCurve = new AnimationCurve();
        }

        // Copy Button with Timer Logic
        bool hasData = _normalizedPoints.Count > 0 || _generatedCurve.length > 0;
        double timeSinceCopy = EditorApplication.timeSinceStartup - _lastCopyTime;
        bool isShowingFeedback = timeSinceCopy < CopyFeedbackDuration;

        EditorGUI.BeginDisabledGroup(!hasData);
        
        Color originalColor = GUI.backgroundColor;
        if (isShowingFeedback) GUI.backgroundColor = Color.green;
        string buttonText = isShowingFeedback ? "COPIED" : "Copy Curve";

        // Applying the same fixed width here
        if (GUILayout.Button(buttonText, GUILayout.Height(30), GUILayout.Width(stabilizedButtonWidth)))
        {
            _curveClipboard = new AnimationCurve(_generatedCurve.keys);
            _lastCopyTime = EditorApplication.timeSinceStartup;
        }

        GUI.backgroundColor = originalColor; 
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.EndHorizontal();

        if (isShowingFeedback) Repaint();

        EditorGUILayout.Space(5);
    }

    // --- Grid Drawing ---
    private void DrawSquareGrid(Rect rect)
    {
        Handles.BeginGUI();
        Handles.color = new Color(1f, 1f, 1f, 0.04f); 
        for (float x = rect.x; x <= rect.xMax; x += GridStep)
            Handles.DrawLine(new Vector2(x, rect.y), new Vector2(x, rect.yMax));
        for (float y = rect.yMax; y >= rect.y; y -= GridStep)
            Handles.DrawLine(new Vector2(rect.x, y), new Vector2(rect.xMax, y));
        Handles.EndGUI();
    }

    // --- Input Handling ---
    private void HandleDrawingInput()
    {
        Event e = Event.current;
        if (!_drawArea.Contains(e.mousePosition)) return;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            Undo.RecordObject(this, "Sketch Curve");
            _normalizedPoints.Clear();
            _normalizedPoints.Add(NormalizeMousePos(e.mousePosition));
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && e.button == 0)
        {
            Vector2 normPos = NormalizeMousePos(e.mousePosition);
            if (_normalizedPoints.Count == 0 || Vector2.Distance(_normalizedPoints[_normalizedPoints.Count - 1], normPos) > 0.005f)
            {
                _normalizedPoints.Add(normPos);
                ProcessPointsToCurve();
                e.Use();
            }
        }
    }

    private Vector2 NormalizeMousePos(Vector2 mousePos)
    {
        float x = (mousePos.x - _drawArea.x) / _drawArea.width;
        float y = 1.0f - ((mousePos.y - _drawArea.y) / _drawArea.height);
        return new Vector2(x, y);
    }

    private void ProcessPointsToCurve()
    {
        if (_normalizedPoints.Count < 2) return;
        List<Vector2> simplified = new List<Vector2>();
        float scaledSmoothing = _smoothing / 500f; 
        SimplifyDouglasPeucker(_normalizedPoints, 0, _normalizedPoints.Count - 1, scaledSmoothing, simplified);
        simplified.Add(_normalizedPoints[_normalizedPoints.Count - 1]);

        Keyframe[] keys = new Keyframe[simplified.Count];
        for (int i = 0; i < simplified.Count; i++)
            keys[i] = new Keyframe(simplified[i].x, simplified[i].y);

        _generatedCurve.keys = keys;

        for (int i = 0; i < _generatedCurve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(_generatedCurve, i, AnimationUtility.TangentMode.Auto);
            AnimationUtility.SetKeyRightTangentMode(_generatedCurve, i, AnimationUtility.TangentMode.Auto);
        }
    }

    // --- Douglas-Peucker Logic ---
    private void SimplifyDouglasPeucker(List<Vector2> points, int first, int last, float tolerance, List<Vector2> result)
    {
        float maxDistance = 0;
        int index = 0;
        for (int i = first + 1; i < last; i++)
        {
            float distance = DistancePointLine(points[i], points[first], points[last]);
            if (distance > maxDistance) { maxDistance = distance; index = i; }
        }
        if (maxDistance > tolerance)
        {
            SimplifyDouglasPeucker(points, first, index, tolerance, result);
            SimplifyDouglasPeucker(points, index, last, tolerance, result);
        }
        else result.Add(points[first]);
    }

    private float DistancePointLine(Vector2 p, Vector2 a, Vector2 b)
    {
        float num = Mathf.Abs((b.y - a.y) * p.x - (b.x - a.x) * p.y + b.x * a.y - b.y * a.x);
        float den = Mathf.Sqrt(Mathf.Pow(b.y - a.y, 2) + Mathf.Pow(b.x - a.x, 2));
        return num / (den == 0 ? 1 : den);
    }

    private void DrawPreview()
    {
        if (_normalizedPoints.Count < 2) return;
        Handles.BeginGUI();
        Handles.color = Color.green;
        for (int i = 0; i < _normalizedPoints.Count - 1; i++)
        {
            Vector3 p1 = Denormalize(_normalizedPoints[i]);
            Vector3 p2 = Denormalize(_normalizedPoints[i+1]);
            Handles.DrawLine(p1, p2);
        }
        Handles.EndGUI();
        Repaint();
    }

    private Vector3 Denormalize(Vector2 normalized)
    {
        return new Vector3(
            normalized.x * _drawArea.width + _drawArea.x, 
            (1f - normalized.y) * _drawArea.height + _drawArea.y
        );
    }

    private void HandleCurveFieldContextMenu(Rect rect)
    {
        Event e = Event.current;
        if (e.type == EventType.ContextClick && rect.Contains(e.mousePosition))
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Copy Curve Data"), false, () => {
                _curveClipboard = new AnimationCurve(_generatedCurve.keys);
                _lastCopyTime = EditorApplication.timeSinceStartup;
            });
            if (_curveClipboard != null)
            {
                menu.AddItem(new GUIContent("Paste Curve Data"), false, () => {
                    Undo.RecordObject(this, "Paste Curve");
                    _generatedCurve = new AnimationCurve(_curveClipboard.keys);
                    _normalizedPoints.Clear(); 
                    Repaint();
                });
            }
            menu.ShowAsContext();
            e.Use();
        }
    }
}

[CustomPropertyDrawer(typeof(AnimationCurve))]
internal sealed class CurveSketchPropertyDrawer : PropertyDrawer
{
    private const float ButtonWidth = 22f;
    private const float ButtonGap = 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.AnimationCurve)
        {
            EditorGUI.PropertyField(position, property, label, true);
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        Rect buttonRect = new(
            position.xMax - ButtonWidth,
            position.y,
            ButtonWidth,
            EditorGUIUtility.singleLineHeight);
        Rect fieldRect = new(
            position.x,
            position.y,
            Mathf.Max(0f, buttonRect.xMin - ButtonGap - position.x),
            position.height);

        EditorGUI.BeginChangeCheck();
        AnimationCurve curve = EditorGUI.CurveField(fieldRect, label, property.animationCurveValue);
        if (EditorGUI.EndChangeCheck())
            property.animationCurveValue = curve;

        GUIContent sketchContent = EditorGUIUtility.IconContent("d_editicon.sml");
        sketchContent.tooltip = "Sketch this curve";
        if (GUI.Button(buttonRect, sketchContent, EditorStyles.miniButton))
        {
            UnityEngine.Object[] targets = property.serializedObject.targetObjects;
            PopupWindow.Show(
                buttonRect,
                new CompactCurveSketchPopup(targets, property.propertyPath, property.animationCurveValue));
        }

        EditorGUI.EndProperty();
    }
}

internal sealed class CompactCurveSketchPopup : PopupWindowContent
{
    private const float PopupWidth = 340f;
    private const float PopupHeight = 260f;
    private const float CanvasHeight = 155f;
    private const float GridStep = 24f;

    private readonly UnityEngine.Object[] _targets;
    private readonly string _propertyPath;
    private readonly List<Vector2> _points = new();
    private AnimationCurve _curve;
    private float _smoothing = 2f;
    private Rect _drawArea;
    private bool _isDrawing;

    public CompactCurveSketchPopup(
        UnityEngine.Object[] targets,
        string propertyPath,
        AnimationCurve initialCurve)
    {
        _targets = targets;
        _propertyPath = propertyPath;
        _curve = CopyCurve(initialCurve);
    }

    public override Vector2 GetWindowSize() => new(PopupWidth, PopupHeight);

    public override void OnGUI(Rect rect)
    {
        EditorGUILayout.LabelField("Curve Sketcher", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        _smoothing = EditorGUILayout.Slider("Smoothing", _smoothing, 0.1f, 20f);
        if (EditorGUI.EndChangeCheck() && _points.Count > 1)
            RebuildCurve();

        _drawArea = EditorGUILayout.GetControlRect(false, CanvasHeight);
        DrawCanvas(_drawArea);
        HandleDrawingInput();

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Clear"))
            {
                _points.Clear();
                _curve = new AnimationCurve();
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel"))
                editorWindow.Close();

            using (new EditorGUI.DisabledScope(_targets == null || _targets.Length == 0))
            {
                if (GUILayout.Button("Apply"))
                {
                    ApplyCurve();
                    editorWindow.Close();
                }
            }
        }

        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            Event.current.Use();
            editorWindow.Close();
        }
    }

    private void DrawCanvas(Rect rect)
    {
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));

        Handles.BeginGUI();
        Handles.color = new Color(1f, 1f, 1f, 0.04f);
        for (float x = rect.x; x <= rect.xMax; x += GridStep)
            Handles.DrawLine(new Vector2(x, rect.y), new Vector2(x, rect.yMax));
        for (float y = rect.y; y <= rect.yMax; y += GridStep)
            Handles.DrawLine(new Vector2(rect.x, y), new Vector2(rect.xMax, y));

        if (_curve != null && _curve.length > 0)
        {
            Handles.color = new Color(0.45f, 0.85f, 0.55f, 1f);
            Vector3 previous = CurvePoint(rect, 0f, _curve.Evaluate(0f));
            const int segmentCount = 80;
            for (int i = 1; i <= segmentCount; i++)
            {
                float time = i / (float)segmentCount;
                Vector3 current = CurvePoint(rect, time, _curve.Evaluate(time));
                Handles.DrawAAPolyLine(2f, previous, current);
                previous = current;
            }
        }

        Handles.EndGUI();
        GUI.Label(rect, "Draw from left to right", EditorStyles.centeredGreyMiniLabel);
    }

    private void HandleDrawingInput()
    {
        Event current = Event.current;
        bool isInside = _drawArea.Contains(current.mousePosition);

        if (current.type == EventType.MouseDown && current.button == 0 && isInside)
        {
            _points.Clear();
            _points.Add(Normalize(current.mousePosition));
            _isDrawing = true;
            current.Use();
            return;
        }

        if (current.type == EventType.MouseDrag && current.button == 0 && _isDrawing)
        {
            Vector2 point = Normalize(current.mousePosition);
            if (_points.Count == 0 || Vector2.Distance(_points[^1], point) > 0.004f)
            {
                _points.Add(point);
                RebuildCurve();
            }

            current.Use();
            editorWindow.Repaint();
            return;
        }

        if (current.type == EventType.MouseUp && current.button == 0 && _isDrawing)
        {
            _isDrawing = false;
            if (_points.Count > 1)
                RebuildCurve();
            current.Use();
        }
    }

    private Vector2 Normalize(Vector2 mousePosition)
    {
        float x = Mathf.InverseLerp(_drawArea.xMin, _drawArea.xMax, mousePosition.x);
        float y = 1f - Mathf.InverseLerp(_drawArea.yMin, _drawArea.yMax, mousePosition.y);
        return new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
    }

    private void RebuildCurve()
    {
        if (_points.Count < 2)
            return;

        List<Vector2> simplified = new();
        Simplify(_points, 0, _points.Count - 1, _smoothing / 500f, simplified);
        simplified.Add(_points[^1]);

        Keyframe[] keys = new Keyframe[simplified.Count];
        for (int i = 0; i < simplified.Count; i++)
            keys[i] = new Keyframe(simplified[i].x, simplified[i].y);

        _curve = new AnimationCurve(keys);
        for (int i = 0; i < _curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(_curve, i, AnimationUtility.TangentMode.Auto);
            AnimationUtility.SetKeyRightTangentMode(_curve, i, AnimationUtility.TangentMode.Auto);
        }
    }

    private void ApplyCurve()
    {
        Undo.RecordObjects(_targets, "Sketch Curve");
        foreach (UnityEngine.Object target in _targets)
        {
            if (target == null)
                continue;

            SerializedObject serializedTarget = new(target);
            SerializedProperty property = serializedTarget.FindProperty(_propertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.AnimationCurve)
                continue;

            property.animationCurveValue = CopyCurve(_curve);
            serializedTarget.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }
    }

    private static AnimationCurve CopyCurve(AnimationCurve source)
    {
        if (source == null)
            return new AnimationCurve();

        return new AnimationCurve(source.keys)
        {
            preWrapMode = source.preWrapMode,
            postWrapMode = source.postWrapMode
        };
    }

    private static Vector3 CurvePoint(Rect rect, float time, float value)
    {
        return new Vector3(
            Mathf.Lerp(rect.xMin, rect.xMax, time),
            Mathf.Lerp(rect.yMax, rect.yMin, Mathf.Clamp01(value)));
    }

    private static void Simplify(
        IReadOnlyList<Vector2> points,
        int first,
        int last,
        float tolerance,
        ICollection<Vector2> result)
    {
        float maximumDistance = 0f;
        int splitIndex = 0;
        for (int i = first + 1; i < last; i++)
        {
            float distance = DistanceFromLine(points[i], points[first], points[last]);
            if (distance <= maximumDistance)
                continue;

            maximumDistance = distance;
            splitIndex = i;
        }

        if (maximumDistance > tolerance)
        {
            Simplify(points, first, splitIndex, tolerance, result);
            Simplify(points, splitIndex, last, tolerance, result);
            return;
        }

        result.Add(points[first]);
    }

    private static float DistanceFromLine(Vector2 point, Vector2 start, Vector2 end)
    {
        float numerator = Mathf.Abs(
            (end.y - start.y) * point.x
            - (end.x - start.x) * point.y
            + end.x * start.y
            - end.y * start.x);
        float denominator = Vector2.Distance(start, end);
        return denominator <= Mathf.Epsilon ? 0f : numerator / denominator;
    }
}
