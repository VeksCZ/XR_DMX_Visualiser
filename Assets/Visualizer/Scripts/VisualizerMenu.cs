using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Nastavení ukládané do persistentDataPath/settings.json (přežije restart i aktualizaci).
[Serializable]
public class VisualizerSettings
{
    public int version = 5;
    public List<FixtureEntry> fixtures;
    public float hazeBuildRate = 0.08f;
    public float hazeDecay = 0.01f;
    public int cameraPreset = 0;
    public bool fullscreen = false;
    public int windowWidth = 1600;
    public int windowHeight = 900;
    public string language = "";      // "cs" / "en", prázdné = podle systému
    public bool showStatusBar = true;
    public bool panelOpen = true;
    public bool panelMinimized = false;

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
        if (s.version < 4) s.panelOpen = true;
        if (s.version < 5 && !s.fixtures.Exists(f => f.type == FixtureType.PocketPro))
        {
            // v0.5.1: přibyly ADJ Pocket Pro – doplnit do uloženého patche za GigBar
            int at = s.fixtures.FindIndex(f => f.type == FixtureType.GigBarMoveILS) + 1;
            s.fixtures.Insert(at, new FixtureEntry(FixtureType.PocketPro, "Pocket Pro R", 46));
            s.fixtures.Insert(at, new FixtureEntry(FixtureType.PocketPro, "Pocket Pro L", 33));
        }
        s.version = 5;
        return s;
    }

    public void Save()
    {
        try { File.WriteAllText(FilePath, JsonUtility.ToJson(this, true)); }
        catch (Exception e) { Debug.LogWarning("Settings save failed: " + e.Message); }
    }
}

// Rozhraní aplikace (IMGUI): strohá horní lišta, ovládací panel, dialog Nastavení,
// nápověda, aktualizace a stavová lišta. Ve fullscreenu lišta zmizí, panel zůstane.
public class VisualizerMenu : MonoBehaviour
{
    SceneBuilder scene;
    DmxPatch patch;
    ArtNetReceiver artnet;
    Updater updater;
    VisualizerSettings s;

    bool uiVisible = true;
    int openMenu = -1;
    Rect dropRect;
    readonly Rect[] menuLabelRects = new Rect[3];
    string msg;
    float msgTime;

    // Okna
    const int WinPanel = 0, WinSettings = 1, WinControls = 2, WinUpdates = 3, WinAbout = 4;
    readonly bool[] winOpen = new bool[5];
    readonly Rect[] winRect = new Rect[5];

    // Dialog Nastavení – rozpracované hodnoty, potvrdí se OK / Použít
    int settingsTab;
    bool stEn, stStatus;
    List<FixtureEntry> stFixtures;
    readonly List<string> eName = new List<string>();
    readonly List<string> eUni = new List<string>();
    readonly List<string> eAddr = new List<string>();
    readonly List<bool> e40 = new List<bool>();
    readonly List<bool> eOn = new List<bool>();
    int selFixture;
    bool modeSelectOpen;
    Rect selectRect, selectPopupRect;
    string[] selectModes;
    int selectIndex, selectFor;
    Vector2 lightsScroll;
    string settingsError;

    struct Cam { public string key; public Vector3 pos, look; }
    Cam[] cams;

    int lastPackets;
    float ppsTimer, pps, fps;

    float k = 1f;
    bool stylesReady;
    GUIStyle sBar, sBarItem, sDrop, sDropItem, sWin, sWinTitle, sClose, sMin, sMini, sLabel, sHead, sDim, sWarn, sButton, sBig, sTab, sField, sToggle, sStatus, sListItem, sSelect;
    string[] winTitles = new string[5];
    Texture2D tBar, tDrop, tWin, tHover, tOn, tStatus, tTabOn, tCloseHover;

    bool IsFullscreen => Screen.fullScreenMode != FullScreenMode.Windowed;
    float BarH => IsFullscreen ? 0f : 26 * k;
    float StatusH => s.showStatusBar ? 24 * k : 0f;

    // ------------------------------------------------------------------

