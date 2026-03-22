using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CableManager))]
public class CableHandles : Editor
{
    private int placingPoint = -1; // -1 = none, 0 = start, 1 = end

    public override void OnInspectorGUI()
    {
        EscapeKey();
        DrawDefaultInspector();
        DrawInspectorUI();
    }

    // draws inspector buttons and instructions
    private void DrawInspectorUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Quick Setup", EditorStyles.boldLabel);

        if (GUILayout.Button("Click to Place Start Point", GUILayout.Height(30)))
            placingPoint = 0;

        if (GUILayout.Button("Click to Place End Point", GUILayout.Height(30)))
            placingPoint = 1;

        if (placingPoint >= 0)
            EditorGUILayout.HelpBox("Click in the scene to place the " + (placingPoint == 0 ? "start" : "end") + " point. ESC to cancel.", MessageType.Info);
    }

    // handles scene GUI for placing and dragging points
    public void OnSceneGUI()
    {
        CableManager cable = (CableManager)target;

        if (placingPoint >= 0)
        {
            ClickToPlace(cable);
            return;
        }

        DrawHandles(cable);
    }

    // calls helpers to draw handles
    private void DrawHandles(CableManager cable)
    {
        DrawEndpointMarkers(cable);
        DragPoint(ref cable.startPoint, "Move Cable Start", Color.green);
        DragPoint(ref cable.endPoint, "Move Cable End", Color.red);
    }

    // draws handle markers
    private void DrawEndpointMarkers(CableManager cable)
    {
        float startSize = HandleUtility.GetHandleSize(cable.startPoint) * 0.15f;
        float endSize = HandleUtility.GetHandleSize(cable.endPoint) * 0.15f;

        Handles.color = Color.green;
        Handles.SphereHandleCap(0, cable.startPoint, Quaternion.identity, startSize, EventType.Repaint);

        Handles.color = Color.red;
        Handles.SphereHandleCap(0, cable.endPoint, Quaternion.identity, endSize, EventType.Repaint);
    }

    // handles point placement input
    private void ClickToPlace(CableManager cable)
    {
        Event click = Event.current;

        if (click.type == EventType.MouseDown && click.button == 0)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(click.mousePosition);

            if (placingPoint == 0)
            {
                Undo.RecordObject(cable, "Set Cable Start Point");
                cable.startPoint = ray.origin;
            }
            else
            {
                Undo.RecordObject(cable, "Set Cable End Point");
                cable.endPoint = ray.origin;
            }

            EditorUtility.SetDirty(cable);
            cable.Rebuild();
            placingPoint = -1;
            click.Use();
        }
    }

    // handles point dragging
    private void DragPoint(ref Vector3 point, string label, Color color)
    {
        CableManager cable = (CableManager)target;
        EditorGUI.BeginChangeCheck();
        Handles.color = color;
        Vector3 newPosition = Handles.PositionHandle(point, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(cable, label);
            point = newPosition;
            cable.Rebuild();
            EditorUtility.SetDirty(cable);
        }
    }

    // handles ESC key to cancel point placement
    private void EscapeKey()
    {
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape && placingPoint >= 0)
        {
            placingPoint = -1;
            Event.current.Use();
        }
    }
}
