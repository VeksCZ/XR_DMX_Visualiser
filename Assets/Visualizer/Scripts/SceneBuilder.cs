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

    readonly System.Collections.Generic.List<Renderer> houseFixtures = new System.Collections.Generic.List<Renderer>();

    // Kulatá stropní svítidla (6×) – světlo jde shora dolů širokým kuželem, strop nemá světelné fleky
    void BuildHouseLights(Transform parent)
    {
        float h = ceiling - 0.04f;
        var rim = VisUtil.LitMat(new Color(0.35f, 0.33f, 0.3f));
        foreach (float x in new[] { -roomWidth * 0.3f, 0f, roomWidth * 0.3f })
            foreach (float z in new[] { -roomDepth * 0.15f, roomDepth * 0.25f })
            {
                // těleso + svítící difuzor
                VisUtil.Prim(PrimitiveType.Cylinder, parent, new Vector3(x, h, z), new Vector3(0.62f, 0.03f, 0.62f), rim);
                houseFixtures.Add(VisUtil.Emitter(PrimitiveType.Cylinder, parent, new Vector3(x, h - 0.035f, z), new Vector3(0.55f, 0.01f, 0.55f)));

                var go = new GameObject("House light");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(x, h - 0.08f, z);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // svítí dolů
                var l = go.AddComponent<Light>();
                l.type = LightType.Spot;
                l.spotAngle = 179f;       // skoro polokoule jako skutečné stropní svítidlo – osvítí i stěny až ke stropu
                l.innerSpotAngle = 150f;
                l.range = Mathf.Max(roomWidth, roomDepth) + 2f;
                l.color = warm;
                l.shadows = LightShadows.None;
                l.intensity = 0f;
                l.enabled = false;
                houseLights.Add(l);
            }

        // Odražené světlo (od podlahy, stolů, lidí): měkké bodové zdroje v půlce výšky sálu,
        // rozsvítí strop a horní části stěn bez ostrých fleků
        foreach (float x in new[] { -roomWidth * 0.25f, roomWidth * 0.25f })
            foreach (float z in new[] { -roomDepth * 0.2f, roomDepth * 0.2f })
            {
                var go = new GameObject("Bounce light");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(x, ceiling * 0.45f, z);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = Mathf.Max(roomWidth, roomDepth);
                l.color = warm;
                l.shadows = LightShadows.None;
                l.intensity = 0f;
                l.enabled = false;
                bounceLights.Add(l);
            }
    }
    readonly System.Collections.Generic.List<Light> bounceLights = new System.Collections.Generic.List<Light>();

    void ApplyRoomLight()
    {
        if (Mathf.Approximately(appliedRoomLight, roomLight)) return;
        appliedRoomLight = roomLight;
        float v = roomLight * roomLight;   // posuvník vnímaně lineárně
        foreach (var l in houseLights)
        {
            l.enabled = v > 0.001f;
            l.intensity = v * 90f;
        }
        foreach (var l in bounceLights)
        {
            l.enabled = v > 0.001f;
            l.intensity = v * 5f;
        }
        foreach (var f in houseFixtures) VisUtil.SetColor(f, warm, v * 8f);
        // rozptýlené světlo odražené od stěn a stropu, aby nebyly úplně černé kouty ani strop
        RenderSettings.ambientLight = darkAmbient + warm * (v * 0.6f);
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
        VisUtil.Prim(PrimitiveType.Cube, shell, new Vector3(0, ceiling, 0), new Vector3(w, 0.02f, d), VisUtil.LitMat(new Color(0.6f, 0.58f, 0.55f))); // světlá omítka
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

        BuildTables(room);
    }

    // ---- DJ stoly (bez DMX, zobrazují se podle seznamu) ----
    // ADJ Pro Event Table II: 127 × 61 cm, deska 115,5 cm, rám z trubek Ø32 mm, bílá lycra (scrim) dokola.
    // Vonyx DB3 Pro: 146 × 73 cm, deska ~93 cm, hliníkový rám s černou lycrou na čele a bocích.
    [HideInInspector] public Transform eventTable, djBooth;
    public static readonly Vector3 EventTableSize = new Vector3(1.27f, 1.155f, 0.61f);
    public static readonly Vector3 BoothSize = new Vector3(1.46f, 0.93f, 0.73f);
    float TableZ => backZ + 2f;   // střed stolu

    void BuildTables(Transform room)
    {
        var white = LycraWhite;   // stejná látka jako lycra na stativech repro
        var black = VisUtil.LitMat(new Color(0.03f, 0.03f, 0.035f));
        eventTable = Table(room, "ADJ Pro Event Table II", EventTableSize, white, VisUtil.BodyMat);
        djBooth = Table(room, "Vonyx DB3 Pro", BoothSize, black, VisUtil.BodyMat);
        eventTable.gameObject.SetActive(false);
        djBooth.gameObject.SetActive(false);
    }

    // Stůl potažený lycrou: čelo a boky z látky (kousek nad zemí), nahoře černá deska, vzadu otevřené
    Transform Table(Transform parent, string name, Vector3 size, Material lycra, Material top)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = new Vector3(0, 0, TableZ);
        float w = size.x, h = size.y, d = size.z, th = 0.01f, low = 0.02f;
        float ph = h - 0.03f - low;          // výška lycry
        float py = low + ph * 0.5f;
        VisUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, py, d * 0.5f), new Vector3(w, ph, th), lycra);             // čelo
        VisUtil.Prim(PrimitiveType.Cube, t, new Vector3(-w * 0.5f, py, 0), new Vector3(th, ph, d), lycra);            // boky
        VisUtil.Prim(PrimitiveType.Cube, t, new Vector3(w * 0.5f, py, 0), new Vector3(th, ph, d), lycra);
        VisUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, h - 0.015f, 0), new Vector3(w + 0.02f, 0.03f, d + 0.02f), top); // deska
        // zadní nohy rámu (jsou vidět z pohledu DJ)
        foreach (float x in new[] { -w * 0.5f + 0.03f, w * 0.5f - 0.03f })
            VisUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(x, h * 0.5f, -d * 0.5f + 0.03f), new Vector3(0.032f, h * 0.5f, 0.032f), VisUtil.BodyMat);
        return t;
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
        // rozestavení po stranách: stůl – tuba – repro – tuba (na obou stranách stejně)
        float[] tx = { -2.6f, -1.0f, 1.0f, 2.6f };
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject("Tube " + (i + 1));
            go.transform.SetParent(rig, false);
            go.transform.localPosition = new Vector3(tx[i], 0.25f, backZ + 1.6f);
            // černý stojánek s patkou a malou krabičkou s ovládáním a displejem pod tubou
            VisUtil.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0, -0.24f, 0), new Vector3(0.2f, 0.01f, 0.2f), VisUtil.BodyMat);
            VisUtil.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0, -0.17f, 0), new Vector3(0.03f, 0.07f, 0.03f), VisUtil.BodyMat);
            VisUtil.Prim(PrimitiveType.Cube, go.transform, new Vector3(0, -0.05f, 0), new Vector3(0.06f, 0.1f, 0.06f), VisUtil.BodyMat);
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
            go.transform.localPosition = new Vector3(ux[i], 0.21f, backZ + 0.35f);   // čočka nahoře, těleso stojí na zemi
            go.transform.localEulerAngles = new Vector3(-100f, 0, 0); // nahoru, mírně ke stěně
            var p = go.AddComponent<ParLight>();
            p.housing = ParLight.Housing.Box;
            p.boxDims = new Vector3(0.11f, 0.11f, 0.2f);   // čínský battery par: užší a vyšší těleso
            p.beamAngle = 25f;   // odhad, uprav podle manuálu
            p.fieldAngle = 40f;
            p.beamLength = 5f;
            p.size = 0.1f;
            p.Build();
            uplights[i] = p;
        }

        BuildColleagueTruss(rig);
        BuildSpeakers(rig);
    }

    // Kolegova rampa (černá): dvě kulaté stativové tyče jako prodloužení zadních nohou DJ stolku,
    // nahoře dvě hranaté tyče nad sebou spojené 4 svislými výztuhami.
    // Derby nahoře na krajích, pary visí pod spodní tyčí blíž ke středu, helixy uprostřed nahoře.

    void BuildColleagueTruss(Transform rig)
    {
        const float h = 2.3f, prof = 0.04f, gap = 0.25f;   // výška horní tyče, profil 4×4 cm, rozteč tyčí
        float legX = BoothSize.x * 0.5f - 0.03f;            // v zadních nohách Vonyx DB3
        float tz = TableZ - BoothSize.z * 0.5f + 0.03f;
        float barLow = h - gap;
        truss = new GameObject("Truss (kolega)").transform;
        truss.SetParent(rig, false);
        var m = VisUtil.BodyMat;
        foreach (float x in new[] { -legX, legX })
            VisUtil.Prim(PrimitiveType.Cylinder, truss, new Vector3(x, h * 0.5f, tz), new Vector3(0.035f, h * 0.5f, 0.035f), m); // stativová tyč
        float len = 2 * legX + 0.1f;
        VisUtil.Prim(PrimitiveType.Cube, truss, new Vector3(0, h - prof * 0.5f, tz), new Vector3(len, prof, prof), m);        // horní tyč
        VisUtil.Prim(PrimitiveType.Cube, truss, new Vector3(0, barLow - prof * 0.5f, tz), new Vector3(len, prof, prof), m);   // spodní tyč
        foreach (float x in new[] { -0.66f * legX, -0.22f * legX, 0.22f * legX, 0.66f * legX })                                                                  // výztuhy
            VisUtil.Prim(PrimitiveType.Cube, truss, new Vector3(x, barLow + gap * 0.5f - prof * 0.5f, tz), new Vector3(0.025f, gap - prof, 0.025f), m);
        const float bar = gap + prof;   // pary visí pod spodní tyčí

        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? -1f : 1f;
            // derby nahoře na krajích, míří dopředu na parket (lehce dolů)
            var dgo = new GameObject(i == 0 ? "DerbyStrobe L" : "DerbyStrobe R");
            dgo.transform.SetParent(truss, false);
            dgo.transform.localPosition = new Vector3(s * 0.6f, h + 0.07f, tz);
            dgo.transform.localEulerAngles = new Vector3(15f, 0, 0);
            var ds = dgo.AddComponent<DerbyStrobe>();
            ds.Build();
            if (i == 1) ds.derby.rotationSpeed = -ds.derby.rotationSpeed;
            derbyStrobes[i] = ds;

            // pary visí pod příčkou blíž ke středu, míří dolů před stolek
            var pgo = new GameObject(i == 0 ? "Black Par L" : "Black Par R");
            pgo.transform.SetParent(truss, false);
            pgo.transform.localPosition = new Vector3(s * 0.42f, h - bar - 0.13f, tz + 0.02f);
            pgo.transform.localEulerAngles = new Vector3(40f, 0, 0);
            VisUtil.Prim(PrimitiveType.Cube, truss, new Vector3(s * 0.42f, h - bar - 0.04f, tz), new Vector3(0.22f, 0.02f, 0.04f), VisUtil.BodyMat); // třmen
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
        if (eventTable != null) eventTable.gameObject.SetActive(false);
        if (djBooth != null) djBooth.gameObject.SetActive(false);
        foreach (var x in speakers12) if (x != null) x.gameObject.SetActive(false);
        foreach (var x in speakers14) if (x != null) x.gameObject.SetActive(false);
    }

    // Hlavy Pocket Pro na předních rozích zobrazeného stolu (Event Table / DB3), jinak výchozí místo
    void PlacePockets()
    {
        Vector3 size = new Vector3(1.8f, 0.9f, 0.7f);
        if (eventTable != null && eventTable.gameObject.activeSelf) size = EventTableSize;
        else if (djBooth != null && djBooth.gameObject.activeSelf) size = BoothSize;
        for (int i = 0; i < pockets.Length; i++)
        {
            if (pockets[i] == null) continue;
            float s = i == 0 ? -1f : 1f;
            pockets[i].transform.localPosition = new Vector3(s * (size.x * 0.5f - 0.1f), size.y, TableZ + size.z * 0.5f - 0.1f);
        }
    }

    // Rampa se ukáže, jen když na ní něco je; jeden helix stojí přesně uprostřed, dva vedle sebe
    public void FinishVisibility()
    {
        PlacePockets();
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

    // ---- Reproduktory ----
    // Moje: 2× FBT ProMaxX 12A na trojnožce s bílou lycrou (scrim přes stativ, repro nad ní zůstává vidět).
    // Kolega: 2× FBT ProMaxX 14A na černé trojnožce bez lycry. Rozměry přibližné (š × v × h).
    [HideInInspector] public Transform[] speakers12 = new Transform[2], speakers14 = new Transform[2];

    void BuildSpeakers(Transform rig)
    {
        float z = backZ + 1.8f, x = 1.8f;   // mezi tubami (±1,0 a ±2,6)
        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? -1f : 1f;
            speakers12[i] = Speaker(rig, new Vector3(s * x, 0, z), s, new Vector3(0.39f, 0.62f, 0.35f), 1.55f, true);
            speakers14[i] = Speaker(rig, new Vector3(s * x, 0, z), s, new Vector3(0.43f, 0.70f, 0.40f), 1.5f, false);
            speakers12[i].gameObject.SetActive(false);
            speakers14[i].gameObject.SetActive(false);
        }
    }

    Transform Speaker(Transform parent, Vector3 pos, float side, Vector3 size, float standH, bool lycra)
    {
        var t = Tripod(parent, pos, standH);
        // natočit mírně k parketu
        t.localRotation = Quaternion.Euler(0, -side * 12f, 0);
        var box = new GameObject("FBT ProMaxX").transform;
        box.SetParent(t, false);
        box.localPosition = new Vector3(0, standH + size.y * 0.5f, 0);
        VisUtil.Prim(PrimitiveType.Cube, box, Vector3.zero, size, VisUtil.BodyMat);
        // kovová mřížka zepředu (lehce světlejší) a logo pás
        var grille = VisUtil.LitMat(new Color(0.11f, 0.11f, 0.12f));
        VisUtil.Prim(PrimitiveType.Cube, box, new Vector3(0, -size.y * 0.06f, size.z * 0.5f + 0.003f), new Vector3(size.x * 0.9f, size.y * 0.82f, 0.006f), grille);
        if (!lycra) return t;
        // Bílá lycra: tři trojúhelníkové stěny od horní části tyče k patkám trojnožky
        var go = new GameObject("Lycra");
        go.transform.SetParent(t, false);
        var top = new Vector3(0, standH - 0.03f, 0);
        var feet = new Vector3[3];
        for (int i = 0; i < 3; i++) feet[i] = Quaternion.Euler(0, i * 120f, 0) * Vector3.forward * 0.6f + Vector3.up * 0.01f;
        var v = new System.Collections.Generic.List<Vector3>();
        var tri = new System.Collections.Generic.List<int>();
        for (int i = 0; i < 3; i++)
        {
            var a = feet[i]; var b = feet[(i + 1) % 3];
            int k = v.Count;
            v.Add(top); v.Add(a); v.Add(b);       // vnější strana
            v.Add(top); v.Add(a); v.Add(b);       // vnitřní strana (vlastní vrcholy kvůli normálám)
            tri.AddRange(new[] { k, k + 2, k + 1, k + 3, k + 4, k + 5 });
        }
        var mesh = new Mesh { name = "Lycra" };
        mesh.SetVertices(v);
        mesh.SetTriangles(tri, 0);
        mesh.RecalculateNormals();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = LycraWhite;
        return t;
    }
    static Material lycraMat;
    static Material LycraWhite => lycraMat != null ? lycraMat : (lycraMat = VisUtil.LitMat(new Color(0.92f, 0.92f, 0.9f)));

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
            case FixtureType.EventTable: if (index == 0 && eventTable != null) go = eventTable.gameObject; break;
            case FixtureType.DJBooth: if (index == 0 && djBooth != null) go = djBooth.gameObject; break;
            case FixtureType.Speaker12: if (index < speakers12.Length && speakers12[index] != null) go = speakers12[index].gameObject; break;
            case FixtureType.Speaker14: if (index < speakers14.Length && speakers14[index] != null) go = speakers14[index].gameObject; break;
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
