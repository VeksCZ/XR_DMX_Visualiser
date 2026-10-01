using UnityEngine;

// Derby: vějíř úzkých paprsků přes 131°, LED R/G/B/W, otáčení čočky.
public class Derby : MonoBehaviour
{
    [Header("Stav (později plní DMX)")]
    [Range(0, 1)] public float red, green, blue, white;
    public float rotationSpeed = 40f; // °/s, záporné = opačný směr
    public float strobeHz;

    [Header("Parametry")]
    public float coverage = 131f;
    public int beamsPerColor = 8;
    public float beamAngle = 4f;
    public float beamLength = 6f;
    [Tooltip("Derby má jen 6,5 W LED, paprsky jsou v reálu slabé")]
    public float brightness = 0.2f;

    Transform rotor;
    Renderer[] beams;
    int[] beamColor;
    Renderer lens;
    Renderer[] lensArr;
    static readonly Color[] cols = { Color.red, Color.green, Color.blue, Color.white };

    public void Build()
    {
        var m = VisUtil.BodyMat;
        VisUtil.Prim(PrimitiveType.Cube, transform, new Vector3(0, 0, -0.04f), new Vector3(0.11f, 0.1f, 0.08f), m);
        // čelo: 2 řady × 5 čoček, každá ukazuje směs aktuálně svítících LED
        lensArr = new Renderer[10];
        for (int r = 0; r < 2; r++)
            for (int c = 0; c < 5; c++)
                lensArr[r * 5 + c] = VisUtil.Emitter(PrimitiveType.Sphere, transform,
                    new Vector3(-0.04f + c * 0.02f, r == 0 ? 0.012f : -0.012f, 0f), new Vector3(0.017f, 0.017f, 0.008f));
        lens = lensArr[0];

        rotor = new GameObject("Rotor").transform;
        rotor.SetParent(transform, false);

        int n = beamsPerColor * 4;
        beams = new Renderer[n];
        beamColor = new int[n];
        for (int i = 0; i < n; i++)
        {
            // Paprsky rozprostřené dokola (360°) kolem osy čočky – zlatý úhel dá rovnoměrné pokrytí,
            // odklon od osy roste až do poloviny „coverage“.
            float around = i * 137.508f;
            float off = Mathf.Sqrt((i + 0.5f) / n) * coverage * 0.5f;
            var pivot = new GameObject("B" + i).transform;
            pivot.SetParent(rotor, false);
            pivot.localRotation = Quaternion.Euler(0, 0, around) * Quaternion.Euler(off, 0, 0);
            beams[i] = VisUtil.Beam(pivot, beamAngle, beamLength, 0.006f);
            beamColor[i] = i % 4;
        }

        // Tečky na stěnách/podlaze (viditelné i bez hazu): RGB cookie pro barevné LED + zvlášť cookie pro bílou
        var dirs = new Vector3[n];
        var rgb = new Color[n];
        var wht = new Color[n];
        for (int i = 0; i < n; i++)
        {
            dirs[i] = rotor.GetChild(i).localRotation * Vector3.forward;
            rgb[i] = beamColor[i] == 3 ? Color.black : cols[beamColor[i]];
            wht[i] = beamColor[i] == 3 ? Color.white : Color.black;
        }
        float sa = Mathf.Min(170f, coverage + 20f);
        string key = "derby" + n + "_" + coverage;
        dotsRgb = VisUtil.DotSpot(rotor, sa, dotRange, VisUtil.DotCookie(key + "rgb", dirs, rgb, sa, beamAngle));
        dotsWhite = VisUtil.DotSpot(rotor, sa, dotRange, VisUtil.DotCookie(key + "w", dirs, wht, sa, beamAngle));
    }

    [Tooltip("Síla teček promítaných na plochy")]
    public float dotIntensity = 6f;
    public float dotRange = 14f;
    Light dotsRgb, dotsWhite;

    void Update()
    {
        if (rotor == null) return;
        rotor.Rotate(0, 0, rotationSpeed * Time.deltaTime, Space.Self);
        float gate = VisUtil.StrobeGate(strobeHz);
        float[] lv = { red, green, blue, white };
        Color sum = Color.black;
        for (int i = 0; i < beams.Length; i++)
        {
            int c = beamColor[i];
            VisUtil.SetColor(beams[i], cols[c], lv[c] * gate * brightness);
        }
        for (int c = 0; c < 4; c++) sum += cols[c] * lv[c];
        foreach (var l in lensArr) VisUtil.SetColor(l, sum, gate * 1.5f);
        if (dotsRgb != null)
        {
            dotsRgb.color = new Color(red, green, blue);
            dotsRgb.intensity = Mathf.Max(red, Mathf.Max(green, blue)) > 0.001f ? dotIntensity * gate : 0f;
            dotsWhite.color = Color.white;
            dotsWhite.intensity = white * gate * dotIntensity;
        }
    }
}
