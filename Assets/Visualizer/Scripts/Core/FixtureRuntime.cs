using System.Collections.Generic;
using UnityEngine;

// Světlo postavené ve scéně podle profilu
public class FixtureInstance
{
    public FixtureEntry entry;
    public FixtureProfile profile;
    public int index;               // pořadí v sestavě
    public int slotIndex;           // pořadí mezi záznamy se stejným místem
    public string slot;
    public Transform root;          // umístění (pozice + natočení)
    public Transform body;          // tělo světla (u stativu nad trojnožkou)
    public Component[] parts;       // ParLight, Derby, MovingHead, PixelTube, Laser, Strobe, DerbyStrobe, Helix
    public float hazeOut;           // hazer: výkon 0–1

    public bool Visible => root != null && root.gameObject.activeSelf;
    public T Part<T>(int i) where T : Component => parts != null && i >= 0 && i < parts.Length ? parts[i] as T : null;
}

// Stavba částí a tvarů světla podle profilu
public static class FixtureFactory
{
    public static void Build(FixtureInstance fi)
    {
        var p = fi.profile;
        var parent = fi.body;
        if (p.shapes != null)
            foreach (var s in p.shapes)
            {
                if (s == null) continue;
                var type = s.shape == "cylinder" ? PrimitiveType.Cylinder : s.shape == "sphere" ? PrimitiveType.Sphere : PrimitiveType.Cube;
                var size = ProfileUtil.V(s.size, Vector3.one * 0.1f);
                if (type == PrimitiveType.Cylinder) size.y *= 0.5f;   // Unity válec je 2 jednotky vysoký
                VisUtil.Prim(type, parent, ProfileUtil.V(s.pos), size, ProfileUtil.Mat(s.color), ProfileUtil.V(s.rot));
            }
        int n = p.parts != null ? p.parts.Length : 0;
        fi.parts = new Component[n];
        for (int i = 0; i < n; i++)
        {
            var d = p.parts[i];
            if (d == null) continue;
            var go = new GameObject(string.IsNullOrEmpty(d.name) ? d.type : d.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = ProfileUtil.V(d.pos);
            go.transform.localEulerAngles = ProfileUtil.V(d.rot);
            if (d.scale > 0f) go.transform.localScale = Vector3.one * d.scale;   // zmenšení / zvětšení celé části
            fi.parts[i] = BuildPart(go, d);
        }
    }

    static float Or(float v, float def) => v > 0f ? v : def;
    static int Or(int v, int def) => v > 0 ? v : def;

    static Component BuildPart(GameObject go, ProfilePart d)
    {
        switch (d.type)
        {
            case "par":
            {
                var p = go.AddComponent<ParLight>();
                p.housing = d.housing == "box" ? ParLight.Housing.Box : ParLight.Housing.Round;
                p.size = Or(d.size, p.size);
                p.boxDims = ProfileUtil.V(d.box);
                p.ledCount = Or(d.leds, 1);
                p.beamAngle = Or(d.beam, p.beamAngle);
                p.fieldAngle = Or(d.field, p.fieldAngle);
                p.beamLength = Or(d.beamLength, p.beamLength);
                p.lightIntensity = Or(d.intensity, p.lightIntensity);
                p.Build();
                return p;
            }
            case "derby":
            {
                var p = go.AddComponent<Derby>();
                p.coverage = Or(d.coverage, p.coverage);
                p.beamsPerColor = Or(d.beams, p.beamsPerColor);
                p.beamAngle = Or(d.beam, p.beamAngle);
                p.beamLength = Or(d.beamLength, p.beamLength);
                p.brightness = Or(d.brightness, p.brightness);
                p.lensCols = Or(d.lensCols, p.lensCols);
                p.lensRows = Or(d.lensRows, p.lensRows);
                if (d.box != null && d.box.Length >= 3) p.bodySize = ProfileUtil.V(d.box);
                p.squareLenses = d.housing == "square";
                p.Build();
                if (d.mirror) p.rotationSpeed = -p.rotationSpeed;
                return p;
            }
            case "mover":
            {
                var p = go.AddComponent<MovingHead>();
                p.beamAngle = Or(d.beam, p.beamAngle);
                p.beamLength = Or(d.beamLength, p.beamLength);
                p.lightIntensity = Or(d.intensity, p.lightIntensity);
                p.beamBrightness = Or(d.brightness, p.beamBrightness);
                p.panRange = Or(d.panRange, p.panRange);
                p.tiltRange = Or(d.tiltRange, p.tiltRange);
                p.mountBase = d.housing != "nobase";
                p.Build();
                return p;
            }
            case "tube":
            {
                var p = go.AddComponent<PixelTube>();
                p.length = Or(d.length, p.length);
                p.segments = Or(d.segments, 16);
                p.diameter = Or(d.diameter, p.diameter);
                p.lightIntensity = Or(d.intensity, p.lightIntensity);
                p.Build();
                return p;
            }
            case "laser":
            {
                var p = go.AddComponent<Laser>();
                p.coverage = Or(d.coverage, p.coverage);
                p.columns = Or(d.columns, p.columns);
                p.rows = Or(d.rows, p.rows);
                p.beamLength = Or(d.beamLength, p.beamLength);
                p.brightness = Or(d.brightness, p.brightness);
                p.Build();
                if (d.mirror) p.patternSpeed = -p.patternSpeed;
                return p;
            }
            case "strobe":
            {
                var p = go.AddComponent<Strobe>();
                p.lightIntensity = Or(d.intensity, p.lightIntensity);
                p.spacing = Or(d.spacing, p.spacing);
                p.ledSize = Or(d.size, p.ledSize);
                p.Build();
                return p;
            }
            case "derbystrobe":
            {
                var p = go.AddComponent<DerbyStrobe>();
                p.Build();
                if (d.mirror && p.derby != null) p.derby.rotationSpeed = -p.derby.rotationSpeed;
                return p;
            }
            case "helix":
            {
                var p = go.AddComponent<Helix>();
                p.beamAngle = Or(d.beam, p.beamAngle);
                p.beamLength = Or(d.beamLength, p.beamLength);
                p.brightness = Or(d.brightness, p.brightness);
                p.lightIntensity = Or(d.intensity, p.lightIntensity);
                p.tiltRange = Or(d.tiltRange, p.tiltRange);
                p.Build();
                return p;
            }
        }
        Debug.LogWarning("Neznámý typ části: " + d.type);
        return null;
    }
}

// Převod DMX hodnot na stav světla podle profilu
public static class DmxEngine
{
    // Rozpracovaný stav jedné části během snímku
    class Acc
    {
        public readonly float[] r = new float[4], g = new float[4], b = new float[4], w = new float[4], a = new float[4], uv = new float[4];
        public readonly float[] tilt = new float[4];
        public readonly bool[] tiltSet = new bool[4];
        public readonly float[] led = new float[8];
        public bool hasLed, hasDimmer, hasColor, colorOff, hasStrobe, strobeOff, hasRot, hasPan, hasSpeed, hasPixels;
        public float dimmer, strobeHz, rot, pan, speed, progSpeed = 0.5f, fxSpeed, ptMacroSpeed;
        public int rotRaw = -1, gobo = -1, ptMacro;
        public Color color;
        public ValueRange shutter; public int shutterV;
        public ValueRange program; public ProfileControl programCtl;
        public ValueRange macro; public int macroV; public ValueRange background; public int backgroundV;
        public ValueRange effect; public int effectV;

