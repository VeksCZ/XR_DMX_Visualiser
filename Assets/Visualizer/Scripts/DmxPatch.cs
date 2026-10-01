using System;
using System.Collections.Generic;
using UnityEngine;

public enum FixtureType { GigBarMoveILS, BatteryPar, PixelTube, Hazer }

// Jedno světlo v patchi. Universe je 1-based jako v SoundSwitchi (1 = Art-Net universe 0).
[Serializable]
public class FixtureEntry
{
    public FixtureType type;
    public string name;
    public int universe = 1;
    public int address = 1;
    public bool mode40ch;   // jen pro tuby

    public FixtureEntry() { }
    public FixtureEntry(FixtureType t, string n, int addr) { type = t; name = n; address = addr; }

    public static int Channels(FixtureType t, bool ch40 = false)
    {
        switch (t)
        {
            case FixtureType.GigBarMoveILS: return 52;
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
            case FixtureType.BatteryPar: return "Battery Par";
            case FixtureType.PixelTube: return "Pixel Tube 360";
            default: return "Hazer";
        }
    }

    public static List<FixtureEntry> Defaults() => new List<FixtureEntry>
    {
        new FixtureEntry(FixtureType.GigBarMoveILS, "GigBar", 200),
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

        int pars = 0, tubes = 0;
        bool gigbarDone = false, hazerDone = false;
        float hazeOut = 0f;
        foreach (var f in fixtures)
        {
            if (f == null) continue;
            var d = Uni(f.universe - 1);
            if (d == null) continue;
            int a = f.address - 1;
            switch (f.type)
            {
                case FixtureType.GigBarMoveILS:
                    if (!gigbarDone) { ApplyGigbar(d, a); gigbarDone = true; }
                    break;
                case FixtureType.BatteryPar:
                    if (pars < scene.uplights.Length) ApplyBatteryPar(d, a, scene.uplights[pars]);
                    pars++;
                    break;
                case FixtureType.PixelTube:
                    if (tubes < scene.tubes.Length) ApplyTube(d, a, scene.tubes[tubes], f.mode40ch);
                    tubes++;
                    break;
                case FixtureType.Hazer:
                    if (!hazerDone) { hazeOut = F(d, a); hazerDone = true; }
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
    void ApplyGigbar(byte[] d, int b)
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

        ApplyMover(d, b + 34, g.headL);
        ApplyMover(d, b + 43, g.headR);
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

    void ApplyMover(byte[] d, int b, MovingHead h)
    {
        h.pan = ((I(d, b) << 8) | I(d, b + 1)) / 65535f;
        h.tilt = ((I(d, b + 2) << 8) | I(d, b + 3)) / 65535f;
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

    // ---------------- LED Pixel Tube 360 RGBWA ----------------
    // Mode 1 (12ch): 0 Intensity, 1 Strobe, 2 Color macro, 3 Background, 4 Base tint,
    //                5 Effects, 6 Effect speed, 7 R, 8 G, 9 B, 10 W, 11 A
    // Mode 2 (40ch): 8 pixelů × R,G,B,W,A
    void ApplyTube(byte[] d, int b, PixelTube t, bool mode40ch)
    {
        if (t == null || t.pixels == null) return;
        if (!mode40ch)
        {
            Color c = VisUtil.RGBWA(F(d, b + 7), F(d, b + 8), F(d, b + 9), F(d, b + 10), F(d, b + 11));
            float gate = VisUtil.StrobeGate(StrobeHz(I(d, b + 1)));
            for (int i = 0; i < t.pixels.Length; i++) t.pixels[i] = c * gate;
            t.master = F(d, b);
        }
        else
        {
            int n = Mathf.Min(t.pixels.Length, 8);
            for (int i = 0; i < n; i++)
            {
                int o = b + i * 5;
                t.pixels[i] = VisUtil.RGBWA(F(d, o), F(d, o + 1), F(d, o + 2), F(d, o + 3), F(d, o + 4));
            }
            t.master = 1f;
        }
    }
}
