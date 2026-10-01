using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Prostředí ve VR (Quest 3):
//  • Virtuální sál (výchozí)
//  • Naskenovaná místnost – plochy z Room Setupu Questu (podlaha, stěny, strop, nábytek) místo sálu
//  • Passthrough – skutečné okolí z kamer brýlí; světlo dopadá na „lapače světla“
//    (podlaha, případně naskenované plochy), paprsky se přičítají přes obraz kamer.
// „Umístit DJ stolek“: paprskem ukážeš na podlahu, spouští potvrdíš – virtuální rig se posune
// i natočí tak, že stolek stojí tam a parket směřuje k tobě.
public class VREnvironment : MonoBehaviour
{
    public bool Passthrough { get; private set; }
    public bool ScannedRoom { get; private set; }
    public bool Placing { get; private set; }
    public int PlaneCount => planes.Count;

    VRRig rig;
    SceneBuilder scene;
    VisualizerMenu app;
    Camera cam;
    ARSession session;
    ARCameraManager camManager;
    ARPlaneManager planeManager;
    Material catcherMat, furnitureMat;
    GameObject catcherFloor;
    readonly Dictionary<ARPlane, MeshRenderer> planes = new Dictionary<ARPlane, MeshRenderer>();
    InputAction trigger, cancel;
    Transform marker;
    bool permissionAsked;

    void Start()
    {
        rig = GetComponent<VRRig>();
        scene = FindFirstObjectByType<SceneBuilder>();
        app = FindFirstObjectByType<VisualizerMenu>();
        cam = rig.Head.GetComponent<Camera>();

        var sh = Resources.Load<Shader>("LightCatcher");
        if (sh != null) catcherMat = new Material(sh);

        // AR Foundation: session + passthrough kamera + plochy místnosti (vše zpočátku vypnuté)
        var sgo = new GameObject("AR Session");
        sgo.SetActive(false);
        session = sgo.AddComponent<ARSession>();
        camManager = cam.gameObject.AddComponent<ARCameraManager>();
        camManager.enabled = false;
        planeManager = gameObject.AddComponent<ARPlaneManager>();
        planeManager.enabled = false;
        planeManager.trackablesChanged.AddListener(OnPlanesChanged);

        // podlaha – lapač světla pro passthrough bez skenu
        catcherFloor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        catcherFloor.name = "Light Catcher Floor";
        Destroy(catcherFloor.GetComponent<Collider>());
        catcherFloor.transform.position = new Vector3(0, 0.002f, 0);
        catcherFloor.transform.localScale = new Vector3(4f, 1f, 4f); // 40 × 40 m
        if (catcherMat != null) catcherFloor.GetComponent<Renderer>().sharedMaterial = catcherMat;
        catcherFloor.SetActive(false);

        marker = new GameObject("Place Marker").transform;
        VisUtil.SetColor(VisUtil.Emitter(PrimitiveType.Cylinder, marker, new Vector3(0, 0.005f, -0.35f), new Vector3(1.8f, 0.002f, 0.7f)), new Color(0.3f, 0.6f, 1f), 1.5f);   // půdorys stolku
        VisUtil.SetColor(VisUtil.Emitter(PrimitiveType.Cube, marker, new Vector3(0, 0.01f, 0.5f), new Vector3(0.05f, 0.01f, 1f)), new Color(0.3f, 0.6f, 1f), 2f);          // směr k parketu
        marker.gameObject.SetActive(false);

        trigger = new InputAction("PlaceTrigger", InputActionType.Button, "<XRController>{RightHand}/{TriggerButton}");
        cancel = new InputAction("PlaceCancel", InputActionType.Button, "<XRController>{RightHand}/{SecondaryButton}");
        trigger.Enable(); cancel.Enable();

        var s = app != null ? app.Settings : null;
        Apply(s != null && s.vrPassthrough, s != null && s.vrScannedRoom);
    }

    // ---------- přepínání prostředí ----------
    public void SetPassthrough(bool on) { Apply(on, ScannedRoom); Save(); }
    public void SetScannedRoom(bool on) { Apply(Passthrough, on); Save(); }

    void Save()
    {
        var s = app != null ? app.Settings : null;
        if (s == null) return;
        s.vrPassthrough = Passthrough;
        s.vrScannedRoom = ScannedRoom;
        s.Save();
    }

    void Apply(bool passthrough, bool scanned)
    {
        Passthrough = passthrough;
        ScannedRoom = scanned;
        if (scanned) RequestScenePermission();

        session.gameObject.SetActive(passthrough || scanned);
        camManager.enabled = passthrough;
        // passthrough = průhledné pozadí (alfa 0), jinak černý sál
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = passthrough ? new Color(0, 0, 0, 0) : Color.black;
        planeManager.enabled = scanned;
        foreach (var kv in planes) if (kv.Key != null) kv.Key.gameObject.SetActive(scanned);

        scene.SetVirtualRoom(!passthrough && !scanned);
        // v passthrough jsou ambientní plochy virtuálních předmětů zbytečně tmavé – nevadí, jsou to „fyzické“ objekty
        catcherFloor.SetActive(passthrough && !(scanned && HasFloorPlane()));
        RefreshPlaneMaterials();
        UpdateCeiling();
    }