        public void Reset()
        {
            for (int i = 0; i < 4; i++) { r[i] = g[i] = b[i] = w[i] = a[i] = uv[i] = 0f; tilt[i] = 0.5f; tiltSet[i] = false; }
            for (int i = 0; i < led.Length; i++) led[i] = 1f;
            hasLed = hasDimmer = hasColor = colorOff = hasStrobe = strobeOff = hasRot = hasPan = hasSpeed = hasPixels = false;
            dimmer = 1f; strobeHz = 0f; rot = 0f; pan = 0.5f; speed = 0f; progSpeed = 0.5f; fxSpeed = 0f; ptMacroSpeed = 0f;
            rotRaw = -1; gobo = -1; ptMacro = 0;
            color = Color.white;
            shutter = null; program = null; programCtl = null; macro = null; background = null; effect = null;
        }
    }

    static Acc[] acc = new Acc[0];

    static int I(byte[] d, int ch) => ch >= 0 && ch < 512 ? d[ch] : 0;
    static float F(byte[] d, int ch) => I(d, ch) / 255f;

    public static void Apply(FixtureInstance fi, byte[] d)
    {
        var p = fi.profile;
        var mode = p.Mode(fi.entry.mode);
        if (mode == null) return;
        int baseCh = fi.entry.address - 1;
        int n = fi.parts != null ? fi.parts.Length : 0;
        if (acc.Length < n) { acc = new Acc[n]; for (int i = 0; i < n; i++) acc[i] = new Acc(); }
        for (int i = 0; i < n; i++) acc[i].Reset();
        fi.hazeOut = 0f;

        foreach (var c in mode.controls)
        {
            if (c == null) continue;
            int ch = baseCh + c.ch - 1;
            int v = I(d, ch);
            if (c.fn == "haze") { fi.hazeOut = Mathf.Max(fi.hazeOut, v / 255f); continue; }
            if (c.part < 0 || c.part >= n) continue;
            var A = acc[c.part];
            int s = Mathf.Clamp(c.sub, 0, 3);
            switch (c.fn)
            {
                case "dimmer": A.hasDimmer = true; A.dimmer = v / 255f; break;
                case "red": A.r[s] = v / 255f; break;
                case "green": A.g[s] = v / 255f; break;
                case "blue": A.b[s] = v / 255f; break;
                case "white": A.w[s] = v / 255f; break;
                case "amber": A.a[s] = v / 255f; break;
                case "uv": A.uv[s] = v / 255f; break;
                case "strobe": StrobeCtl(c, v, A); break;
                case "shutter": A.shutter = ProfileUtil.Find(c.ranges, v); A.shutterV = v; break;
                case "pan": A.hasPan = true; A.pan = Sixteen(d, ch, c.fine > 0 ? baseCh + c.fine - 1 : -1); break;
                case "tilt": A.tiltSet[s] = true; A.tilt[s] = Sixteen(d, ch, c.fine > 0 ? baseCh + c.fine - 1 : -1); break;
                case "ptSpeed": A.hasSpeed = true; A.speed = c.invert ? 1f - v / 255f : v / 255f; break;
                case "color": ColorWheel(c, v, A); break;
                case "gobo": A.gobo = Gobo(c, v); break;
                case "rotation": A.hasRot = true; A.rotRaw = v; A.rot = Rotation(c, v); break;
                case "ledLevel": A.hasLed = true; A.led[Mathf.Clamp(c.sub, 0, 7)] = v / 255f; break;
                case "program": A.program = ProfileUtil.Find(c.ranges, v); A.programCtl = c; break;
                case "programSpeed": A.progSpeed = v / 255f; break;
                case "colorMacro": A.macro = ProfileUtil.Find(c.ranges, v); A.macroV = v; break;
                case "background": A.background = ProfileUtil.Find(c.ranges, v); A.backgroundV = v; break;
                case "effect": A.effect = ProfileUtil.Find(c.ranges, v); A.effectV = v; break;
                case "effectSpeed": A.fxSpeed = v / 255f; break;
                case "ptMacro": A.ptMacro = v; break;
                case "ptMacroSpeed": A.ptMacroSpeed = v / 255f; break;
                case "pixels": A.hasPixels = true; Pixels(fi.parts[c.part] as PixelTube, d, ch, c); break;
            }
        }

        for (int i = 0; i < n; i++)
        {
            var part = fi.parts[i];
            if (part == null) continue;
            var A = acc[i];
            switch (part)
            {
                case ParLight pl: FinishPar(pl, A); break;
                case Derby dr: FinishDerby(dr, A); break;
                case MovingHead mh: FinishMover(mh, A, fi.entry); break;
                case PixelTube pt: FinishTube(pt, A); break;
                case Laser ls: FinishLaser(ls, A); break;
                case Strobe st: FinishStrobe(st, A); break;
                case DerbyStrobe ds: FinishDerbyStrobe(ds, A); break;
                case Helix hx: FinishHelix(hx, A); break;
            }
        }
    }

