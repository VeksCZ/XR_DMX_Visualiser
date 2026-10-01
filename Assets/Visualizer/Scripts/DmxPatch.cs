using System;
using System.Collections.Generic;
using UnityEngine;

public enum FixtureType { GigBarMoveILS, BatteryPar, PixelTube, Hazer, PocketPro }

// Jedno světlo v patchi. Universe je 1-based jako v SoundSwitchi (1 = Art-Net universe 0).
[Serializable]
public class FixtureEntry
{
    public FixtureType type;
    public string name;
    public int universe = 1;
    public int address = 1;
    public bool mode40ch;   // jen pro tuby (režim kanálů index 1)
    public bool hidden;     // vypnuté ve scéně (zatržítko v Nastavení → Světla)

    // Kalibrace moving headů (GigBar, Pocket Pro) – jen pro živá data ze SoundSwitche
    public float panOffset;  // ° přičtené k panu (kde má hlava „dopředu“)
    public float tiltOffset; // ° přičtené k tiltu
    public bool invertPan;
    public bool invertTilt;

    public static bool HasMovers(FixtureType t) => t == FixtureType.GigBarMoveILS || t == FixtureType.PocketPro;

    public FixtureEntry() { }
    public FixtureEntry(FixtureType t, string n, int addr) { type = t; name = n; address = addr; }

    // Dostupné režimy kanálů pro select v nastavení
    public static string[] Modes(FixtureType t)
    {
        switch (t)
        {
            case FixtureType.GigBarMoveILS: return new[] { "52ch" };
            case FixtureType.PocketPro: return new[] { "13ch" };
            case FixtureType.BatteryPar: return new[] { "10ch (Mode 2)" };
            case FixtureType.PixelTube: return new[] { "12ch (Mode 1)", "40ch (Mode 2, 8 px)" };
            default: return new[] { "1ch" };
        }
    }

    public static int Channels(FixtureType t, bool ch40 = false)
    {
        switch (t)
        {
            case FixtureType.GigBarMoveILS: return 52;
            case FixtureType.PocketPro: return 13;
            case FixtureType.BatteryPar: return 10;
            case FixtureType.PixelTube: return ch40 ? 40 : 12;
            default: return 1;
        }
    }

    public static string TypeLabel(FixtureType t)
    {
        switch (t)
        {
            case FixtureType.GigBarMoveILS: return "GigBar Move ILS (EU)";
            case FixtureType.PocketPro: return "ADJ Pocket Pro";
            case FixtureType.BatteryPar: return "Battery Par";
            case FixtureType.PixelTube: return "Pixel Tube 360";
            default: return "Hazer";
        }
    }

    public static List<FixtureEntry> Defaults() => new List<FixtureEntry>
    {
        // Kalibrace spočítaná z pozice SS „Stage Center“ (2502.ssproj) → střed parketu
        new FixtureEntry(FixtureType.GigBarMoveILS, "GigBar", 200) { panOffset = 102f, tiltOffset = -1f },
        new FixtureEntry(FixtureType.PocketPro, "Pocket Pro L", 33) { panOffset = 10f, tiltOffset = 43f },
        new FixtureEntry(FixtureType.PocketPro, "Pocket Pro R", 46) { panOffset = -44f, tiltOffset = 45f },
        new FixtureEntry(FixtureType.BatteryPar, "Uplight 1", 110),
        new FixtureEntry(FixtureType.BatteryPar, "Uplight 2", 120),
        new FixtureEntry(FixtureType.BatteryPar, "Uplight 3", 130),
        new FixtureEntry(FixtureType.BatteryPar, "Uplight 4", 140),
        new FixtureEntry(FixtureType.PixelTube, "Tuba 1", 300),
        new FixtureEntry(FixtureType.PixelTube, "Tuba 2", 350),
        new FixtureEntry(FixtureType.PixelTube, "Tuba 3", 400),
        new FixtureEntry(FixtureType.PixelTube, "Tuba 4", 450),
        new FixtureEntry(FixtureType.Hazer, "Hurricane Haze 1DX", 100),
    };
}