    void Start()
    {
        scene = GetComponent<SceneBuilder>();
        patch = GetComponent<DmxPatch>();
        artnet = GetComponent<ArtNetReceiver>();
        updater = GetComponent<Updater>();
        if (updater == null) updater = gameObject.AddComponent<Updater>();
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
        SetCamera(s.cameraPreset);
        winOpen[WinPanel] = s.panelOpen;
        if (s.fullscreen) GoFullscreen();
        else if (!Application.isEditor) Screen.SetResolution(s.windowWidth, s.windowHeight, FullScreenMode.Windowed);
    }

    void Update()
    {
        fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.001f), 0.05f);
        ppsTimer += Time.unscaledDeltaTime;
        if (ppsTimer >= 1f && artnet != null)
        {
            pps = (artnet.packetsReceived - lastPackets) / ppsTimer;
            lastPackets = artnet.packetsReceived;
            ppsTimer = 0f;
        }

        if (GUIUtility.keyboardControl != 0) return;   // píše se do textového pole
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
        if (winOpen[WinSettings]) ApplyVisibility();     // zavření bez uložení vrátí zatržítka
        bool closed = false;
        for (int i = 1; i < winOpen.Length; i++) if (winOpen[i]) { winOpen[i] = false; closed = true; }
        if (!closed && IsFullscreen) ToggleFullscreen();
    }

    // ------------------------------------------------------------------
    // Akce

    void ApplyToPatch()
    {
        if (patch == null) return;
        patch.fixtures = CloneList(s.fixtures);
        patch.hazeBuildRate = s.hazeBuildRate;
        patch.hazeDecay = s.hazeDecay;
        ApplyVisibility();
    }

    // Zatržítka ze seznamu světel → zapnout/vypnout objekty ve scéně
    void ApplyVisibility()
    {
        if (scene == null) return;
        var count = new Dictionary<FixtureType, int>();
        foreach (var f in s.fixtures)
        {
            count.TryGetValue(f.type, out int n);
            scene.SetVisible(f.type, n, !f.hidden);
            count[f.type] = n + 1;
        }
    }

    // Okamžitý náhled zatržítek v dialogu (Zrušit vrátí uložený stav)
    void PreviewVisibility()
    {
        if (scene == null || stFixtures == null) return;
        var count = new Dictionary<FixtureType, int>();
        for (int i = 0; i < stFixtures.Count; i++)
        {
            var t = stFixtures[i].type;
            count.TryGetValue(t, out int n);
            scene.SetVisible(t, n, eOn[i]);
            count[t] = n + 1;
        }
    }

    void CloseSettingsWithoutSaving()
    {
        winOpen[WinSettings] = false;
        ApplyVisibility();
    }

    static List<FixtureEntry> CloneList(List<FixtureEntry> src)
    {
        var l = new List<FixtureEntry>();
        foreach (var f in src)
            l.Add(new FixtureEntry { type = f.type, name = f.name, universe = f.universe, address = f.address, mode40ch = f.mode40ch, hidden = f.hidden });
        return l;
    }

    void OpenSettings()
    {
        stEn = Loc.En;
        stStatus = s.showStatusBar;
        stFixtures = CloneList(s.fixtures);
        LoadEditor();
        settingsError = null;
        OpenWindow(WinSettings);
    }

    void LoadEditor()
    {
        eName.Clear(); eUni.Clear(); eAddr.Clear(); e40.Clear(); eOn.Clear();
        foreach (var f in stFixtures)
        {
            eName.Add(f.name);
            eUni.Add(f.universe.ToString());
            eAddr.Add(f.address.ToString());
            e40.Add(f.mode40ch);
            eOn.Add(!f.hidden);
        }
        selFixture = Mathf.Clamp(selFixture, 0, Mathf.Max(0, stFixtures.Count - 1));
        modeSelectOpen = false;
    }

    // Zkontroluje a uloží vše z dialogu. Vrací true, když se povedlo.
    bool ApplySettings()
    {
        for (int i = 0; i < stFixtures.Count; i++)
        {
            var f = stFixtures[i];
            int ch = FixtureEntry.Channels(f.type, e40[i]);
            string nm = string.IsNullOrWhiteSpace(eName[i]) ? FixtureEntry.TypeLabel(f.type) : eName[i].Trim();
            if (!int.TryParse(eUni[i], out int u) || u < 1 || u > 16) { settingsError = Loc.F("errUniverse", nm); settingsTab = 1; selFixture = i; return false; }
            if (!int.TryParse(eAddr[i], out int a) || a < 1 || a + ch - 1 > 512) { settingsError = Loc.F("errAddress", nm, 513 - ch); settingsTab = 1; selFixture = i; return false; }
            f.name = nm; f.universe = u; f.address = a; f.mode40ch = e40[i]; f.hidden = !eOn[i];
        }
        s.fixtures = CloneList(stFixtures);
        s.showStatusBar = stStatus;
        Loc.En = stEn;
        s.language = stEn ? "en" : "cs";
        ApplyToPatch();
        s.Save();
        settingsError = null;
        Flash(Loc.T("saved"));
        return true;
    }

    string Overlap(int i)
    {
        if (!int.TryParse(eUni[i], out int u) || !int.TryParse(eAddr[i], out int a)) return null;
        int end = a + FixtureEntry.Channels(stFixtures[i].type, e40[i]) - 1;
        for (int j = 0; j < stFixtures.Count; j++)
        {
            if (j == i) continue;
            if (!int.TryParse(eUni[j], out int u2) || !int.TryParse(eAddr[j], out int a2) || u2 != u) continue;
            int end2 = a2 + FixtureEntry.Channels(stFixtures[j].type, e40[j]) - 1;
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

    // Fullscreen vždy v nativním rozlišení monitoru; ovládací panel zůstane otevřený.
    void GoFullscreen()
    {
        if (!IsFullscreen) { s.windowWidth = Screen.width; s.windowHeight = Screen.height; }
        var d = Display.main;
        int w = d.systemWidth > 0 ? d.systemWidth : Screen.currentResolution.width;
        int h = d.systemHeight > 0 ? d.systemHeight : Screen.currentResolution.height;
        Screen.SetResolution(w, h, FullScreenMode.FullScreenWindow);
        s.fullscreen = true;
        openMenu = -1;
        winOpen[WinPanel] = true;
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

    void TogglePanel()
    {
        if (winOpen[WinPanel] && s.panelMinimized) s.panelMinimized = false;   // sbalený → rozbalit
        else winOpen[WinPanel] = !winOpen[WinPanel];
        s.panelOpen = winOpen[WinPanel];
        s.Save();
    }

    void OpenWindow(int w)
    {
        winOpen[w] = true;
        if (winRect[w].width < 1)
        {
            float width = w == WinSettings ? 720 : w == WinPanel ? 340 : 440;
            float x = w == WinPanel ? 16 * k : 380 * k;
            winRect[w] = new Rect(x, 40 * k, width * k, 10);
        }
        if (w == WinUpdates && (updater.state == Updater.State.Idle || updater.state == Updater.State.Error)) updater.Check();
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
    // Menu

    struct Item { public string label, shortcut; public bool check, separator; public Action action; }
    static Item It(string label, Action a, string sc = null, bool check = false) => new Item { label = label, action = a, shortcut = sc, check = check };
    static Item Sep() => new Item { separator = true };

    string[] MenuTitles => new[] { Loc.T("file"), Loc.T("view"), Loc.T("help") };

    List<Item> MenuItems(int m)
    {
        var l = new List<Item>();
        switch (m)
        {
            case 0:
                l.Add(It(Loc.T("settingsMenu"), OpenSettings));
                l.Add(Sep());
                l.Add(It(Loc.T("quit"), Quit, "Alt+F4"));
                break;
            case 1:
                l.Add(It(Loc.T("panelMenu"), TogglePanel, null, winOpen[WinPanel]));
                l.Add(It(Loc.T("fullscreen"), ToggleFullscreen, "F11", IsFullscreen));
                break;
            case 2:
                l.Add(It(Loc.T("controlsMenu"), () => OpenWindow(WinControls)));
                l.Add(It(Loc.T("updatesMenu"), () => { OpenWindow(WinUpdates); updater.Check(); }));
                l.Add(Sep());
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
        float nk = Screen.dpi > 0 ? Mathf.Clamp(Screen.dpi / 96f, 1f, 2f) : 1f;
        if (stylesReady && Mathf.Approximately(nk, k) && tBar != null) return;
        k = nk;
        stylesReady = true;

        tBar = Tex(new Color(0.11f, 0.11f, 0.13f, 1f));
        tDrop = Tex(new Color(0.15f, 0.15f, 0.18f, 1f));
        tWin = Tex(new Color(0.09f, 0.09f, 0.11f, 0.97f));
        tHover = Tex(new Color(0.22f, 0.42f, 0.70f, 1f));
        tOn = Tex(new Color(0.22f, 0.22f, 0.26f, 1f));
        tStatus = Tex(new Color(0.08f, 0.08f, 0.10f, 0.92f));
        tTabOn = Tex(new Color(0.18f, 0.45f, 0.75f, 1f));
        tCloseHover = Tex(new Color(0.75f, 0.2f, 0.2f, 1f));

        var text = new Color(0.93f, 0.93f, 0.95f);
        var dim = new Color(0.64f, 0.66f, 0.72f);
        int fs = Mathf.RoundToInt(13 * k);

        sBar = new GUIStyle(); sBar.normal.background = tBar;
        sBarItem = new GUIStyle { fontSize = fs, alignment = TextAnchor.MiddleCenter, padding = new RectOffset((int)(10 * k), (int)(10 * k), 0, 0) };
        sBarItem.normal.textColor = text;
        sBarItem.hover.background = sBarItem.onNormal.background = sBarItem.onHover.background = tOn;
        sBarItem.hover.textColor = sBarItem.onNormal.textColor = sBarItem.onHover.textColor = Color.white;

        sDrop = new GUIStyle(); sDrop.normal.background = tDrop;
        sDropItem = new GUIStyle { fontSize = fs, alignment = TextAnchor.MiddleLeft, padding = new RectOffset((int)(10 * k), (int)(10 * k), 0, 0) };
        sDropItem.normal.textColor = text;
        sDropItem.hover.background = tHover; sDropItem.hover.textColor = Color.white;

        sWin = new GUIStyle { fontSize = fs + 1, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
        sWin.normal.background = sWin.onNormal.background = tWin;
        sWin.normal.textColor = sWin.onNormal.textColor = text;
        sWin.border = new RectOffset(2, 2, 2, 2);
        sWin.padding = new RectOffset((int)(12 * k), (int)(12 * k), (int)(42 * k), (int)(12 * k));
        sWinTitle = new GUIStyle { fontSize = fs + 1, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        sWinTitle.normal.textColor = text;

        sClose = new GUIStyle { fontSize = Mathf.RoundToInt(16 * k), alignment = TextAnchor.MiddleCenter };
        sClose.normal.textColor = dim;
        sClose.hover.textColor = Color.white; sClose.hover.background = tCloseHover;
        sMin = new GUIStyle(sClose);
        sMin.hover.background = tOn;
        sMini = new GUIStyle { fontSize = Mathf.RoundToInt(18 * k), alignment = TextAnchor.MiddleCenter };
        sMini.normal.background = tWin; sMini.normal.textColor = text;
        sMini.hover.background = tHover; sMini.hover.textColor = Color.white;

        sLabel = new GUIStyle(GUI.skin.label) { fontSize = fs, wordWrap = true };
        sLabel.normal.textColor = text;
        sHead = new GUIStyle(sLabel) { fontStyle = FontStyle.Bold };
        sDim = new GUIStyle(sLabel) { fontSize = Mathf.RoundToInt(12 * k) };
        sDim.normal.textColor = dim;
        sWarn = new GUIStyle(sDim);
        sWarn.normal.textColor = new Color(1f, 0.6f, 0.45f);
        sButton = new GUIStyle(GUI.skin.button) { fontSize = fs, fixedHeight = 26 * k };
        sBig = new GUIStyle(sButton) { fixedHeight = 32 * k, fontStyle = FontStyle.Bold };
        sTab = new GUIStyle(sButton);
        sTab.onNormal.background = sTab.onHover.background = sTab.onActive.background = tTabOn;
        sTab.onNormal.textColor = sTab.onHover.textColor = Color.white;
        sField = new GUIStyle(GUI.skin.textField) { fontSize = fs, fixedHeight = 22 * k };
        sToggle = new GUIStyle(GUI.skin.toggle) { fontSize = fs };
        sToggle.normal.textColor = sToggle.onNormal.textColor = text;
        sToggle.hover.textColor = sToggle.onHover.textColor = Color.white;

        sListItem = new GUIStyle(sDropItem) { fixedHeight = 24 * k };
        sListItem.onNormal.background = sListItem.onHover.background = sListItem.onActive.background = tTabOn;
        sListItem.onNormal.textColor = sListItem.onHover.textColor = Color.white;
        sSelect = new GUIStyle(GUI.skin.button) { fontSize = fs, alignment = TextAnchor.MiddleLeft, fixedHeight = 24 * k, padding = new RectOffset((int)(8 * k), (int)(8 * k), 0, 0) };

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

        if (openMenu >= 0 && e.type == EventType.MouseDown && !dropRect.Contains(e.mousePosition) && !new Rect(0, 0, Screen.width, BarH).Contains(e.mousePosition))
            openMenu = -1;

        string[] titles = { Loc.T("panelTitle"), Loc.T("settingsTitle"), Loc.T("controlsTitle"), Loc.T("updatesTitle"), Loc.T("aboutTitle") };
        winTitles = titles;
        for (int w = 0; w < winOpen.Length; w++)
        {
            if (!winOpen[w]) continue;
            if (winRect[w].width < 1) OpenWindow(w);
            if (w == WinPanel && s.panelMinimized)
            {
                // sbalený panel = jen malé tlačítko v rohu
                var mini = new Rect(8 * k, BarH + 8 * k, 34 * k, 30 * k);
                if (GUI.Button(mini, new GUIContent("≡", Loc.T("panelTitle")), sMini)) { s.panelMinimized = false; s.Save(); }
                continue;
            }
            winRect[w] = GUILayout.Window(10 + w, winRect[w], DrawWindow, GUIContent.none, sWin);
            winRect[w].x = Mathf.Clamp(winRect[w].x, 0, Screen.width - 60 * k);
            winRect[w].y = Mathf.Clamp(winRect[w].y, BarH, Mathf.Max(BarH, Screen.height - StatusH - 40 * k));
        }

        DrawStatusBar();
        if (!IsFullscreen) DrawMenuBar();

        if (openMenu >= 0 && !IsFullscreen)
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
        GUI.Box(new Rect(0, 0, Screen.width, BarH), GUIContent.none, sBar);
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
            if (openMenu >= 0 && openMenu != i && Event.current.type == EventType.Repaint && r.Contains(Event.current.mousePosition)) openMenu = i;
            x += w;
        }
        var right = new GUIContent("DMX Visualiser " + Application.version);
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
            if (GUI.Button(r, (it.check ? "✓  " : "     ") + it.label, sDropItem))
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
            y += itemH;
        }
    }

    void DrawStatusBar()
    {
        if (!s.showStatusBar) return;
        bool has = artnet != null && artnet.HasData;
        bool demo = patch != null && patch.forceDemo;
        string state = demo || !has ? "<color=#f0b040>◐ " + Loc.T("demo") + "</color>" : "<color=#4be07a>● " + Loc.T("live") + "</color>";

        var unis = new SortedSet<int>();
        if (patch != null) foreach (var f in patch.fixtures) unis.Add(f.universe);

        string src = has ? Loc.T("source") + ": " + artnet.lastSender + "  •  " + Mathf.RoundToInt(pps) + " pkt/s"
                         : Loc.T("source") + ": " + Loc.T("noData");
        string text = state
            + "    " + src
            + "    |    " + Loc.T("listen") + ": " + (artnet != null ? artnet.bindInfo : "-")
            + "    |    " + Loc.T("universe") + ": " + string.Join(", ", unis)
            + "    |    " + Loc.T("camera") + ": " + Loc.T(cams[Mathf.Clamp(s.cameraPreset, 0, cams.Length - 1)].key)
            + "    |    Haze " + HazePercent() + " %"
            + "    |    " + Mathf.RoundToInt(fps) + " FPS";
        GUI.Label(new Rect(0, Screen.height - StatusH, Screen.width, StatusH), text, sStatus);
    }

    bool LiveData => artnet != null && artnet.HasData && patch != null && !patch.forceDemo;
    int HazePercent() => Mathf.RoundToInt((LiveData ? patch.hazeDensity : scene.haze / 3f) * 100);

    void DrawWindow(int id)
    {
        int w = id - 10;
        // titulek kreslíme sami – vestavěný titulek okna se překrýval s obsahem
        GUI.Label(new Rect(12 * k, 6 * k, winRect[w].width - 50 * k, 24 * k), winTitles[w], sWinTitle);
        GUI.DrawTexture(new Rect(0, 32 * k, winRect[w].width, 1), tOn);
        if (GUI.Button(new Rect(winRect[w].width - 30 * k, 4 * k, 26 * k, 24 * k), "×", sClose))
        {
            winOpen[w] = false;
            if (w == WinPanel) { s.panelOpen = false; s.Save(); }
            if (w == WinSettings) ApplyVisibility();
        }
        if (w == WinPanel && GUI.Button(new Rect(winRect[w].width - 58 * k, 4 * k, 26 * k, 24 * k), "–", sMin))
        {
            s.panelMinimized = true;
            s.Save();
        }

        switch (w)
        {
            case WinPanel: DrawPanel(); break;
            case WinSettings: DrawSettings(); break;
            case WinControls: GUILayout.Label(Loc.T("controlsText"), sLabel); break;
            case WinUpdates: DrawUpdates(); break;
            case WinAbout: GUILayout.Label(Loc.F("aboutText", Application.version), sLabel); break;
        }
        GUI.DragWindow(new Rect(0, 0, 10000, 30 * k));
    }

    // ---- Ovládací panel ----
    void DrawPanel()
    {
        bool has = artnet != null && artnet.HasData;
        bool demo = patch != null && patch.forceDemo;
        string st = demo ? "<color=#f0b040>◐</color> " + Loc.T("demoManual")
                  : has ? "<color=#4be07a>●</color> " + Loc.F("liveFrom", artnet.lastSender)
                  : "<color=#f0b040>◐</color> " + Loc.T("demoRunning");
        var rich = new GUIStyle(sLabel) { richText = true };
        GUILayout.Label(st, rich);
        GUILayout.Space(8 * k);

        GUILayout.Label(Loc.T("camera") + "  (1–4)", sHead);
        GUILayout.BeginHorizontal();
        for (int i = 0; i < cams.Length; i++)
            if (GUILayout.Toggle(s.cameraPreset == i, Loc.T(cams[i].key), sTab) && s.cameraPreset != i) SetCamera(i);
        GUILayout.EndHorizontal();
        GUILayout.Space(10 * k);

        if (LiveData)
        {
            GUILayout.Label(Loc.F("hazeLive", HazePercent()), sHead);
            patch.hazeDensity = GUILayout.HorizontalSlider(patch.hazeDensity, 0f, 1f);
        }
        else
        {
            GUILayout.Label(Loc.F("hazeDemo", HazePercent()), sHead);
            scene.haze = GUILayout.HorizontalSlider(scene.haze, 0f, 3f);
        }
        GUILayout.Space(12 * k);

        if (patch != null)
        {
            if (GUILayout.Button(patch.forceDemo ? Loc.T("stopDemo") : Loc.T("startDemo"), sBig)) patch.forceDemo = !patch.forceDemo;
            if (!has && !patch.forceDemo) GUILayout.Label(Loc.T("demoHint"), sDim);
        }
        GUILayout.Space(6 * k);
        if (GUILayout.Button(IsFullscreen ? Loc.T("exitFullscreen") + "  (F11)" : Loc.T("fullscreen") + "  (F11)", sButton)) ToggleFullscreen();
    }

    // ---- Nastavení ----
    void DrawSettings()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(settingsTab == 0, Loc.T("tabGeneral"), sTab)) settingsTab = 0;
        if (GUILayout.Toggle(settingsTab == 1, Loc.T("tabLights"), sTab)) settingsTab = 1;
        if (GUILayout.Toggle(settingsTab == 2, Loc.T("tabArtNet"), sTab)) settingsTab = 2;
        GUILayout.EndHorizontal();
        GUILayout.Space(10 * k);

        // Klik mimo rozbalený select ho zavře
        var ev = Event.current;
        if (modeSelectOpen && ev.type == EventType.MouseDown && !selectPopupRect.Contains(ev.mousePosition) && !selectRect.Contains(ev.mousePosition))
            modeSelectOpen = false;

        // Obsah záložky má pevnou výšku – tlačítka jsou vždy na stejném místě vpravo dole
        GUILayout.BeginVertical(GUILayout.Height(470 * k));
        if (settingsTab == 0)
        {
            GUILayout.Label(Loc.T("language"), sHead);
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(!stEn, "Čeština", sTab)) stEn = false;
            if (GUILayout.Toggle(stEn, "English", sTab)) stEn = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(10 * k);
            stStatus = GUILayout.Toggle(stStatus, " " + Loc.T("showStatusBar"), sToggle);
        }
        else if (settingsTab == 1)
        {
            DrawLightsEditor();
        }
        else
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
            GUILayout.Label(Loc.T("artnetHint"), sDim);
        }

        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();

        GUILayout.Space(10 * k);
        GUILayout.BeginHorizontal();
        if (!string.IsNullOrEmpty(settingsError)) GUILayout.Label(settingsError, sWarn);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(Loc.T("ok"), sButton, GUILayout.Width(90 * k))) { if (ApplySettings()) winOpen[WinSettings] = false; }
        if (GUILayout.Button(Loc.T("apply"), sButton, GUILayout.Width(90 * k))) ApplySettings();
        if (GUILayout.Button(Loc.T("cancel"), sButton, GUILayout.Width(90 * k))) CloseSettingsWithoutSaving();
        GUILayout.EndHorizontal();

        if (settingsTab == 1 && modeSelectOpen) DrawModePopup();
    }

    // Rozbalený select režimu kanálů – plovoucí nabídka pod tlačítkem, nic neposouvá
    void DrawModePopup()
    {
        if (selectModes == null || selectFor < 0 || selectFor >= e40.Count) return;
        float ih = 24 * k;
        selectPopupRect = new Rect(selectRect.x, selectRect.yMax + 2 * k, selectRect.width, ih * selectModes.Length + 4 * k);
        GUI.DrawTexture(selectPopupRect, tDrop);
        for (int j = 0; j < selectModes.Length; j++)
        {
            var r = new Rect(selectPopupRect.x, selectPopupRect.y + 2 * k + j * ih, selectPopupRect.width, ih);
            if (GUI.Button(r, (j == selectIndex ? "✓  " : "     ") + selectModes[j], sDropItem))
            {
                e40[selectFor] = j == 1;
                modeSelectOpen = false;
            }
        }
    }

    // Světla: vlevo seznam se zatržítky (zobrazit ve scéně), vpravo nastavení vybraného světla
    void DrawLightsEditor()
    {
        GUILayout.Label(Loc.T("patchHint"), sDim);
        GUILayout.Space(6 * k);
        GUILayout.BeginHorizontal();

        // ---- seznam ----
        GUILayout.BeginVertical(GUILayout.Width(260 * k));
        lightsScroll = GUILayout.BeginScrollView(lightsScroll, GUI.skin.box, GUILayout.Height(Mathf.Min(Screen.height * 0.5f, 360 * k)));
        for (int i = 0; i < stFixtures.Count; i++)
        {
            GUILayout.BeginHorizontal();
            bool was = eOn[i];
            eOn[i] = GUILayout.Toggle(eOn[i], GUIContent.none, sToggle, GUILayout.Width(20 * k));
            if (eOn[i] != was) PreviewVisibility();
            string label = eName[i] + (Overlap(i) != null ? "   ⚠" : "");
            bool sel = selFixture == i;
            if (GUILayout.Toggle(sel, label, sListItem) && !sel)
            {
                selFixture = i;
                modeSelectOpen = false;
                GUIUtility.keyboardControl = 0;
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
        GUILayout.Space(4 * k);
        if (GUILayout.Button(Loc.T("resetLights"), sButton))
        {
            stFixtures = FixtureEntry.Defaults();
            LoadEditor();
            PreviewVisibility();
            Flash(Loc.T("resetLightsDone"));
        }
        GUILayout.EndVertical();

        GUILayout.Space(14 * k);

        // ---- detail ----
        GUILayout.BeginVertical();
        if (stFixtures.Count > 0)
        {
            int i = Mathf.Clamp(selFixture, 0, stFixtures.Count - 1);
            var f = stFixtures[i];
            GUILayout.Label(FixtureEntry.TypeLabel(f.type), sHead);
            bool wasOn = eOn[i];
            eOn[i] = GUILayout.Toggle(eOn[i], " " + Loc.T("showInScene"), sToggle);
            if (eOn[i] != wasOn) PreviewVisibility();
            GUILayout.Space(8 * k);

            float lw = 120 * k;
            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T("name"), sLabel, GUILayout.Width(lw));
            eName[i] = GUILayout.TextField(eName[i], sField);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T("universe"), sLabel, GUILayout.Width(lw));
            eUni[i] = GUILayout.TextField(eUni[i], sField, GUILayout.Width(60 * k));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T("address"), sLabel, GUILayout.Width(lw));
            eAddr[i] = GUILayout.TextField(eAddr[i], sField, GUILayout.Width(80 * k));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // select režimu kanálů – rozbalená nabídka se kreslí navrch (DrawModePopup), nic neposouvá
            var modes = FixtureEntry.Modes(f.type);
            int mi = Mathf.Clamp(e40[i] ? 1 : 0, 0, modes.Length - 1);
            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T("channelMode"), sLabel, GUILayout.Width(lw));
            if (GUILayout.Button(modes[mi] + "   ▾", sSelect)) modeSelectOpen = !modeSelectOpen;
            if (Event.current.type == EventType.Repaint) selectRect = GUILayoutUtility.GetLastRect();
            GUILayout.EndHorizontal();
            selectModes = modes;
            selectIndex = mi;
            selectFor = i;

            GUILayout.Space(8 * k);
            int ch = FixtureEntry.Channels(f.type, e40[i]);
            if (int.TryParse(eAddr[i], out int a))
                GUILayout.Label(Loc.F("channelsInfo", ch, a, a + ch - 1), sDim);
            string ov = Overlap(i);
            if (ov != null) GUILayout.Label(Loc.F("overlap", ov), sWarn);
        }
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    // ---- Aktualizace ----
    void DrawUpdates()
    {
        GUILayout.Label(Loc.F("currentVersion", Application.version), sLabel);
        GUILayout.Space(8 * k);
        switch (updater.state)
        {
            case Updater.State.Checking: GUILayout.Label(Loc.T("checking"), sHead); break;
            case Updater.State.UpToDate: GUILayout.Label(Loc.T("upToDate"), sHead); break;
            case Updater.State.Available:
                GUILayout.Label(Loc.F("newVersion", updater.latestVersion), sHead);
                GUILayout.Space(6 * k);
                if (GUILayout.Button(Loc.T("install"), sBig)) updater.DownloadAndInstall();
                if (GUILayout.Button(Loc.T("openWeb"), sButton)) updater.OpenReleasePage();
                break;
            case Updater.State.Downloading: GUILayout.Label(Loc.F("downloading", Mathf.RoundToInt(updater.progress * 100)), sHead); break;
            case Updater.State.Installing: GUILayout.Label(Loc.T("installing"), sHead); break;
            case Updater.State.Error:
                GUILayout.Label(Loc.F("updateError", updater.error), sWarn);
                if (GUILayout.Button(Loc.T("openWeb"), sButton)) updater.OpenReleasePage();
                break;
        }
        GUILayout.Space(8 * k);
        if (updater.state != Updater.State.Checking && updater.state != Updater.State.Downloading && updater.state != Updater.State.Installing)
            if (GUILayout.Button(Loc.T("checkNow"), sButton)) updater.Check();
    }
}
