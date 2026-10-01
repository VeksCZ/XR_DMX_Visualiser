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
    public int version = 3;
    public List<FixtureEntry> fixtures;
    public float hazeBuildRate = 0.08f;
    public float hazeDecay = 0.01f;
    public int cameraPreset = 0;
    public bool fullscreen = false;
    public int windowWidth = 1600;
    public int windowHeight = 900;
    public string language = "";      // "cs" / "en", prázdné = podle systému
    public bool showStatusBar = true;

    public static string FilePath => Path.Combine(Application.persistentDataPath, "settings.json");

    public static VisualizerSettings Load()
    {
        VisualizerSettings s = null;
        try
        {
            if (File.Exists(FilePath)) s = JsonUtility.FromJson<VisualizerSettings>(File.ReadAllText(FilePath));
        }
        catch (Exception e) { Debug.LogWarning("Settings load failed: " + e.Message); }
        if (s == null) s = new VisualizerSettings();
        if (s.fixtures == null || s.fixtures.Count == 0) s.fixtures = FixtureEntry.Defaults();
        if (string.IsNullOrEmpty(s.language))
            s.language = Application.systemLanguage == SystemLanguage.Czech || Application.systemLanguage == SystemLanguage.Slovak ? "cs" : "en";
        if (s.windowWidth < 640 || s.windowHeight < 360) { s.windowWidth = 1600; s.windowHeight = 900; }
        s.version = 3;
        return s;
    }

    public void Save()
    {
        try { File.WriteAllText(FilePath, JsonUtility.ToJson(this, true)); }
        catch (Exception e) { Debug.LogWarning("Settings save failed: " + e.Message); }
    }
}

// Rozhraní aplikace (IMGUI): horní lišta s menu, zavíratelná okna, stavová lišta dole.
public class VisualizerMenu : MonoBehaviour
{
    SceneBuilder scene;
    DmxPatch patch;
    ArtNetReceiver artnet;
    VisualizerSettings s;

    // ---- stav UI ----
    bool uiVisible = true;
    int openMenu = -1;                 // otevřené rozbalovací menu na liště
    Rect dropRect;
    readonly Rect[] menuLabelRects = new Rect[4];
    string msg;
    float msgTime;

    // Okna
    const int WinLights = 0, WinArtNet = 1, WinHaze = 2, WinControls = 3, WinAbout = 4;
    readonly bool[] winOpen = new bool[5];
    readonly Rect[] winRect = new Rect[5];
    Vector2 lightsScroll;

    // Editace patche
    readonly List<string> eName = new List<string>();
    readonly List<string> eUni = new List<string>();
    readonly List<string> eAddr = new List<string>();
    readonly List<bool> e40 = new List<bool>();

    // Kamera
    struct Cam { public string key; public Vector3 pos, look; }
    Cam[] cams;

    // Statistiky
    int lastPackets;
    float ppsTimer, pps, fps;

    // Styly
    float k = 1f;
    bool stylesReady;
    GUIStyle sBar, sBarItem, sDrop, sDropItem, sWin, sClose, sLabel, sHead, sDim, sWarn, sButton, sField, sToggle, sStatus;
    Texture2D tBar, tDrop, tWin, tHover, tOn, tStatus;

    float BarH => 26 * k;
    float StatusH => 24 * k;

    // ------------------------------------------------------------------

    void Start()
    {
        scene = GetComponent<SceneBuilder>();
        patch = GetComponent<DmxPatch>();
        artnet = GetComponent<ArtNetReceiver>();
        s = VisualizerSettings.Load();
        Loc.En = s.language == "en";

        Application.runInBackground = true;
        Application.targetFrameRate = 60;

        float back = -scene.roomDepth * 0.5f;
        float front = -back;
        float rig = back + 1.3f;
        cams = new[]
        {
            new Cam { key = "camHost",  pos = new Vector3(0, 1.7f, front - 1.5f),               look = new Vector3(0, 1.6f, rig) },
            new Cam { key = "camFloor", pos = new Vector3(1.8f, 1.7f, rig + 6.5f),              look = new Vector3(0, 1.6f, rig) },
            new Cam { key = "camDJ",    pos = new Vector3(0, 1.75f, rig + 1.1f),                look = new Vector3(0, 1.0f, front) },
            new Cam { key = "camTop",   pos = new Vector3(0, scene.ceiling - 0.15f, rig + 4.5f), look = new Vector3(0, 0f, rig + 1.2f) },
        };

        ApplyToPatch();
        LoadEditor();
        SetCamera(s.cameraPreset);
        if (s.fullscreen) GoFullscreen(); else if (!Application.isEditor) Screen.SetResolution(s.windowWidth, s.windowHeight, FullScreenMode.Windowed);
    }

