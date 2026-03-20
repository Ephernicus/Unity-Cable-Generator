using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CableManager))]
public class CableHandles : Editor
{
    public void OnSceneGUI()
    {

        // get reference and safety check
        CableManager cable = (CableManager)target;
        if (cable.startPoint == null || cable.endPoint == null) return;

        // get start and end transforms
        Transform start = cable.startPoint;
        Transform end = cable.endPoint;

        // scale handle size based on distance to camera
        float startSize = HandleUtility.GetHandleSize(start.position) * 0.15f; 
        float endSize = HandleUtility.GetHandleSize(end.position) * 0.15f;

        // draw endpoint markers
        Handles.color = Color.green;
        Handles.SphereHandleCap(0, start.position, Quaternion.identity, startSize, EventType.Repaint);
        Handles.color = Color.red;
        Handles.SphereHandleCap(0, end.position, Quaternion.identity, endSize, EventType.Repaint);

        // helper function call for start + end points
        HandlePosition(start, "Move Cable Start", Color.green);
        HandlePosition(end, "Move Cable End", Color.red);
    }

    // handle position changes for cable endpoints
    private void HandlePosition(Transform point, string label, Color color)
    {
        CableManager cable = (CableManager)target;
        EditorGUI.BeginChangeCheck();
        Handles.color = color;
        Vector3 newPosition = Handles.PositionHandle(point.position, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(point, label);
            point.position = newPosition;
            cable.Rebuild();

            // mark objects as changed/dirty
            EditorUtility.SetDirty(point);
            EditorUtility.SetDirty(cable);
        }
    }
}