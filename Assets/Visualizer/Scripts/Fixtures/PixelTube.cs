using UnityEngine;

// Čínská pixel tuba 360° (RGB + W + A). Délka ~1 m.
// 40ch mód = 8 segmentů × 5 kanálů (R,G,B,W,A), 80ch = 16 segmentů.
// Pokud manuál říká jinak, stačí změnit 'segments'.
public class PixelTube : MonoBehaviour
{
    [Header("Stav (později plní DMX) - RGBWA na segment")]
    public Color[] pixels;      // výsledná barva segmentu (RGB)
    [Range(0, 1)] public float master = 1f;

    [Header("Parametry")]
    public float length = 1.0f;
    public int segments = 8;
    public float diameter = 0.045f;
    public float lightIntensity = 3f;

    Renderer[] segs;
    Light glow;

    public void Build()
    {
        var m = VisUtil.BodyMat;
        // koncovky
        VisUtil.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, -0.015f, 0), new Vector3(diameter * 1.2f, 0.015f, diameter * 1.2f), m);
        VisUtil.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, length + 0.015f, 0), new Vector3(diameter * 1.2f, 0.015f, diameter * 1.2f), m);

        segs = new Renderer[segments];
        pixels = new Color[segments];
        float h = length / segments;
        for (int i = 0; i < segments; i++)
        {
            segs[i] = VisUtil.Emitter(PrimitiveType.Cylinder, transform,
                new Vector3(0, h * (i + 0.5f), 0), new Vector3(diameter, h * 0.5f * 0.97f, diameter));
            // mléčný difuzor: ve vypnutém stavu nevýrazně šedobílý, se světlem v sále světlejší
            VisUtil.SetOffLook(segs[i], new Color(0.05f, 0.05f, 0.055f), new Color(0.75f, 0.75f, 0.78f));
        }

        glow = VisUtil.Point(transform, new Vector3(0, length * 0.5f, 0), 5f);
    }

    void Update()
    {
        if (segs == null) return;
        Color avg = Color.black;
        for (int i = 0; i < segs.Length; i++)
        {
            VisUtil.SetColor(segs[i], pixels[i], master * 4f);
            avg += pixels[i];
        }
        avg /= segs.Length;
        float mx = Mathf.Max(avg.r, Mathf.Max(avg.g, avg.b));
        glow.color = mx > 0.001f ? avg / mx : Color.black;
        glow.intensity = mx * master * lightIntensity;
    }
}
