using UnityEngine;

// Postaví sál a celý rig při spuštění. Stačí dát na prázdný GameObject.
public class SceneBuilder : MonoBehaviour
{
    [Header("Shadery (přetáhni z Visualizer/Shaders)")]
    public Shader beamShader;
    public Shader emissiveShader;
    [Tooltip("URP/Lit – musí být přiřazený, aby se dostal do buildu")]
    public Shader litShader;

    [Header("Atmosféra")]
    [Range(0, 3)] public float haze = 1f;

    [Header("Sál (m)")]
    public float roomWidth = 14f;
    public float roomDepth = 12f;
    public float ceiling = 4f;

    [Header("Rig")]
    public float gigbarHeight = 2.2f;
    [Tooltip("Střed parketu – sem míří SS pozice Stage Center (kalibrace hlav)")]
    public Vector3 danceFloorCenter = Vector3.zero;
    public int tubeSegments = 8; // 40ch mód; 80ch = 16

    [HideInInspector] public GigBar gigbar;
    [HideInInspector] public PixelTube[] tubes = new PixelTube[4];
    [HideInInspector] public ParLight[] uplights = new ParLight[4];
    [HideInInspector] public MovingHead[] pockets = new MovingHead[2];
    Transform gigbarTripod;

    float backZ;

    void Awake()
    {
        VisUtil.Init(beamShader, emissiveShader, litShader);
        BuildRoom();
        BuildRig();
        SetupCamera();
        if (GetComponent<VisualizerMenu>() == null) gameObject.AddComponent<VisualizerMenu>();
    }

    void Update()
    {
        Shader.SetGlobalFloat("_Haze", haze);
        Shader.SetGlobalFloat("_FloorY", 0f);
        Shader.SetGlobalFloat("_CeilingY", ceiling);
    }

