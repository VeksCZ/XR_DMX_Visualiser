using UnityEngine;

// Strobo na Gigbaru: 4 studeně bílé LED, 0-30 Hz.
public class Strobe : MonoBehaviour
{
    [Header("Stav (později plní DMX)")]
    [Range(0, 1)] public float dimmer;
    public float strobeHz;
    public float[] ledLevels = { 1f, 1f, 1f, 1f }; // jas jednotlivých LED (GigBar ch 27-30)
    public float lightIntensity = 40f;

    Renderer[] leds = new Renderer[4];
    Light glow;
    static readonly Color cool = new Color(0.9f, 0.95f, 1f);

    public void Build()
    {
        // 4 LED po dvou vlevo a vpravo od laseru
        float[] xs = { -0.105f, -0.07f, 0.07f, 0.105f };
        for (int i = 0; i < 4; i++)
            leds[i] = VisUtil.Emitter(PrimitiveType.Cube, transform, new Vector3(xs[i], 0, 0), new Vector3(0.025f, 0.025f, 0.004f));
        glow = VisUtil.Point(transform, new Vector3(0, 0, 0.3f), 10f);
    }

    void Update()
    {
        if (glow == null) return;
        float I = dimmer * VisUtil.StrobeGate(strobeHz, 0.1f);
        float sum = 0f;
        for (int i = 0; i < 4; i++)
        {
            VisUtil.SetColor(leds[i], cool, I * ledLevels[i] * 15f);
            sum += ledLevels[i];
        }
        glow.color = cool;
        glow.intensity = I * sum * 0.25f * lightIntensity;
    }
}
