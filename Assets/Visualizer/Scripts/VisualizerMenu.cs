using System;
using System.IO;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Nastavení, která se ukládají vedle aplikace (persistentDataPath/settings.json).
[Serializable]
public class VisualizerSettings
{
    public int universe = 0;
    public int gigbarAddress = 200;
    public int[] parAddresses = { 110, 120, 130, 140 };
    public int[] tubeAddresses = { 300, 350, 400, 450 };
    public int hazeAddress = 100;
    public bool tube40ch = false;
    public float hazeBuildRate = 0.08f;
    public float hazeDecay = 0.01f;
    public int cameraPreset = 0;
    public bool fullscreen = false;

    public static string FilePath => Path.Combine(Application.persistentDataPath, "settings.json");

    public static VisualizerSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonUtility.FromJson<VisualizerSettings>(File.ReadAllText(FilePath));
                if (s != null) return s;
            }
        }
        catch (Exception e) { Debug.LogWarning("Nastavení nejde načíst: " + e.Message); }
        return new VisualizerSettings();
    }

    public void Save()
    {
        try { File.WriteAllText(FilePath, JsonUtility.ToJson(this, true)); }
        catch (Exception e) { Debug.LogWarning("Nastavení nejde uložit: " + e.Message); }
    }
}

// Jednoduché menu přes IMGUI. Tab/F1 = menu, F11 = celá obrazovka, 1-4 = pohledy kamery.
public class VisualizerMenu : MonoBehaviour
{
    SceneBuilder scene;
    DmxPatch patch;
    ArtNetReceiver artnet;
    VisualizerSettings s;

    bool show = true;
    Rect win = new Rect(16, 16, 360, 10);
    string sUni, sGig, sHaze;
    readonly string[] sPar = new string[4];
    readonly string[] sTube = new string[4];
    string msg;
    float msgTime;

    struct Cam { public string name; public Vector3 pos, look; }
    Cam[] cams;

    void Start()
    {
        scene = GetComponent<SceneBuilder>();
        patch = GetComponent<DmxPatch>();
        artnet = GetComponent<ArtNetReceiver>();
        s = VisualizerSettings.Load();

        Application.runInBackground = true;
        Application.targetFrameRate = 60;

        float back = -scene.roomDepth * 0.5f;
        float rig = back + 1.3f;
        cams = new[]
        {
            new Cam { name = "Host",   pos = new Vector3(0, 1.7f, -back - 1.5f), look = new Vector3(0, 1.6f, rig) },
            new Cam { name = "Parket", pos = new Vector3(1.2f, 1.7f, rig + 4f),   look = new Vector3(0, 2.0f, rig) },
            new Cam { name = "DJ",     pos = new Vector3(0, 1.75f, rig + 1.1f),   look = new Vector3(0, 1.0f, -back) },
            new Cam { name = "Shora",  pos = new Vector3(0, scene.ceiling - 0.3f, -back - 1f), look = new Vector3(0, 0.3f, rig + 2f) },
        };

        ApplyToPatch();
        RefreshStrings();
        SetCamera(s.cameraPreset);
        if (s.fullscreen) Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k == null) return;
        if (k.tabKey.wasPressedThisFrame || k.f1Key.wasPressedThisFrame) show = !show;
        if (k.f11Key.wasPressedThisFrame) ToggleFullscreen();
        if (k.digit1Key.wasPressedThisFrame) SetCamera(0);
        if (k.digit2Key.wasPressedThisFrame) SetCamera(1);
        if (k.digit3Key.wasPressedThisFrame) SetCamera(2);
        if (k.digit4Key.wasPressedThisFrame) SetCamera(3);
#else
        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.F1)) show = !show;
        if (Input.GetKeyDown(KeyCode.F11)) ToggleFullscreen();
        for (int i = 0; i < 4; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) SetCamera(i);