    void Update()
    {
        // pkt/s a FPS
        fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.001f), 0.05f);
        ppsTimer += Time.unscaledDeltaTime;
        if (ppsTimer >= 1f && artnet != null)
        {
            pps = (artnet.packetsReceived - lastPackets) / ppsTimer;
            lastPackets = artnet.packetsReceived;
            ppsTimer = 0f;
        }

        // Při psaní do textového pole klávesové zkratky ignorujeme
        if (GUIUtility.keyboardControl != 0) return;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.hKey.wasPressedThisFrame) uiVisible = !uiVisible;
        if (kb.f11Key.wasPressedThisFrame) ToggleFullscreen();
        if (kb.escapeKey.wasPressedThisFrame) EscapePressed();
        if (kb.digit1Key.wasPressedThisFrame) SetCamera(0);
        if (kb.digit2Key.wasPressedThisFrame) SetCamera(1);
        if (kb.digit3Key.wasPressedThisFrame) SetCamera(2);
        if (kb.digit4Key.wasPressedThisFrame) SetCamera(3);
#else
        if (Input.GetKeyDown(KeyCode.H)) uiVisible = !uiVisible;
        if (Input.GetKeyDown(KeyCode.F11)) ToggleFullscreen();
        if (Input.GetKeyDown(KeyCode.Escape)) EscapePressed();
        for (int i = 0; i < 4; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) SetCamera(i);