    static float Sixteen(byte[] d, int ch, int fine) => fine >= 0 ? ((I(d, ch) << 8) | I(d, fine)) / 65535f : I(d, ch) / 255f;

    static Color Uv(float uv) => new Color(0.35f, 0f, 1f) * uv * 0.6f;
    static Color Mix(Acc A, int s) => VisUtil.RGBWA(A.r[s], A.g[s], A.b[s], A.w[s], A.a[s]) + Uv(A.uv[s]);

    // Strobo: bez rozsahů 0–9 vypnuto, 10–255 = 1–20 Hz
    static void StrobeCtl(ProfileControl c, int v, Acc A)
    {
        A.hasStrobe = true;
        var r = ProfileUtil.Find(c.ranges, v);
        if (c.ranges == null || c.ranges.Length == 0)
        {
            A.strobeOff = v < 10;
            A.strobeHz = v < 10 ? 0f : Mathf.Lerp(1f, 20f, (v - 10) / 245f);
            return;
        }
        if (r == null || r.type == "off" || r.type == "open") { A.strobeOff = r == null || r.type == "off"; A.strobeHz = 0f; return; }
        A.strobeOff = false;
        if (r.type == "random") A.strobeHz = Random.Range(Or(r.a, 2f), Or(r.b, 15f));
        else A.strobeHz = Mathf.Lerp(r.a, r.b, ProfileUtil.Pos(r, v));
    }