#endif
    }

    void ApplyToPatch()
    {
        if (patch == null) return;
        patch.gigbarUniverse = patch.parUniverse = patch.tubeUniverse = patch.hazeUniverse = s.universe;
        patch.gigbarAddress = s.gigbarAddress;
        patch.parAddresses = (int[])s.parAddresses.Clone();
        patch.tubeAddresses = (int[])s.tubeAddresses.Clone();
        patch.hazeAddress = s.hazeAddress;
        patch.tubeMode = s.tube40ch ? DmxPatch.TubeMode.Ch40_8Pixels : DmxPatch.TubeMode.Ch12_SoundSwitch;
        patch.hazeBuildRate = s.hazeBuildRate;
        patch.hazeDecay = s.hazeDecay;
    }

    void RefreshStrings()
    {
        sUni = (s.universe + 1).ToString(); // v UI jako SoundSwitch: Universe 1 = Art-Net 0
        sGig = s.gigbarAddress.ToString();
        sHaze = s.hazeAddress.ToString();
        for (int i = 0; i < 4; i++)
        {
            sPar[i] = i < s.parAddresses.Length ? s.parAddresses[i].ToString() : "0";
            sTube[i] = i < s.tubeAddresses.Length ? s.tubeAddresses[i].ToString() : "0";
        }
    }

    bool ReadStrings()
    {
        bool ok = true;
        ok &= Parse(sUni, 1, 16, v => s.universe = v - 1);
        ok &= Parse(sGig, 1, 512 - 51, v => s.gigbarAddress = v);
        ok &= Parse(sHaze, 1, 512, v => s.hazeAddress = v);
        var pa = new int[4];
        var ta = new int[4];
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            ok &= Parse(sPar[i], 1, 503, v => pa[idx] = v);
            ok &= Parse(sTube[i], 1, 512 - 11, v => ta[idx] = v);
        }
        s.parAddresses = pa;
        s.tubeAddresses = ta;
        return ok;
    }

    static bool Parse(string txt, int min, int max, Action<int> set)
    {
        if (int.TryParse(txt, out int v) && v >= min && v <= max) { set(v); return true; }
        return false;
    }

    void SetCamera(int i)
    {
        if (cams == null || i < 0 || i >= cams.Length) return;
        var cam = Camera.main;
        if (cam == null) return;
        cam.transform.position = cams[i].pos;
        cam.transform.LookAt(cams[i].look);
        var fly = cam.GetComponent<FlyCamera>();
        if (fly != null) fly.SyncAngles();
        s.cameraPreset = i;
    }

    void ToggleFullscreen()
    {
        bool fs = Screen.fullScreenMode != FullScreenMode.Windowed;
        Screen.fullScreenMode = fs ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
        s.fullscreen = !fs;
    }

    void Flash(string m) { msg = m; msgTime = Time.unscaledTime; }

    void OnGUI()
    {
        float scale = Mathf.Max(1f, Screen.height / 1080f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));

        // Stavový řádek dole
        bool live = artnet != null && artnet.HasData && (patch == null || !patch.forceDemo);
        string st = live
            ? "● LIVE  Art-Net od " + artnet.lastSender + "  (" + artnet.packetsReceived + " paketů)"
            : "○ Čekám na Art-Net – běží demo";
        GUI.Label(new Rect(10, Screen.height / scale - 26, 900, 22), st + "     Tab = menu");

        if (show) win = GUILayout.Window(1, win, DrawWindow, "DMX Visualiser");
    }

    void DrawWindow(int id)
    {
        bool live = artnet != null && artnet.HasData;
        GUILayout.Label(live ? "Art-Net: přijímám od " + artnet.lastSender : "Art-Net: žádná data (port " + (artnet != null ? artnet.port : 6454) + ")");
        if (patch != null) patch.forceDemo = GUILayout.Toggle(patch.forceDemo, " Vynutit demo (ignorovat Art-Net)");

        GUILayout.Space(6);
        GUILayout.Label("Pohled kamery (1–4):");
        GUILayout.BeginHorizontal();
        for (int i = 0; i < cams.Length; i++)
            if (GUILayout.Toggle(s.cameraPreset == i, cams[i].name, "Button")) if (s.cameraPreset != i) SetCamera(i);
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        if (live && patch != null && !patch.forceDemo)
        {
            GUILayout.Label("Haze v sále: " + Mathf.RoundToInt(patch.hazeDensity * 100) + " %  (řídí hazer z DMX)");
            patch.hazeDensity = GUILayout.HorizontalSlider(patch.hazeDensity, 0f, 1f);
        }
        else
        {
            GUILayout.Label("Haze: " + scene.haze.ToString("0.00"));
            scene.haze = GUILayout.HorizontalSlider(scene.haze, 0f, 3f);
        }

        GUILayout.Space(8);
        GUILayout.Label("DMX patch (adresy jako v SoundSwitchi):");
        Field("Universe", ref sUni);
        Field("GigBar Move ILS (52ch)", ref sGig);
        Field("Hazer (1ch)", ref sHaze);
        GUILayout.Label("Battery pary (10ch):");
        GUILayout.BeginHorizontal();
        for (int i = 0; i < 4; i++) sPar[i] = GUILayout.TextField(sPar[i], GUILayout.Width(70));
        GUILayout.EndHorizontal();
        GUILayout.Label("Pixel tuby:");
        GUILayout.BeginHorizontal();
        for (int i = 0; i < 4; i++) sTube[i] = GUILayout.TextField(sTube[i], GUILayout.Width(70));
        GUILayout.EndHorizontal();
        s.tube40ch = GUILayout.Toggle(s.tube40ch, " Tuby ve 40ch módu (8 pixelů), jinak 12ch");

        GUILayout.Space(8);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Použít a uložit"))
        {
            if (ReadStrings()) { ApplyToPatch(); s.Save(); Flash("Uloženo"); }
            else Flash("Chybná adresa – zkontroluj čísla");
        }
        if (GUILayout.Button(Screen.fullScreenMode == FullScreenMode.Windowed ? "Celá obrazovka" : "Okno")) ToggleFullscreen();
        if (GUILayout.Button("Ukončit"))
        {
            s.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        GUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(msg) && Time.unscaledTime - msgTime < 3f) GUILayout.Label(msg);
        GUILayout.Label("Pravé tl. myši + WASD = kamera, Q/E dolů/nahoru, Shift rychleji, F11 celá obrazovka");
        GUI.DragWindow();
    }

    static void Field(string label, ref string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(190));
        value = GUILayout.TextField(value, GUILayout.Width(80));
        GUILayout.EndHorizontal();
    }

    void OnApplicationQuit() { if (s != null) s.Save(); }
}
