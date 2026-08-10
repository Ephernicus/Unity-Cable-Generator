using UnityEngine;
using UnityEngine.InputSystem;

public class MoveCamera : MonoBehaviour
{
    // speed of camera movement and rotation, exposed in inspector
    public float moveSpeed = 20f;
    public float lookSpeed = 9f;

    float yaw; // horizontal scanning
    float pitch; //vertical scanning

    void Start()
    {
        // lock cursor to the center of screen and hide it
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse == null || keyboard == null) return; // null check for input devices

        // camera rotation
        Vector2 mouseDelta = mouse.delta.ReadValue();
        yaw += mouseDelta.x * lookSpeed * Time.deltaTime;
        pitch -= mouseDelta.y * lookSpeed * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, -80f, 80f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0);

        // camera movement
        Vector3 dir = Vector3.zero; // 
        if (keyboard.wKey.isPressed) dir += transform.forward;
        if (keyboard.sKey.isPressed) dir -= transform.forward;
        if (keyboard.dKey.isPressed) dir += transform.right;
        if (keyboard.aKey.isPressed) dir -= transform.right;

        // 3x speed when shift is held down
        transform.position += dir * moveSpeed * (keyboard.shiftKey.isPressed ? 3f : 1f) * Time.deltaTime;
    }
}