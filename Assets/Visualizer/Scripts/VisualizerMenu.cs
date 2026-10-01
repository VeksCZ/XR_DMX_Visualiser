using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Nastavení ukládané do persistentDataPath/settings.json (přežije restart i nový build).
[Serializable]
public class VisualizerSettings
{
    public int version = 2;
    public List<FixtureEntry> fixtures;
    public float hazeBuildRate = 0.08f;
    public float hazeDecay = 0.01f;
    public int cameraPreset = 0;
    public bool fullscreen = false;

    public static string FilePath => Path.Combine(Application.persistentDataPath, "settings.json");

    public static VisualizerSettings Load()
    {
        VisualizerSettings s = null;
        try
        {
            if (File.Exists(FilePath)) s = JsonUtility.FromJson<VisualizerSettings>(File.ReadAllText(FilePath));
        }
        catch (Exception e) { Debug.LogWarning("Nastavení nejde načíst: " + e.Message); }
        if (s == null) s = new VisualizerSettings();
        if (s.version < 2 || s.fixtures == null || s.fixtures.Count == 0) { s.fixtures = FixtureEntry.Defaults(); s.version = 2; }
        return s;
    }

    public void Save()
    {
        try { File.WriteAllText(FilePath, JsonUtility.ToJson(this, true)); }
        catch (Exception e) { Debug.LogWarning("Nastavení nejde uložit: " + e.Message); }
    }
}

// Menu přes IMGUI. Tab/F1 = menu, F11 = celá obrazovka, 1-4 = pohledy kamery.
public class VisualizerMenu : MonoBehaviour
{
    SceneBuilder scene;
    DmxPatch patch;
    ArtNetReceiver artnet;
    VisualizerSettings s;

    bool show = true;
    int tab;
    Rect win = new Rect(16, 16, 440, 10);
    Vector2 scroll;
    string msg;
    float msgTime;

    // Editace patche – texty polí, dokud se nepoužijí
    readonly List<string> eName = new List<string>();
    readonly List<string> eUni = new List<string>();
    readonly List<string> eAddr = new List<string>();
    readonly List<bool> e40 = new List<bool>();

    struct Cam { public string name; public Vector3 pos, look; }
    Cam[] cams;

    // Styly
    float styleScale = -1f;
    GUIStyle sWin, sLabel, sHead, sDim, sWarn, sButton, sTab, sField, sToggle, sBar;
    Texture2D texWin, texBar, texTabOn;

    void Start()
    {
        scene = GetComponent<SceneBuilder>();
        patch = GetComponent<DmxPatch>();
        artnet = GetComponent<ArtNetReceiver>();
        s = VisualizerSettings.Load();

        Application.runInBackground = true;
        Application.targetFrameRate = 60;

        float back = -scene.roomDepth * 0.5f;   // zadní stěna
        float front = -back;                     // přední stěna
        float rig = back + 1.3f;                 // GigBar
        cams = new[]
        {
            new Cam { name = "Host",   pos = new Vector3(0, 1.7f, front - 1.5f),         look = new Vector3(0, 1.6f, rig) },
            new Cam { name = "Parket", pos = new Vector3(1.8f, 1.7f, rig + 6.5f),        look = new Vector3(0, 1.6f, rig) },
            new Cam { name = "DJ",     pos = new Vector3(0, 1.75f, rig + 1.1f),          look = new Vector3(0, 1.0f, front) },
            new Cam { name = "Shora",  pos = new Vector3(0, scene.ceiling - 0.15f, rig + 4.5f), look = new Vector3(0, 0f, rig + 1.2f) },
        };

        ApplyToPatch();
        LoadEditor();
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

    // ---------------- Patch ----------------

    void ApplyToPatch()
    {
        if (patch == null) return;
        patch.fixtures = new List<FixtureEntry>();
        foreach (var f in s.fixtures)
            patch.fixtures.Add(new FixtureEntry { type = f.type, name = f.name, universe = f.universe, address = f.address, mode40ch = f.mode40ch });
        patch.hazeBuildRate = s.hazeBuildRate;
        patch.hazeDecay = s.hazeDecay;
    }

    void LoadEditor()
    {
        eName.Clear(); eUni.Clear(); eAddr.Clear(); e40.Clear();
        foreach (var f in s.fixtures)
        {
            eName.Add(f.name);
            eUni.Add(f.universe.ToString());
            eAddr.Add(f.address.ToString());
            e40.Add(f.mode40ch);
        }
    }

    // Přenese texty z editoru do nastavení. Vrací chybu, nebo null.
    string StoreEditor()
    {
        for (int i = 0; i < s.fixtures.Count; i++)
        {
            var f = s.fixtures[i];
            int ch = FixtureEntry.Channels(f.type, e40[i]);
            if (!int.TryParse(eUni[i], out int u) || u < 1 || u > 16) return f.name + ": universe musí být 1–16";
            if (!int.TryParse(eAddr[i], out int a) || a < 1 || a + ch - 1 > 512) return f.name + ": adresa musí být 1–" + (513 - ch);
            f.name = string.IsNullOrWhiteSpace(eName[i]) ? FixtureEntry.TypeLabel(f.type) : eName[i].Trim();
            f.universe = u;
            f.address = a;
            f.mode40ch = e40[i];
        }
        return null;
    }

    // Překryvy adres v editoru (podle aktuálních textů)
    string Overlap(int i)
    {
        if (!int.TryParse(eUni[i], out int u) || !int.TryParse(eAddr[i], out int a)) return null;
        int end = a + FixtureEntry.Channels(s.fixtures[i].type, e40[i]) - 1;
        for (int j = 0; j < s.fixtures.Count; j++)
        {
            if (j == i) continue;
            if (!int.TryParse(eUni[j], out int u2) || !int.TryParse(eAddr[j], out int a2) || u2 != u) continue;
            int end2 = a2 + FixtureEntry.Channels(s.fixtures[j].type, e40[j]) - 1;
            if (a <= end2 && a2 <= end) return eName[j];
        }
        return null;
    }

    // ---------------- Kamera / okno ----------------

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
        s.Save();
    }

