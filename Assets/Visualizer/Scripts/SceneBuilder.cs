using UnityEngine;

// Postaví sál a celý rig při spuštění. Stačí dát na prázdný GameObject.
public class SceneBuilder : MonoBehaviour
{
    [Header("Shadery (přetáhni z Visualizer/Shaders)")]
    public Shader beamShader;
    public Shader emissiveShader;

    [Header("Atmosféra")]
    [Range(0, 3)] public float haze = 1f;

    [Header("Sál (m)")]
    public float roomWidth = 14f;
    public float roomDepth = 12f;
    public float ceiling = 4f;

    [Header("Rig")]
    public float gigbarHeight = 2.2f;
    public int tubeSegments = 8; // 40ch mód; 80ch = 16

    [HideInInspector] public GigBar gigbar;
    [HideInInspector] public PixelTube[] tubes = new PixelTube[4];
    [HideInInspector] public ParLight[] uplights = new ParLight[4];

    float backZ;

    void Awake()
    {
        VisUtil.Init(beamShader, emissiveShader);
        BuildRoom();
        BuildRig();
        SetupCamera();
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

        // DJ stolek
        VisUtil.Prim(PrimitiveType.Cube, room, new Vector3(0, 0.45f, backZ + 2f), new Vector3(1.8f, 0.9f, 0.7f), VisUtil.LitMat(new Color(0.8f, 0.8f, 0.8f)));
    }

    void BuildRig()
    {
        var rig = new GameObject("Rig").transform;

        // Gigbar na stativu za DJ stolkem
        float gz = backZ + 1.3f;
        Tripod(rig, new Vector3(0, 0, gz), gigbarHeight);
        gigbar = new GameObject("GigBAR Move + ILS").AddComponent<GigBar>();
        gigbar.transform.SetParent(rig, false);
        gigbar.transform.localPosition = new Vector3(0, gigbarHeight, gz);
        gigbar.Build();

        // 4 tuby po stranách stolku, svisle na nízkých stojánkách
        float[] tx = { -2.6f, -1.6f, 1.6f, 2.6f };
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject("Tube " + (i + 1));
            go.transform.SetParent(rig, false);
            go.transform.localPosition = new Vector3(tx[i], 0.25f, backZ + 1.6f);
            VisUtil.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0, -0.125f, 0), new Vector3(0.03f, 0.125f, 0.03f), VisUtil.BodyMat);
            var t = go.AddComponent<PixelTube>();
            t.segments = tubeSegments;
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

    void Tripod(Transform parent, Vector3 pos, float h)
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
    }

    void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null) cam = new GameObject("Main Camera").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.allowHDR = true;
        cam.transform.position = new Vector3(0, 1.7f, roomDepth * 0.5f - 1.5f);
        cam.transform.LookAt(new Vector3(0, 1.6f, backZ + 1.3f));
        if (cam.GetComponent<FlyCamera>() == null) cam.gameObject.AddComponent<FlyCamera>();
    }
}