    void BuildRoom()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.02f, 0.02f, 0.025f);
        RenderSettings.skybox = null;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) l.gameObject.SetActive(false);

        var room = new GameObject("Room").transform;
        backZ = -roomDepth * 0.5f;
        float w = roomWidth, d = roomDepth;
        VisUtil.Prim(PrimitiveType.Cube, room, new Vector3(0, -0.01f, 0), new Vector3(w, 0.02f, d), VisUtil.FloorMat);
        VisUtil.Prim(PrimitiveType.Cube, room, new Vector3(0, ceiling, 0), new Vector3(w, 0.02f, d), VisUtil.BodyMat);
        VisUtil.Prim(PrimitiveType.Cube, room, new Vector3(0, ceiling * 0.5f, backZ), new Vector3(w, ceiling, 0.1f), VisUtil.WallMat);
        VisUtil.Prim(PrimitiveType.Cube, room, new Vector3(0, ceiling * 0.5f, -backZ), new Vector3(w, ceiling, 0.1f), VisUtil.WallMat);
        VisUtil.Prim(PrimitiveType.Cube, room, new Vector3(-w * 0.5f, ceiling * 0.5f, 0), new Vector3(0.1f, ceiling, d), VisUtil.WallMat);
        VisUtil.Prim(PrimitiveType.Cube, room, new Vector3(w * 0.5f, ceiling * 0.5f, 0), new Vector3(0.1f, ceiling, d), VisUtil.WallMat);

        // Nenápadný křížek ve středu parketu (cíl kalibrace hlav)
        var fc = danceFloorCenter + new Vector3(0, 0.002f, 0);
        var markMat = VisUtil.LitMat(new Color(0.25f, 0.25f, 0.25f));
        VisUtil.Prim(PrimitiveType.Cube, room, fc, new Vector3(0.4f, 0.002f, 0.03f), markMat);
        VisUtil.Prim(PrimitiveType.Cube, room, fc, new Vector3(0.03f, 0.002f, 0.4f), markMat);

        // DJ stolek
        VisUtil.Prim(PrimitiveType.Cube, room, new Vector3(0, 0.45f, backZ + 2f), new Vector3(1.8f, 0.9f, 0.7f), VisUtil.LitMat(new Color(0.8f, 0.8f, 0.8f)));
    }

    void BuildRig()
    {
        var rig = new GameObject("Rig").transform;

        // Gigbar na stativu za DJ stolkem
        float gz = backZ + 1.3f;
        gigbarTripod = Tripod(rig, new Vector3(0, 0, gz), gigbarHeight);
        gigbar = new GameObject("GigBAR Move + ILS").AddComponent<GigBar>();
        gigbar.transform.SetParent(rig, false);
        gigbar.transform.localPosition = new Vector3(0, gigbarHeight, gz);
        gigbar.Build();

        // 2× ADJ Pocket Pro na předních rozích DJ stolku (stolek: střed backZ+2, 1,8 × 0,9 × 0,7 m)
        float tableFront = backZ + 2f + 0.35f;
        float[] pxs = { -0.78f, 0.78f };
        for (int i = 0; i < 2; i++)
        {
            var go = new GameObject(i == 0 ? "Pocket Pro L" : "Pocket Pro R");
            go.transform.SetParent(rig, false);
            go.transform.localPosition = new Vector3(pxs[i], 0.9f, tableFront - 0.1f);
            var h = go.AddComponent<MovingHead>();
            h.beamAngle = 15f;          // ADJ Pocket Pro: 25 W, 15°
            h.tiltRange = 230f;
            h.lightIntensity = 130f;
            h.beamBrightness = 1.5f;
            h.beamLength = 10f;
            h.Build();
            pockets[i] = h;
        }

        // 4 tuby po stranách stolku, svisle na nízkých stojánkách
        float[] tx = { -2.6f, -1.6f, 1.6f, 2.6f };
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject("Tube " + (i + 1));
            go.transform.SetParent(rig, false);
            go.transform.localPosition = new Vector3(tx[i], 0.25f, backZ + 1.6f);
            VisUtil.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0, -0.125f, 0), new Vector3(0.03f, 0.125f, 0.03f), VisUtil.BodyMat);
            var t = go.AddComponent<PixelTube>();
            t.segments = Mathf.Max(16, tubeSegments); // jemnější dělení kvůli efektům tuby (40ch se roztáhne)
            t.Build();
            tubes[i] = t;
        }

        // 4 battery pary jako uplight na zadní stěnu
        float[] ux = { -5.5f, -3.5f, 3.5f, 5.5f };
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject("BatteryPar " + (i + 1));
            go.transform.SetParent(rig, false);
            go.transform.localPosition = new Vector3(ux[i], 0.12f, backZ + 0.35f);
            go.transform.localEulerAngles = new Vector3(-100f, 0, 0); // nahoru, mírně ke stěně
            var p = go.AddComponent<ParLight>();
            p.housing = ParLight.Housing.Box;
            p.beamAngle = 25f;   // odhad, uprav podle manuálu
            p.fieldAngle = 40f;
            p.beamLength = 5f;
            p.size = 0.1f;
            p.Build();
            uplights[i] = p;
        }
    }

    Transform Tripod(Transform parent, Vector3 pos, float h)
    {
        var t = new GameObject("Tripod").transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        VisUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(0, h * 0.5f, 0), new Vector3(0.037f, h * 0.5f, 0.037f), VisUtil.BodyMat);
        for (int i = 0; i < 3; i++)
        {
            float a = i * 120f;
            var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
            var leg = VisUtil.Prim(PrimitiveType.Cylinder, t, dir * 0.3f + Vector3.up * 0.35f, new Vector3(0.025f, 0.42f, 0.025f), VisUtil.BodyMat);
            leg.localRotation = Quaternion.FromToRotation(Vector3.up, (Vector3.up * 0.7f - dir * 0.6f).normalized);
        }
        return t;
    }

    // Zapnutí / vypnutí světla ve scéně (index = pořadí světla daného typu)
    public void SetVisible(FixtureType type, int index, bool visible)
    {
        GameObject go = null;
        switch (type)
        {
            case FixtureType.GigBarMoveILS:
                if (index == 0 && gigbar != null)
                {
                    gigbar.gameObject.SetActive(visible);
                    if (gigbarTripod != null) gigbarTripod.gameObject.SetActive(visible);
                }
                return;
            case FixtureType.PocketPro: if (index < pockets.Length && pockets[index] != null) go = pockets[index].gameObject; break;
            case FixtureType.BatteryPar: if (index < uplights.Length && uplights[index] != null) go = uplights[index].gameObject; break;
            case FixtureType.PixelTube: if (index < tubes.Length && tubes[index] != null) go = tubes[index].gameObject; break;
        }
        if (go != null) go.SetActive(visible);
    }

    void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null) cam = new GameObject("Main Camera").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.allowHDR = true;
        if (VRRig.IsVR)
        {
            // Quest: bez HDR a post-processingu (bloom je na mobilním GPU drahý), hlava na parketu
            cam.allowHDR = false;
            var data = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (data != null) data.renderPostProcessing = false;
            var fly = cam.GetComponent<FlyCamera>();
            if (fly != null) Destroy(fly);
            VRRig.Create(cam, new Vector3(0, 0, 1.5f), 180f);
            return;
        }
        cam.transform.position = new Vector3(0, 1.7f, roomDepth * 0.5f - 1.5f);
        cam.transform.LookAt(new Vector3(0, 1.6f, backZ + 1.3f));
        if (cam.GetComponent<FlyCamera>() == null) cam.gameObject.AddComponent<FlyCamera>();
    }
}
