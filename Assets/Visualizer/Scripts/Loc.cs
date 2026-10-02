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
        { "roomLight", new[] { "Světlo v sále: {0} %", "Room light: {0} %" } },
        { "vrLight", new[] { "Světlo", "Light" } },
        { "hazeDemo", new[] { "Viditelnost paprsků (haze): {0} %", "Beam visibility (haze): {0} %" } },
        { "liveFrom", new[] { "Přijímám data od {0}", "Receiving data from {0}" } },
        { "demoRunning", new[] { "Běží demo – žádná data", "Demo running – no data" } },
        { "demoManual", new[] { "Běží demo (spuštěné ručně)", "Demo running (started manually)" } },

        // Nastavení
        { "settingsTitle", new[] { "Nastavení", "Settings" } },
        { "tabGeneral", new[] { "Obecné", "General" } },
        { "tabLights", new[] { "Světla", "Fixtures" } },
        { "tabArtNet", new[] { "Art-Net", "Art-Net" } },
        { "tabQuest", new[] { "Quest", "Quest" } },
        { "propHint", new[] { "Vybavení bez DMX – jen se zobrazuje ve scéně. Hlavy na stole (Pocket Pro) se posadí na rohy zobrazeného stolu.", "Equipment without DMX – shown in the scene only. Pocket Pro heads sit on the corners of the shown table." } },
        { "qTitle", new[] { "Meta Quest – aplikace a nastavení", "Meta Quest – app and settings" } },
        { "qHint", new[] {
            "Připoj Quest kabelem USB (vývojářský režim zapnutý) a v brýlích potvrď povolení USB ladění.",
            "Connect the Quest with a USB cable (developer mode on) and allow USB debugging in the headset." } },
        { "qAdbMissing", new[] { "nenalezeno", "not found" } },
        { "qDevice", new[] { "Zařízení", "Device" } },
        { "qAppVersion", new[] { "Aplikace v Questu", "App on Quest" } },
        { "qPcVersion", new[] { "(PC: {0})", "(PC: {0})" } },
        { "qDetect", new[] { "Hledat Quest", "Detect Quest" } },
        { "qGetAdb", new[] { "Stáhnout ADB od Googlu (~7 MB)", "Download ADB from Google (~7 MB)" } },
        { "qInstall", new[] { "Nainstalovat aplikaci do Questu", "Install the app on the Quest" } },
        { "qReinstall", new[] { "Přeinstalovat aplikaci", "Reinstall the app" } },
        { "qUpdate", new[] { "Aktualizovat aplikaci v Questu na {0}", "Update the Quest app to {0}" } },
        { "qPush", new[] { "Přenést nastavení světel do Questu", "Copy light settings to the Quest" } },
        { "qPushHint", new[] {
            "Přenese uložené nastavení: patch světel, kalibraci hlav a hazer. Passthrough, naskenovaná místnost a pohled v Questu zůstanou. Aplikace v Questu se restartuje.",
            "Copies the saved settings: light patch, head calibration and hazer. Passthrough, scanned room and view on the Quest stay. The Quest app restarts." } },
        { "qDetecting", new[] { "Hledám Quest…", "Looking for the Quest…" } },
        { "qNoAdb", new[] { "ADB nenalezeno – stáhni ho tlačítkem níže.", "ADB not found – download it with the button below." } },
        { "qNoDevice", new[] { "Quest není připojený (nebo není zapnutý vývojářský režim).", "No Quest connected (or developer mode is off)." } },
        { "qUnauthorized", new[] { "Quest čeká na povolení USB ladění – potvrď ho v brýlích a dej Hledat Quest.", "The Quest is waiting for USB debugging permission – allow it in the headset and press Detect Quest." } },
        { "qDeviceState", new[] { "Quest je ve stavu: {0}", "Quest state: {0}" } },
        { "qNotInstalled", new[] { "Quest připojený, aplikace zatím není nainstalovaná.", "Quest connected, the app is not installed yet." } },
        { "qInstalled", new[] { "Quest připojený, aplikace {0} nainstalovaná.", "Quest connected, app {0} installed." } },
        { "qDownloadingAdb", new[] { "Stahuji ADB…", "Downloading ADB…" } },
        { "qFindingApk", new[] { "Hledám APK na GitHubu…", "Looking for the APK on GitHub…" } },
        { "qNoApk", new[] { "Vydání {0} na GitHubu nemá APK pro Quest.", "Release {0} on GitHub has no Quest APK." } },
        { "qDownloadingApk", new[] { "Stahuji aplikaci pro Quest…", "Downloading the Quest app…" } },
        { "qInstalling", new[] { "Instaluji do Questu…", "Installing on the Quest…" } },
        { "qInstallFailed", new[] { "Instalace selhala: {0}", "Install failed: {0}" } },
        { "qInstallDone", new[] { "Hotovo – v Questu je aplikace {0} a spouští se.", "Done – app {0} is on the Quest and starting." } },
        { "qPushing", new[] { "Přenáším nastavení…", "Copying settings…" } },
        { "qPushDone", new[] { "Nastavení přeneseno, aplikace v Questu se restartovala.", "Settings copied, the Quest app restarted." } },
        { "language", new[] { "Jazyk", "Language" } },
        { "showStatusBar", new[] { "Zobrazovat stavovou lištu", "Show status bar" } },
        { "ok", new[] { "OK", "OK" } },
        { "apply", new[] { "Použít", "Apply" } },
        { "cancel", new[] { "Zrušit", "Cancel" } },
        { "saved", new[] { "Nastavení uloženo", "Settings saved" } },
        { "resetLights", new[] { "Obnovit výchozí světla", "Restore default fixtures" } },
        { "resetLightsDone", new[] { "Načtena výchozí světla – potvrď OK nebo Použít", "Default fixtures loaded – confirm with OK or Apply" } },
        { "patchHint", new[] {
            "Zatržítkem světlo zapneš / vypneš ve scéně. Klikni na světlo a vpravo nastav adresu jako v SoundSwitchi. „+ Přidat“ přidá další světlo podle profilu.",
            "Use the checkbox to show / hide a fixture in the scene. Click a fixture to set its address (as in SoundSwitch) on the right. \"+ Add\" adds another fixture from a profile." } },
        { "address", new[] { "Adresa", "Address" } },
        { "name", new[] { "Název", "Name" } },
        { "channelMode", new[] { "Režim kanálů", "Channel mode" } },
        { "channelsInfo", new[] { "{0} kanálů  •  DMX {1}–{2}", "{0} channels  •  DMX {1}–{2}" } },
        { "showInScene", new[] { "Zobrazit ve scéně", "Show in scene" } },
        { "calibration", new[] { "Kalibrace hlav", "Head calibration" } },
        { "panOffset", new[] { "Posun panu", "Pan offset" } },
        { "presetLoad", new[] { "Načíst sestavu:", "Load rig:" } },
        { "presetMine", new[] { "Moje", "Mine" } },
        { "presetColleague", new[] { "Kolega", "Colleague" } },
        { "vrEnv", new[] { "Prostředí", "Environment" } },
        { "vrEnvVirtual", new[] { "Virtuální sál", "Virtual hall" } },
        { "vrEnvScan", new[] { "Naskenovaná místnost", "Scanned room" } },
        { "vrPlace", new[] { "Umístit DJ stolek", "Place DJ table" } },
        { "vrOn", new[] { "zap", "on" } },
        { "vrOff", new[] { "vyp", "off" } },
        { "vrCalib", new[] { "Aktuální pozice ze SS = střed parketu (všechny hlavy)", "Current SS position = dance floor center (all heads)" } },
        { "vrCalibDone", new[] { "Nakalibrováno a uloženo: {0} světel", "Calibrated and saved: {0} fixtures" } },
        { "vrCalibNoData", new[] { "Nejdou data ze SoundSwitche – nastav v SS Stage Center", "No data from SoundSwitch – set Stage Center in SS" } },
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
        { "errPos", new[] { "{0}: neplatné umístění (čísla v metrech a stupních)", "{0}: invalid placement (numbers in metres and degrees)" } },
        { "addFixture", new[] { "+ Přidat", "+ Add" } },
        { "removeFixture", new[] { "Odebrat", "Remove" } },
        { "pickProfile", new[] { "Přidat světlo / vybavení:", "Add fixture / equipment:" } },
        { "rigFile", new[] { "Sestava jako soubor:", "Rig as a file:" } },
        { "rigExport", new[] { "Exportovat", "Export" } },
        { "rigImport", new[] { "Importovat…", "Import…" } },
        { "rigFolder", new[] { "Otevřít složku sestav", "Open rigs folder" } },
        { "rigExported", new[] { "Sestava uložena: {0}", "Rig saved: {0}" } },
        { "rigImported", new[] { "Načtena sestava {0} – potvrď OK nebo Použít", "Rig {0} loaded – confirm with OK or Apply" } },
        { "rigImportErr", new[] { "Sestavu nejde načíst: {0}", "Cannot load rig: {0}" } },
        { "rigNone", new[] { "Ve složce Rigs zatím nejsou žádné sestavy.", "There are no rigs in the Rigs folder yet." } },
        { "rigShareHint", new[] {
            "Soubor sestavy obsahuje světla, adresy, kalibraci, umístění i vlastní profily – stačí ho poslat a druhý si ho importuje.",
            "A rig file contains the fixtures, addresses, calibration, placement and custom profiles – send it and the other person imports it." } },
        { "profiles", new[] { "Profily světel:", "Fixture profiles:" } },
        { "profilesFolder", new[] { "Složka profilů", "Profiles folder" } },
        { "profilesReload", new[] { "Načíst znovu", "Reload" } },
        { "profilesReloaded", new[] { "Načteno {0} profilů", "{0} profiles loaded" } },
        { "profileErrors", new[] { "⚠ Chyby v profilech: {0}", "⚠ Profile errors: {0}" } },
        { "profileSource", new[] { "Profil: {0}", "Profile: {0}" } },
        { "srcBuiltin", new[] { "vestavěný", "built-in" } },
        { "srcEmbedded", new[] { "uložený v sestavě", "stored in the rig" } },
        { "profileEdit", new[] { "Uložit profil jako soubor k úpravě", "Save profile as an editable file" } },
        { "profileSaved", new[] { "Profil uložen: {0}", "Profile saved: {0}" } },
        { "profileMissing", new[] { "⚠ Chybí profil „{0}“ – přidej jeho soubor do složky profilů", "⚠ Missing profile \"{0}\" – add its file to the profiles folder" } },
        { "placement", new[] { "Umístění ve scéně", "Placement in the scene" } },
        { "customPos", new[] { "Vlastní umístění", "Custom placement" } },
        { "posXYZ", new[] { "Poloha X / Y / Z (m)", "Position X / Y / Z (m)" } },
        { "rotXYZ", new[] { "Natočení X / Y / Z (°)", "Rotation X / Y / Z (°)" } },
        { "posHint", new[] {
            "X doprava, Y nahoru, Z k hostům; 0 = střed parketu, zadní stěna je na Z = −6. Natočení X = sklon dolů.",
            "X right, Y up, Z towards the guests; 0 = dance floor centre, the back wall is at Z = −6. Rotation X = tilt down." } },
        { "posAuto", new[] { "Automaticky podle profilu", "Automatic (from profile)" } },

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
            "DMX Visualiser {0}\n\nVizualizace světel ze SoundSwitche přes Art-Net.\nSvětla se popisují profily (JSON) – vlastní přidáš do složky Profiles.\n\nVeksCZ  •  github.com/VeksCZ/XR_DMX_Visualiser",
            "DMX Visualiser {0}\n\nLighting visualisation from SoundSwitch over Art-Net.\nFixtures are described by profiles (JSON) – add your own to the Profiles folder.\n\nVeksCZ  •  github.com/VeksCZ/XR_DMX_Visualiser" } },
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