// Mapování DMX kanálů na světla ve scéně podle profilů ze SoundSwitch projektu 2502.ssproj.
// N-tý záznam daného typu ovládá N-té světlo daného typu ve scéně.
public class DmxPatch : MonoBehaviour
{
    public ArtNetReceiver artnet;
    public SceneBuilder scene;
    public DemoDriver demo;
    [Tooltip("Ignorovat Art-Net a pouštět demo")]
    public bool forceDemo;

    public List<FixtureEntry> fixtures = FixtureEntry.Defaults();

    [Header("Hazer – simulace hustoty v sále")]
    [Tooltip("Kolik hazu přibude za sekundu při plném výkonu")]
    public float hazeBuildRate = 0.08f;
    [Tooltip("Jak rychle haze mizí (podíl za sekundu)")]
    public float hazeDecay = 0.01f;
    [Range(0, 1)] public float hazeDensity = 0.3f; // počáteční stav sálu
    public float hazeToBeam = 2.5f;                // převod hustoty na viditelnost paprsků

    readonly byte[][] uniBuf = new byte[16][];
    readonly int[] uniFrame = new int[16];

    void Start()
    {
        if (artnet == null) artnet = FindFirstObjectByType<ArtNetReceiver>();
        if (scene == null) scene = FindFirstObjectByType<SceneBuilder>();
        if (demo == null) demo = FindFirstObjectByType<DemoDriver>();
    }

    void Update()
    {
        if (artnet == null || scene == null || scene.gigbar == null) return;
        bool live = artnet.HasData && !forceDemo;
        if (demo != null) demo.enabled = !live; // bez Art-Netu (nebo s vynuceným demem) běží demo
        if (!live) return;

        int pars = 0, tubes = 0, pockets = 0;
        bool gigbarDone = false, hazerDone = false;
        float hazeOut = 0f;
        foreach (var f in fixtures)
        {
            if (f == null) continue;
            var d = Uni(f.universe - 1);
            if (d == null) continue;
            int a = f.address - 1;
            bool on = !f.hidden;    // vypnuté světlo se počítá do pořadí, ale neovládá se
            switch (f.type)
            {
                case FixtureType.GigBarMoveILS:
                    if (!gigbarDone) { if (on) ApplyGigbar(d, a, f); gigbarDone = true; }
                    break;
                case FixtureType.BatteryPar:
                    if (on && pars < scene.uplights.Length) ApplyBatteryPar(d, a, scene.uplights[pars]);
                    pars++;
                    break;
                case FixtureType.PixelTube:
                    if (on && tubes < scene.tubes.Length) ApplyTube(d, a, scene.tubes[tubes], f.mode40ch);
                    tubes++;
                    break;
                case FixtureType.Hazer:
                    if (!hazerDone) { if (on) hazeOut = F(d, a); hazerDone = true; }
                    break;
                case FixtureType.PocketPro:
                    if (on && scene.pockets != null && pockets < scene.pockets.Length) ApplyPocketPro(d, a, scene.pockets[pockets], f);
                    pockets++;
                    break;
            }
        }

        // Hazer: kouř se nevykresluje jako oblak, jen „hromadí“ v sále a zviditelňuje paprsky.
        hazeDensity += hazeOut * hazeBuildRate * Time.deltaTime;
        hazeDensity -= hazeDensity * hazeDecay * Time.deltaTime;
        hazeDensity = Mathf.Clamp01(hazeDensity);
        scene.haze = Mathf.Clamp(0.05f + hazeDensity * hazeToBeam, 0f, 3f);
    }

    // Každé universe se z přijímače kopíruje nejvýš jednou za snímek.
    byte[] Uni(int u)
    {
        if (u < 0 || u >= uniBuf.Length) return null;
        if (uniBuf[u] == null) { uniBuf[u] = new byte[512]; uniFrame[u] = -1; }
        if (uniFrame[u] != Time.frameCount)
        {
            artnet.GetUniverse(u, uniBuf[u]);
            uniFrame[u] = Time.frameCount;
        }
        return uniBuf[u];
    }

    static float F(byte[] d, int ch) => ch >= 0 && ch < 512 ? d[ch] / 255f : 0f;
    static int I(byte[] d, int ch) => ch >= 0 && ch < 512 ? d[ch] : 0;

    // Obecný převod strobe kanálu na Hz (0-9 = bez strobe). Přesné tabulky se liší podle světla.
    static float StrobeHz(int v) => v < 10 ? 0f : Mathf.Lerp(1f, 20f, (v - 10) / 245f);

