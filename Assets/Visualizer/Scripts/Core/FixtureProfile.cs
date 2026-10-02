using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

// ============================================================================
// Profily světel a vybavení (JSON). Profil popisuje, jak světlo vypadá (části + tvary)
// a co znamenají jeho DMX kanály (režimy → ovládání → rozsahy hodnot).
// Vestavěné profily: Resources/Profiles/*.json. Vlastní: složka Profiles vedle .exe
// (Quest: persistentDataPath/Profiles) a profily vložené do settings.json / sestavy.
// Popis formátu: Profiles/README.md v repozitáři.
// ============================================================================

[Serializable]
public class FixtureProfile
{
    public string id;               // jednoznačný identifikátor, např. "adj-pocket-pro"
    public string name;             // název v seznamu
    public string manufacturer;
    public string kind;             // "light" (výchozí), "hazer", "prop"
    public string placement;        // výchozí místo ve scéně (stand, tableCorner, tube, uplight, trussTop, trussHang, trussCenter, table, speaker, floor)
    public float mountHeight;       // stand: výška světla na stativu (m)
    public string notes;
    public ProfilePart[] parts;     // svítící části
    public ProfileShape[] shapes;   // pevné tvary (tělo, třmeny, stojánek)
    public ProfileMode[] modes;     // DMX režimy
    public PropSpec prop;           // vybavení bez DMX (stůl, repro)

    [NonSerialized] public string source;   // "builtin", "embedded" nebo cesta k souboru

    public bool IsProp => kind == "prop";
    public bool IsHazer => kind == "hazer";
    public int ModeCount => modes != null ? modes.Length : 0;
    public ProfileMode Mode(int i) => modes == null || modes.Length == 0 ? null : modes[Mathf.Clamp(i, 0, modes.Length - 1)];
    public int Channels(int mode) { var m = Mode(mode); return m != null ? m.channels : 0; }
    public string Label => string.IsNullOrEmpty(manufacturer) || (name ?? "").StartsWith(manufacturer) ? name : manufacturer + " " + name;

    public bool HasMovers
    {
        get
        {
            if (parts != null) foreach (var p in parts) if (p != null && p.type == "mover") return true;
            return false;
        }
    }
}

[Serializable]
public class ProfilePart
{
    public string type;         // par, derby, mover, tube, laser, strobe, derbystrobe, helix
    public string name;
    public float[] pos;         // m, vůči světlu
    public float[] rot;         // ° (x, y, z)
    // par
    public string housing;      // round / box
    public float size;
    public float[] box;         // rozměry hranatého těla (š, v, hloubka)
    public int leds;            // 1 = jedna čočka, 3 = trojúhelník
    public float beam, field, beamLength, intensity, brightness;
    // derby / laser
    public float coverage;
    public int beams;           // derby: paprsků na barvu
    public int lensCols, lensRows;
    public int columns, rows;   // laser
    public bool mirror;         // opačný směr otáčení v demu
    // mover
    public float panRange, tiltRange;
    // tube
    public float length;
    public int segments;
    public float diameter;
    public string offColor;     // vzhled vypnutého difuzoru
}

[Serializable]
public class ProfileShape
{
    public string shape;        // cube / cylinder (válec: size = průměr, výška, průměr)
    public float[] pos;
    public float[] size;
    public float[] rot;
    public string color;        // "#rrggbb", prázdné = černé tělo
}

[Serializable]
public class ProfileMode
{
    public string name;
    public int channels;
    public ProfileControl[] controls;
}

// Jedna DMX funkce. ch / fine jsou čísla kanálů v rámci světla od 1 (jako v manuálech).
[Serializable]
public class ProfileControl
{
    public string fn;           // dimmer, red, green, blue, white, amber, uv, strobe, shutter, pan, tilt, ptSpeed,
                                // color, gobo, rotation, ledLevel, program, programSpeed, colorMacro, background,
                                // effect, effectSpeed, pixels, ptMacro, ptMacroSpeed, haze, none
    public int ch;
    public int fine;
    public int part;            // index části (parts[])
    public int sub;             // lišta helixu / LED stroba
    public int count;           // pixels: počet pixelů
    public string layout;       // pixels: pořadí barev, např. "RGBWA"
    public float min, max;      // rotation: max °/s; program: rychlost min–max
    public bool invert;         // ptSpeed: 0 = pomalu
    public ValueRange[] ranges;
}

[Serializable]
public class ValueRange
{
    public int from, to;
    public string type;         // podle funkce: off, strobe, open, closed, pulse, random, rampUp, rampDown, color, split,
                                // hue, rainbow, cycle, slots, gobo, gobos, effects, autoEffect, stop, cw, ccw,
                                // jump, fade, auto, derbyAuto, helixShow
    public string color, color2;
    public string[] colors;
    public float a, b, sat;
    public int step;
}

[Serializable]
public class PropSpec
{
    public string type;         // table / speaker
    public float[] size;        // m (š, v, h)
    public float standHeight;   // repro: výška stativu
    public string lycra;        // barva lycry, prázdné = bez lycry
    public string top;          // barva desky stolu
}