    static float Or(float v, float def) => v > 0f ? v : def;

    // Otáčení: bez rozsahů 0 stop, 1–127 CW pomalu→rychle, 128 stop, 129–255 CCW
    static float Rotation(ProfileControl c, int v)
    {
        float max = Or(c.max, 150f);
        if (c.ranges == null || c.ranges.Length == 0)
        {
            if (v == 0 || v == 128) return 0f;
            if (v < 128) return Mathf.Lerp(0.1f, 1f, v / 127f) * max;
            return -Mathf.Lerp(0.1f, 1f, (v - 128) / 127f) * max;
        }
        var r = ProfileUtil.Find(c.ranges, v);
        if (r == null || r.type == "stop") return 0f;
        float sp = Mathf.Lerp(Or(r.a, 0.1f), Or(r.b, 1f), ProfileUtil.Pos(r, v)) * max;
        return r.type == "ccw" ? -sp : sp;
    }

    // Výchozí paleta pro "cycle": všechny kombinace R, G, B, W (jako derby)
    static readonly Color[] RgbwCombos =
    {
        new Color(1,0,0,0), new Color(0,1,0,0), new Color(0,0,1,0), new Color(0,0,0,1),
        new Color(1,1,0,0), new Color(1,0,1,0), new Color(1,0,0,1), new Color(0,1,1,0),
        new Color(0,1,0,1), new Color(0,0,1,1), new Color(1,1,1,0), new Color(1,1,0,1),
        new Color(1,0,1,1), new Color(0,1,1,1), new Color(1,1,1,1),
    };

    // Barva podle rozsahu (barevné kolo, makra, laser). false = bez barvy (vypnuto).
    public static bool RangeColor(ValueRange r, int v, out Color c)
    {
        c = Color.white;
        if (r == null) return false;
        switch (r.type)
        {
            case "off": c = Color.clear; return false;
            case "color": c = ProfileUtil.Hex(r.color, Color.white); return true;
            case "split": c = Color.Lerp(ProfileUtil.Hex(r.color, Color.white), ProfileUtil.Hex(r.color2, Color.white), 0.5f); return true;
            case "hue":
                c = Color.HSVToRGB(Mathf.Repeat(Mathf.Lerp(r.a, Or(r.b, 1f), r.to > r.from ? (v - r.from) / (float)(r.to - r.from + 1) : 0f), 1f), Or(r.sat, 1f), 1f);
                c.a = 0f;
                return true;
            case "rainbow":
                c = Color.HSVToRGB(Mathf.Repeat(Time.time * Or(r.a, 0.3f), 1f), 1f, 1f);
                c.a = 0f;
                return true;
            case "cycle":
            {
                int k = (int)(Time.time * Or(r.a, 2f));
                if (r.colors != null && r.colors.Length > 0) c = ProfileUtil.Hex(r.colors[k % r.colors.Length], Color.white);
                else c = RgbwCombos[k % RgbwCombos.Length];
                return true;
            }
            case "slots":
                if (r.colors == null || r.colors.Length == 0) return false;
                c = ProfileUtil.Hex(r.colors[ProfileUtil.Slot(r, v, r.colors.Length)], Color.white);
                return true;
        }
        return false;
    }

