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
    [Tooltip("Střed parketu – sem míří SS pozice Stage Center (kalibrace hlav)")]
    public Vector3 danceFloorCenter = Vector3.zero;

    // Světla a vybavení postavené podle sestavy (pořadí = pořadí v seznamu)
    [HideInInspector] public readonly System.Collections.Generic.List<FixtureInstance> instances = new System.Collections.Generic.List<FixtureInstance>();
    Transform rig;
    Transform truss;

    float backZ;

    void Awake()
    {
        VisUtil.Init(beamShader, emissiveShader, litShader);
        BuildRoom();
        rig = new GameObject("Rig").transform;
        BuildColleagueTruss(rig);
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
        // nasvícení mléčných difuzorů (tuby) světlem v sále – lineární hodnota, shader Visualizer/Emissive
        Shader.SetGlobalVector("_RoomLight", (Vector4)(warm.linear * (v * 1.2f)));
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
    }

    // ================= Sestava (světla a vybavení podle profilů) =================
    // ADJ Pro Event Table II / Vonyx DB3 Pro – rozměry ze profilů; rampa kolegy vychází z rozměrů DB3
    public static readonly Vector3 BoothSize = new Vector3(1.46f, 0.93f, 0.73f);
    float TableZ => backZ + 2f;   // střed stolu
    const float TrussH = 2.3f, TrussProf = 0.04f, TrussGap = 0.25f;
    float TrussZ => TableZ - BoothSize.z * 0.5f + 0.03f;

    // Postaví celou sestavu znovu (po změně seznamu, režimu nebo umístění)
    public void Rebuild(System.Collections.Generic.List<FixtureEntry> list)
    {
        foreach (var fi in instances) if (fi.root != null) Destroy(fi.root.gameObject);
        instances.Clear();
        var slotCount = new System.Collections.Generic.Dictionary<string, int>();
        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            var p = e != null ? e.Profile : null;
            var fi = new FixtureInstance { entry = e, profile = p, index = i };
            instances.Add(fi);
            if (p == null) continue;    // neznámý profil – nastavení ukáže varování
            fi.slot = string.IsNullOrEmpty(p.placement) ? "floor" : p.placement;
            slotCount.TryGetValue(fi.slot, out int n);
            fi.slotIndex = n;
            slotCount[fi.slot] = n + 1;

            fi.root = new GameObject(string.IsNullOrEmpty(e.name) ? p.name : e.name).transform;
            fi.root.SetParent(rig, false);
            fi.body = fi.root;
            if (p.IsProp) BuildProp(fi);
            else if (!p.IsHazer)
            {
                if (fi.slot == "stand")
                {
                    // světlo na stativu s trojnožkou (GigBar): umístění = pata stativu
                    float h = p.mountHeight > 0f ? p.mountHeight : 2.2f;
                    Tripod(fi.root, Vector3.zero, h);
                    fi.body = new GameObject("Body").transform;
                    fi.body.SetParent(fi.root, false);
                    fi.body.localPosition = new Vector3(0, h, 0);
                }
                FixtureFactory.Build(fi);
            }
            fi.root.gameObject.SetActive(!e.hidden);
        }
        Layout();
    }

    public void SetVisible(int index, bool visible)
    {
        if (index >= 0 && index < instances.Count && instances[index].root != null)
            instances[index].root.gameObject.SetActive(visible);
    }

    // Po změně viditelnosti: hlavy na rohy zobrazeného stolu, helixy na střed rampy, rampa jen když je na ní světlo
    public void FinishVisibility() => Layout();

    void Layout()
    {
        Vector3 table = new Vector3(1.8f, 0.9f, 0.7f);
        foreach (var fi in instances)
            if (fi.Visible && fi.profile != null && fi.profile.IsProp && fi.profile.prop.type == "table")
            { table = ProfileUtil.V(fi.profile.prop.size, table); break; }
        int centerCount = 0;
        foreach (var fi in instances) if (fi.Visible && fi.slot == "trussCenter" && !fi.entry.customPos) centerCount++;

        bool trussUsed = false;
        int ci = 0;
        foreach (var fi in instances)
        {
            if (fi.root == null) continue;
            if (fi.entry.customPos)
            {
                fi.root.localPosition = fi.entry.pos;
                fi.root.localEulerAngles = fi.entry.rot;
                continue;
            }
            if (fi.Visible && fi.slot.StartsWith("truss")) trussUsed = true;
            int k = fi.slot == "trussCenter" ? (fi.Visible ? ci++ : 0) : fi.slotIndex;
            Placement(fi.slot, k, table, centerCount, out Vector3 pos, out Vector3 rot);
            fi.root.localPosition = pos;
            fi.root.localEulerAngles = rot;
        }
        if (truss != null) truss.gameObject.SetActive(trussUsed);
    }

    // Výchozí místa: stůl – tuba – repro – tuba na obou stranách, uplighty u zadní stěny, kolegova rampa nad boothem
    void Placement(string slot, int i, Vector3 table, int centerCount, out Vector3 pos, out Vector3 rot)
    {
        rot = Vector3.zero;
        float s = i % 2 == 0 ? -1f : 1f;
        switch (slot)
        {
            case "stand":
                pos = new Vector3(i == 0 ? 0f : (i % 2 == 1 ? -1f : 1f) * 3.2f * ((i + 1) / 2), 0f, backZ + 1.3f);
                break;
            case "tableCorner":
                pos = new Vector3(s * (table.x * 0.5f - 0.1f - (i / 2) * 0.3f), table.y, TableZ + table.z * 0.5f - 0.1f);
                break;
            case "tube":
            {
                float[] xs = { -2.6f, -1.0f, 1.0f, 2.6f };
                pos = new Vector3(i < 4 ? xs[i] : s * (2.6f + 1.6f * ((i - 2) / 2)), 0.25f, backZ + 1.6f);
                break;
            }
            case "uplight":
            {
                float[] xs = { -5.5f, -3.5f, 3.5f, 5.5f, -1.5f, 1.5f, -4.5f, 4.5f, -2.5f, 2.5f, -6.5f, 6.5f };
                pos = new Vector3(xs[i % xs.Length], 0.21f, backZ + 0.35f);   // čočka nahoře, těleso stojí na zemi
                rot = new Vector3(-100f, 0, 0);                               // nahoru, mírně ke stěně
                break;
            }
            case "trussTop":
            {
                float[] xs = { -0.6f, 0.6f, -0.2f, 0.2f };
                pos = new Vector3(xs[i % 4], TrussH + 0.07f, TrussZ);       // derby nahoře na krajích, míří dopředu
                rot = new Vector3(15f, 0, 0);
                break;
            }
            case "trussHang":
            {
                float[] xs = { -0.42f, 0.42f, -0.15f, 0.15f };
                pos = new Vector3(xs[i % 4], TrussH - (TrussGap + TrussProf) - 0.13f, TrussZ + 0.02f);  // pary visí pod příčkou
                rot = new Vector3(40f, 0, 0);
                break;
            }
            case "trussCenter":
                pos = new Vector3((i - (Mathf.Max(1, centerCount) - 1) * 0.5f) * 0.5f, TrussH, TrussZ);   // 1 helix uprostřed, 2 vedle sebe
                break;
            case "table":
                pos = new Vector3(0, 0, TableZ);
                break;
            case "speaker":
                pos = new Vector3(s * (1.8f + 1.0f * (i / 2)), 0, backZ + 1.8f);
                rot = new Vector3(0, -s * 12f, 0);   // natočit mírně k parketu
                break;
            default:
                pos = new Vector3(-3f + 0.6f * i, 0, backZ + 2.8f);
                break;
        }
    }

    // Kolegova rampa (černá): dvě kulaté stativové tyče jako prodloužení zadních nohou DJ stolku,
    // nahoře dvě hranaté tyče nad sebou spojené 4 svislými výztuhami. Zobrazí se, jen když je na ní světlo.
    void BuildColleagueTruss(Transform parent)
    {
        float legX = BoothSize.x * 0.5f - 0.03f;
        float tz = TrussZ, h = TrussH, prof = TrussProf, gap = TrussGap;
        float barLow = h - gap;
        truss = new GameObject("Truss (kolega)").transform;
        truss.SetParent(parent, false);
        var m = VisUtil.BodyMat;
        foreach (float x in new[] { -legX, legX })
            VisUtil.Prim(PrimitiveType.Cylinder, truss, new Vector3(x, h * 0.5f, tz), new Vector3(0.035f, h * 0.5f, 0.035f), m); // stativová tyč
        float len = 2 * legX + 0.1f;
        VisUtil.Prim(PrimitiveType.Cube, truss, new Vector3(0, h - prof * 0.5f, tz), new Vector3(len, prof, prof), m);        // horní tyč
        VisUtil.Prim(PrimitiveType.Cube, truss, new Vector3(0, barLow - prof * 0.5f, tz), new Vector3(len, prof, prof), m);   // spodní tyč
        foreach (float x in new[] { -0.66f * legX, -0.22f * legX, 0.22f * legX, 0.66f * legX })                               // výztuhy
            VisUtil.Prim(PrimitiveType.Cube, truss, new Vector3(x, barLow + gap * 0.5f - prof * 0.5f, tz), new Vector3(0.025f, gap - prof, 0.025f), m);
        float bar = gap + prof;
        foreach (float x in new[] { -0.42f, 0.42f })                                                                         // třmeny parů
            VisUtil.Prim(PrimitiveType.Cube, truss, new Vector3(x, h - bar - 0.04f, tz), new Vector3(0.22f, 0.02f, 0.04f), m);
        truss.gameObject.SetActive(false);
    }

    // ---- Vybavení bez DMX ----
    void BuildProp(FixtureInstance fi)
    {
        var sp = fi.profile.prop;
        var size = ProfileUtil.V(sp.size, new Vector3(1.2f, 0.9f, 0.6f));
        if (sp.type == "speaker") Speaker(fi.root, size, sp.standHeight > 0f ? sp.standHeight : 1.5f, sp.lycra);
        else Table(fi.root, size, ProfileUtil.Mat(string.IsNullOrEmpty(sp.lycra) ? "#080809" : sp.lycra), ProfileUtil.Mat(sp.top));
    }

    // Stůl potažený lycrou: čelo a boky z látky (kousek nad zemí), nahoře deska, vzadu otevřené
    void Table(Transform t, Vector3 size, Material lycra, Material top)
    {
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
    }

    // Repro na trojnožce; s lycrou = látkový scrim přes stativ (repro nad ní zůstává vidět)
    void Speaker(Transform t, Vector3 size, float standH, string lycraColor)
    {
        Tripod(t, Vector3.zero, standH);
        var box = new GameObject("Box").transform;
        box.SetParent(t, false);
        box.localPosition = new Vector3(0, standH + size.y * 0.5f, 0);
        VisUtil.Prim(PrimitiveType.Cube, box, Vector3.zero, size, VisUtil.BodyMat);
        // kovová mřížka zepředu (lehce světlejší)
        VisUtil.Prim(PrimitiveType.Cube, box, new Vector3(0, -size.y * 0.06f, size.z * 0.5f + 0.003f), new Vector3(size.x * 0.9f, size.y * 0.82f, 0.006f), ProfileUtil.Mat("#1c1c1f"));
        if (string.IsNullOrEmpty(lycraColor)) return;
        // tři trojúhelníkové stěny od horní části tyče k patkám trojnožky
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
        go.AddComponent<MeshRenderer>().sharedMaterial = ProfileUtil.Mat(lycraColor);
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