    // GigBar strobe kanály (manuál): 0-250 rychlost pomalu→rychle, 251-255 do zvuku.
    // 0 bereme jako „bez stroba“, jinak by vše pořád blikalo.
    static float GigStrobeHz(int v) => v == 0 ? 0f : v > 250 ? 8f : Mathf.Lerp(1f, 20f, v / 250f);

    // Rotace (derby, laser): 0 stop, 1-127 CW pomalu→rychle, 128 stop, 129-255 CCW pomalu→rychle
    static float Rotation(int v, float maxDegPerSec)
    {
        if (v == 0 || v == 128) return 0f;
        if (v < 128) return Mathf.Lerp(0.1f, 1f, v / 127f) * maxDegPerSec;
        return -Mathf.Lerp(0.1f, 1f, (v - 128) / 127f) * maxDegPerSec;
    }

    static Color Uv(float uv) => new Color(0.35f, 0f, 1f) * uv * 0.6f;

    // ---------------- GigBar Move ILS (EU), 52 kanálů (offsety 0-based, ověřeno manuálem) ----------------
    // Par 1 0-6 / Par 2 7-13: R,G,B,A,W,UV,Strobe
    // Derby 1 14-19 / Derby 2 20-25: R,G,B,W,Strobe,Rotation
    // Flash 26-29 jas 4 bílých LED, 30 strobe
    // Laser 31 barva, 32 strobe, 33 rotace
    // Spot 1 34-42 / Spot 2 43-51: Pan, Pan fine, Tilt, Tilt fine, Speed, Color, Gobo, Dimmer, Shutter
    void ApplyGigbar(byte[] d, int b, FixtureEntry f)
    {
        var g = scene.gigbar;
        ApplyBarPar(d, b + 0, g.parL);
        ApplyBarPar(d, b + 7, g.parR);
        ApplyDerby(d, b + 14, g.derbyL);
        ApplyDerby(d, b + 20, g.derbyR);

        for (int i = 0; i < 4; i++) g.strobe.ledLevels[i] = F(d, b + 26 + i);
        g.strobe.dimmer = 1f;
        g.strobe.strobeHz = GigStrobeHz(I(d, b + 30));

        // Laser: 0-5 vyp, pak R / G / B / R+G / R+B / G+B / RGB po 36 hodnotách
        int lc = I(d, b + 31);
        Color[] lcol = { Color.red, Color.green, Color.blue, Color.yellow, Color.magenta, Color.cyan, Color.white };
        g.laser.intensity = lc <= 5 ? 0f : 1f;
        if (lc > 5) g.laser.color = lcol[Mathf.Min((lc - 6) / 36, 6)];
        g.laser.strobeHz = GigStrobeHz(I(d, b + 32));
        g.laser.patternSpeed = Rotation(I(d, b + 33), 90f);

        ApplyMover(d, b + 34, g.headL, f);
        ApplyMover(d, b + 43, g.headR, f);
    }

    void ApplyBarPar(byte[] d, int b, ParLight p)
    {
        Color c = VisUtil.RGBWA(F(d, b), F(d, b + 1), F(d, b + 2), F(d, b + 4), F(d, b + 3)) + Uv(F(d, b + 5));
        float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        p.dimmer = Mathf.Clamp01(m);
        p.color = m > 0.001f ? c / m : Color.black;
        p.strobeHz = GigStrobeHz(I(d, b + 6));
    }

    void ApplyDerby(byte[] d, int b, Derby dr)
    {
        dr.red = F(d, b); dr.green = F(d, b + 1); dr.blue = F(d, b + 2); dr.white = F(d, b + 3);
        dr.strobeHz = GigStrobeHz(I(d, b + 4));
        dr.rotationSpeed = Rotation(I(d, b + 5), 150f);
    }

