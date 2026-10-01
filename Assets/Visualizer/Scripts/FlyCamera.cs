using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Pravé tlačítko myši = rozhlížení, WASD = chůze, Q/E = dolů/nahoru, Shift = rychleji.
public class FlyCamera : MonoBehaviour
{
    public float speed = 3f;
    public float lookSpeed = 0.15f;
    float yaw, pitch;

    void Start() { SyncAngles(); }

    // Po programovém přesunu kamery (předvolby pohledu) převezme aktuální natočení.
    public void SyncAngles()
    {
        var e = transform.eulerAngles;
        yaw = e.y;
        pitch = e.x > 180 ? e.x - 360 : e.x;
    }

    void Update()
    {
        Vector3 move = Vector3.zero;
        bool look, fast;
        Vector2 delta;
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current; var m = Mouse.current;
        if (k == null || m == null) return;
        if (k.wKey.isPressed) move.z += 1; if (k.sKey.isPressed) move.z -= 1;
        if (k.dKey.isPressed) move.x += 1; if (k.aKey.isPressed) move.x -= 1;
        if (k.eKey.isPressed) move.y += 1; if (k.qKey.isPressed) move.y -= 1;
        fast = k.leftShiftKey.isPressed;
        look = m.rightButton.isPressed;
        delta = m.delta.ReadValue();
#else
        if (Input.GetKey(KeyCode.W)) move.z += 1; if (Input.GetKey(KeyCode.S)) move.z -= 1;
        if (Input.GetKey(KeyCode.D)) move.x += 1; if (Input.GetKey(KeyCode.A)) move.x -= 1;
        if (Input.GetKey(KeyCode.E)) move.y += 1; if (Input.GetKey(KeyCode.Q)) move.y -= 1;
        fast = Input.GetKey(KeyCode.LeftShift);
        look = Input.GetMouseButton(1);
        delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
#endif
        if (look)
        {
            yaw += delta.x * lookSpeed;
            pitch = Mathf.Clamp(pitch - delta.y * lookSpeed, -89f, 89f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0);
        }
        float sp = speed * (fast ? 3f : 1f) * Time.deltaTime;
        transform.position += transform.rotation * new Vector3(move.x, 0, move.z) * sp + Vector3.up * move.y * sp;
    }
}
