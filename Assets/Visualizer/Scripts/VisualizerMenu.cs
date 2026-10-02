using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Nastavení settings.json – na Windows ve složce s .exe (portable), jinde v persistentDataPath.
[Serializable]
public class VisualizerSettings
{
    public int version = 9;
    public List<FixtureEntry> fixtures;
    public float hazeBuildRate = 0.08f;
    public float hazeDecay = 0.01f;
    public int cameraPreset = 0;
    public bool fullscreen = false;
    public int windowWidth = 1600;
    public int windowHeight = 900;
    public string language = "";      // "cs" / "en", prázdné = podle systému
    public bool showStatusBar = true;
    public bool vrPassthrough = false;     // Quest: místo virtuálního sálu skutečné okolí z kamer
    public bool vrScannedRoom = false;     // Quest: místo virtuálního sálu naskenovaná místnost (room setup)
    public bool panelOpen = true;
    public bool panelMinimized = false;
    public float roomLight = 0f;           // teplé světlo v sále (0–1)
    public List<FixtureProfile> customProfiles = new List<FixtureProfile>();   // vlastní profily použité v sestavě (přenáší se s nastavením)

    // Windows: portable – settings.json leží ve složce s .exe (přenáší se se složkou, aktualizace ho nepřepíše).
    // Když do složky nejde zapisovat (např. Program Files), zůstává v persistentDataPath.
    static string filePath;
    public static string FilePath
    {
        get
        {
            if (filePath != null) return filePath;
            string legacy = Path.Combine(Application.persistentDataPath, "settings.json");
            filePath = legacy;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                string appDir = Path.GetDirectoryName(Application.dataPath);
                string portable = Path.Combine(appDir, "settings.json");
                if (!File.Exists(portable))
                {
                    if (File.Exists(legacy)) File.Copy(legacy, portable);       // převzít dosavadní nastavení
                    else { File.WriteAllText(portable + ".tmp", ""); File.Delete(portable + ".tmp"); } // test zápisu
                }
                filePath = portable;
            }
            catch (Exception e) { Debug.LogWarning("Portable settings unavailable: " + e.Message); }
#endif
            return filePath;
        }
    }

    public static VisualizerSettings Load()
    {
        VisualizerSettings s = null;
        try
        {
            if (File.Exists(FilePath)) s = JsonUtility.FromJson<VisualizerSettings>(File.ReadAllText(FilePath));
        }
        catch (Exception e) { Debug.LogWarning("Settings load failed: " + e.Message); }
        if (s == null) s = new VisualizerSettings();
        bool empty = s.fixtures == null || s.fixtures.Count == 0;
        if (empty) s.fixtures = RigPresets.Mine();
        if (string.IsNullOrEmpty(s.language))
            s.language = Application.systemLanguage == SystemLanguage.Czech || Application.systemLanguage == SystemLanguage.Slovak ? "cs" : "en";
        if (s.windowWidth < 640 || s.windowHeight < 360) { s.windowWidth = 1600; s.windowHeight = 900; }
        if (s.version < 4) s.panelOpen = true;
        if (!empty && s.version < 9) MigrateLegacy(s);
        foreach (var f in s.fixtures) f.Migrate();   // typ jako číslo → id profilu
        if (s.customProfiles == null) s.customProfiles = new List<FixtureProfile>();
        ProfileLibrary.Reload(s.customProfiles);
        s.version = 9;
        return s;
    }

    // Převody nastavení ze starších verzí (patch se tehdy ukládal jako typ světla)
    static void MigrateLegacy(VisualizerSettings s)
    {
        bool Old(FixtureEntry f) => string.IsNullOrEmpty(f.profile);
        if (s.version < 5 && !s.fixtures.Exists(f => Old(f) && f.type == FixtureType.PocketPro))
        {
            // v0.5.1: přibyly ADJ Pocket Pro – doplnit do uloženého patche za GigBar
            int at = s.fixtures.FindIndex(f => Old(f) && f.type == FixtureType.GigBarMoveILS) + 1;
            s.fixtures.Insert(at, new FixtureEntry { type = FixtureType.PocketPro, name = "Pocket Pro R", address = 46 });
            s.fixtures.Insert(at, new FixtureEntry { type = FixtureType.PocketPro, name = "Pocket Pro L", address = 33 });
        }
        if (s.version < 6)
        {
            // v0.5.1: hlavy GigBaru mají „dopředu“ kolem 1/3 rozsahu panu
            foreach (var f in s.fixtures) if (Old(f) && f.type == FixtureType.GigBarMoveILS && f.panOffset == 0f) f.panOffset = 90f;
        }
        if (s.version < 7)
        {
            // v0.5.1: kalibrace na SS „Stage Center“ = střed parketu (jen pokud ji uživatel neměnil)
            var defs = RigPresets.Mine();
            int pi = 0;
            foreach (var f in s.fixtures)
            {
                if (!Old(f)) continue;
                FixtureEntry def = null;
                if (f.type == FixtureType.GigBarMoveILS && f.panOffset == 90f) def = defs[0];
                else if (f.type == FixtureType.PocketPro) { if (pi < 2 && f.panOffset == 0f) def = defs[1 + pi]; pi++; }
                if (def == null || f.tiltOffset != 0f || f.invertPan || f.invertTilt) continue;
                f.panOffset = def.panOffset; f.tiltOffset = def.tiltOffset;
            }
        }
        if (s.version < 8 && !s.fixtures.Exists(f => Old(f) && f.type >= FixtureType.EventTable))
        {
            // v0.5.5: stůl / booth a repro jako položky seznamu – podle toho, čí sestava je uložená
            bool colleague = s.fixtures.Exists(f => Old(f) && (f.type == FixtureType.DerbyStrobe || f.type == FixtureType.BlackPar || f.type == FixtureType.DoubleHelix));
            s.fixtures.AddRange(colleague
                ? new List<FixtureEntry> { new FixtureEntry("vonyx-db3-pro", "DJ booth", 1), new FixtureEntry("fbt-promaxx-14a", "Repro L", 1), new FixtureEntry("fbt-promaxx-14a", "Repro R", 1) }
                : new List<FixtureEntry> { new FixtureEntry("adj-pro-event-table-2", "DJ stůl", 1), new FixtureEntry("fbt-promaxx-12a", "Repro L", 1), new FixtureEntry("fbt-promaxx-12a", "Repro R", 1) });
        }
    }

    // Do nastavení se ukládají profily, které nejsou vestavěné (nebo jsou upravené) – aby šly se sestavou do Questu a ke kolegovi
    public void EmbedProfiles() => customProfiles = UsedCustomProfiles(fixtures);

    public static List<FixtureProfile> UsedCustomProfiles(List<FixtureEntry> list)
    {
        var res = new List<FixtureProfile>();
        var seen = new HashSet<string>();
        if (list == null) return res;
        foreach (var f in list)
        {
            var p = f != null ? f.Profile : null;
            if (p == null || p.source == "builtin" || !seen.Add(p.id)) continue;
            res.Add(p);
        }
        return res;
    }

    public void Save()
    {
        EmbedProfiles();
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
    QuestTools quest;          // jen Windows: instalace a nastavení Quest aplikace přes ADB
    bool questDetected;
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
    readonly List<int> eMode = new List<int>();
    readonly List<bool> eCustom = new List<bool>();
    readonly List<string[]> ePos = new List<string[]>();   // x, y, z, rx, ry, rz
    readonly List<bool> eOn = new List<bool>();
    readonly List<string> eOffset = new List<string>();
    readonly List<string> eTiltOffset = new List<string>();
    readonly List<bool> eInvPan = new List<bool>();
    readonly List<bool> eInvTilt = new List<bool>();
    int selFixture;
    Rect selectPopupRect;
    string[] selectModes;
    int selectIndex, selectFor;
    Vector2 lightsScroll, detailScroll, pickScroll;
    bool rigDirty;                 // seznam v dialogu se změnil – náhled ve scéně postavit znovu
    int popup;                     // 0 = nic, 1 = režim kanálů, 2 = přidat světlo, 3 = import sestavy
    Rect popupAnchor;
    string rigName = "";
    string[] rigFiles;
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
#if UNITY_STANDALONE_WIN
        quest = GetComponent<QuestTools>();
        if (quest == null) quest = gameObject.AddComponent<QuestTools>();
#endif
        s = VisualizerSettings.Load();
        Loc.En = s.language == "en";
        scene.roomLight = s.roomLight;

        Application.runInBackground = true;
        Application.targetFrameRate = VRRig.IsVR ? -1 : 60;   // v brýlích určuje snímky headset (72–120 Hz)

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
        winOpen[WinPanel] = s.panelOpen;
        if (VRRig.IsVR) { uiVisible = false; return; }   // v brýlích se 2D menu nekreslí (VR menu přijde zvlášť)
        SetCamera(s.cameraPreset);
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
        if (scene != null) scene.Rebuild(patch.fixtures);
    }

    // Uložený stav → scéna (po zavření dialogu bez uložení)
    void ApplyVisibility()
    {
        if (scene == null || patch == null) return;
        rigDirty = false;
        scene.Rebuild(patch.fixtures);
    }

    // Okamžitý náhled dialogu ve scéně (Zrušit vrátí uložený stav)
    void PreviewVisibility()
    {
        if (scene == null || stFixtures == null) return;
        for (int i = 0; i < stFixtures.Count; i++) stFixtures[i].hidden = !eOn[i];
        bool same = !rigDirty && scene.instances.Count == stFixtures.Count;
        for (int i = 0; same && i < stFixtures.Count; i++) same = scene.instances[i].entry == stFixtures[i];
        if (!same)
        {
            for (int i = 0; i < stFixtures.Count; i++) stFixtures[i].mode = eMode[i];
            scene.Rebuild(stFixtures);
            rigDirty = false;
            return;
        }
        for (int i = 0; i < stFixtures.Count; i++) scene.SetVisible(i, eOn[i]);
        scene.FinishVisibility();
    }

    void CloseSettingsWithoutSaving()
    {
        winOpen[WinSettings] = false;
        popup = 0;
        ApplyVisibility();
    }

    static List<FixtureEntry> CloneList(List<FixtureEntry> src)
    {
        var l = new List<FixtureEntry>();
        foreach (var f in src) if (f != null) l.Add(f.Clone());
        return l;
    }

    void OpenSettings()
    {
        stEn = Loc.En;
        stStatus = s.showStatusBar;
        stFixtures = CloneList(s.fixtures);
        LoadEditor();
        settingsError = null;
        rigDirty = true;
        popup = 0;
        OpenWindow(WinSettings);
        PreviewVisibility();   // náhled pracuje s kopií z dialogu
    }

    static string Num(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    void LoadEditor()
    {
        eName.Clear(); eUni.Clear(); eAddr.Clear(); eMode.Clear(); eOn.Clear(); eOffset.Clear(); eTiltOffset.Clear(); eInvPan.Clear(); eInvTilt.Clear();
        eCustom.Clear(); ePos.Clear();
        foreach (var f in stFixtures) AddEditorRow(f);
        selFixture = Mathf.Clamp(selFixture, 0, Mathf.Max(0, stFixtures.Count - 1));
        popup = 0;
    }

    void AddEditorRow(FixtureEntry f, int at = -1)
    {
        if (at < 0) at = eName.Count;
        eName.Insert(at, f.name);
        eUni.Insert(at, f.universe.ToString());
        eAddr.Insert(at, f.address.ToString());
        eMode.Insert(at, f.mode);
        eOn.Insert(at, !f.hidden);
        eOffset.Insert(at, f.panOffset.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
        eTiltOffset.Insert(at, f.tiltOffset.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
        eInvPan.Insert(at, f.invertPan);
        eInvTilt.Insert(at, f.invertTilt);
        eCustom.Insert(at, f.customPos);
        ePos.Insert(at, new[] { Num(f.pos.x), Num(f.pos.y), Num(f.pos.z), Num(f.rot.x), Num(f.rot.y), Num(f.rot.z) });
    }

    void RemoveEditorRow(int i)
    {
        stFixtures.RemoveAt(i);
        eName.RemoveAt(i); eUni.RemoveAt(i); eAddr.RemoveAt(i); eMode.RemoveAt(i); eOn.RemoveAt(i); eOffset.RemoveAt(i);
        eTiltOffset.RemoveAt(i); eInvPan.RemoveAt(i); eInvTilt.RemoveAt(i); eCustom.RemoveAt(i); ePos.RemoveAt(i);
    }

    void MoveEditorRow(int i, int d)
    {
        int j = i + d;
        if (j < 0 || j >= stFixtures.Count) return;
        var f = stFixtures[i];
        // zachovat rozepsané hodnoty
        RemoveRowInto(i, out string n, out string u, out string a, out int m, out bool on, out string po, out string to, out bool ip, out bool it, out bool cu, out string[] ps);
        stFixtures.Insert(j, f);
        eName.Insert(j, n); eUni.Insert(j, u); eAddr.Insert(j, a); eMode.Insert(j, m); eOn.Insert(j, on); eOffset.Insert(j, po);
        eTiltOffset.Insert(j, to); eInvPan.Insert(j, ip); eInvTilt.Insert(j, it); eCustom.Insert(j, cu); ePos.Insert(j, ps);
        selFixture = j;
        rigDirty = true;
        PreviewVisibility();
    }

    void RemoveRowInto(int i, out string n, out string u, out string a, out int m, out bool on, out string po, out string to, out bool ip, out bool it, out bool cu, out string[] ps)
    {
        n = eName[i]; u = eUni[i]; a = eAddr[i]; m = eMode[i]; on = eOn[i]; po = eOffset[i]; to = eTiltOffset[i];
        ip = eInvPan[i]; it = eInvTilt[i]; cu = eCustom[i]; ps = ePos[i];
        RemoveEditorRow(i);
    }

    // Nové světlo podle profilu: za vybrané, první volná adresa za posledním světlem
    void AddFixture(FixtureProfile p)
    {
        var f = new FixtureEntry(p.id, p.Label, 1);
        if (!p.IsProp)
        {
            int end = 0;
            for (int i = 0; i < stFixtures.Count; i++)
            {
                var q = stFixtures[i].Profile;
                if (q == null || q.IsProp) continue;
                if (int.TryParse(eUni[i], out int u) && u == 1 && int.TryParse(eAddr[i], out int a))
                    end = Mathf.Max(end, a + q.Channels(eMode[i]) - 1);
            }
            int ch = p.Channels(0);
            f.address = end + ch <= 512 ? end + 1 : 1;
        }
        int at = stFixtures.Count == 0 ? 0 : Mathf.Clamp(selFixture + 1, 0, stFixtures.Count);
        stFixtures.Insert(at, f);
        AddEditorRow(f, at);
        selFixture = at;
        rigDirty = true;
        PreviewVisibility();
    }

    // Zkontroluje a uloží vše z dialogu. Vrací true, když se povedlo.
    bool ApplySettings()
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        for (int i = 0; i < stFixtures.Count; i++)
        {
            var f = stFixtures[i];
            var p = f.Profile;
            int ch = p == null || p.IsProp ? 0 : p.Channels(eMode[i]);
            string nm = string.IsNullOrWhiteSpace(eName[i]) ? (p != null ? p.Label : f.profile) : eName[i].Trim();
            if (ch > 0)
            {
                if (!int.TryParse(eUni[i], out int u) || u < 1 || u > 16) { settingsError = Loc.F("errUniverse", nm); settingsTab = 1; selFixture = i; return false; }
                if (!int.TryParse(eAddr[i], out int a) || a < 1 || a + ch - 1 > 512) { settingsError = Loc.F("errAddress", nm, 513 - ch); settingsTab = 1; selFixture = i; return false; }
                f.universe = u; f.address = a;
            }
            if (!TryDeg(eOffset[i], out float po) || po < -540f || po > 540f)
            { settingsError = Loc.F("errOffset", nm); settingsTab = 1; selFixture = i; return false; }
            if (!TryDeg(eTiltOffset[i], out float to) || to < -270f || to > 270f)
            { settingsError = Loc.F("errTiltOffset", nm); settingsTab = 1; selFixture = i; return false; }
            var v = new float[6];
            for (int k = 0; k < 6; k++)
                if (!TryDeg(ePos[i][k], out v[k]) || Mathf.Abs(v[k]) > (k < 3 ? 50f : 360f))
                { if (eCustom[i]) { settingsError = Loc.F("errPos", nm); settingsTab = 1; selFixture = i; return false; } v[k] = 0f; }
            f.name = nm; f.mode = eMode[i]; f.hidden = !eOn[i];
            f.panOffset = po; f.tiltOffset = to; f.invertPan = eInvPan[i]; f.invertTilt = eInvTilt[i];
            f.customPos = eCustom[i];
            f.pos = new Vector3(v[0], v[1], v[2]);
            f.rot = new Vector3(v[3], v[4], v[5]);
        }
        s.fixtures = CloneList(stFixtures);
        s.showStatusBar = stStatus;
        Loc.En = stEn;
        s.language = stEn ? "en" : "cs";
        ApplyToPatch();
        s.Save();
        // dialog dál pracuje s vlastní kopií (scéna teď ukazuje uložený stav)
        stFixtures = CloneList(s.fixtures);
        rigDirty = true;
        settingsError = null;
        Flash(Loc.T("saved"));
        return true;
    }

    int EditorChannels(int i)
    {
        var p = stFixtures[i].Profile;
        return p == null || p.IsProp ? 0 : p.Channels(eMode[i]);
    }

    string Overlap(int i)
    {
        int ch = EditorChannels(i);
        if (ch == 0) return null;
        if (!int.TryParse(eUni[i], out int u) || !int.TryParse(eAddr[i], out int a)) return null;
        int end = a + ch - 1;
        for (int j = 0; j < stFixtures.Count; j++)
        {
            int ch2 = j == i ? 0 : EditorChannels(j);
            if (ch2 == 0) continue;
            if (!int.TryParse(eUni[j], out int u2) || !int.TryParse(eAddr[j], out int a2) || u2 != u) continue;
            int end2 = a2 + ch2 - 1;
            if (a <= end2 && a2 <= end) return eName[j];
        }
        return null;
    }

    void SetCamera(int i)
    {
        if (cams == null || i < 0 || i >= cams.Length) return;
        if (VRRig.Instance != null) { VRRig.Instance.Teleport(cams[i].pos, cams[i].look); s.cameraPreset = i; return; }
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

    void OnApplicationQuit()
    {
        if (s != null) s.Save();
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Pojistka: nastavení je uložené – kdyby se ukončování Unity zaseklo (vlákna, sokety, ovladač GPU),
        // proces se po 3 s ukončí sám, aby Windows nehlásily „program neodpovídá“.
        new System.Threading.Thread(() =>
        {
            System.Threading.Thread.Sleep(3000);
            try { System.Diagnostics.Process.GetCurrentProcess().Kill(); } catch { }
        }) { IsBackground = true, Name = "QuitWatchdog" }.Start();
#endif
    }

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

    // ---- API pro VR menu (VRMenu) ----
    public VisualizerSettings Settings => s;
    public bool HasArtNet => artnet != null && artnet.HasData;
    public bool DemoForced => patch != null && patch.forceDemo;
    public string Sender => artnet != null ? artnet.lastSender : "";
    public float PacketsPerSec => pps;
    public float Fps => fps;
    public int HazePct => HazePercent();
    public int RoomLightPct => Mathf.RoundToInt(s.roomLight * 100);
    public void AddRoomLight(float d) { s.roomLight = Mathf.Clamp01(s.roomLight + d); scene.roomLight = s.roomLight; s.Save(); }
    public int CameraCount => cams != null ? cams.Length : 0;
    public int CurrentCamera => s != null ? s.cameraPreset : 0;
    public string CameraName(int i) => Loc.T(cams[i].key);
    public void ToggleDemo() { if (patch != null) patch.forceDemo = !patch.forceDemo; }
    public void GoToCamera(int i) { SetCamera(i); s.Save(); }
    public void AddHaze(float d)
    {
        if (LiveData) patch.hazeDensity = Mathf.Clamp01(patch.hazeDensity + d);
        else scene.haze = Mathf.Clamp(scene.haze + d * 3f, 0f, 3f);
    }

    // „Aktuální pozice ze SS = střed parketu“ pro všechny hlavy najednou. Vrací počet nakalibrovaných světel.
    public int CalibrateAllToCenter()
    {
        if (patch == null || scene == null || !HasArtNet) return 0;
        int n = 0;
        for (int i = 0; i < s.fixtures.Count; i++)
        {
            var f = s.fixtures[i];
            if (f == null || f.hidden || !f.HasMovers) continue;
            if (patch.SolveOffsets(i, f, scene.danceFloorCenter, out float po, out float to))
            {
                f.panOffset = Mathf.Round(po);
                f.tiltOffset = Mathf.Round(to);
                n++;
            }
        }
        if (n > 0) { ApplyToPatch(); s.Save(); }
        return n;
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
        GUILayout.Label(Loc.F("roomLight", RoomLightPct), sHead);
        s.roomLight = GUILayout.HorizontalSlider(s.roomLight, 0f, 1f);
        scene.roomLight = s.roomLight;
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
        // Otevřená nabídka leží nad ostatními prvky – klik do ní musí dostat ona, ne prvek pod ní
        var ev0 = Event.current;
        if (settingsTab == 1 && popup != 0 && selectPopupRect.Contains(ev0.mousePosition)
            && (ev0.type == EventType.MouseDown || ev0.type == EventType.MouseUp || ev0.type == EventType.ScrollWheel))
        {
            DrawPopup();
            if (ev0.type != EventType.Used) ev0.Use();
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(settingsTab == 0, Loc.T("tabGeneral"), sTab)) settingsTab = 0;
        if (GUILayout.Toggle(settingsTab == 1, Loc.T("tabLights"), sTab)) settingsTab = 1;
        if (GUILayout.Toggle(settingsTab == 2, Loc.T("tabArtNet"), sTab)) settingsTab = 2;
        if (quest != null && GUILayout.Toggle(settingsTab == 3, Loc.T("tabQuest"), sTab)) settingsTab = 3;
        GUILayout.EndHorizontal();
        GUILayout.Space(10 * k);

        // Klik mimo rozbalený select ho zavře
        var ev = Event.current;
        if (popup != 0 && ev.type == EventType.MouseDown && !selectPopupRect.Contains(ev.mousePosition) && !FromScreen(popupAnchor).Contains(ev.mousePosition))
            popup = 0;

        // Obsah záložky má pevnou výšku – tlačítka jsou vždy na stejném místě vpravo dole
        GUILayout.BeginVertical(GUILayout.Height(520 * k));
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
        else if (settingsTab == 3 && quest != null)
        {
            DrawQuest();
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

        if (settingsTab == 1 && popup != 0) DrawPopup();
    }

    // Plovoucí nabídky (režim kanálů, přidat světlo, import sestavy) – kreslí se navrch, nic neposouvají
    // Kotva nabídky v souřadnicích obrazovky – tlačítko může být uvnitř posuvného panelu
    static Rect ToScreen(Rect r) => new Rect(GUIUtility.GUIToScreenPoint(r.position), r.size);
    static Rect FromScreen(Rect r) => new Rect(GUIUtility.ScreenToGUIPoint(r.position), r.size);

    void DrawPopup()
    {
        float ih = 24 * k;
        var popupAnchor = FromScreen(this.popupAnchor);
        if (popup == 1)
        {
            if (selectModes == null || selectFor < 0 || selectFor >= eMode.Count) { popup = 0; return; }
            selectPopupRect = new Rect(popupAnchor.x, popupAnchor.yMax + 2 * k, popupAnchor.width, ih * selectModes.Length + 4 * k);
            GUI.DrawTexture(selectPopupRect, tDrop);
            for (int j = 0; j < selectModes.Length; j++)
            {
                var r = new Rect(selectPopupRect.x, selectPopupRect.y + 2 * k + j * ih, selectPopupRect.width, ih);
                if (GUI.Button(r, (j == selectIndex ? "✓  " : "     ") + selectModes[j], sDropItem))
                {
                    if (eMode[selectFor] != j) { eMode[selectFor] = j; rigDirty = true; PreviewVisibility(); }
                    popup = 0;
                }
            }
            return;
        }

        // seznam profilů nebo souborů sestav
        var labels = new List<string>();
        List<FixtureProfile> profs = null;
        if (popup == 2)
        {
            profs = ProfileLibrary.All;
            foreach (var p in profs) labels.Add((p.IsProp ? "▫  " : p.IsHazer ? "≈  " : "•  ") + p.Label);
        }
        else
        {
            if (rigFiles == null || rigFiles.Length == 0) labels.Add(Loc.T("rigNone"));
            else foreach (var f in rigFiles) labels.Add(Path.GetFileNameWithoutExtension(f));
        }
        float w = Mathf.Max(popupAnchor.width, 360 * k);
        float hMax = Mathf.Min(ih * labels.Count + 4 * k, 330 * k);
        selectPopupRect = new Rect(popupAnchor.x, popupAnchor.y - hMax - 2 * k, w, hMax);   // nad tlačítkem (je dole v okně)
        if (selectPopupRect.y < 40 * k) selectPopupRect.y = popupAnchor.yMax + 2 * k;
        GUI.DrawTexture(selectPopupRect, tDrop);
        pickScroll = GUI.BeginScrollView(selectPopupRect, pickScroll, new Rect(0, 0, w - 18 * k, ih * labels.Count + 4 * k));
        for (int j = 0; j < labels.Count; j++)
        {
            var r = new Rect(0, 2 * k + j * ih, w - 18 * k, ih);
            if (!GUI.Button(r, labels[j], sDropItem)) continue;
            popup = 0;
            if (profs != null) AddFixture(profs[j]);
            else if (rigFiles != null && j < rigFiles.Length) ImportRig(rigFiles[j]);
        }
        GUI.EndScrollView();
    }

    void LoadPreset(List<FixtureEntry> list, string flash = null)
    {
        stFixtures = list;
        selFixture = 0;
        LoadEditor();
        rigDirty = true;
        PreviewVisibility();
        Flash(flash ?? Loc.T("resetLightsDone"));
    }

    // ---- Sestava jako soubor (export / import) ----
    [Serializable]
    class RigFile
    {
        public string app = "DMX Visualiser";
        public string appVersion;
        public int version = 9;
        public List<FixtureEntry> fixtures;
        public List<FixtureProfile> customProfiles;   // stejný název jako v settings.json → jde importovat i settings.json
    }

    static string RigsDir =>
#if UNITY_EDITOR
        Path.Combine(Path.GetDirectoryName(Application.dataPath), "Build", "Rigs");
#else
        Path.Combine(Path.GetDirectoryName(VisualizerSettings.FilePath), "Rigs");
#endif

    void ExportRig()
    {
        if (!ApplySettings()) return;   // exportuje se to, co je v dialogu
        try
        {
            string name = ProfileLibrary.SafeName(string.IsNullOrWhiteSpace(rigName) ? "sestava" : rigName.Trim());
            Directory.CreateDirectory(RigsDir);
            string path = Path.Combine(RigsDir, name + ".json");
            var rf = new RigFile { appVersion = Application.version, fixtures = CloneList(s.fixtures), customProfiles = VisualizerSettings.UsedCustomProfiles(s.fixtures) };
            File.WriteAllText(path, JsonUtility.ToJson(rf, true));
            Flash(Loc.F("rigExported", path));
        }
        catch (Exception e) { settingsError = Loc.F("rigImportErr", e.Message); }
    }

    void OpenRigPicker()
    {
        try { rigFiles = Directory.Exists(RigsDir) ? Directory.GetFiles(RigsDir, "*.json") : new string[0]; }
        catch { rigFiles = new string[0]; }
        popup = 3;
    }

    void ImportRig(string path)
    {
        try
        {
            var rf = JsonUtility.FromJson<RigFile>(File.ReadAllText(path));
            if (rf == null || rf.fixtures == null || rf.fixtures.Count == 0) throw new Exception(Loc.En ? "no fixtures in the file" : "soubor neobsahuje světla");
            // vlastní profily ze sestavy do knihovny (soubory ve složce Profiles), aby je šlo i upravit
            if (rf.customProfiles != null)
                foreach (var p in rf.customProfiles)
                    if (p != null && ProfileLibrary.Validate(p) == null) ProfileLibrary.SaveToLibrary(p);
            ProfileLibrary.Reload(s.customProfiles);
            foreach (var f in rf.fixtures) if (f != null) f.Migrate();
            rf.fixtures.RemoveAll(f => f == null);
            rigName = Path.GetFileNameWithoutExtension(path);
            LoadPreset(rf.fixtures, Loc.F("rigImported", rigName));
        }
        catch (Exception e) { settingsError = Loc.F("rigImportErr", e.Message); }
    }

    static void OpenFolder(string dir)
    {
        try { Directory.CreateDirectory(dir); Application.OpenURL("file:///" + dir.Replace('\\', '/')); }
        catch (Exception e) { Debug.LogWarning(e.Message); }
    }

    void ReloadProfiles()
    {
        ProfileLibrary.Reload(s.customProfiles);
        rigDirty = true;
        PreviewVisibility();
        Flash(Loc.F("profilesReloaded", ProfileLibrary.All.Count));
    }

    // Světla: vlevo seznam se zatržítky (zobrazit ve scéně), vpravo nastavení vybraného světla
    void DrawLightsEditor()
    {
        GUILayout.Label(Loc.T("patchHint"), sDim);
        GUILayout.Space(6 * k);
        GUILayout.BeginHorizontal();

        // ---- seznam ----
        GUILayout.BeginVertical(GUILayout.Width(280 * k));
        lightsScroll = GUILayout.BeginScrollView(lightsScroll, GUI.skin.box, GUILayout.Height(Mathf.Min(Screen.height * 0.4f, 250 * k)));
        for (int i = 0; i < stFixtures.Count; i++)
        {
            GUILayout.BeginHorizontal();
            bool was = eOn[i];
            eOn[i] = GUILayout.Toggle(eOn[i], GUIContent.none, sToggle, GUILayout.Width(20 * k));
            if (eOn[i] != was) PreviewVisibility();
            string label = eName[i] + (Overlap(i) != null || stFixtures[i].Profile == null ? "   ⚠" : "");
            bool sel = selFixture == i;
            if (GUILayout.Toggle(sel, label, sListItem) && !sel)
            {
                selFixture = i;
                popup = 0;
                GUIUtility.keyboardControl = 0;
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(Loc.T("addFixture"), sButton)) popup = popup == 2 ? 0 : 2;
        if (Event.current.type == EventType.Repaint && popup == 2) popupAnchor = ToScreen(GUILayoutUtility.GetLastRect());
        GUI.enabled = stFixtures.Count > 0;
        if (GUILayout.Button(Loc.T("removeFixture"), sButton) && selFixture < stFixtures.Count)
        {
            RemoveEditorRow(selFixture);
            selFixture = Mathf.Clamp(selFixture, 0, Mathf.Max(0, stFixtures.Count - 1));
            rigDirty = true;
            PreviewVisibility();
        }
        if (GUILayout.Button("↑", sButton, GUILayout.Width(28 * k))) MoveEditorRow(selFixture, -1);
        if (GUILayout.Button("↓", sButton, GUILayout.Width(28 * k))) MoveEditorRow(selFixture, 1);
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        // Načíst výchozí sestavu (přepíše seznam v dialogu, uloží se až OK / Použít)
        GUILayout.Space(4 * k);
        GUILayout.BeginHorizontal();
        GUILayout.Label(Loc.T("presetLoad"), sDim, GUILayout.Width(100 * k));
        if (GUILayout.Button(Loc.T("presetMine"), sButton)) LoadPreset(RigPresets.Mine());
        if (GUILayout.Button(Loc.T("presetColleague"), sButton)) LoadPreset(RigPresets.Colleague());
        GUILayout.EndHorizontal();

        // Sestava jako soubor – poslat kolegovi / přenést na jiný počítač
        GUILayout.Space(4 * k);
        GUILayout.Label(Loc.T("rigFile"), sDim);
        GUILayout.BeginHorizontal();
        rigName = GUILayout.TextField(rigName, sField);
        if (GUILayout.Button(Loc.T("rigExport"), sButton, GUILayout.Width(90 * k))) ExportRig();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(Loc.T("rigImport"), sButton)) { if (popup == 3) popup = 0; else OpenRigPicker(); }
        if (Event.current.type == EventType.Repaint && popup == 3) popupAnchor = ToScreen(GUILayoutUtility.GetLastRect());
        if (GUILayout.Button(Loc.T("rigFolder"), sButton)) OpenFolder(RigsDir);
        GUILayout.EndHorizontal();

        // Profily světel (JSON) – vlastní světla bez nového buildu
        GUILayout.Space(4 * k);
        GUILayout.BeginHorizontal();
        GUILayout.Label(Loc.T("profiles"), sDim, GUILayout.Width(100 * k));
        if (GUILayout.Button(Loc.T("profilesFolder"), sButton)) OpenFolder(ProfileLibrary.UserDir);
        if (GUILayout.Button(Loc.T("profilesReload"), sButton)) ReloadProfiles();
        GUILayout.EndHorizontal();
        if (ProfileLibrary.Errors.Count > 0) GUILayout.Label(Loc.F("profileErrors", string.Join("; ", ProfileLibrary.Errors)), sWarn);
        GUILayout.EndVertical();

        GUILayout.Space(14 * k);

        // ---- detail ----
        detailScroll = GUILayout.BeginScrollView(detailScroll);
        GUILayout.BeginVertical();
        if (stFixtures.Count > 0) DrawFixtureDetail(Mathf.Clamp(selFixture, 0, stFixtures.Count - 1));
        GUILayout.EndVertical();
        GUILayout.EndScrollView();
        GUILayout.EndHorizontal();
    }

    void DrawFixtureDetail(int i)
    {
        var f = stFixtures[i];
        var p = f.Profile;
        float lw = 120 * k;
        if (p == null)
        {
            GUILayout.Label(eName[i], sHead);
            GUILayout.Label(Loc.F("profileMissing", f.profile), sWarn);
            return;
        }
        GUILayout.Label(p.Label, sHead);
        string src = p.source == "builtin" ? Loc.T("srcBuiltin") : p.source == "embedded" ? Loc.T("srcEmbedded") : Path.GetFileName(p.source);
        GUILayout.BeginHorizontal();
        GUILayout.Label(Loc.F("profileSource", src), sDim);
        if (GUILayout.Button(Loc.T("profileEdit"), sButton, GUILayout.ExpandWidth(false)))
        {
            try
            {
                var copy = JsonUtility.FromJson<FixtureProfile>(JsonUtility.ToJson(p));
                string path = ProfileLibrary.SaveToLibrary(copy);
                Flash(Loc.F("profileSaved", path));
                OpenFolder(ProfileLibrary.UserDir);
            }
            catch (Exception e) { settingsError = e.Message; }
        }
        GUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(p.notes)) GUILayout.Label(p.notes, sDim);

        bool wasOn = eOn[i];
        eOn[i] = GUILayout.Toggle(eOn[i], " " + Loc.T("showInScene"), sToggle);
        if (eOn[i] != wasOn) PreviewVisibility();
        GUILayout.Space(6 * k);

        GUILayout.BeginHorizontal();
        GUILayout.Label(Loc.T("name"), sLabel, GUILayout.Width(lw));
        eName[i] = GUILayout.TextField(eName[i], sField);
        GUILayout.EndHorizontal();

        if (p.IsProp) GUILayout.Label(Loc.T("propHint"), sDim);
        else
        {
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

            // select režimu kanálů – rozbalená nabídka se kreslí navrch (DrawPopup)
            var modes = new string[Mathf.Max(1, p.ModeCount)];
            for (int m = 0; m < p.ModeCount; m++) modes[m] = p.modes[m].name ?? (p.modes[m].channels + "ch");
            int mi = Mathf.Clamp(eMode[i], 0, modes.Length - 1);
            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T("channelMode"), sLabel, GUILayout.Width(lw));
            if (GUILayout.Button(modes[mi] + "   ▾", sSelect)) popup = popup == 1 ? 0 : 1;
            if (Event.current.type == EventType.Repaint && popup == 1) popupAnchor = ToScreen(GUILayoutUtility.GetLastRect());
            GUILayout.EndHorizontal();
            selectModes = modes;
            selectIndex = mi;
            selectFor = i;

            GUILayout.Space(6 * k);
            int ch = p.Channels(mi);
            if (int.TryParse(eAddr[i], out int a))
                GUILayout.Label(Loc.F("channelsInfo", ch, a, a + ch - 1), sDim);
            string ov = Overlap(i);
            if (ov != null) GUILayout.Label(Loc.F("overlap", ov), sWarn);
        }

        if (!p.IsHazer) DrawPlacement(i, lw);
        if (p.HasMovers) DrawCalibration(i, f, lw);
    }

    // Umístění ve scéně: automaticky podle profilu, nebo vlastní souřadnice
    void DrawPlacement(int i, float lw)
    {
        GUILayout.Space(10 * k);
        GUILayout.Label(Loc.T("placement"), sHead);
        GUILayout.BeginHorizontal();
        bool was = eCustom[i];
        if (GUILayout.Toggle(!eCustom[i], Loc.T("posAuto"), sTab)) eCustom[i] = false;
        if (GUILayout.Toggle(eCustom[i], Loc.T("customPos"), sTab)) eCustom[i] = true;
        GUILayout.EndHorizontal();
        if (eCustom[i] && !was && scene != null && i < scene.instances.Count && scene.instances[i].root != null)
        {
            // předvyplnit aktuálním místem ve scéně
            var t = scene.instances[i].root;
            var lp = t.localPosition; var le = t.localEulerAngles;
            ePos[i] = new[] { Num(Round(lp.x)), Num(Round(lp.y)), Num(Round(lp.z)), Num(Ang(le.x)), Num(Ang(le.y)), Num(Ang(le.z)) };
        }
        if (eCustom[i] != was) ApplyPlacementPreview(i);
        if (!eCustom[i]) return;

        bool changed = false;
        GUILayout.BeginHorizontal();
        GUILayout.Label(Loc.T("posXYZ"), sLabel, GUILayout.Width(lw + 40 * k));
        for (int c = 0; c < 3; c++) { string o = ePos[i][c]; ePos[i][c] = GUILayout.TextField(ePos[i][c], sField, GUILayout.Width(60 * k)); changed |= o != ePos[i][c]; }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label(Loc.T("rotXYZ"), sLabel, GUILayout.Width(lw + 40 * k));
        for (int c = 3; c < 6; c++) { string o = ePos[i][c]; ePos[i][c] = GUILayout.TextField(ePos[i][c], sField, GUILayout.Width(60 * k)); changed |= o != ePos[i][c]; }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Label(Loc.T("posHint"), sDim);
        if (changed) ApplyPlacementPreview(i);
    }

    static float Round(float v) => Mathf.Round(v * 100f) / 100f;
    static float Ang(float v) { v = Mathf.Repeat(v + 180f, 360f) - 180f; return Mathf.Round(v * 10f) / 10f; }

    // Náhled umístění hned při psaní (uloží se až OK / Použít)
    void ApplyPlacementPreview(int i)
    {
        var f = stFixtures[i];
        f.customPos = eCustom[i];
        var v = new float[6];
        for (int c = 0; c < 6; c++) if (!TryDeg(ePos[i][c], out v[c])) return;
        f.pos = new Vector3(v[0], v[1], v[2]);
        f.rot = new Vector3(v[3], v[4], v[5]);
        if (scene != null && i < scene.instances.Count && scene.instances[i].entry == f) scene.FinishVisibility();
        else { rigDirty = true; PreviewVisibility(); }
    }

    // Kalibrace moving headů + živý odečet pan/tilt ze SoundSwitche
    void DrawCalibration(int i, FixtureEntry f, float lw)
    {
        GUILayout.Space(10 * k);
        GUILayout.Label(Loc.T("calibration"), sHead);
        GUILayout.BeginHorizontal();
        GUILayout.Label(Loc.T("panOffset"), sLabel, GUILayout.Width(lw));
        eOffset[i] = GUILayout.TextField(eOffset[i], sField, GUILayout.Width(60 * k));
        GUILayout.Label("°", sLabel, GUILayout.Width(16 * k));
        GUILayout.Space(12 * k);
        GUILayout.Label(Loc.T("tiltOffset"), sLabel);
        eTiltOffset[i] = GUILayout.TextField(eTiltOffset[i], sField, GUILayout.Width(60 * k));
        GUILayout.Label("°", sLabel, GUILayout.Width(16 * k));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        eInvPan[i] = GUILayout.Toggle(eInvPan[i], " " + Loc.T("invertPan"), sToggle);
        eInvTilt[i] = GUILayout.Toggle(eInvTilt[i], " " + Loc.T("invertTilt"), sToggle);
        GUILayout.EndHorizontal();

        // živé hodnoty podle aktuálně zadané adresy (i před uložením)
        int.TryParse(eUni[i], out int u);
        int.TryParse(eAddr[i], out int a);
        var probe = new FixtureEntry(f.profile, f.name, a, eMode[i]) { universe = u, invertPan = eInvPan[i], invertTilt = eInvTilt[i] };
        DmxPatch.MoverRanges(f.Profile, out float panRange, out float tiltRange);
        if (patch != null && patch.ReadPanTilt(probe, out int pv, out int tv))
        {
            float pn = pv / 65535f, tn = tv / 65535f;
            if (eInvPan[i]) pn = 1f - pn;
            if (eInvTilt[i]) tn = 1f - tn;
            TryDeg(eOffset[i], out float po);
            TryDeg(eTiltOffset[i], out float to);
            float panDeg = (pn - 0.5f) * panRange + po;
            float tiltDeg = (tn - 0.5f) * tiltRange + to;
            GUILayout.Label(Loc.F("liveValues", pv, panDeg.ToString("+0;-0;0"), tv, tiltDeg.ToString("+0;-0;0")), sLabel);

            // Jedním klikem: aktuální pozice ze SS (např. Stage Center) = střed parketu
            if (GUILayout.Button(Loc.T("calibCenter"), sButton, GUILayout.ExpandWidth(false)) && scene != null)
            {
                if (rigDirty) PreviewVisibility();   // scéna musí odpovídat dialogu
                if (patch.SolveOffsets(i, probe, scene.danceFloorCenter, out float npo, out float nto))
                {
                    eOffset[i] = npo.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
                    eTiltOffset[i] = nto.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }
        else GUILayout.Label(Loc.T("liveNone"), sDim);
        GUILayout.Label(Loc.T("calibrationHint"), sDim);
    }

    static bool TryDeg(string s, out float v) =>
        float.TryParse((s ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v);

    // ---- Quest (ADB) ----
    void DrawQuest()
    {
        if (!questDetected) { questDetected = true; quest.Detect(); }
        bool busy = quest.busy;
        bool ready = quest.adbPath != null && quest.device != null && quest.deviceState == "device";

        GUILayout.Label(Loc.T("qTitle"), sHead);
        GUILayout.Label(Loc.T("qHint"), sDim);
        GUILayout.Space(8 * k);

        GUILayout.Label("ADB: " + (quest.adbPath ?? Loc.T("qAdbMissing")), sLabel);
        GUILayout.Label(Loc.T("qDevice") + ": " + (quest.device == null ? "-" : quest.deviceModel + " (" + quest.deviceState + ")"), sLabel);
        GUILayout.Label(Loc.T("qAppVersion") + ": " + (quest.installedVersion ?? "-") + "    " + Loc.F("qPcVersion", Application.version), sLabel);
        GUILayout.Space(8 * k);

        string st = quest.status + (quest.progress >= 0f ? "  " + Mathf.RoundToInt(quest.progress * 100) + " %" : "");
        GUILayout.Label(st, quest.statusError ? sWarn : sHead);
        GUILayout.Space(10 * k);

        GUI.enabled = !busy;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(Loc.T("qDetect"), sButton, GUILayout.Width(200 * k))) quest.Detect();
        if (quest.adbPath == null && GUILayout.Button(Loc.T("qGetAdb"), sButton, GUILayout.Width(260 * k))) quest.DownloadAdb();
        GUILayout.EndHorizontal();
        GUILayout.Space(6 * k);
        GUI.enabled = !busy && ready;
        string inst = quest.installedVersion == null ? Loc.T("qInstall")
            : (quest.installedVersion == Application.version ? Loc.T("qReinstall") : Loc.F("qUpdate", Application.version));
        if (GUILayout.Button(inst, sBig)) quest.InstallApp();
        GUI.enabled = !busy && ready && quest.installedVersion != null;
        if (GUILayout.Button(Loc.T("qPush"), sBig)) { s.EmbedProfiles(); quest.PushSettings(s); }
        GUI.enabled = true;
        GUILayout.Label(Loc.T("qPushHint"), sDim);
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