// Starší verze ukládaly typ jako číslo – jen pro převod starého settings.json
public enum FixtureType { GigBarMoveILS, BatteryPar, PixelTube, Hazer, PocketPro, BlackPar, DerbyStrobe, DoubleHelix,
    EventTable, DJBooth, Speaker12, Speaker14 }

// Jedno světlo / kus vybavení v sestavě. Universe je 1-based jako v SoundSwitchi (1 = Art-Net universe 0).
[Serializable]
public class FixtureEntry
{
    public string profile;      // id profilu
    public int mode;            // index režimu v profilu
    public string name;
    public int universe = 1;
    public int address = 1;
    public bool hidden;         // vypnuté ve scéně

    // Kalibrace moving headů – jen pro živá data ze SoundSwitche
    public float panOffset;
    public float tiltOffset;
    public bool invertPan;
    public bool invertTilt;

    // Vlastní umístění ve scéně (jinak podle profilu)
    public bool customPos;
    public Vector3 pos;
    public Vector3 rot;

    // starý formát (do verze 0.5.5)
    public FixtureType type;
    public bool mode40ch;

    public FixtureEntry() { }
    public FixtureEntry(string profileId, string n, int addr, int modeIndex = 0) { profile = profileId; name = n; address = addr; mode = modeIndex; }

    public FixtureEntry Clone() => (FixtureEntry)MemberwiseClone();

    public FixtureProfile Profile => ProfileLibrary.Get(profile);
    public int Channels { get { var p = Profile; return p == null || p.IsProp ? 0 : p.Channels(mode); } }
    public bool IsProp { get { var p = Profile; return p != null && p.IsProp; } }
    public bool HasMovers { get { var p = Profile; return p != null && p.HasMovers; } }

    public static string LegacyId(FixtureType t)
    {
        switch (t)
        {
            case FixtureType.GigBarMoveILS: return "chauvet-gigbar-move-ils";
            case FixtureType.BatteryPar: return "battery-par-rgbwauv";
            case FixtureType.PixelTube: return "pixel-tube-360";
            case FixtureType.Hazer: return "hurricane-haze";
            case FixtureType.PocketPro: return "adj-pocket-pro";
            case FixtureType.BlackPar: return "light4me-black-par-30x3";
            case FixtureType.DerbyStrobe: return "beamz-derbystrobe";
            case FixtureType.DoubleHelix: return "beamz-mhl820-double-helix";
            case FixtureType.EventTable: return "adj-pro-event-table-2";
            case FixtureType.DJBooth: return "vonyx-db3-pro";
            case FixtureType.Speaker12: return "fbt-promaxx-12a";
            default: return "fbt-promaxx-14a";
        }
    }

    // Převod záznamu ze starého formátu (typ jako číslo, mode40ch)
    public void Migrate()
    {
        if (!string.IsNullOrEmpty(profile)) return;
        profile = LegacyId(type);
        mode = mode40ch ? 1 : 0;
        mode40ch = false;
    }
}

// Knihovna profilů: vestavěné < vložené v nastavení < soubory ve složce Profiles
public static class ProfileLibrary
{
    static readonly Dictionary<string, FixtureProfile> builtin = new Dictionary<string, FixtureProfile>();
    static readonly Dictionary<string, FixtureProfile> all = new Dictionary<string, FixtureProfile>();
    static bool loaded;
    public static readonly List<string> Errors = new List<string>();

