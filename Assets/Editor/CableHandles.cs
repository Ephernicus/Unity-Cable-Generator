using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CableManager))]
public class CableHandles : Editor
{
    private bool snapEnabled = true;
    private bool prevSnapEnabled = true;
    private const int snapDistance = 1;
    private bool editGroup = false;
    private CableManager cable; 
    private Collider[] collisionRange = new Collider[16]; // reusable array for collision checks

    public override void OnInspectorGUI() 
    {
        // ==================== INSPECTOR GUI ====================

        CableMode prevMode = cable.mode;
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
        snapEnabled = EditorGUILayout.ToggleLeft("Snapping", snapEnabled, EditorStyles.boldLabel);

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

        // snap existing endpoints when toggling snap on
        if (snapEnabled && !prevSnapEnabled)
        {
            cable.startPoint = SnapToSurface(cable.startPoint);
            cable.endPoint = SnapToSurface(cable.endPoint);
            cable.Rebuild();
        }
        prevSnapEnabled = snapEnabled;

        // ==================== BATCH ====================

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Batch Management", EditorStyles.boldLabel);
        cable.group = (CableGroup)EditorGUILayout.EnumPopup("Group", cable.group);
        editGroup = EditorGUILayout.ToggleLeft("Edit Group", editGroup);

        if (editGroup)
        {
            string groupName = System.Text.RegularExpressions.Regex.Replace(cable.group.ToString(), "(\\d)", " $1");
            EditorGUILayout.HelpBox("Changes will apply to all cables in " + groupName + ".", MessageType.None);
        }

        // update current selected cable if anything changed
        if (GUI.changed)
        {
            // flag changes for saving
            EditorUtility.SetDirty(cable);

            // detect what changed
            bool structureChanged = cable.startPoint != prevStart || cable.endPoint != prevEnd || cable.segmentCount != prevSegments;
            bool shapeChanged = cable.ringSides != prevSides || cable.cableRadius != prevRadius;

            if (cable.mode == CableMode.Static)
                cable.Rebuild();
            else if (structureChanged)
                cable.InitializePhysics();
            else if (shapeChanged)
                cable.RebuildMesh();

            // if edit group is selcted propagate changes to other cables in same group
            if (editGroup)
            {
                // look for all cables in scene
                CableManager[] allCables = FindObjectsByType<CableManager>(FindObjectsSortMode.None);

                foreach (var other in allCables)
                {   // skip current selected and cables in different groups
                    if (other == cable || other.group != cable.group) continue;

                    // apply changes to other cables
                    other.mode = cable.mode;
                    other.segmentCount = cable.segmentCount;
                    other.ringSides = cable.ringSides;
                    other.cableRadius = cable.cableRadius;
                    other.sag = cable.sag;
                    other.gravity = cable.gravity;
                    other.damping = cable.damping;
                    other.stiffness = cable.stiffness;
                    EditorUtility.SetDirty(other);

                    if (other.mode == CableMode.Static)
                        other.Rebuild();
                    else if (structureChanged)
                        other.InitializePhysics();
                    else if (shapeChanged)
                        other.RebuildMesh();
                }
            }
        }
    }

    // ==================== CABLE HANDLES  ====================

    // hides transform tool when cable is selected
    private void OnEnable()
    {
        cable = (CableManager)target;
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
        DrawEndpointMarkers(cable);
        DragPoint(ref cable.startPoint, Color.green);
        DragPoint(ref cable.endPoint, Color.red);
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

    // handles point dragging with optional surface snapping
    private void DragPoint(ref Vector3 point, Color color)
    {
        // native function, starts listening for change
        EditorGUI.BeginChangeCheck();
        Handles.color = color;
        Vector3 newPosition = Handles.PositionHandle(point, Quaternion.identity);

        // if change detected return true, snap to surface if enabled, update cable and mark dirty
        if (EditorGUI.EndChangeCheck())
        {
            if (snapEnabled)
                newPosition = SnapToSurface(newPosition);

            point = newPosition;
            if (cable.mode == CableMode.Static)
                cable.Rebuild();
            EditorUtility.SetDirty(cable);
        }
    }

    // ==================== SNAPPING ====================

    // finds the closest surface point within snap distance
    private Vector3 SnapToSurface(Vector3 position)
    {
        // check for colliders within snap distance of point
        int hits = Physics.OverlapSphereNonAlloc(position, snapDistance, collisionRange);
        // if no nearby colliders, keep original position
        if (hits == 0) return position;

        // set closestDist to max float value so any distance will be smaller
        float closestDist = float.MaxValue;
        // set closestPoint to original position
        Vector3 closestPoint = position;

        // iterate through all colliders and find closest point
        for (int i = 0; i < hits; i++)
        {
            Collider col = collisionRange[i];
            // find closest point on collider surface to given position
            Vector3 surface = col.ClosestPoint(position);
            // distance from point to surface
            float dist = Vector3.Distance(position, surface);
            // keep track of closest point and distance
            if (dist < closestDist)
            {
                closestDist = dist;
                closestPoint = surface;
            }
        }

        // push point slightly inside surface
        Vector3 inwardDir = (closestPoint - position).normalized;
        // if point is exactly on the surface default to down direction
        if (inwardDir.sqrMagnitude < 0.001f) inwardDir = Vector3.down;
        return closestPoint + inwardDir * cable.cableRadius;
    }
}