#endif
    }

    void EscapePressed()
    {
        if (openMenu >= 0) { openMenu = -1; return; }
        for (int i = 0; i < winOpen.Length; i++) winOpen[i] = false;
    }

    // ------------------------------------------------------------------
    // Akce

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

    string StoreEditor()
    {
        for (int i = 0; i < s.fixtures.Count; i++)
        {
            var f = s.fixtures[i];
            int ch = FixtureEntry.Channels(f.type, e40[i]);
            string nm = string.IsNullOrWhiteSpace(eName[i]) ? FixtureEntry.TypeLabel(f.type) : eName[i].Trim();
            if (!int.TryParse(eUni[i], out int u) || u < 1 || u > 16) return Loc.F("errUniverse", nm);
            if (!int.TryParse(eAddr[i], out int a) || a < 1 || a + ch - 1 > 512) return Loc.F("errAddress", nm, 513 - ch);
            f.name = nm;
            f.universe = u;
            f.address = a;
            f.mode40ch = e40[i];
        }
        return null;
    }

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

    bool IsFullscreen => Screen.fullScreenMode != FullScreenMode.Windowed;

    // Fullscreen vždy v nativním rozlišení monitoru – jinak by obraz zůstal v rozlišení okna.
    void GoFullscreen()
    {
        if (!IsFullscreen) { s.windowWidth = Screen.width; s.windowHeight = Screen.height; }
        var d = Display.main;
        int w = d.systemWidth > 0 ? d.systemWidth : Screen.currentResolution.width;
        int h = d.systemHeight > 0 ? d.systemHeight : Screen.currentResolution.height;
        Screen.SetResolution(w, h, FullScreenMode.FullScreenWindow);
        s.fullscreen = true;
    }

    void GoWindowed()
    {
        Screen.SetResolution(s.windowWidth, s.windowHeight, FullScreenMode.Windowed);
        s.fullscreen = false;
    }

    void ToggleFullscreen()
    {
        if (IsFullscreen) GoWindowed(); else GoFullscreen();
        s.Save();
    }

    void SetLanguage(bool en)
    {
        Loc.En = en;
        s.language = en ? "en" : "cs";
        s.Save();
    }

    void OpenWindow(int w)
    {
        if (w == WinLights && !winOpen[w]) LoadEditor();
        winOpen[w] = true;
        if (winRect[w].width < 1)
        {
            float width = w == WinLights ? 470 : w == WinAbout || w == WinControls ? 440 : 400;
            winRect[w] = new Rect(16 * k + w * 24 * k, BarH + 12 * k + w * 24 * k, width * k, 10);
        }
        GUI.FocusWindow(10 + w);
    }

    void Flash(string m) { msg = m; msgTime = Time.unscaledTime; }

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

    // ------------------------------------------------------------------
    // Menu – definice položek

    struct Item
    {
        public string label, shortcut;
        public bool check, separator, header;
        public Action action;
    }

    static Item It(string label, Action a, string sc = null, bool check = false) => new Item { label = label, action = a, shortcut = sc, check = check };
    static Item Sep() => new Item { separator = true };
    static Item Head(string label) => new Item { label = label, header = true };

    string[] MenuTitles => new[] { Loc.T("file"), Loc.T("view"), Loc.T("settings"), Loc.T("help") };

    List<Item> MenuItems(int m)
    {
        var l = new List<Item>();
        switch (m)
        {
            case 0:
                l.Add(It(Loc.T("saveSettings"), () => { s.Save(); Flash(Loc.T("saved")); }));
                l.Add(It(Loc.T("resetPatch"), () => { s.fixtures = FixtureEntry.Defaults(); ApplyToPatch(); LoadEditor(); s.Save(); Flash(Loc.T("saved")); }));
                l.Add(Sep());
                l.Add(It(Loc.T("quit"), Quit, "Alt+F4"));
                break;
            case 1:
                l.Add(Head(Loc.T("camera")));
                for (int i = 0; i < cams.Length; i++) { int c = i; l.Add(It(Loc.T(cams[i].key), () => SetCamera(c), (i + 1).ToString(), s.cameraPreset == i)); }
                l.Add(Sep());
                l.Add(It(Loc.T("fullscreen"), ToggleFullscreen, "F11", IsFullscreen));
                l.Add(It(Loc.T("statusBar"), () => { s.showStatusBar = !s.showStatusBar; s.Save(); }, null, s.showStatusBar));
                l.Add(It(Loc.T("hideUI"), () => uiVisible = false, "H"));
                break;
            case 2:
                l.Add(It(Loc.T("lightsMenu"), () => OpenWindow(WinLights)));
                l.Add(It(Loc.T("artnetMenu"), () => OpenWindow(WinArtNet)));
                l.Add(It(Loc.T("hazeMenu"), () => OpenWindow(WinHaze)));
                l.Add(Sep());
                l.Add(Head(Loc.T("language")));
                l.Add(It("Čeština", () => SetLanguage(false), null, !Loc.En));
                l.Add(It("English", () => SetLanguage(true), null, Loc.En));
                break;
            case 3:
                l.Add(It(Loc.T("controlsMenu"), () => OpenWindow(WinControls)));
                l.Add(It(Loc.T("aboutMenu"), () => OpenWindow(WinAbout)));
                break;
        }
        return l;
    }

    // ------------------------------------------------------------------
    // Styly

    static Texture2D Tex(Color c)
    {
        var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    void EnsureStyles()
    {
        // Velikost UI podle DPI monitoru, ne podle velikosti okna – menu má pořád stejnou velikost.
        float nk = Screen.dpi > 0 ? Mathf.Clamp(Screen.dpi / 96f, 1f, 2f) : 1f;
        if (stylesReady && Mathf.Approximately(nk, k) && tBar != null) return;
        k = nk;
        stylesReady = true;

        tBar = Tex(new Color(0.11f, 0.11f, 0.13f, 1f));
        tDrop = Tex(new Color(0.15f, 0.15f, 0.18f, 1f));
        tWin = Tex(new Color(0.09f, 0.09f, 0.11f, 0.97f));
        tHover = Tex(new Color(0.22f, 0.42f, 0.70f, 1f));
        tOn = Tex(new Color(0.20f, 0.20f, 0.24f, 1f));
        tStatus = Tex(new Color(0.08f, 0.08f, 0.10f, 0.92f));

        var text = new Color(0.93f, 0.93f, 0.95f);
        var dim = new Color(0.62f, 0.64f, 0.70f);
        int fs = Mathf.RoundToInt(13 * k);

        sBar = new GUIStyle(); sBar.normal.background = tBar;
        sBarItem = new GUIStyle { fontSize = fs, alignment = TextAnchor.MiddleCenter, padding = new RectOffset((int)(10 * k), (int)(10 * k), 0, 0) };
        sBarItem.normal.textColor = text;
        sBarItem.hover.background = tOn; sBarItem.hover.textColor = Color.white;
        sBarItem.onNormal.background = tOn; sBarItem.onNormal.textColor = Color.white;
        sBarItem.onHover.background = tOn; sBarItem.onHover.textColor = Color.white;

        sDrop = new GUIStyle { padding = new RectOffset(0, 0, (int)(4 * k), (int)(4 * k)) };
        sDrop.normal.background = tDrop;
        sDropItem = new GUIStyle { fontSize = fs, alignment = TextAnchor.MiddleLeft, padding = new RectOffset((int)(10 * k), (int)(10 * k), 0, 0) };
        sDropItem.normal.textColor = text;
        sDropItem.hover.background = tHover; sDropItem.hover.textColor = Color.white;

        sWin = new GUIStyle { fontSize = fs + 1, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
        sWin.normal.background = sWin.onNormal.background = tWin;
        sWin.normal.textColor = sWin.onNormal.textColor = text;
        sWin.border = new RectOffset(2, 2, 2, 2);
        sWin.padding = new RectOffset((int)(12 * k), (int)(12 * k), (int)(34 * k), (int)(12 * k));
        sWin.contentOffset = new Vector2(12 * k, 8 * k);

        sClose = new GUIStyle { fontSize = Mathf.RoundToInt(16 * k), alignment = TextAnchor.MiddleCenter };
        sClose.normal.textColor = dim;
        sClose.hover.textColor = Color.white; sClose.hover.background = Tex(new Color(0.75f, 0.2f, 0.2f, 1f));

        sLabel = new GUIStyle(GUI.skin.label) { fontSize = fs, wordWrap = true };
        sLabel.normal.textColor = text;
        sHead = new GUIStyle(sLabel) { fontStyle = FontStyle.Bold };
        sDim = new GUIStyle(sLabel) { fontSize = Mathf.RoundToInt(12 * k) };
        sDim.normal.textColor = dim;
        sWarn = new GUIStyle(sDim);
        sWarn.normal.textColor = new Color(1f, 0.6f, 0.45f);
        sButton = new GUIStyle(GUI.skin.button) { fontSize = fs, fixedHeight = 26 * k };
        sField = new GUIStyle(GUI.skin.textField) { fontSize = fs, fixedHeight = 22 * k };
        sToggle = new GUIStyle(GUI.skin.toggle) { fontSize = fs };
        sToggle.normal.textColor = sToggle.onNormal.textColor = text;
        sToggle.hover.textColor = sToggle.onHover.textColor = Color.white;

        sStatus = new GUIStyle { fontSize = Mathf.RoundToInt(12 * k), alignment = TextAnchor.MiddleLeft, padding = new RectOffset((int)(10 * k), (int)(10 * k), 0, 0), richText = true };
        sStatus.normal.background = tStatus;
        sStatus.normal.textColor = new Color(0.82f, 0.83f, 0.87f);
    }

    // ------------------------------------------------------------------
    // Kreslení

    void OnGUI()
    {
        EnsureStyles();
        if (!uiVisible) return;
        var e = Event.current;

        // Klik mimo rozbalené menu ho zavře
        if (openMenu >= 0 && e.type == EventType.MouseDown && !dropRect.Contains(e.mousePosition) && !new Rect(0, 0, Screen.width, BarH).Contains(e.mousePosition))
            openMenu = -1;

        // Okna
        string[] titles = { Loc.T("lightsTitle"), Loc.T("artnetTitle"), Loc.T("hazeTitle"), Loc.T("controlsTitle"), Loc.T("aboutTitle") };
        for (int w = 0; w < winOpen.Length; w++)
        {
            if (!winOpen[w]) continue;
            winRect[w] = GUILayout.Window(10 + w, winRect[w], DrawWindow, titles[w], sWin);
            // držet okno v obrazovce
            winRect[w].x = Mathf.Clamp(winRect[w].x, 0, Screen.width - 60 * k);
            winRect[w].y = Mathf.Clamp(winRect[w].y, BarH, Screen.height - 40 * k);
        }

        DrawStatusBar();
        DrawMenuBar();

        // Rozbalené menu jako okno, aby bylo vždy nahoře
        if (openMenu >= 0)
        {
            var items = MenuItems(openMenu);
            float itemH = 24 * k, w = 260 * k, h = 8 * k;
            foreach (var it in items) h += it.separator ? 9 * k : itemH;
            dropRect = new Rect(menuLabelRects[openMenu].x, BarH, w, h);
            GUI.Window(99, dropRect, id => DrawDropdown(items, itemH, w), GUIContent.none, sDrop);
            GUI.BringWindowToFront(99);
        }

        if (!string.IsNullOrEmpty(msg) && Time.unscaledTime - msgTime < 3f)
        {
            var c = new GUIContent(msg);
            var sz = sStatus.CalcSize(c);
            GUI.Label(new Rect(Screen.width - sz.x - 16 * k, BarH + 8 * k, sz.x, 26 * k), c, sStatus);
        }
    }

    void DrawMenuBar()
    {
        var bar = new Rect(0, 0, Screen.width, BarH);
        GUI.Box(bar, GUIContent.none, sBar);
        float x = 4 * k;
        var titles = MenuTitles;
        for (int i = 0; i < titles.Length; i++)
        {
            var c = new GUIContent(titles[i]);
            float w = sBarItem.CalcSize(c).x;
            var r = new Rect(x, 0, w, BarH);
            menuLabelRects[i] = r;
            bool on = openMenu == i;
            if (GUI.Toggle(r, on, c, sBarItem) != on) openMenu = on ? -1 : i;
            // když je nějaké menu otevřené, najetí myší přepne na jiné
            if (openMenu >= 0 && openMenu != i && Event.current.type == EventType.Repaint && r.Contains(Event.current.mousePosition)) openMenu = i;
            x += w;
        }

        // vpravo název a stav
        var right = new GUIContent("DMX Visualiser");
        var rs = sDim.CalcSize(right);
        GUI.Label(new Rect(Screen.width - rs.x - 10 * k, (BarH - rs.y) * 0.5f, rs.x, rs.y), right, sDim);
    }

    void DrawDropdown(List<Item> items, float itemH, float w)
    {
        float y = 4 * k;
        foreach (var it in items)
        {
            if (it.separator)
            {
                GUI.DrawTexture(new Rect(8 * k, y + 4 * k, w - 16 * k, 1), tOn);
                y += 9 * k;
                continue;
            }
            var r = new Rect(0, y, w, itemH);
            if (it.header)
            {
                GUI.Label(new Rect(r.x + 10 * k, r.y + 3 * k, r.width, r.height), it.label, sDim);
            }
            else
            {
                string label = (it.check ? "✓  " : "     ") + it.label;
                if (GUI.Button(r, label, sDropItem))
                {
                    openMenu = -1;
                    it.action?.Invoke();
                }
                if (!string.IsNullOrEmpty(it.shortcut))
                {
                    var sc = new GUIContent(it.shortcut);
                    var ss = sDim.CalcSize(sc);
                    GUI.Label(new Rect(w - ss.x - 12 * k, y + (itemH - ss.y) * 0.5f, ss.x, ss.y), sc, sDim);
                }
            }
            y += itemH;
        }
    }

    void DrawStatusBar()
    {
        if (!s.showStatusBar) return;
        bool has = artnet != null && artnet.HasData;
        bool forced = patch != null && patch.forceDemo;
        string state = forced ? "<color=#f0b040>◐ " + Loc.T("forced") + "</color>"
                     : has ? "<color=#4be07a>● " + Loc.T("live") + "</color>"
                     : "<color=#9aa0aa>○ " + Loc.T("demo") + "</color>";

        var unis = new SortedSet<int>();
        if (patch != null) foreach (var f in patch.fixtures) unis.Add(f.universe);

        string src = has ? Loc.T("source") + ": " + artnet.lastSender + "  •  " + Mathf.RoundToInt(pps) + " pkt/s"
                         : Loc.T("source") + ": " + Loc.T("noData");
        string text = state
            + "    " + src
            + "    |    " + Loc.T("listen") + ": " + (artnet != null ? artnet.bindInfo : "-")
            + "    |    " + Loc.T("universe") + ": " + string.Join(", ", unis)
            + "    |    " + Loc.T("camera") + ": " + Loc.T(cams[Mathf.Clamp(s.cameraPreset, 0, cams.Length - 1)].key)
            + "    |    Haze " + Mathf.RoundToInt((patch != null && has && !forced ? patch.hazeDensity : scene.haze / 3f) * 100) + " %"
            + "    |    " + Mathf.RoundToInt(fps) + " FPS";
        GUI.Label(new Rect(0, Screen.height - StatusH, Screen.width, StatusH), text, sStatus);
    }

    void DrawWindow(int id)
    {
        int w = id - 10;
        // zavírací křížek
        if (GUI.Button(new Rect(winRect[w].width - 30 * k, 4 * k, 26 * k, 24 * k), "×", sClose)) winOpen[w] = false;

        switch (w)
        {
            case WinLights: DrawLights(); break;
            case WinArtNet: DrawArtNet(); break;
            case WinHaze: DrawHaze(); break;
            case WinControls: GUILayout.Label(Loc.T("controlsText"), sLabel); break;
            case WinAbout: GUILayout.Label(Loc.F("aboutText", Application.version), sLabel); break;
        }
        GUI.DragWindow(new Rect(0, 0, 10000, 30 * k));
    }

    void DrawArtNet()
    {
        bool has = artnet != null && artnet.HasData;
        if (artnet != null)
        {
            GUILayout.Label(has ? Loc.F("receivingFrom", artnet.lastSender) : Loc.T("noData"), sHead);
            GUILayout.Label(Loc.F("packetsInfo", artnet.packetsReceived, Mathf.RoundToInt(pps)), sLabel);
            GUILayout.Label(Loc.T("listen") + ": " + artnet.bindInfo, sLabel);
            GUILayout.Label(Loc.F("pollReplies", artnet.pollsAnswered), sLabel);
        }
        GUILayout.Space(8 * k);
        if (patch != null) patch.forceDemo = GUILayout.Toggle(patch.forceDemo, " " + Loc.T("forceDemo"), sToggle);
        GUILayout.Space(8 * k);
        GUILayout.Label(Loc.T("artnetHint"), sDim);
    }

    void DrawHaze()
    {
        bool live = artnet != null && artnet.HasData && patch != null && !patch.forceDemo;
        if (live)
        {
            GUILayout.Label(Loc.F("hazeLive", Mathf.RoundToInt(patch.hazeDensity * 100)), sHead);
            patch.hazeDensity = GUILayout.HorizontalSlider(patch.hazeDensity, 0f, 1f);
        }
        else
        {
            GUILayout.Label(Loc.F("hazeDemo", scene.haze.ToString("0.00")), sHead);
            scene.haze = GUILayout.HorizontalSlider(scene.haze, 0f, 3f);
        }
        GUILayout.Space(10 * k);
        GUILayout.Label(Loc.T("hazeHint"), sDim);
    }

    void DrawLights()
    {
        GUILayout.Label(Loc.T("patchHint"), sDim);
        GUILayout.Space(4 * k);

        lightsScroll = GUILayout.BeginScrollView(lightsScroll, GUILayout.Height(Mathf.Min(Screen.height * 0.55f, 480 * k)));
        for (int i = 0; i < s.fixtures.Count; i++)
        {
            var f = s.fixtures[i];
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            eName[i] = GUILayout.TextField(eName[i], sField, GUILayout.Width(180 * k));
            GUILayout.Label(FixtureEntry.TypeLabel(f.type), sDim);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("U", sLabel, GUILayout.Width(16 * k));
            eUni[i] = GUILayout.TextField(eUni[i], sField, GUILayout.Width(40 * k));
            GUILayout.Label(Loc.T("address"), sLabel, GUILayout.Width(62 * k));
            eAddr[i] = GUILayout.TextField(eAddr[i], sField, GUILayout.Width(60 * k));
            int ch = FixtureEntry.Channels(f.type, e40[i]);
            string range = int.TryParse(eAddr[i], out int a) ? a + "–" + (a + ch - 1) : "?";
            GUILayout.Label(ch + "ch  (" + range + ")", sDim);
            GUILayout.EndHorizontal();

            if (f.type == FixtureType.PixelTube)
                e40[i] = GUILayout.Toggle(e40[i], " " + Loc.T("mode40"), sToggle);

            string ov = Overlap(i);
            if (ov != null) GUILayout.Label(Loc.F("overlap", ov), sWarn);
            GUILayout.EndVertical();
        }
        GUILayout.EndScrollView();

        GUILayout.Space(8 * k);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(Loc.T("apply"), sButton))
        {
            string err = StoreEditor();
            if (err == null) { ApplyToPatch(); s.Save(); Flash(Loc.T("saved")); }
            else Flash(err);
        }
        if (GUILayout.Button(Loc.T("discard"), sButton)) { LoadEditor(); Flash(Loc.T("discarded")); }
        if (GUILayout.Button(Loc.T("defaults"), sButton)) { s.fixtures = FixtureEntry.Defaults(); LoadEditor(); Flash(Loc.T("defaultsLoaded")); }
        GUILayout.EndHorizontal();
    }
}
