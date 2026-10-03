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
    public int lensCols = 7, lensRows = 3;
    [Tooltip("Rozměry těla (š, v, hloubka)")]
    public Vector3 bodySize = new Vector3(0.11f, 0.1f, 0.08f);
    [Tooltip("Hranaté čočky přes celé čelo (GigBar) místo malých kulatých")]
    public bool squareLenses;

    Transform rotor;
    Renderer[] beams;
    int[] beamColor;
    Renderer lens;
    Renderer[] lensArr;
    static readonly Color[] cols = { Color.red, Color.green, Color.blue, Color.white };

    public void Build()
    {
        var m = VisUtil.BodyMat;
        VisUtil.Prim(PrimitiveType.Cube, transform, new Vector3(0, 0, -bodySize.z * 0.5f), bodySize, m);
        // čelo: mřížka čoček (GigBar derby 7 × 3), každá ukazuje směs aktuálně svítících LED
        lensArr = new Renderer[lensCols * lensRows];
        for (int r = 0; r < lensRows; r++)
            for (int c = 0; c < lensCols; c++)
            {
                if (squareLenses)
                {
                    // hranaté čočky přes celou šířku i výšku čela (malé mezery mezi nimi)
                    float px = bodySize.x * 0.92f / lensCols, py = bodySize.y * 0.86f / lensRows;
                    lensArr[r * lensCols + c] = VisUtil.Emitter(PrimitiveType.Cube, transform,
                        new Vector3((c - (lensCols - 1) * 0.5f) * px, ((lensRows - 1) * 0.5f - r) * py, 0.002f), new Vector3(px * 0.88f, py * 0.88f, 0.004f));
                }
                else
                {
                    const float pitch = 0.014f;
                    lensArr[r * lensCols + c] = VisUtil.Emitter(PrimitiveType.Sphere, transform,
                        new Vector3((c - (lensCols - 1) * 0.5f) * pitch, ((lensRows - 1) * 0.5f - r) * pitch, 0f), new Vector3(0.012f, 0.012f, 0.006f));
                }
            }
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
