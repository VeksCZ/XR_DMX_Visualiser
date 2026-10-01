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
        { "settings", new[] { "Nastavení", "Settings" } },
        { "help", new[] { "Nápověda", "Help" } },

        // Soubor
        { "saveSettings", new[] { "Uložit nastavení", "Save settings" } },
        { "resetPatch", new[] { "Obnovit výchozí patch", "Reset patch to defaults" } },
        { "quit", new[] { "Ukončit", "Quit" } },

        // Zobrazení
        { "camera", new[] { "Kamera", "Camera" } },
        { "camHost", new[] { "Host", "Guest" } },
        { "camFloor", new[] { "Parket", "Dance floor" } },
        { "camDJ", new[] { "DJ", "DJ" } },
        { "camTop", new[] { "Shora", "Top" } },
        { "fullscreen", new[] { "Celá obrazovka", "Fullscreen" } },
        { "statusBar", new[] { "Stavová lišta", "Status bar" } },
        { "hideUI", new[] { "Skrýt rozhraní", "Hide interface" } },

        // Nastavení
        { "lightsMenu", new[] { "Světla a DMX adresy…", "Fixtures & DMX addresses…" } },
        { "lightsTitle", new[] { "Světla a DMX adresy", "Fixtures & DMX addresses" } },
        { "artnetMenu", new[] { "Art-Net…", "Art-Net…" } },
        { "artnetTitle", new[] { "Art-Net", "Art-Net" } },
        { "hazeMenu", new[] { "Atmosféra (haze)…", "Atmosphere (haze)…" } },
        { "hazeTitle", new[] { "Atmosféra", "Atmosphere" } },
        { "language", new[] { "Jazyk", "Language" } },

        // Nápověda
        { "controlsMenu", new[] { "Ovládání…", "Controls…" } },
        { "controlsTitle", new[] { "Ovládání", "Controls" } },
        { "aboutMenu", new[] { "O aplikaci…", "About…" } },
        { "aboutTitle", new[] { "O aplikaci", "About" } },

        // Stavová lišta
        { "live", new[] { "LIVE", "LIVE" } },
        { "demo", new[] { "DEMO", "DEMO" } },
        { "forced", new[] { "VYNUCENÉ DEMO", "FORCED DEMO" } },
        { "source", new[] { "Zdroj", "Source" } },
        { "listen", new[] { "Naslouchá", "Listening" } },
        { "noData", new[] { "žádná data", "no data" } },
        { "universe", new[] { "Universe", "Universe" } },

        // Art-Net okno
        { "receivingFrom", new[] { "Přijímám od {0}", "Receiving from {0}" } },
        { "packetsInfo", new[] { "{0} paketů  •  {1} pkt/s", "{0} packets  •  {1} pkt/s" } },
        { "pollReplies", new[] { "Odpovědí na ArtPoll: {0}", "ArtPoll replies: {0}" } },
        { "forceDemo", new[] { "Vynutit demo (ignorovat Art-Net)", "Force demo (ignore Art-Net)" } },
        { "artnetHint", new[] {
            "SoundSwitch najde visualizér sám jako Art-Net zařízení „DJ Visualizer“. Při prvním spuštění povol aplikaci ve firewallu (privátní síť). Nespouštěj zároveň Play v Unity.",
            "SoundSwitch finds the visualiser by itself as the Art-Net device \"DJ Visualizer\". On first launch allow the app in the firewall (private network). Don't run Unity Play mode at the same time." } },

        // Haze okno
        { "hazeLive", new[] { "Haze v sále: {0} %", "Haze in the room: {0} %" } },
        { "hazeDemo", new[] { "Viditelnost paprsků: {0}", "Beam visibility: {0}" } },
        { "hazeHint", new[] {
            "Haze se nekreslí jako oblak – zviditelňuje paprsky. Při živých datech přibývá podle kanálu hazeru a pomalu mizí.",
            "Haze isn't drawn as a cloud – it makes the beams visible. With live data it builds up from the hazer channel and slowly fades." } },

        // Světla okno
        { "patchHint", new[] {
            "Adresy jako v SoundSwitchi. N-té světlo daného typu v seznamu řídí N-té světlo ve scéně.",
            "Addresses as in SoundSwitch. The Nth fixture of a type in the list drives the Nth fixture of that type in the scene." } },
        { "address", new[] { "Adresa", "Address" } },
        { "mode40", new[] { "40ch mód (8 pixelů), jinak 12ch", "40ch mode (8 pixels), otherwise 12ch" } },
        { "overlap", new[] { "⚠ Překrývá se s: {0}", "⚠ Overlaps with: {0}" } },
        { "apply", new[] { "Použít a uložit", "Apply & save" } },
        { "discard", new[] { "Zahodit změny", "Discard changes" } },
        { "defaults", new[] { "Výchozí patch", "Default patch" } },
        { "saved", new[] { "Uloženo", "Saved" } },
        { "discarded", new[] { "Změny zahozeny", "Changes discarded" } },
        { "defaultsLoaded", new[] { "Načten výchozí patch – ulož tlačítkem Použít", "Default patch loaded – press Apply to save" } },
        { "errUniverse", new[] { "{0}: universe musí být 1–16", "{0}: universe must be 1–16" } },
        { "errAddress", new[] { "{0}: adresa musí být 1–{1}", "{0}: address must be 1–{1}" } },

        // Ovládání / O aplikaci
        { "controlsText", new[] {
            "Pravé tlačítko myši + pohyb myší – rozhlížení\nW A S D – pohyb,  Q / E – dolů / nahoru,  Shift – rychleji\n1 – 4 – pohledy kamery\nF11 – celá obrazovka\nH – skrýt / zobrazit rozhraní\nEsc – zavřít menu a okna",
            "Right mouse button + mouse – look around\nW A S D – move,  Q / E – down / up,  Shift – faster\n1 – 4 – camera views\nF11 – fullscreen\nH – hide / show interface\nEsc – close menus and windows" } },
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