    public static string UserDir
    {
        get
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return Path.Combine(Path.GetDirectoryName(Application.dataPath), "Profiles");
#elif UNITY_EDITOR
            return Path.Combine(Path.GetDirectoryName(Application.dataPath), "Build", "Profiles");
#else
            return Path.Combine(Application.persistentDataPath, "Profiles");
#endif
        }
    }

    public static FixtureProfile Get(string id)
    {
        if (!loaded) Reload(null);
        return id != null && all.TryGetValue(id, out var p) ? p : null;
    }

    public static bool IsBuiltin(string id) { if (!loaded) Reload(null); return id != null && builtin.ContainsKey(id); }
    public static FixtureProfile Builtin(string id) { if (!loaded) Reload(null); return id != null && builtin.TryGetValue(id, out var p) ? p : null; }

    // Seřazené pro výběr v nastavení: nejdřív světla, pak hazery, nakonec vybavení
    public static List<FixtureProfile> All
    {
        get
        {
            if (!loaded) Reload(null);
            var l = new List<FixtureProfile>(all.Values);
            l.Sort((x, y) =>
            {
                int kx = x.IsProp ? 2 : x.IsHazer ? 1 : 0, ky = y.IsProp ? 2 : y.IsHazer ? 1 : 0;
                return kx != ky ? kx.CompareTo(ky) : string.Compare(x.Label, y.Label, StringComparison.OrdinalIgnoreCase);
            });
            return l;
        }
    }

    public static void Reload(List<FixtureProfile> embedded)
    {
        loaded = true;
        Errors.Clear();
        builtin.Clear();
        all.Clear();
        foreach (var ta in Resources.LoadAll<TextAsset>("Profiles"))
        {
            var p = Parse(ta.text, "builtin", out string err);
            if (p == null) { Errors.Add(ta.name + ": " + err); continue; }
            builtin[p.id] = p;
            all[p.id] = p;
        }
        if (embedded != null)
            foreach (var e in embedded)
                if (e != null && !string.IsNullOrEmpty(e.id) && Validate(e) == null) { e.source = "embedded"; all[e.id] = e; }
        try
        {
            if (Directory.Exists(UserDir))
                foreach (var f in Directory.GetFiles(UserDir, "*.json"))
                {
                    var p = Parse(File.ReadAllText(f), f, out string err);
                    if (p == null) { Errors.Add(Path.GetFileName(f) + ": " + err); continue; }
                    all[p.id] = p;
                }
        }
        catch (Exception e) { Errors.Add(e.Message); }
        foreach (var er in Errors) Debug.LogWarning("Profile: " + er);
    }

    public static FixtureProfile Parse(string json, string source, out string error)
    {
        error = null;
        FixtureProfile p;
        try { p = JsonUtility.FromJson<FixtureProfile>(json); }
        catch (Exception e) { error = e.Message; return null; }
        error = Validate(p);
        if (error != null) return null;
        p.source = source;
        return p;
    }

    public static string Validate(FixtureProfile p)
    {
        if (p == null) return "prázdný soubor";
        if (string.IsNullOrEmpty(p.id)) return "chybí \"id\"";
        if (string.IsNullOrEmpty(p.name)) p.name = p.id;
        if (p.IsProp) return p.prop == null ? "vybavení nemá \"prop\"" : null;
        if (p.modes == null || p.modes.Length == 0) return "chybí \"modes\"";
        foreach (var m in p.modes)
        {
            if (m == null || m.channels < 1 || m.channels > 512) return "režim musí mít 1–512 kanálů";
            if (m.controls == null) m.controls = new ProfileControl[0];
            foreach (var c in m.controls)
            {
                if (c == null) continue;
                if (c.ch < 1 || c.ch > m.channels) return "kanál " + c.ch + " (" + c.fn + ") je mimo režim " + m.name;
                if (c.fine > m.channels) return "jemný kanál " + c.fine + " je mimo režim " + m.name;
                if (!p.IsHazer && (p.parts == null || c.part < 0 || c.part >= p.parts.Length)) return "ovládání " + c.fn + " míří na neexistující část " + c.part;
            }
        }
        return null;
    }

    // Uloží profil do složky Profiles (název souboru podle id). Vrací cestu.
    public static string SaveToLibrary(FixtureProfile p)
    {
        Directory.CreateDirectory(UserDir);
        string path = Path.Combine(UserDir, SafeName(p.id) + ".json");
        File.WriteAllText(path, JsonUtility.ToJson(p, true));
        p.source = path;
        all[p.id] = p;
        return path;
    }

    public static string SafeName(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Trim();
    }
}

public static class ProfileUtil
{
    public static Vector3 V(float[] a, Vector3 def = default) =>
        a == null || a.Length < 3 ? def : new Vector3(a[0], a[1], a[2]);

    static readonly Dictionary<string, Color> colorCache = new Dictionary<string, Color>();

    // "#rrggbb" nebo "#rrggbbww" (poslední dvojice = bílá LED, uloží se do alfy)
    public static Color Hex(string s, Color def = default)
    {
        if (string.IsNullOrEmpty(s)) return def;
        if (colorCache.TryGetValue(s, out var c)) return c;
        string h = s.TrimStart('#');
        c = def;
        if ((h.Length == 6 || h.Length == 8) && uint.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v))
        {
            if (h.Length == 6) c = new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, 0f);
            else c = new Color(((v >> 24) & 255) / 255f, ((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);
        }
        colorCache[s] = c;
        return c;
    }

    static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();
    public static Material Mat(string color)
    {
        if (string.IsNullOrEmpty(color)) return VisUtil.BodyMat;
        if (matCache.TryGetValue(color, out var m) && m != null) return m;
        var c = Hex(color, new Color(0.06f, 0.06f, 0.07f));
        c.a = 1f;
        return matCache[color] = VisUtil.LitMat(c);
    }

    public static ValueRange Find(ValueRange[] ranges, int v)
    {
        if (ranges == null) return null;
        foreach (var r in ranges) if (r != null && v >= r.from && v <= r.to) return r;
        return null;
    }

    // Poloha hodnoty v rozsahu 0–1
    public static float Pos(ValueRange r, int v) => r.to > r.from ? Mathf.Clamp01((v - r.from) / (float)(r.to - r.from)) : 0f;

    // Pořadí hodnoty v rozsahu, rozděleném na n stejných dílů (nebo po step hodnotách)
    public static int Slot(ValueRange r, int v, int n)
    {
        if (n <= 1) return 0;
        int k = r.step > 0 ? (v - r.from) / r.step : (v - r.from) * n / Mathf.Max(1, r.to - r.from + 1);
        return Mathf.Clamp(k, 0, n - 1);
    }
}