    void RequestScenePermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (permissionAsked) return;
        permissionAsked = true;
        const string perm = "com.oculus.permission.USE_SCENE";
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(perm))
            UnityEngine.Android.Permission.RequestUserPermission(perm);
#endif
    }

    // ---------- naskenované plochy ----------
    void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> e)
    {
        foreach (var p in e.added)
        {
            var mf = p.gameObject.AddComponent<MeshFilter>();
            var mr = p.gameObject.AddComponent<MeshRenderer>();
            p.gameObject.AddComponent<ARPlaneMeshVisualizer>();
            planes[p] = mr;
        }
        foreach (var kv in e.removed) planes.Remove(kv.Value);
        RefreshPlaneMaterials();
        UpdateCeiling();
        if (Passthrough) catcherFloor.SetActive(!(ScannedRoom && HasFloorPlane()));
    }

    void RefreshPlaneMaterials()
    {
        foreach (var kv in planes)
        {
            if (kv.Key == null || kv.Value == null) continue;
            var c = kv.Key.classifications;
            // dveře, okna, obrazy a neviditelné stěny leží ve stěně – nekreslit (z-fighting)
            bool skip = (c & (PlaneClassifications.DoorFrame | PlaneClassifications.WindowFrame |
                              PlaneClassifications.WallArt | PlaneClassifications.InvisibleWallFace)) != 0;
            kv.Value.enabled = !skip;
            Material m;
            if (Passthrough) m = catcherMat;
            else if ((c & PlaneClassifications.Floor) != 0) m = VisUtil.FloorMat;
            else if ((c & PlaneClassifications.Ceiling) != 0) m = VisUtil.BodyMat;
            else if ((c & (PlaneClassifications.Table | PlaneClassifications.SeatOfAnyType)) != 0)
                m = furnitureMat != null ? furnitureMat : (furnitureMat = VisUtil.LitMat(new Color(0.3f, 0.3f, 0.32f)));
            else m = VisUtil.WallMat;
            kv.Value.sharedMaterial = m;
        }
    }

    bool HasFloorPlane()
    {
        foreach (var kv in planes) if (kv.Key != null && (kv.Key.classifications & PlaneClassifications.Floor) != 0) return true;
        return false;
    }

    // Paprsky se ořezávají o strop: ve skenu skutečná výška stropu, v passthrough bez skenu neořezávat
    void UpdateCeiling()
    {
        float ceil = -1f;
        if (ScannedRoom)
            foreach (var kv in planes)
                if (kv.Key != null && (kv.Key.classifications & PlaneClassifications.Ceiling) != 0)
                    ceil = Mathf.Max(ceil, kv.Key.center.y);
        scene.ceilingOverride = ceil > 0f ? ceil : (Passthrough ? 20f : -1f);
    }

    // ---------- umístění DJ stolku ----------
    int placeStartFrame;

    public void StartPlacement()
    {
        Placing = true;
        placeStartFrame = Time.frameCount;   // stisk, kterým se volba spustila, nesmí hned umístit
        rig.locomotionEnabled = false;
        marker.gameObject.SetActive(true);
    }

    void StopPlacement()
    {
        Placing = false;
        rig.locomotionEnabled = true;
        marker.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!Placing || rig.RightAim == null) return;
        if (cancel.WasPressedThisFrame()) { StopPlacement(); return; }

        // průsečík paprsku s podlahou (y = 0)
        var aim = rig.RightAim;
        var r = new Ray(aim.position, aim.forward);
        if (!new Plane(Vector3.up, Vector3.zero).Raycast(r, out float d) || d > 15f) return;
        Vector3 p = r.GetPoint(d);
        Vector3 toUser = rig.Head.position - p; toUser.y = 0;
        if (toUser.sqrMagnitude < 0.01f) return;
        marker.position = p;
        marker.rotation = Quaternion.LookRotation(toUser.normalized, Vector3.up);

        if (trigger.WasPressedThisFrame() && Time.frameCount > placeStartFrame + 1)
        {
            // posunout a natočit XR Origin tak, aby bod p (realita) = kotva stolku (virtuál)
            // a směr „k uživateli“ = +Z virtuálního sálu (směr k parketu)
            float dy = Vector3.SignedAngle(toUser, Vector3.forward, Vector3.up);
            transform.RotateAround(p, Vector3.up, dy);
            transform.position += scene.DJTableAnchor - p;
            StopPlacement();
        }
    }

    void OnDestroy()
    {
        trigger?.Dispose(); cancel?.Dispose();
    }
}
