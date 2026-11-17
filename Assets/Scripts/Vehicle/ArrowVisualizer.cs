using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class ArrowVisualizer : MonoBehaviour
{
    [Header("Component references")]
    public PIDController pidController;

    [Header("Forward-direction arrow")]
    public bool showForwardArrow = true;
    public Color forwardArrowColor = Color.black;
    public float forwardArrowLength = 2.0f;
    [Range(1f, 10f)]
    public float forwardArrowThickness = 5.0f;

    [Header("Steering-command arrow")]
    public bool showOffsetArrow = true;
    public Color offsetArrowColor = Color.black;
    public float offsetArrowLengthScale = 1.0f;
    [Range(1f, 10f)]
    public float offsetArrowThickness = 5.0f;

    [Header("Gizmo height")]
    [Tooltip("Shared world-space anchor height for arrows and text.")]
    public float gizmoHeight = 2.0f;

    [Header("Screen-space text")]
    public bool showOffsetText = true;
    public Color textColor = Color.black;
    [Range(10, 40)]
    public int textFontSize = 30;
    [Tooltip("Text offset in screen pixels. X is horizontal; positive Y moves upward.")]
    public Vector2 textScreenOffset = new Vector2(0, 30);
    [Tooltip("Text format. {0:F2} displays the steering command to two decimal places.")]
    public string textFormat = "Steering command: {0:F2}";
    [Tooltip("Text area size in screen pixels (width, height).")]
    public Vector2 textAreaSize = new Vector2(400, 50);

    [Header("Editor preview")]
    [Range(-1f, 1f)]
    public float editorPreviewOffset = 0.5f;

    void OnDrawGizmos()
    {
#if UNITY_EDITOR
        if (pidController == null) return;

        Vector3 basePos = transform.position + Vector3.up * gizmoHeight;

        if (showForwardArrow)
        {
            Handles.color = forwardArrowColor;
            Vector3 forwardDir = transform.forward * forwardArrowLength;
            DrawArrow(basePos, forwardDir, forwardArrowThickness);
        }

        if (showOffsetArrow)
        {
            float targetOffset = Application.isPlaying ? pidController.TargetOffset : editorPreviewOffset;

            if (Mathf.Abs(targetOffset) > 0.001f)
            {
                Handles.color = offsetArrowColor;
                float dynamicLength = targetOffset * offsetArrowLengthScale;
                // Positive paper commands turn left; Unity local +X points right.
                Vector3 offsetDir = -transform.right * dynamicLength;
                DrawArrow(basePos, offsetDir, offsetArrowThickness);
            }
        }
#endif
    }

    void OnGUI()
    {
        if (!showOffsetText) return;
        if (pidController == null && Application.isPlaying) return;

#if UNITY_EDITOR
        Camera camera = SceneView.currentDrawingSceneView?.camera ?? Camera.main;
        if (camera == null) return;
#else
        Camera camera = Camera.main;
        if (camera == null) return;
#endif

        float targetValue = Application.isPlaying ? pidController.TargetOffset : editorPreviewOffset;
        string labelText = string.Format(textFormat, targetValue);

        if (!Application.isPlaying)
        {
            labelText += " [Preview]";
        }

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = textFontSize,
            normal = { textColor = textColor },
            alignment = TextAnchor.MiddleCenter
        };

        Vector3 worldAnchor = transform.position + Vector3.up * gizmoHeight;
        Vector3 screenPoint = camera.WorldToScreenPoint(worldAnchor);

        if (screenPoint.z < 0) return;

        float finalX = screenPoint.x + textScreenOffset.x;
        float finalY = Screen.height - screenPoint.y - textScreenOffset.y;

        Rect labelRect = new Rect(finalX - (textAreaSize.x / 2), finalY - (textAreaSize.y / 2), textAreaSize.x, textAreaSize.y);

        GUI.Label(labelRect, labelText, style);
    }

    void DrawArrow(Vector3 start, Vector3 dir, float thickness)
    {
#if UNITY_EDITOR
        if (dir == Vector3.zero) return;
        Vector3 end = start + dir;
        Handles.DrawLine(start, end, thickness);

        float arrowHeadAngle = 20.0f;
        float arrowHeadLength = 0.35f * dir.magnitude;
        Vector3 right = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 180 + arrowHeadAngle, 0) * new Vector3(0, 0, 1);
        Vector3 left = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 180 - arrowHeadAngle, 0) * new Vector3(0, 0, 1);

        Handles.DrawLine(end, end + right * arrowHeadLength, thickness);
        Handles.DrawLine(end, end + left * arrowHeadLength, thickness);
#endif
    }
}