    static void ColorWheel(ProfileControl c, int v, Acc A)
    {
        A.hasColor = true;
        A.colorOff = !RangeColor(ProfileUtil.Find(c.ranges, v), v, out A.color);
    }

    static int Gobo(ProfileControl c, int v)
    {
        var r = ProfileUtil.Find(c.ranges, v);
        if (r == null) return 0;
        switch (r.type)
        {
            case "gobo": return (int)r.a;
            case "gobos": return (int)r.a + ProfileUtil.Slot(r, v, (int)r.b - (int)r.a + 1);
            case "cycle": return (int)Mathf.Repeat(Time.time * Or(r.b, 2f), Mathf.Max(1f, r.a));
        }
        return 0;
    }

    static void Pixels(PixelTube t, byte[] d, int ch, ProfileControl c)
    {
        if (t == null || t.pixels == null) return;
        string lay = string.IsNullOrEmpty(c.layout) ? "RGB" : c.layout.ToUpperInvariant();
        int cnt = Mathf.Max(1, c.count), n = t.pixels.Length, w = lay.Length;
        for (int i = 0; i < n; i++)
        {
            int o = ch + Mathf.Min(i * cnt / n, cnt - 1) * w;
            float R = 0, G = 0, B = 0, W = 0, Am = 0, U = 0;
            for (int k = 0; k < w; k++)
            {
                float x = F(d, o + k);
                switch (lay[k]) { case 'R': R = x; break; case 'G': G = x; break; case 'B': B = x; break; case 'W': W = x; break; case 'A': Am = x; break; case 'U': U = x; break; }
            }
            t.pixels[i] = VisUtil.RGBWA(R, G, B, W, Am) + Uv(U);
        }
    }

    static Color Program(Acc A, Color c)
    {
        if (A.program == null || A.programCtl == null) return c;
        float t = Time.time * Mathf.Lerp(Or(A.programCtl.min, 0.2f), Or(A.programCtl.max, 4f), A.progSpeed);
        switch (A.program.type)
        {
            case "jump": return Color.HSVToRGB(Mathf.Floor(t * 2f) % 7 / 7f, 1f, 1f);
            case "fade": return Color.HSVToRGB(Mathf.Repeat(t * 0.3f, 1f), 1f, 1f);
            case "pulse": return Color.HSVToRGB(Mathf.Floor(t) % 7 / 7f, 1f, 1f) * (0.5f + 0.5f * Mathf.Sin(t * 6.28f));
            case "auto": return Color.HSVToRGB(Mathf.Repeat(t * 0.5f, 1f), 1f, 1f);
        }
        return c;
    }

    static void FinishPar(ParLight p, Acc A)
    {
        Color c = Mix(A, 0);
        if (A.hasColor) c = A.colorOff ? Color.black : A.color;
        c = Program(A, c);
        float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        p.color = m > 0.001f ? WithAlpha(c / m) : Color.black;
        p.dimmer = A.dimmer * Mathf.Clamp01(m);
        p.strobeHz = A.strobeHz;
    }

    static void FinishDerby(Derby dr, Acc A)
    {
        if (A.hasColor)
        {
            Color k = A.colorOff ? Color.clear : A.color;
            dr.red = k.r; dr.green = k.g; dr.blue = k.b; dr.white = k.a;
        }
        else { dr.red = A.r[0]; dr.green = A.g[0]; dr.blue = A.b[0]; dr.white = A.w[0]; }
        if (A.hasDimmer) { dr.red *= A.dimmer; dr.green *= A.dimmer; dr.blue *= A.dimmer; dr.white *= A.dimmer; }
        dr.strobeHz = A.strobeHz;
        if (A.hasRot) dr.rotationSpeed = A.rot;
    }

    static void FinishLaser(Laser l, Acc A)
    {
        if (A.hasColor) { l.intensity = A.colorOff ? 0f : 1f; if (!A.colorOff) { var c = A.color; c.a = 1f; l.color = c; } }
        else
        {
            Color c = Mix(A, 0);
            float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            l.intensity = Mathf.Clamp01(m);
            if (m > 0.001f) l.color = c / m;
        }
        l.intensity *= A.dimmer;
        l.strobeHz = A.strobeHz;
        if (A.hasRot) l.patternSpeed = A.rot;
    }

