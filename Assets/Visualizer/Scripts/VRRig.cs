using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using TrackedPoseDriver = UnityEngine.InputSystem.XR.TrackedPoseDriver;

// VR (Quest 3 přes OpenXR): kamera sledovaná hlavou, ovladače a pohyb páčkami.
// Levá páčka = chůze ve směru pohledu, pravá páčka = otočení po 30°.
// Origin stojí na podlaze (tracking origin Floor), takže výška hlavy je skutečná.
public class VRRig : MonoBehaviour
{
    public static VRRig Instance { get; private set; }
    public static bool IsVR => Application.platform == RuntimePlatform.Android || XRSettings.isDeviceActive;

    public float moveSpeed = 1.5f;
    public float snapAngle = 30f;

    Transform head;
    InputAction move, turn;
    bool turnReady = true;

    public static VRRig Create(Camera cam, Vector3 floorPos, float yaw)
    {
        // XR Origin (core-utils) – potřebuje ho AR Foundation pro naskenované plochy místnosti;
        // vše sledované (hlava, ovladače, plochy) leží pod ním, takže posun originu posune i realitu.
        var root = new GameObject("XR Origin");
        root.SetActive(false);
        root.transform.SetPositionAndRotation(floorPos, Quaternion.Euler(0, yaw, 0));
        var offset = new GameObject("Camera Offset").transform;
        offset.SetParent(root.transform, false);
        var xo = root.AddComponent<Unity.XR.CoreUtils.XROrigin>();
        xo.Camera = cam;
        xo.CameraFloorOffsetObject = offset.gameObject;
        xo.RequestedTrackingOriginMode = Unity.XR.CoreUtils.XROrigin.TrackingOriginMode.Floor;
        var rig = root.AddComponent<VRRig>();
        Instance = rig;

        cam.transform.SetParent(offset, false);
        cam.transform.localPosition = new Vector3(0, 1.7f, 0);
        cam.transform.localRotation = Quaternion.identity;
        cam.nearClipPlane = 0.05f;
        var tpd = cam.gameObject.AddComponent<TrackedPoseDriver>();
        tpd.positionInput = Prop("<XRHMD>/centerEyePosition", "Vector3");
        tpd.rotationInput = Prop("<XRHMD>/centerEyeRotation", "Quaternion");
        rig.head = cam.transform;

        rig.LeftHand = rig.Controller(offset, "LeftHand");
        rig.RightHand = rig.Controller(offset, "RightHand");
        rig.RightAim = Pose(offset, "Aim R", "<XRController>{RightHand}/pointerPosition", "<XRController>{RightHand}/pointerRotation");
        root.AddComponent<VREnvironment>();
        root.AddComponent<VRMenu>();
        root.SetActive(true);

        rig.move = new InputAction("Move", binding: "<XRController>{LeftHand}/{Primary2DAxis}");
        rig.turn = new InputAction("Turn", binding: "<XRController>{RightHand}/{Primary2DAxis}");
        rig.move.Enable(); rig.turn.Enable();
        return rig;
    }

    static InputActionProperty Prop(string binding, string type)
    {
        var a = new InputAction(binding: binding, expectedControlType: type);
        a.Enable();
        return new InputActionProperty(a);
    }

    public Transform Head => head;
    public Transform LeftHand { get; private set; }
    public Transform RightHand { get; private set; }
    public Transform RightAim { get; private set; }

    static Transform Pose(Transform root, string name, string pos, string rot)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        var tpd = go.AddComponent<TrackedPoseDriver>();
        tpd.positionInput = Prop(pos, "Vector3");
        tpd.rotationInput = Prop(rot, "Quaternion");
        return go.transform;
    }

    // Ovladač sledující skutečnou ruku: oficiální model Quest 3 (Touch Plus) z Meta XR SDK,
    // když je k dispozici (Resources/VRAssets), jinak jednoduchý kvádr.
    Transform Controller(Transform root, string hand)
    {
        var t = Pose(root, "Controller " + hand, "<XRController>{" + hand + "}/devicePosition", "<XRController>{" + hand + "}/deviceRotation");
        var assets = Resources.Load<VRAssets>("VRAssets");
        var prefab = assets != null ? (hand == "LeftHand" ? assets.leftController : assets.rightController) : null;
        if (prefab != null)
        {
            var m = Instantiate(prefab, t, false);
            m.transform.localPosition = assets.modelOffset;
            m.transform.localEulerAngles = assets.modelRotation;
            var mat = VisUtil.LitMat(new Color(0.45f, 0.45f, 0.47f)); // v tmavém sále musí být vidět
            foreach (var r in m.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
            foreach (var a in m.GetComponentsInChildren<Animator>()) a.enabled = false;
        }
        else VisUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0, 0.02f), new Vector3(0.04f, 0.03f, 0.12f), VisUtil.BodyMat);
        return t;
    }

    // Pohyb páčkami vypnutý např. při umisťování DJ stolku
    [HideInInspector] public bool locomotionEnabled = true;

    void Update()
    {
        if (head == null || move == null || !locomotionEnabled) return;
        Vector2 m = move.ReadValue<Vector2>();
        if (m.sqrMagnitude > 0.04f)
        {
            Vector3 f = head.forward; f.y = 0; f.Normalize();
            Vector3 r = head.right; r.y = 0; r.Normalize();
            transform.position += (f * m.y + r * m.x) * moveSpeed * Time.deltaTime;
        }
        float t = turn.ReadValue<Vector2>().x;
        if (turnReady && Mathf.Abs(t) > 0.7f)
        {
            var pivot = new Vector3(head.position.x, transform.position.y, head.position.z);
            transform.RotateAround(pivot, Vector3.up, Mathf.Sign(t) * snapAngle);
            turnReady = false;
        }
        else if (Mathf.Abs(t) < 0.3f) turnReady = true;
    }

    // Přesun na pohled (Parket / DJ / ...): hlava se ocitne nad bodem pos a dívá se k look.
    public void Teleport(Vector3 pos, Vector3 look)
    {
        Vector3 dir = look - pos; dir.y = 0;
        if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
        float wantYaw = Quaternion.LookRotation(dir).eulerAngles.y;
        float headYaw = head != null ? head.localEulerAngles.y : 0f;
        transform.rotation = Quaternion.Euler(0, wantYaw - headYaw, 0);
        Vector3 headOffset = head != null ? transform.TransformVector(new Vector3(head.localPosition.x, 0, head.localPosition.z)) : Vector3.zero;
        transform.position = new Vector3(pos.x, 0, pos.z) - headOffset;
    }

    void OnDestroy()
    {
        move?.Dispose(); turn?.Dispose();
        if (Instance == this) Instance = null;
    }
}
