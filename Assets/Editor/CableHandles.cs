using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CableManager))]
public class CableHandles : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CableManager cable = (CableManager)target;

        if (GUILayout.Button("Rebuild"))
        {
            Undo.RecordObject(cable, "Rebuild Cable");
            cable.Rebuild();
            EditorUtility.SetDirty(cable);
        }
    }

    void OnSceneGUI()
    {
        CableManager cable = (CableManager)target;
        if (cable.startPoint == null || cable.endPoint == null) return;

        EditorGUI.BeginChangeCheck();

        Vector3 startPos = Handles.PositionHandle(cable.startPoint.position, Quaternion.identity);
        Vector3 endPos = Handles.PositionHandle(cable.endPoint.position, Quaternion.identity);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(cable.startPoint, "Move Cable Start");
            Undo.RecordObject(cable.endPoint, "Move Cable End");

            cable.startPoint.position = startPos;
            cable.endPoint.position = endPos;

            cable.Rebuild();
        }
    }
}