    static void FinishStrobe(Strobe s, Acc A)
    {
        for (int i = 0; i < s.ledLevels.Length && i < A.led.Length; i++) s.ledLevels[i] = A.hasLed ? A.led[i] : 1f;
        s.dimmer = A.dimmer;
        s.strobeHz = A.strobeHz;
    }

    static void FinishDerbyStrobe(DerbyStrobe ds, Acc A)
    {
        var dr = ds.derby;
        if (dr == null) return;
        Color k = A.hasColor && !A.colorOff ? A.color : Color.clear;
        if (A.program != null && A.program.type == "derbyAuto")
        {
            float sp = A.progSpeed;
            k = RgbwCombos[(int)(Time.time * Mathf.Lerp(Or(A.programCtl.min, 0.5f), Or(A.programCtl.max, 6f), sp)) % RgbwCombos.Length];
            if (A.rotRaw >= 0 && A.rotRaw < 5) A.rot = Mathf.Lerp(30f, 150f, sp);
        }
        dr.red = k.r * A.dimmer; dr.green = k.g * A.dimmer; dr.blue = k.b * A.dimmer; dr.white = k.a * A.dimmer;
        dr.strobeHz = 0f;
        dr.rotationSpeed = A.hasRot ? A.rot : 0f;
        // strobo kanál ovládá bílý SMD panel
        ds.strobeDimmer = A.hasStrobe && !A.strobeOff ? 1f : 0f;
        ds.strobeHz = A.strobeHz;
    }

    static void FinishHelix(Helix h, Acc A)
    {
        h.tilt1 = A.tilt[0];
        h.tilt2 = A.tiltSet[1] ? A.tilt[1] : A.tilt[0];
        h.color1 = VisUtil.RGBWA(A.r[0], A.g[0], A.b[0], A.w[0], A.a[0]);
        h.color2 = VisUtil.RGBWA(A.r[1], A.g[1], A.b[1], A.w[1], A.a[1]);
        if (A.program != null && A.program.type == "helixShow")
        {
            float t = Time.time * Mathf.Lerp(Or(A.programCtl.min, 0.2f), Or(A.programCtl.max, 2f), A.progSpeed);
            h.tilt1 = 0.5f + 0.35f * Mathf.Sin(t * 3.1f);
            h.tilt2 = 0.5f - 0.35f * Mathf.Sin(t * 3.1f);
            h.color1 = Color.HSVToRGB(Mathf.Repeat(t * 0.2f, 1f), 1f, 1f);
            h.color2 = Color.HSVToRGB(Mathf.Repeat(t * 0.2f + 0.5f, 1f), 1f, 1f);
        }
        h.strobeHz = A.strobeHz;
        h.dimmer = A.dimmer;
    }