    void Flash(string m) { msg = m; msgTime = Time.unscaledTime; }

    // ---------------- GUI ----------------

    static Texture2D Tex(Color c)
    {
        var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    void BuildStyles(float k)
    {
        styleScale = k;
        int fs = Mathf.RoundToInt(14 * k);
        if (texWin == null) texWin = Tex(new Color(0.07f, 0.07f, 0.09f, 0.95f));
        if (texBar == null) texBar = Tex(new Color(0f, 0f, 0f, 0.6f));
        if (texTabOn == null) texTabOn = Tex(new Color(0.15f, 0.45f, 0.75f, 1f));
        var text = new Color(0.93f, 0.93f, 0.95f);

        sWin = new GUIStyle(GUI.skin.window) { fontSize = fs + 2, fontStyle = FontStyle.Bold };
        sWin.normal.background = sWin.onNormal.background = texWin;
        sWin.normal.textColor = sWin.onNormal.textColor = text;
        sWin.padding = new RectOffset((int)(12 * k), (int)(12 * k), (int)(30 * k), (int)(12 * k));

        sLabel = new GUIStyle(GUI.skin.label) { fontSize = fs, wordWrap = true };
        sLabel.normal.textColor = text;
        sHead = new GUIStyle(sLabel) { fontStyle = FontStyle.Bold };
        sDim = new GUIStyle(sLabel) { fontSize = Mathf.RoundToInt(12 * k) };
        sDim.normal.textColor = new Color(0.65f, 0.67f, 0.72f);
        sWarn = new GUIStyle(sDim);
        sWarn.normal.textColor = new Color(1f, 0.55f, 0.4f);

        sButton = new GUIStyle(GUI.skin.button) { fontSize = fs, fixedHeight = 28 * k };
        sTab = new GUIStyle(sButton);
        sTab.onNormal.background = sTab.onHover.background = sTab.onActive.background = texTabOn;
        sTab.onNormal.textColor = sTab.onHover.textColor = Color.white;
        sField = new GUIStyle(GUI.skin.textField) { fontSize = fs, fixedHeight = 24 * k };
        sToggle = new GUIStyle(GUI.skin.toggle) { fontSize = fs };
        sToggle.normal.textColor = sToggle.onNormal.textColor = text;
        sBar = new GUIStyle(sLabel) { padding = new RectOffset(8, 8, 4, 4), wordWrap = false };
        sBar.normal.background = texBar;

        win.width = 460 * k;
    }

    void OnGUI()
    {
        float k = Mathf.Clamp(Screen.height / 900f, 1f, 2.5f);
        if (sWin == null || !Mathf.Approximately(k, styleScale)) BuildStyles(k);

        bool live = artnet != null && artnet.HasData && (patch == null || !patch.forceDemo);
        string st = live
            ? "● LIVE  Art-Net od " + artnet.lastSender + "   (" + artnet.packetsReceived + " paketů)"
            : (patch != null && patch.forceDemo ? "◐ Vynucené demo" : "○ Čekám na Art-Net – běží demo");
        var content = new GUIContent(st + "      Tab = menu");
        var size = sBar.CalcSize(content);
        GUI.Label(new Rect(10, Screen.height - size.y - 10, size.x, size.y), content, sBar);

        if (show) win = GUILayout.Window(1, win, DrawWindow, "DMX Visualiser", sWin);
    }

    void DrawWindow(int id)
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(tab == 0, "Přehled", sTab)) tab = 0;
        if (GUILayout.Toggle(tab == 1, "Světla (" + s.fixtures.Count + ")", sTab)) { if (tab != 1) LoadEditor(); tab = 1; }
        GUILayout.EndHorizontal();
        GUILayout.Space(8 * styleScale);

