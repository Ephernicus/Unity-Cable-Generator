using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CableManager))]
public class CableHandles : Editor
{
    public override void OnInspectorGUI()
    {
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

    // handles scene GUI for dragging points
    public void OnSceneGUI()
    {
        CableManager cable = (CableManager)target;
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


}