    static void FinishMover(MovingHead h, Acc A, FixtureEntry f)
    {
        if (A.hasPan || A.tiltSet[0])
        {
            float pan = A.pan, tilt = A.tilt[0];
            if (f.invertPan) pan = 1f - pan;
            if (f.invertTilt) tilt = 1f - tilt;
            h.pan = pan + f.panOffset / Mathf.Max(1f, h.panRange);
            h.tilt = tilt + f.tiltOffset / Mathf.Max(1f, h.tiltRange);
        }
        if (A.hasSpeed)
        {
            float spd = Mathf.Lerp(1f, 0.1f, A.speed);   // 0 = nejrychleji
            h.maxPanSpeed = 300f * spd;
            h.maxTiltSpeed = 200f * spd;
        }

        // P/T makra (Pocket Pro): 8–255 = makro 1–31 po 8 hodnotách; obrazce kolem nastavené pozice
        if (A.ptMacro >= 8)
        {
            int m = Mathf.Min((A.ptMacro - 8) / 8, 30);
            float hz = Mathf.Lerp(0.08f, 0.8f, A.ptMacroSpeed);
            float a = Time.time * hz * Mathf.PI * 2f;
            float amp = 12f + 6f * (m / 6);
            float x, y;
            switch (m % 6)
            {
                case 0: x = Mathf.Cos(a); y = Mathf.Sin(a); break;
                case 1: x = Mathf.Sin(a); y = Mathf.Sin(2f * a) * 0.5f; break;
                case 2: x = Mathf.Sin(a); y = 0f; break;
                case 3: x = 0f; y = Mathf.Sin(a); break;
                case 4: x = Mathf.Sin(a); y = Mathf.Sin(a); break;
                default: x = Mathf.Clamp(Mathf.Cos(a) * 1.4f, -1f, 1f); y = Mathf.Clamp(Mathf.Sin(a) * 1.4f, -1f, 1f); break;
            }
            h.pan += x * amp / Mathf.Max(1f, h.panRange);
            h.tilt += y * amp / Mathf.Max(1f, h.tiltRange);
            h.maxPanSpeed = 400f; h.maxTiltSpeed = 300f;
        }

        if (A.hasColor) h.color = A.colorOff ? Color.white : WithAlpha(A.color);
        else
        {
            Color c = Mix(A, 0);
            float mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            h.color = mx > 0.001f ? c / mx : Color.white;
        }
        if (A.gobo >= 0) h.gobo = A.gobo;

        float dim = A.dimmer;
        h.strobeHz = A.strobeHz;
        var r = A.shutter;
        if (r != null)
        {
            float pos = ProfileUtil.Pos(r, A.shutterV);
            switch (r.type)
            {
                case "closed": dim = 0f; break;
                case "strobe": h.strobeHz = Mathf.Lerp(Or(r.a, 1f), Or(r.b, 20f), pos); break;
                case "pulse": dim *= 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(Or(r.a, 2f), Or(r.b, 15f), pos)); break;
                case "random": h.strobeHz = Random.Range(Or(r.a, 2f), Or(r.b, 15f)); break;
                case "rampUp": dim *= Mathf.Repeat(Time.time * Mathf.Lerp(Or(r.a, 0.5f), Or(r.b, 4f), pos), 1f); break;
                case "rampDown": dim *= 1f - Mathf.Repeat(Time.time * Mathf.Lerp(Or(r.a, 0.5f), Or(r.b, 4f), pos), 1f); break;
            }
        }
        h.dimmer = dim;
    }

    static Color WithAlpha(Color c) { c.a = 1f; return c; }

    static void FinishTube(PixelTube t, Acc A)
    {
        if (t.pixels == null) return;
        if (A.hasPixels) { t.master = A.dimmer; return; }
        int n = t.pixels.Length;
        Color fg = Mix(A, 0);
        if (A.macro != null && RangeColor(A.macro, A.macroV, out Color mc)) fg = mc;
        Color bg = Color.black;
        if (A.background != null && RangeColor(A.background, A.backgroundV, out Color bc)) bg = bc;
        fg.a = 1f; bg.a = 1f;
        float gate = VisUtil.StrobeGate(A.strobeHz);
        var e = A.effect;
        if (e == null || e.type == "off") for (int i = 0; i < n; i++) t.pixels[i] = fg;
        else
        {
            if (Mathf.Max(fg.r, Mathf.Max(fg.g, fg.b)) < 0.01f) fg = Color.white;   // efekt bez barvy svítí bíle
            int idx = e.type == "autoEffect"
                ? Mathf.FloorToInt(Time.time / Or(e.a, 8f)) % TubeEffects.Count
                : (int)e.a + ProfileUtil.Slot(e, A.effectV, (int)Or(e.b, TubeEffects.Count - 1) - (int)e.a + 1);
            TubeEffects.Render(t.pixels, idx, A.fxSpeed, fg, bg);
        }
        for (int i = 0; i < n; i++) t.pixels[i] *= gate;
        t.master = A.dimmer;
    }

    // Tma: všechna světla zhasnutá, motory stojí (pan/tilt zůstává, kde byl)
    public static void Blackout(FixtureInstance fi)
    {
        fi.hazeOut = 0f;
        if (fi.parts == null) return;
        foreach (var part in fi.parts)
        {
            switch (part)
            {
                case ParLight p: p.dimmer = 0f; p.strobeHz = 0f; break;
                case Derby d: StopDerby(d); break;
                case MovingHead h: h.dimmer = 0f; h.strobeHz = 0f; break;
                case PixelTube t: t.master = 0f; break;
                case Laser l: l.intensity = 0f; l.patternSpeed = 0f; break;
                case Strobe s: s.dimmer = 0f; break;
                case DerbyStrobe ds: ds.strobeDimmer = 0f; if (ds.derby != null) StopDerby(ds.derby); break;
                case Helix hx: hx.dimmer = 0f; break;
            }
        }
    }

