using System.Collections.Generic;

// Jednoduchá lokalizace CZ / EN. Loc.T("klíč"), Loc.F("klíč", argumenty).
public static class Loc
{
    public static bool En;

    static readonly Dictionary<string, string[]> D = new Dictionary<string, string[]>
    {
        // Hlavní lišta
        { "file", new[] { "Soubor", "File" } },
        { "view", new[] { "Zobrazení", "View" } },
        { "help", new[] { "Nápověda", "Help" } },
        { "settingsMenu", new[] { "Nastavení…", "Settings…" } },
        { "quit", new[] { "Ukončit", "Quit" } },
        { "panelMenu", new[] { "Ovládací panel", "Control panel" } },
        { "fullscreen", new[] { "Celá obrazovka", "Fullscreen" } },
        { "exitFullscreen", new[] { "Ukončit celou obrazovku", "Exit fullscreen" } },
        { "controlsMenu", new[] { "Ovládání…", "Controls…" } },
        { "updatesMenu", new[] { "Zkontrolovat aktualizace…", "Check for updates…" } },
        { "aboutMenu", new[] { "O aplikaci…", "About…" } },

        // Ovládací panel
        { "panelTitle", new[] { "Ovládací panel", "Control panel" } },
        { "camera", new[] { "Kamera", "Camera" } },
        { "camHost", new[] { "Host", "Guest" } },
        { "camFloor", new[] { "Parket", "Dance floor" } },
        { "camDJ", new[] { "DJ", "DJ" } },
        { "camTop", new[] { "Shora", "Top" } },
        { "startDemo", new[] { "▶  Spustit demo", "▶  Start demo" } },
        { "stopDemo", new[] { "■  Zastavit demo", "■  Stop demo" } },
        { "demoHint", new[] { "Demo běží samo, dokud nepřijdou data ze SoundSwitche.", "The demo runs by itself until data arrives from SoundSwitch." } },
        { "hazeLive", new[] { "Haze v sále: {0} %", "Haze in the room: {0} %" } },
        { "hazeDemo", new[] { "Viditelnost paprsků (haze): {0} %", "Beam visibility (haze): {0} %" } },
        { "liveFrom", new[] { "Přijímám data od {0}", "Receiving data from {0}" } },
        { "demoRunning", new[] { "Běží demo – žádná data", "Demo running – no data" } },
        { "demoManual", new[] { "Běží demo (spuštěné ručně)", "Demo running (started manually)" } },

        // Nastavení
        { "settingsTitle", new[] { "Nastavení", "Settings" } },
        { "tabGeneral", new[] { "Obecné", "General" } },
        { "tabLights", new[] { "Světla", "Fixtures" } },
        { "tabArtNet", new[] { "Art-Net", "Art-Net" } },
        { "language", new[] { "Jazyk", "Language" } },
        { "showStatusBar", new[] { "Zobrazovat stavovou lištu", "Show status bar" } },
        { "ok", new[] { "OK", "OK" } },
        { "apply", new[] { "Použít", "Apply" } },
        { "cancel", new[] { "Zrušit", "Cancel" } },
        { "saved", new[] { "Nastavení uloženo", "Settings saved" } },
        { "resetLights", new[] { "Obnovit výchozí světla", "Restore default fixtures" } },
        { "resetLightsDone", new[] { "Načtena výchozí světla – potvrď OK nebo Použít", "Default fixtures loaded – confirm with OK or Apply" } },
        { "patchHint", new[] {
            "Zatržítkem světlo zapneš / vypneš ve scéně. Klikni na světlo a vpravo nastav adresu jako v SoundSwitchi.",
            "Use the checkbox to show / hide a fixture in the scene. Click a fixture to set its address (as in SoundSwitch) on the right." } },
        { "address", new[] { "Adresa", "Address" } },
        { "name", new[] { "Název", "Name" } },
        { "channelMode", new[] { "Režim kanálů", "Channel mode" } },
        { "channelsInfo", new[] { "{0} kanálů  •  DMX {1}–{2}", "{0} channels  •  DMX {1}–{2}" } },
        { "showInScene", new[] { "Zobrazit ve scéně", "Show in scene" } },
        { "calibration", new[] { "Kalibrace hlav", "Head calibration" } },
        { "panOffset", new[] { "Posun panu", "Pan offset" } },
        { "tiltOffset", new[] { "Posun tiltu", "Tilt offset" } },
        { "calibCenter", new[] { "Aktuální pozice ze SS = střed parketu", "Current SS position = dance floor center" } },
        { "errTiltOffset", new[] { "{0}: posun tiltu musí být −270 až 270°", "{0}: tilt offset must be −270 to 270°" } },
        { "invertPan", new[] { "Obrátit pan", "Invert pan" } },
        { "invertTilt", new[] { "Obrátit tilt", "Invert tilt" } },
        { "liveValues", new[] { "Živě ze SS:  pan {0} → {1}°   tilt {2} → {3}°", "Live from SS:  pan {0} → {1}°   tilt {2} → {3}°" } },
        { "liveNone", new[] { "Živé hodnoty: žádná data ze SoundSwitche", "Live values: no data from SoundSwitch" } },
        { "calibrationHint", new[] {
            "Nejjednodušší: v SS dej Static Look s pozicí Stage Center, klikni na tlačítko výše a OK. Hlavy pak míří na křížek uprostřed parketu.",
            "Easiest: in SS set a Static Look with position Stage Center, click the button above and OK. The heads will then hit the cross in the middle of the dance floor." } },
        { "errOffset", new[] { "{0}: posun panu musí být −540 až 540°", "{0}: pan offset must be −540 to 540°" } },
        { "overlap", new[] { "⚠ Překrývá se s: {0}", "⚠ Overlaps with: {0}" } },
        { "errUniverse", new[] { "{0}: universe musí být 1–16", "{0}: universe must be 1–16" } },
        { "errAddress", new[] { "{0}: adresa musí být 1–{1}", "{0}: address must be 1–{1}" } },

        // Art-Net
        { "receivingFrom", new[] { "Přijímám od {0}", "Receiving from {0}" } },
        { "noData", new[] { "žádná data", "no data" } },
        { "packetsInfo", new[] { "{0} paketů  •  {1} pkt/s", "{0} packets  •  {1} pkt/s" } },
        { "listen", new[] { "Naslouchá", "Listening" } },
        { "pollReplies", new[] { "Odpovědí na ArtPoll: {0}", "ArtPoll replies: {0}" } },
        { "artnetHint", new[] {
            "SoundSwitch najde visualizér sám jako Art-Net zařízení „DJ Visualizer“. Při prvním spuštění povol aplikaci ve firewallu (privátní síť). Nespouštěj zároveň Play v Unity.",
            "SoundSwitch finds the visualiser by itself as the Art-Net device \"DJ Visualizer\". On first launch allow the app in the firewall (private network). Don't run Unity Play mode at the same time." } },

        // Stavová lišta
        { "live", new[] { "LIVE", "LIVE" } },
        { "demo", new[] { "DEMO", "DEMO" } },
        { "source", new[] { "Zdroj", "Source" } },
        { "universe", new[] { "Universe", "Universe" } },

        // Aktualizace
        { "updatesTitle", new[] { "Aktualizace", "Updates" } },
        { "currentVersion", new[] { "Nainstalovaná verze: {0}", "Installed version: {0}" } },
        { "checkNow", new[] { "Zkontrolovat", "Check now" } },
        { "checking", new[] { "Kontroluji…", "Checking…" } },
        { "upToDate", new[] { "Máš nejnovější verzi.", "You have the latest version." } },
        { "newVersion", new[] { "Je k dispozici verze {0}.", "Version {0} is available." } },
        { "install", new[] { "Stáhnout a nainstalovat", "Download and install" } },
        { "openWeb", new[] { "Otevřít na GitHubu", "Open on GitHub" } },
        { "downloading", new[] { "Stahuji… {0} %", "Downloading… {0} %" } },
        { "installing", new[] { "Instaluji – aplikace se restartuje…", "Installing – the app will restart…" } },
        { "updateError", new[] { "Aktualizace se nepovedla: {0}", "Update failed: {0}" } },
        { "noRelease", new[] { "Na GitHubu zatím není žádné vydání (nebo je repozitář soukromý).", "No release on GitHub yet (or the repository is private)." } },
        { "noAsset", new[] { "Vydání neobsahuje .zip s aplikací.", "The release has no app .zip." } },
        { "editorNoInstall", new[] { "V Unity editoru se instalace nespouští.", "Installing is disabled in the Unity editor." } },

        // Ovládání / O aplikaci
        { "controlsTitle", new[] { "Ovládání", "Controls" } },
        { "aboutTitle", new[] { "O aplikaci", "About" } },
        { "controlsText", new[] {
            "Pravé tlačítko myši + pohyb myší – rozhlížení\nW A S D – pohyb,  Q / E – dolů / nahoru,  Shift – rychleji\n1 – 4 – pohledy kamery\nF11 – celá obrazovka\nH – skrýt / zobrazit rozhraní\nEsc – zavřít okna / ukončit celou obrazovku",
            "Right mouse button + mouse – look around\nW A S D – move,  Q / E – down / up,  Shift – faster\n1 – 4 – camera views\nF11 – fullscreen\nH – hide / show interface\nEsc – close windows / exit fullscreen" } },
        { "aboutText", new[] {
            "DMX Visualiser {0}\n\nVizualizace světel ze SoundSwitche přes Art-Net.\nGigBar Move ILS (EU), battery pary, pixel tuby, hazer.\n\nVeksCZ  •  github.com/VeksCZ/XR_DMX_Visualiser",
            "DMX Visualiser {0}\n\nLighting visualisation from SoundSwitch over Art-Net.\nGigBar Move ILS (EU), battery pars, pixel tubes, hazer.\n\nVeksCZ  •  github.com/VeksCZ/XR_DMX_Visualiser" } },
    };

    public static string T(string key)
    {
        return D.TryGetValue(key, out var v) ? v[En ? 1 : 0] : key;
    }

    public static string F(string key, params object[] args)
    {
        return string.Format(T(key), args);
    }
}