    // Barevné kolo GigBaru podle manuálu (EU Rev. 13)
    static Color MoverColor(int v)
    {
        if (v <= 6) return Color.white;
        if (v <= 13) return Color.red;
        if (v <= 20) return new Color(1f, 0.5f, 0.05f);     // oranžová
        if (v <= 27) return Color.yellow;
        if (v <= 34) return Color.green;
        if (v <= 41) return Color.blue;
        if (v <= 48) return new Color(1f, 0.8f, 0.6f);      // CTO
        if (v <= 55) return Color.cyan;
        if (v <= 62) return Color.magenta;
        if (v <= 64) return new Color(0.7f, 1f, 0.2f);      // lime
        if (v <= 189) return Color.HSVToRGB((v - 65) / 125f, 0.8f, 1f);              // index (přibližně)
        if (v >= 222 && v <= 223) return Color.white;
        return Color.HSVToRGB(Mathf.Repeat(Time.time * 0.3f, 1f), 1f, 1f);           // scroll
    }

    // Shutter: 0-3 zavřeno, 4-7 otevřeno, 8-76 strobe, 77-145 pulse, 146-215 random, 216-255 otevřeno
    static void Shutter(int v, MovingHead h, ref float dim)
    {
        h.strobeHz = 0f;
        if (v <= 3) dim = 0f;
        else if (v >= 8 && v <= 76) h.strobeHz = Mathf.Lerp(1f, 20f, (v - 8) / 68f);
        else if (v >= 77 && v <= 145) dim *= 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(2f, 15f, (v - 77) / 68f));
        else if (v >= 146 && v <= 215) h.strobeHz = UnityEngine.Random.Range(2f, 15f);
    }

    // Pan/tilt 16bit + kalibrace z nastavení (posun „dopředu“, otočení směrů)
    static void PanTilt(byte[] d, int b, MovingHead h, FixtureEntry f)
    {
        float pan = ((I(d, b) << 8) | I(d, b + 1)) / 65535f;
        float tilt = ((I(d, b + 2) << 8) | I(d, b + 3)) / 65535f;
        if (f != null)
        {
            if (f.invertPan) pan = 1f - pan;
            if (f.invertTilt) tilt = 1f - tilt;
            pan += f.panOffset / Mathf.Max(1f, h.panRange);
            tilt += f.tiltOffset / Mathf.Max(1f, h.tiltRange);
        }
        h.pan = pan;
        h.tilt = tilt;
    }

    // Kalibrace "aktuální pozice ze SS = bod target" (typicky střed parketu).
    // Spočítá posun panu a tiltu tak, aby aktuální DMX hodnoty mířily do targetu.
    // U GigBaru se průměruje přes obě hlavy (SS posílá oběma skoro stejné hodnoty).
    // typeIndex = pořadí záznamu mezi světly stejného typu (Pocket Pro L = 0, R = 1).
    public bool SolveOffsets(FixtureEntry f, int typeIndex, Vector3 target, out float panOffset, out float tiltOffset)
    {
        panOffset = tiltOffset = 0f;
        if (f == null || scene == null || !FixtureEntry.HasMovers(f.type) || artnet == null || !artnet.HasData) return false;
        var d = Uni(f.universe - 1);
        if (d == null) return false;
        var heads = new List<MovingHead>();
        var bases = new List<int>();
        if (f.type == FixtureType.GigBarMoveILS)
        {
            if (scene.gigbar == null) return false;
            heads.Add(scene.gigbar.headL); bases.Add(f.address - 1 + 34);
            heads.Add(scene.gigbar.headR); bases.Add(f.address - 1 + 43);
        }
        else
        {
            if (scene.pockets == null || typeIndex < 0 || typeIndex >= scene.pockets.Length) return false;
            heads.Add(scene.pockets[typeIndex]); bases.Add(f.address - 1);
        }
        int n = 0;
        for (int i = 0; i < heads.Count; i++)
        {
            var h = heads[i];
            if (h == null) continue;
            int b = bases[i];
            float pan = ((I(d, b) << 8) | I(d, b + 1)) / 65535f;
            float tilt = ((I(d, b + 2) << 8) | I(d, b + 3)) / 65535f;
            if (f.invertPan) pan = 1f - pan;
            if (f.invertTilt) tilt = 1f - tilt;
            float p0 = (pan - 0.5f) * h.panRange, t0 = (tilt - 0.5f) * h.tiltRange;
            h.AimAngles(target, out float yaw, out float tlt);
            // Dvě řešení (tilt t / yaw, nebo tilt −t / yaw+180) a násobky 360° – bereme nejmenší posun.
            float bestCost = float.MaxValue, bp = 0f, bt = 0f;
            for (int s = 0; s < 2; s++)
            {
                float T = s == 0 ? tlt : -tlt, Y = s == 0 ? yaw : yaw + 180f;
                for (int k = -2; k <= 2; k++)
                {
                    float po = Y + 360f * k - p0, to = T - t0;
                    float c = Mathf.Abs(po) + Mathf.Abs(to);
                    if (c < bestCost) { bestCost = c; bp = po; bt = to; }
                }
            }
            panOffset += bp; tiltOffset += bt; n++;
        }
        if (n == 0) return false;
        panOffset /= n; tiltOffset /= n;
        return true;
    }

    // Živý odečet pro dialog Nastavení: surové 16bit hodnoty pan/tilt první hlavy světla
    public bool ReadPanTilt(FixtureEntry f, out int pan, out int tilt)
    {
        pan = tilt = 0;
        if (artnet == null || !artnet.HasData || f == null || !FixtureEntry.HasMovers(f.type)) return false;
        var d = Uni(f.universe - 1);
        if (d == null) return false;
        int b = f.address - 1 + (f.type == FixtureType.GigBarMoveILS ? 34 : 0);
        pan = (I(d, b) << 8) | I(d, b + 1);
        tilt = (I(d, b + 2) << 8) | I(d, b + 3);
        return true;
    }

    void ApplyMover(byte[] d, int b, MovingHead h, FixtureEntry f)
    {
        PanTilt(d, b, h, f);
        // Speed: 0 = nejrychleji (typicky u Chauvet), 255 = nejpomaleji
        float spd = Mathf.Lerp(1f, 0.1f, F(d, b + 4));
        h.maxPanSpeed = 300f * spd;
        h.maxTiltSpeed = 200f * spd;
        h.color = MoverColor(I(d, b + 5));
        // Gobo: 0-5 open, gobo 1-9 po 6 hodnotách (9 = 54-63), 64-189 shake, 190+ scroll
        int gv = I(d, b + 6);
        if (gv <= 5) h.gobo = 0;
        else if (gv <= 63) h.gobo = Mathf.Min((gv - 6) / 6 + 1, 9);
        else if (gv <= 189) h.gobo = 9;
        else h.gobo = (int)Mathf.Repeat(Time.time * 2f, 10f);
        float dim = F(d, b + 7);
        Shutter(I(d, b + 8), h, ref dim);
        h.dimmer = dim;
    }

    // ---------------- Wireless Battery LED Stage Up Par, Mode 2 (10ch) ----------------
    // 0 Intensity, 1 R, 2 G, 3 B, 4 W, 5 A, 6 UV, 7 Strobe, 8 Macro, 9 Macro speed
    void ApplyBatteryPar(byte[] d, int b, ParLight p)
    {
        if (p == null) return;
        Color c = VisUtil.RGBWA(F(d, b + 1), F(d, b + 2), F(d, b + 3), F(d, b + 4), F(d, b + 5)) + Uv(F(d, b + 6));
        float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        p.color = m > 0.001f ? c / m : Color.black;
        p.dimmer = F(d, b) * Mathf.Clamp01(m);
        p.strobeHz = StrobeHz(I(d, b + 7));
    }

    // ---------------- ADJ Pocket Pro, 13ch (podle Open Fixture Library) ----------------
    // 0-1 Pan 16bit (540°), 2-3 Tilt 16bit (230°), 4 Color, 5 Gobo, 6 Shutter, 7 Dimmer,
    // 8 P/T makra, 9 rychlost maker, 10 křivky dimmeru, 11 P/T speed (0 = rychle), 12 speciální
    static readonly Color[] PocketColors =
    {
        Color.white,                       // open
        Color.red,
        new Color(1f, 0.5f, 0.05f),        // oranžová
        Color.yellow,
        Color.green,
        new Color(0.45f, 0.1f, 1f),        // UV
        Color.blue,
        new Color(1f, 0.4f, 0.75f),        // růžová
    };

    static Color PocketColor(int v)
    {
        if (v <= 56) return PocketColors[v <= 7 ? 0 : Mathf.Min(1 + (v - 8) / 7, 7)];   // 0-7 open, pak po 7
        if (v <= 127)                      // půlené barvy mezi sloty
        {
            int i = Mathf.Min((v - 57) / 10, 6);
            return Color.Lerp(PocketColors[i], PocketColors[i + 1], 0.5f);
        }
        if (v >= 190 && v <= 193) return Color.white;
        return Color.HSVToRGB(Mathf.Repeat(Time.time * 0.3f, 1f), 1f, 1f); // rotace kola
    }

    void ApplyPocketPro(byte[] d, int b, MovingHead h, FixtureEntry f)
    {
        if (h == null) return;
        PanTilt(d, b, h, f);
        float spd = Mathf.Lerp(1f, 0.1f, F(d, b + 11));
        h.maxPanSpeed = 300f * spd;
        h.maxTiltSpeed = 200f * spd;
        h.color = PocketColor(I(d, b + 4));

        int gv = I(d, b + 5);                                  // 8 slotů po 8 hodnotách, slot 1 = open
        if (gv <= 63) h.gobo = gv / 8;
        else if (gv <= 127) h.gobo = (gv - 64) / 8;            // shake – bereme jako statické gobo
        else h.gobo = (int)Mathf.Repeat(Time.time * 2f, 8f);   // rotace kola

        float dim = F(d, b + 7);
        int sh = I(d, b + 6);
        h.strobeHz = 0f;
        if (sh <= 7) dim = 0f;                                                     // zavřeno
        else if (sh >= 16 && sh <= 131) h.strobeHz = Mathf.Lerp(1f, 20f, (sh - 16) / 115f);
        else if (sh >= 140 && sh <= 181) dim *= Mathf.Repeat(Time.time * Mathf.Lerp(0.5f, 4f, (sh - 140) / 41f), 1f);       // ramp up
        else if (sh >= 190 && sh <= 231) dim *= 1f - Mathf.Repeat(Time.time * Mathf.Lerp(0.5f, 4f, (sh - 190) / 41f), 1f);  // ramp down
        else if (sh >= 240 && sh <= 247) h.strobeHz = UnityEngine.Random.Range(2f, 15f);
        h.dimmer = dim;
    }

    // ---------------- LED Pixel Tube 360 RGBWA ----------------
    // Mode 1 (12ch): 0 Intensity, 1 Strobe, 2 Color macro, 3 Background, 4 Base tint,
    //                5 Effects, 6 Effect speed, 7 R, 8 G, 9 B, 10 W, 11 A
    // Mode 2 (40ch): 8 pixelů × R,G,B,W,A
    // Rozsahy podle manuálu tuby: Color 0-10 nic / 11-255 barvy, Background 0 = nic,
    // Effect 0-10 vypnuto, 11-214 = 60 efektů, 215-220 všechny dokola, 221-255 do zvuku.
    // Názvy jednotlivých 60 efektů manuál neuvádí – rodiny níže jsou přiblížení, ověřené jsou
    // 138 = 4 segmenty + 4 mezery jedou shora dolů, 200 = plynoucí duha.
    void ApplyTube(byte[] d, int b, PixelTube t, bool mode40ch)
    {
        if (t == null || t.pixels == null) return;
        int n = t.pixels.Length;
        if (!mode40ch)
        {
            Color fg = VisUtil.RGBWA(F(d, b + 7), F(d, b + 8), F(d, b + 9), F(d, b + 10), F(d, b + 11));
            if (TubeMacro(I(d, b + 2), out Color mc)) fg = mc;
            TubeMacro(I(d, b + 3), out Color bg); // bez barvy pozadí = černá
            float gate = VisUtil.StrobeGate(StrobeHz(I(d, b + 1)));
            int fx = I(d, b + 5);
            if (fx <= 10) for (int i = 0; i < n; i++) t.pixels[i] = fg;
            else
            {
                if (fg.maxColorComponent < 0.01f) fg = Color.white; // efekt bez barvy svítí bíle
                int idx = fx <= 214 ? Mathf.Min((fx - 11) * 60 / 204, 59)
                        : Mathf.FloorToInt(Time.time / 8f) % 60;        // všechny efekty / do zvuku: střídat
                TubeEffect(t.pixels, idx, F(d, b + 6), fg, bg);
            }
            for (int i = 0; i < n; i++) t.pixels[i] *= gate;
            t.master = F(d, b);
        }
        else
        {
            // 8 DMX pixelů roztažených na segmenty modelu
            for (int i = 0; i < n; i++)
            {
                int o = b + Mathf.Min(i * 8 / n, 7) * 5;
                t.pixels[i] = VisUtil.RGBWA(F(d, o), F(d, o + 1), F(d, o + 2), F(d, o + 3), F(d, o + 4));
            }
            t.master = 1f;
        }
    }

    // Barevná makra tuby (pořadí odhadnuté, 11-255 rozděleno rovnoměrně)
    static readonly Color[] TubeColors =
    {
        Color.red, new Color(1f, 0.45f, 0f), Color.yellow, new Color(0.5f, 1f, 0f), Color.green,
        new Color(0f, 1f, 0.6f), Color.cyan, new Color(0f, 0.5f, 1f), Color.blue, new Color(0.5f, 0f, 1f),
        Color.magenta, new Color(1f, 0.3f, 0.6f), Color.white, new Color(1f, 0.8f, 0.55f), new Color(1f, 0.6f, 0.1f),
    };
    static bool TubeMacro(int v, out Color c)
    {
        c = Color.black;
        if (v <= 10) return false;
        c = TubeColors[Mathf.Min((v - 11) * TubeColors.Length / 245, TubeColors.Length - 1)];
        return true;
    }

    // Efekty tuby: 6 rodin po 10 variantách (index 0-59). Liché varianty jedou shora dolů.
    // speed 0-1 (kanál Effect speed, víc = rychleji). pixels[0] je dole.
    static void TubeEffect(Color[] px, int idx, float speed, Color fg, Color bg)
    {
        int n = px.Length;
        int fam = idx / 10, v = idx % 10;
        float dir = v % 2 == 1 ? 1f : -1f;                        // +1 = posun dolů
        float ph = Time.time * Mathf.Lerp(0.15f, 2.5f, speed);     // cykly za sekundu
        for (int i = 0; i < n; i++)
        {
            float u = (i + 0.5f) / n;                              // 0 dole, 1 nahoře
            Color c;
            switch (fam)
            {
                case 0: // běžící světlo s ocasem (délka podle varianty)
                {
                    float head = Mathf.Repeat(-dir * ph, 1f);
                    float dist = Mathf.Repeat((u - head) * dir, 1f); // vzdálenost za hlavou
                    float tail = 0.08f + 0.06f * (v / 2);
                    c = Color.Lerp(bg, fg, Mathf.Clamp01(1f - dist / tail));
                    break;
                }
                case 1: // stírání: barva se nasouvá a zase odjíždí
                {
                    float p = Mathf.Repeat(ph * 0.5f, 1f) * 2f;
                    float edge = p < 1f ? p : p - 1f;
                    float pos = dir > 0 ? 1f - u : u;
                    bool on = p < 1f ? pos < edge : pos >= edge;
                    c = on ? fg : bg;
                    break;
                }
                case 2: // blok jezdí tam a zpět (ping-pong)
                {
                    float center = Mathf.PingPong(ph * 2f, 1f);
                    float w = 0.12f + 0.04f * (v / 2);
                    c = Mathf.Abs(u - center) < w ? fg : bg;
                    break;
                }
                case 3: // segmenty s mezerami jedou po tubě (2 nebo 4 segmenty)
                {
                    int blocks = v < 4 ? 2 : 4;
                    c = Mathf.Repeat(u * blocks + dir * ph, 1f) < 0.5f ? fg : bg;
                    break;
                }
                case 4: // dýchání / jiskření
                    if (v < 5) c = Color.Lerp(bg, fg, 0.5f + 0.5f * Mathf.Sin(ph * Mathf.PI * 2f));
                    else
                    {
                        float r = Mathf.Repeat(Mathf.Sin((i + 1) * 12.9898f + Mathf.Floor(ph * 4f) * 78.233f) * 43758.55f, 1f);
                        c = r > 0.7f ? fg : bg;
                    }
                    break;
                default: // duha: v < 5 celá tuba mění barvu, jinak duha plyne po tubě
                    c = v < 5 ? Color.HSVToRGB(Mathf.Repeat(ph * 0.5f, 1f), 1f, 1f)
                              : Color.HSVToRGB(Mathf.Repeat(u + dir * ph * 0.5f, 1f), 1f, 1f);
                    break;
            }
            px[i] = c;
        }
    }
}