        if (tab == 0) DrawOverview(); else DrawFixtures();

        if (!string.IsNullOrEmpty(msg) && Time.unscaledTime - msgTime < 4f) GUILayout.Label(msg, sHead);
        GUI.DragWindow(new Rect(0, 0, 10000, 30 * styleScale));
    }

    void DrawOverview()
    {
        bool live = artnet != null && artnet.HasData;
        GUILayout.Label("Art-Net", sHead);
        if (artnet != null)
        {
            GUILayout.Label(live ? "Přijímám od " + artnet.lastSender + "  •  " + artnet.packetsReceived + " paketů" : "Žádná data", sLabel);
            GUILayout.Label("Poslouchám na " + artnet.bindInfo + "  •  ArtPoll odpovědí: " + artnet.pollsAnswered, sDim);
        }
        if (patch != null) patch.forceDemo = GUILayout.Toggle(patch.forceDemo, " Vynutit demo (ignorovat Art-Net)", sToggle);

        GUILayout.Space(10 * styleScale);
        GUILayout.Label("Pohled kamery  (klávesy 1–4)", sHead);
        GUILayout.BeginHorizontal();
        for (int i = 0; i < cams.Length; i++)
            if (GUILayout.Toggle(s.cameraPreset == i, cams[i].name, sTab) && s.cameraPreset != i) SetCamera(i);
        GUILayout.EndHorizontal();

        GUILayout.Space(10 * styleScale);
        if (live && patch != null && !patch.forceDemo)
        {
            GUILayout.Label("Haze v sále: " + Mathf.RoundToInt(patch.hazeDensity * 100) + " %  (přibývá podle hazeru z DMX)", sHead);
            patch.hazeDensity = GUILayout.HorizontalSlider(patch.hazeDensity, 0f, 1f);
        }
        else
        {
            GUILayout.Label("Haze: " + scene.haze.ToString("0.00"), sHead);
            scene.haze = GUILayout.HorizontalSlider(scene.haze, 0f, 3f);
        }

        GUILayout.Space(12 * styleScale);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(Screen.fullScreenMode == FullScreenMode.Windowed ? "Celá obrazovka (F11)" : "Okno (F11)", sButton)) ToggleFullscreen();
        if (GUILayout.Button("Ukončit", sButton)) Quit();
        GUILayout.EndHorizontal();
        GUILayout.Space(6 * styleScale);
        GUILayout.Label("Kamera: pravé tlačítko myši + WASD, Q/E dolů/nahoru, Shift rychleji", sDim);
    }

    void DrawFixtures()
    {
        GUILayout.Label("Adresy jako v SoundSwitchi. N-té světlo daného typu v seznamu řídí N-té světlo ve scéně.", sDim);
        GUILayout.Space(4 * styleScale);

        float k = styleScale;
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Min(Screen.height * 0.6f, 520 * k)));
        for (int i = 0; i < s.fixtures.Count; i++)
        {
            var f = s.fixtures[i];
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            eName[i] = GUILayout.TextField(eName[i], sField, GUILayout.Width(170 * k));
            GUILayout.Label(FixtureEntry.TypeLabel(f.type), sDim);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("U", sLabel, GUILayout.Width(16 * k));
            eUni[i] = GUILayout.TextField(eUni[i], sField, GUILayout.Width(40 * k));
            GUILayout.Label("Adresa", sLabel, GUILayout.Width(58 * k));
            eAddr[i] = GUILayout.TextField(eAddr[i], sField, GUILayout.Width(60 * k));
            int ch = FixtureEntry.Channels(f.type, e40[i]);
            string range = int.TryParse(eAddr[i], out int a) ? a + "–" + (a + ch - 1) : "?";
            GUILayout.Label(ch + "ch  (" + range + ")", sDim);
            GUILayout.EndHorizontal();

            if (f.type == FixtureType.PixelTube)
                e40[i] = GUILayout.Toggle(e40[i], " 40ch mód (8 pixelů), jinak 12ch", sToggle);

            string ov = Overlap(i);
            if (ov != null) GUILayout.Label("⚠ Překrývá se s: " + ov, sWarn);
            GUILayout.EndVertical();
        }
        GUILayout.EndScrollView();

        GUILayout.Space(8 * k);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Použít a uložit", sButton))
        {
            string err = StoreEditor();
            if (err == null) { ApplyToPatch(); s.Save(); Flash("Uloženo"); }
            else Flash(err);
        }
        if (GUILayout.Button("Zahodit změny", sButton)) { LoadEditor(); Flash("Změny zahozeny"); }
        if (GUILayout.Button("Výchozí patch", sButton)) { s.fixtures = FixtureEntry.Defaults(); LoadEditor(); Flash("Načten výchozí patch – ulož tlačítkem Použít"); }
        GUILayout.EndHorizontal();
    }

    void Quit()
    {
        s.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnApplicationQuit() { if (s != null) s.Save(); }
}
