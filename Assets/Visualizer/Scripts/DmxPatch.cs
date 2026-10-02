using System.Collections.Generic;
using UnityEngine;

// Výchozí sestavy (tlačítka „Moje sestava“ / „Sestava kolegy“ v nastavení)
public static class RigPresets
{
    const string GigBar = "chauvet-gigbar-move-ils", Pocket = "adj-pocket-pro", Battery = "battery-par-rgbwauv",
        Tube = "pixel-tube-360", Haze = "hurricane-haze", BlackPar = "light4me-black-par-30x3",
        DerbyStrobe = "beamz-derbystrobe", Helix = "beamz-mhl820-double-helix";

    public static List<FixtureEntry> Mine() => new List<FixtureEntry>
    {
        // Kalibrace spočítaná z pozice SS „Stage Center“ (2502.ssproj) → střed parketu
        new FixtureEntry(GigBar, "GigBar", 200) { panOffset = 102f, tiltOffset = -1f },
        new FixtureEntry(Pocket, "Pocket Pro L", 33) { panOffset = 10f, tiltOffset = 43f },
        new FixtureEntry(Pocket, "Pocket Pro R", 46) { panOffset = -44f, tiltOffset = 45f },
        new FixtureEntry(Battery, "Uplight 1", 110),
        new FixtureEntry(Battery, "Uplight 2", 120),
        new FixtureEntry(Battery, "Uplight 3", 130),
        new FixtureEntry(Battery, "Uplight 4", 140),
        new FixtureEntry(Tube, "Tuba 1", 300),
        new FixtureEntry(Tube, "Tuba 2", 350),
        new FixtureEntry(Tube, "Tuba 3", 400),
        new FixtureEntry(Tube, "Tuba 4", 450),
        new FixtureEntry(Haze, "Hurricane Haze 1DX", 100),
        new FixtureEntry("adj-pro-event-table-2", "DJ stůl", 1),
        new FixtureEntry("fbt-promaxx-12a", "Repro L", 1),
        new FixtureEntry("fbt-promaxx-12a", "Repro R", 1),
    };

    // Kolegova sestava podle jeho SoundSwitch patche: rampa nad boothem – derby nahoře na krajích,
    // pary visí blíž ke středu, helixy uprostřed (v SS je nepoužívá → adresy jen volné na konci universa).
    public static List<FixtureEntry> Colleague() => new List<FixtureEntry>
    {
        new FixtureEntry(DerbyStrobe, "Derby Strobe L", 28),
        new FixtureEntry(DerbyStrobe, "Derby Strobe R", 1),
        new FixtureEntry(BlackPar, "Black Par L", 18),
        new FixtureEntry(BlackPar, "Black Par R", 8),
        new FixtureEntry(Haze, "Hurricane Haze 4D", 35, 1),
        // projekt „Save 1_5 hlavy“: navíc 2× Pocket Pro na rozích stolu (kalibrace převzatá z mé sestavy)
        new FixtureEntry(Pocket, "Pocket Pro L", 46) { panOffset = 10f, tiltOffset = 43f },
        new FixtureEntry(Pocket, "Pocket Pro R", 60) { panOffset = -44f, tiltOffset = 45f },
        new FixtureEntry(Helix, "Helix 1 (nepoužívá)", 477),
        new FixtureEntry(Helix, "Helix 2 (nepoužívá)", 495),
        new FixtureEntry("vonyx-db3-pro", "DJ booth", 1),
        new FixtureEntry("fbt-promaxx-14a", "Repro L", 1),
        new FixtureEntry("fbt-promaxx-14a", "Repro R", 1),
    };
}

// Art-Net → světla ve scéně podle profilů (DmxEngine). Bez živých dat a bez dema je tma.
public class DmxPatch : MonoBehaviour
{
    public ArtNetReceiver artnet;
    public SceneBuilder scene;
    public DemoDriver demo;
    [Tooltip("Ignorovat Art-Net a pouštět demo")]
    public bool forceDemo;

    [HideInInspector] public List<FixtureEntry> fixtures = new List<FixtureEntry>();

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
        if (artnet == null || scene == null) return;
        bool live = artnet.HasData && !forceDemo;
        // demo jen na vyžádání; bez Art-Netu a bez dema je na světlech tma a nic se nehýbe
        if (demo != null) demo.enabled = forceDemo;
        if (!live)
        {
            if (!forceDemo) foreach (var fi in scene.instances) DmxEngine.Blackout(fi);
            return;
        }

        float hazeOut = 0f;
        foreach (var fi in scene.instances)
        {
            if (fi.profile == null || fi.entry == null || fi.profile.IsProp) continue;
            if (fi.entry.hidden) { DmxEngine.Blackout(fi); continue; }
            var d = Uni(fi.entry.universe - 1);
            if (d == null) continue;
            DmxEngine.Apply(fi, d);
            hazeOut = Mathf.Max(hazeOut, fi.hazeOut);
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

    // Kalibrace "aktuální pozice ze SS = bod target" (typicky střed parketu).
    // Spočítá posun panu a tiltu tak, aby aktuální DMX hodnoty mířily do targetu.
    // Víc hlav v jednom světle (GigBar) se průměruje. index = pořadí světla ve scéně, f = zadané hodnoty z dialogu.
    public bool SolveOffsets(int index, FixtureEntry f, Vector3 target, out float panOffset, out float tiltOffset)
    {
        panOffset = tiltOffset = 0f;
        if (f == null || scene == null || artnet == null || !artnet.HasData) return false;
        if (index < 0 || index >= scene.instances.Count) return false;
        var fi = scene.instances[index];
        var p = f.Profile;
        if (p == null || fi.parts == null || fi.profile != p) return false;
        var d = Uni(f.universe - 1);
        if (d == null) return false;
        int n = 0;
        for (int i = 0; i < fi.parts.Length; i++)
        {
            var h = fi.parts[i] as MovingHead;
            if (h == null || !DmxEngine.RawPanTilt(f, p, i, d, out float pan, out float tilt)) continue;
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
        if (artnet == null || !artnet.HasData || f == null) return false;
        var d = Uni(f.universe - 1);
        return d != null && DmxEngine.ReadPanTilt(f, f.Profile, d, out pan, out tilt);
    }

    // Rozsah panu/tiltu první hlavy (pro zobrazení úhlů)
    public static void MoverRanges(FixtureProfile p, out float panRange, out float tiltRange)
    {
        panRange = 540f; tiltRange = 180f;
        if (p == null || p.parts == null) return;
        foreach (var part in p.parts)
            if (part != null && part.type == "mover")
            {
                if (part.panRange > 0f) panRange = part.panRange;
                if (part.tiltRange > 0f) tiltRange = part.tiltRange;
                return;
            }
    }
}