    static void StopDerby(Derby d) { d.red = d.green = d.blue = d.white = 0f; d.rotationSpeed = 0f; d.strobeHz = 0f; }

    // Pan/tilt první hlavy (16bit) – živý odečet v nastavení
    public static bool ReadPanTilt(FixtureEntry f, FixtureProfile p, byte[] d, out int pan, out int tilt)
    {
        pan = tilt = 0;
        var mode = p != null ? p.Mode(f.mode) : null;
        if (mode == null || p.parts == null) return false;
        int mover = -1;
        for (int i = 0; i < p.parts.Length; i++) if (p.parts[i] != null && p.parts[i].type == "mover") { mover = i; break; }
        if (mover < 0) return false;
        bool any = false;
        int b = f.address - 1;
        foreach (var c in mode.controls)
        {
            if (c == null || c.part != mover) continue;
            int v = c.fine > 0 ? (I(d, b + c.ch - 1) << 8) | I(d, b + c.fine - 1) : I(d, b + c.ch - 1) * 257;
            if (c.fn == "pan") { pan = v; any = true; }
            else if (c.fn == "tilt") { tilt = v; any = true; }
        }
        return any;
    }

    // Surové pan/tilt (0–1) konkrétní hlavy – pro kalibraci
    public static bool RawPanTilt(FixtureEntry f, FixtureProfile p, int part, byte[] d, out float pan, out float tilt)
    {
        pan = tilt = 0.5f;
        var mode = p != null ? p.Mode(f.mode) : null;
        if (mode == null) return false;
        bool any = false;
        int b = f.address - 1;
        foreach (var c in mode.controls)
        {
            if (c == null || c.part != part) continue;
            if (c.fn == "pan") { pan = Sixteen(d, b + c.ch - 1, c.fine > 0 ? b + c.fine - 1 : -1); any = true; }
            else if (c.fn == "tilt") { tilt = Sixteen(d, b + c.ch - 1, c.fine > 0 ? b + c.fine - 1 : -1); any = true; }
        }
        return any;
    }
}

// Efekty pixel tuby: 6 rodin po 10 variantách (index 0–59). Liché varianty jedou shora dolů.
// speed 0–1 (víc = rychleji). pixels[0] je dole.
public static class TubeEffects
{
    public const int Count = 60;

    public static void Render(Color[] px, int idx, float speed, Color fg, Color bg)
    {
        int n = px.Length;
        idx = Mathf.Clamp(idx, 0, Count - 1);
        int fam = idx / 10, v = idx % 10;
        float dir = v % 2 == 1 ? 1f : -1f;
        float ph = Time.time * Mathf.Lerp(0.15f, 2.5f, speed);
        for (int i = 0; i < n; i++)
        {
            float u = (i + 0.5f) / n;
            Color c;
            switch (fam)
            {
                case 0: // běžící světlo s ocasem
                {
                    float head = Mathf.Repeat(-dir * ph, 1f);
                    float dist = Mathf.Repeat((u - head) * dir, 1f);
                    float tail = 0.08f + 0.06f * (v / 2);
                    c = Color.Lerp(bg, fg, Mathf.Clamp01(1f - dist / tail));
                    break;
                }
                case 1: // stírání
                {
                    float p = Mathf.Repeat(ph * 0.5f, 1f) * 2f;
                    float edge = p < 1f ? p : p - 1f;
                    float pos = dir > 0 ? 1f - u : u;
                    bool on = p < 1f ? pos < edge : pos >= edge;
                    c = on ? fg : bg;
                    break;
                }
                case 2: // blok tam a zpět
                {
                    float center = Mathf.PingPong(ph * 2f, 1f);
                    float w = 0.12f + 0.04f * (v / 2);
                    c = Mathf.Abs(u - center) < w ? fg : bg;
                    break;
                }
                case 3: // segmenty s mezerami
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
                default: // duha
                    c = v < 5 ? Color.HSVToRGB(Mathf.Repeat(ph * 0.5f, 1f), 1f, 1f)
                              : Color.HSVToRGB(Mathf.Repeat(u + dir * ph * 0.5f, 1f), 1f, 1f);
                    break;
            }
            px[i] = c;
        }
    }
}
