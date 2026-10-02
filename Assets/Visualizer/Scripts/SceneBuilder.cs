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
    // Kolegova rampa nad stolem
    [HideInInspector] public ParLight[] blackPars = new ParLight[2];
    [HideInInspector] public DerbyStrobe[] derbyStrobes = new DerbyStrobe[2];
    [HideInInspector] public Helix[] helixes = new Helix[2];
    Transform truss;
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

    // Světlo v sále (0–1): teplé stropní osvětlení, které na svatbách svítí, aby lidi viděli na stoly
    [Range(0, 1)] public float roomLight = 0f;
    readonly System.Collections.Generic.List<Light> houseLights = new System.Collections.Generic.List<Light>();
    static readonly Color warm = new Color(1f, 0.72f, 0.42f);   // ~2700 K
    static readonly Color darkAmbient = new Color(0.02f, 0.02f, 0.025f);
    float appliedRoomLight = -1f;

    void BuildHouseLights(Transform parent)
    {
        float h = ceiling - 0.3f;
        foreach (float x in new[] { -roomWidth * 0.3f, 0f, roomWidth * 0.3f })
            foreach (float z in new[] { -roomDepth * 0.15f, roomDepth * 0.25f })
            {
                var go = new GameObject("House light");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(x, h, z);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 9f;
                l.color = warm;
                l.shadows = LightShadows.None;
                l.intensity = 0f;
                l.enabled = false;
                houseLights.Add(l);
            }
    }

    void ApplyRoomLight()
    {
        if (Mathf.Approximately(appliedRoomLight, roomLight)) return;
        appliedRoomLight = roomLight;
        float v = roomLight * roomLight;   // posuvník vnímaně lineárně
        foreach (var l in houseLights)
        {
            l.enabled = v > 0.001f;
            l.intensity = v * 6f;
        }
        RenderSettings.ambientLight = darkAmbient + warm * (v * 0.12f);
    }

    void Update()
    {
        ApplyRoomLight();
        Shader.SetGlobalFloat("_Haze", haze);
        Shader.SetGlobalFloat("_FloorY", 0f);
        Shader.SetGlobalFloat("_CeilingY", ceilingOverride > 0f ? ceilingOverride : ceiling);
    }

    // ---- VR prostředí ----
    Transform shell;
    [HideInInspector] public float ceilingOverride = -1f;  // strop naskenované místnosti / passthrough
    public bool VirtualRoomVisible => shell != null && shell.gameObject.activeSelf;
    public void SetVirtualRoom(bool visible) { if (shell != null) shell.gameObject.SetActive(visible); }
    // Bod na podlaze pod předním okrajem DJ stolku – kotva pro „Umístit DJ stolek“
    public Vector3 DJTableAnchor => new Vector3(0, 0, backZ + 2.35f);

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
        // Plášť sálu (podlaha, strop, stěny) zvlášť – ve VR jde vypnout (passthrough / naskenovaná místnost)
        shell = new GameObject("Shell").transform;
        shell.SetParent(room, false);
        VisUtil.Prim(PrimitiveType.Cube, shell, new Vector3(0, -0.01f, 0), new Vector3(w, 0.02f, d), VisUtil.FloorMat);
        VisUtil.Prim(PrimitiveType.Cube, shell, new Vector3(0, ceiling, 0), new Vector3(w, 0.02f, d), VisUtil.BodyMat);
        VisUtil.Prim(PrimitiveType.Cube, shell, new Vector3(0, ceiling * 0.5f, backZ), new Vector3(w, ceiling, 0.1f), VisUtil.WallMat);
        VisUtil.Prim(PrimitiveType.Cube, shell, new Vector3(0, ceiling * 0.5f, -backZ), new Vector3(w, ceiling, 0.1f), VisUtil.WallMat);
        VisUtil.Prim(PrimitiveType.Cube, shell, new Vector3(-w * 0.5f, ceiling * 0.5f, 0), new Vector3(0.1f, ceiling, d), VisUtil.WallMat);
        VisUtil.Prim(PrimitiveType.Cube, shell, new Vector3(w * 0.5f, ceiling * 0.5f, 0), new Vector3(0.1f, ceiling, d), VisUtil.WallMat);
        BuildHouseLights(shell);   // patří k virtuálnímu sálu – v passthrough svítí skutečná místnost

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

        BuildColleagueTruss(rig);
    }

    // Kolegova rampa: dvě nohy u zadní hrany DJ stolku, na obou stranách kousek přesazené,
    // nahoře příčka. Derby nahoře na krajích (svítí nahoru), pary visí blíž ke středu, helix uprostřed.
    void BuildColleagueTruss(Transform rig)
    {
        const float h = 2.3f, legX = 1.1f, bar = 0.15f;
        float tz = backZ + 2f - 0.35f - 0.05f;   // těsně za zadní hranou stolku
        truss = new GameObject("Truss (kolega)").transform;
        truss.SetParent(rig, false);
        var m = VisUtil.LitMat(new Color(0.55f, 0.55f, 0.58f));   // hliníkový truss
        foreach (float x in new[] { -legX, legX })
        {
            VisUtil.Prim(PrimitiveType.Cube, truss, new Vector3(x, h * 0.5f, tz), new Vector3(bar, h, bar), m);
            VisUtil.Prim(PrimitiveType.Cube, truss, new Vector3(x, 0.005f, tz), new Vector3(0.5f, 0.01f, 0.5f), VisUtil.BodyMat);
        }
        VisUtil.Prim(PrimitiveType.Cube, truss, new Vector3(0, h - bar * 0.5f, tz), new Vector3(2 * legX + bar, bar, bar), m);

        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? -1f : 1f;
            // derby nahoře na krajích, míří dopředu na parket (lehce dolů)
            var dgo = new GameObject(i == 0 ? "DerbyStrobe L" : "DerbyStrobe R");
            dgo.transform.SetParent(truss, false);
            dgo.transform.localPosition = new Vector3(s * 0.95f, h + 0.07f, tz);
            dgo.transform.localEulerAngles = new Vector3(15f, 0, 0);
            var ds = dgo.AddComponent<DerbyStrobe>();
            ds.Build();
            if (i == 1) ds.derby.rotationSpeed = -ds.derby.rotationSpeed;
            derbyStrobes[i] = ds;

            // pary visí pod příčkou blíž ke středu, míří dolů před stolek
            var pgo = new GameObject(i == 0 ? "Black Par L" : "Black Par R");
            pgo.transform.SetParent(truss, false);
            pgo.transform.localPosition = new Vector3(s * 0.55f, h - bar - 0.13f, tz + 0.02f);
            pgo.transform.localEulerAngles = new Vector3(40f, 0, 0);
            VisUtil.Prim(PrimitiveType.Cube, truss, new Vector3(s * 0.55f, h - bar - 0.04f, tz), new Vector3(0.22f, 0.02f, 0.04f), VisUtil.BodyMat); // třmen
            var p = pgo.AddComponent<ParLight>();
            p.housing = ParLight.Housing.Round;
            p.size = 0.19f;
            p.beamAngle = 25f;
            p.fieldAngle = 40f;
            p.beamLength = 7f;
            p.lightIntensity = 30f;
            p.Build();
            blackPars[i] = p;

            // helix(y) uprostřed nahoře na příčce
            var hgo = new GameObject("Helix " + (i + 1));
            hgo.transform.SetParent(truss, false);
            hgo.transform.localPosition = new Vector3(i == 0 ? 0f : 0.35f, h, tz);
            var hx = hgo.AddComponent<Helix>();
            hx.Build();
            helixes[i] = hx;
        }
    }

    // Skrýt všechna světla – pak je VisualizerMenu podle patche zase zapne (co v patchi není, nesvítí ve scéně)
    public void HideAllFixtures()
    {
        if (gigbar != null) gigbar.gameObject.SetActive(false);
        if (gigbarTripod != null) gigbarTripod.gameObject.SetActive(false);
        foreach (var x in pockets) if (x != null) x.gameObject.SetActive(false);
        foreach (var x in uplights) if (x != null) x.gameObject.SetActive(false);
        foreach (var x in tubes) if (x != null) x.gameObject.SetActive(false);
        foreach (var x in blackPars) if (x != null) x.gameObject.SetActive(false);
        foreach (var x in derbyStrobes) if (x != null) x.gameObject.SetActive(false);
        foreach (var x in helixes) if (x != null) x.gameObject.SetActive(false);
    }

    // Rampa se ukáže, jen když na ní něco je; jeden helix stojí přesně uprostřed, dva vedle sebe
    public void FinishVisibility()
    {
        if (truss == null) return;
        bool any = false;
        foreach (var x in blackPars) any |= x != null && x.gameObject.activeSelf;
        foreach (var x in derbyStrobes) any |= x != null && x.gameObject.activeSelf;
        int hc = 0;
        foreach (var x in helixes) if (x != null && x.gameObject.activeSelf) hc++;
        any |= hc > 0;
        for (int i = 0; i < truss.childCount; i++)
        {
            var c = truss.GetChild(i);
            if (c.GetComponent<ParLight>() == null && c.GetComponent<DerbyStrobe>() == null && c.GetComponent<Helix>() == null)
                c.gameObject.SetActive(any);
        }
        if (helixes[0] != null) helixes[0].transform.localPosition = new Vector3(hc > 1 ? -0.25f : 0f, helixes[0].transform.localPosition.y, helixes[0].transform.localPosition.z);
        if (helixes[1] != null) helixes[1].transform.localPosition = new Vector3(0.25f, helixes[1].transform.localPosition.y, helixes[1].transform.localPosition.z);
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
            case FixtureType.BlackPar: if (index < blackPars.Length && blackPars[index] != null) go = blackPars[index].gameObject; break;
            case FixtureType.DerbyStrobe: if (index < derbyStrobes.Length && derbyStrobes[index] != null) go = derbyStrobes[index].gameObject; break;
            case FixtureType.DoubleHelix: if (index < helixes.Length && helixes[index] != null) go = helixes[index].gameObject; break;
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
