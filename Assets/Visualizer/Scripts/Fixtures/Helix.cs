using UnityEngine;

// BeamZ MHL820 Double Helix: základna se dvěma vodorovnými lištami, na každé 4× 3W RGBW LED.
// Každá lišta se naklápí (otáčí kolem své délky) zvlášť – 16bit tilt v DMX.
// Rozsah naklápění výrobce neuvádí, bereme 270°.
public class Helix : MonoBehaviour
{
    [Header("Stav (plní DMX)")]
    [Range(0, 1)] public float tilt1 = 0.5f, tilt2 = 0.5f;
    public Color color1 = Color.white, color2 = Color.white;
    [Range(0, 1)] public float dimmer;
    public float strobeHz;

    [Header("Parametry")]
    public float tiltRange = 270f;
    public float maxTiltSpeed = 360f;
    public float beamAngle = 6f;
    public float beamLength = 8f;
    public float brightness = 0.6f;
    public float lightIntensity = 12f;

    readonly Transform[] bars = new Transform[2];
    readonly Renderer[,] beams = new Renderer[2, 4];
    readonly Renderer[,] lenses = new Renderer[2, 4];
    readonly Light[] spots = new Light[2];
    readonly float[] cur = new float[2];

    public void Build()
    {
        var m = VisUtil.BodyMat;
        VisUtil.Prim(PrimitiveType.Cube, transform, new Vector3(0, 0.04f, 0), new Vector3(0.36f, 0.08f, 0.2f), m);           // základna
        for (int b = 0; b < 2; b++)
        {
            float z = b == 0 ? -0.05f : 0.05f;
            VisUtil.Prim(PrimitiveType.Cube, transform, new Vector3(-0.17f, 0.12f, z), new Vector3(0.015f, 0.09f, 0.03f), m); // držáky
            VisUtil.Prim(PrimitiveType.Cube, transform, new Vector3(0.17f, 0.12f, z), new Vector3(0.015f, 0.09f, 0.03f), m);
            var bar = new GameObject("Bar " + (b + 1)).transform;
            bar.SetParent(transform, false);
            bar.localPosition = new Vector3(0, 0.15f, z);
            VisUtil.Prim(PrimitiveType.Cylinder, bar, Vector3.zero, new Vector3(0.05f, 0.155f, 0.05f), m, new Vector3(0, 0, 90));
            for (int i = 0; i < 4; i++)
            {
                float x = -0.105f + i * 0.07f;
                lenses[b, i] = VisUtil.Emitter(PrimitiveType.Cylinder, bar, new Vector3(x, 0, 0.026f), new Vector3(0.03f, 0.002f, 0.03f), new Vector3(90, 0, 0));
                var bm = VisUtil.Beam(bar, beamAngle, beamLength, 0.012f);
                bm.transform.localPosition = new Vector3(x, 0, 0.027f);
                beams[b, i] = bm;
            }
            spots[b] = VisUtil.Spot(bar, 25f, 15f, 10f);
            spots[b].transform.localPosition = new Vector3(0, 0, 0.03f);
            bars[b] = bar;
        }
    }

    void Update()
    {
        if (bars[0] == null) return;
        float gate = VisUtil.StrobeGate(strobeHz);
        for (int b = 0; b < 2; b++)
        {
            float target = ((b == 0 ? tilt1 : tilt2) - 0.5f) * tiltRange;
            cur[b] = Mathf.MoveTowards(cur[b], target, maxTiltSpeed * Time.deltaTime);
            bars[b].localRotation = Quaternion.Euler(cur[b], 0, 0); // střed rozsahu = svítí dopředu na parket
            Color c = b == 0 ? color1 : color2;
            float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            Color n = m > 0.001f ? c / m : Color.black;
            float I = dimmer * Mathf.Clamp01(m) * gate;
            for (int i = 0; i < 4; i++)
            {
                VisUtil.SetColor(beams[b, i], n, I * brightness);
                VisUtil.SetColor(lenses[b, i], n, I * 5f);
            }
            spots[b].color = n;
            spots[b].intensity = I * lightIntensity;
        }
    }
}
