using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CableManager))]
public class CableHandles : Editor
{
    private int placingPoint = -1; // -1 = none, 0 = start, 1 = end

    public override void OnInspectorGUI()
    {
        EscapeKey();

        CableManager cable = (CableManager)target;

        // mode dropdown
        cable.mode = (CableMode)EditorGUILayout.EnumPopup("Mode", cable.mode);

        // snapshot values to detect what changed
        Vector3 prevStart = cable.startPoint;
        Vector3 prevEnd = cable.endPoint;
        int prevSegments = cable.segmentCount;
        int prevSides = cable.ringSides;
        float prevRadius = cable.cableRadius;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Endpoints", EditorStyles.boldLabel);
        cable.startPoint = EditorGUILayout.Vector3Field("Start Point", cable.startPoint);
        cable.endPoint = EditorGUILayout.Vector3Field("End Point", cable.endPoint);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Cable Shape", EditorStyles.boldLabel);
        cable.segmentCount = EditorGUILayout.IntSlider("Segments", cable.segmentCount, 1, 100);
        cable.ringSides = EditorGUILayout.IntSlider("Sides", cable.ringSides, 3, 32);
        cable.cableRadius = EditorGUILayout.Slider("Radius", cable.cableRadius, 0.001f, 1f);

        EditorGUILayout.Space();

        // mode-specific settings
        if (cable.mode == CableMode.Static)
        {
            EditorGUILayout.LabelField("Static Settings", EditorStyles.boldLabel);
            cable.sag = EditorGUILayout.Slider("Sag", cable.sag, 0f, 8f);
        }
        else
        {
            EditorGUILayout.LabelField("Physics Settings", EditorStyles.boldLabel);
            cable.gravity = EditorGUILayout.Slider("Gravity", cable.gravity, 0f, 20f);
            cable.damping = EditorGUILayout.Slider("Damping", cable.damping, 0.5f, 1f);
            cable.stiffness = EditorGUILayout.IntSlider("Stiffness", cable.stiffness, 1, 30);
        }

        // mark dirty if anything changed
        if (GUI.changed)
        {
            Undo.RecordObject(cable, "Edit Cable");
            EditorUtility.SetDirty(cable);

            bool structureChanged = cable.startPoint != prevStart || cable.endPoint != prevEnd || cable.segmentCount != prevSegments;
            bool shapeChanged = cable.ringSides != prevSides || cable.cableRadius != prevRadius;

            if (cable.mode == CableMode.Static)
                cable.Rebuild();
            else if (structureChanged)
                cable.InitializePhysics();
            else if (shapeChanged)
                cable.RebuildMesh();
        }

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

    // hides transform tool when cable is selected
    private void OnEnable()
    {
        Tools.hidden = true;
    }

    // restores transform tool when cable is deselected
    private void OnDisable()
    {
        Tools.hidden = false;
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

            // raycast against scene geometry, fallback to a point 10 units from camera
            Vector3 hitPoint;
            if (Physics.Raycast(ray, out RaycastHit hit))
                hitPoint = hit.point;
            else
                hitPoint = ray.GetPoint(10f);

            if (placingPoint == 0)
            {
                Undo.RecordObject(cable, "Set Cable Start Point");
                cable.startPoint = hitPoint;
            }
            else
            {
                Undo.RecordObject(cable, "Set Cable End Point");
                cable.endPoint = hitPoint;
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
