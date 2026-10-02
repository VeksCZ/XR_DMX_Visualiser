using UnityEngine;

// Strobo na Gigbaru: 4 kulaté studeně bílé LED rovnoměrně po šířce čela tyče, 0-30 Hz.
public class Strobe : MonoBehaviour
{
    [Header("Stav (později plní DMX)")]
    [Range(0, 1)] public float dimmer;
    public float strobeHz;
    public float[] ledLevels = { 1f, 1f, 1f, 1f }; // jas jednotlivých LED (GigBar ch 27-30)
    public float lightIntensity = 90f;

    [Header("Parametry")]
    [Tooltip("Rozteč LED (m) – všechny stejně daleko od sebe, symetricky kolem středu")]
    public float spacing = 0.26f;
    [Tooltip("Průměr kulaté LED (m)")]
    public float ledSize = 0.035f;

    Renderer[] leds = new Renderer[4];
    Light glow;
    static readonly Color cool = new Color(0.9f, 0.95f, 1f);

    public void Build()
    {
        for (int i = 0; i < 4; i++)
            leds[i] = VisUtil.Emitter(PrimitiveType.Cylinder, transform, new Vector3((i - 1.5f) * spacing, 0, 0.001f),
                new Vector3(ledSize, 0.002f, ledSize), new Vector3(90, 0, 0));
        // záblesk svítí jen dopředu – široký spot před čelem, aby se nerozsvítila celá konstrukce baru
        glow = VisUtil.Spot(transform, 150f, 110f, 12f);
        glow.transform.localPosition = new Vector3(0, 0, 0.03f);
    }

    void Update()
    {
        if (glow == null) return;
        float I = dimmer * VisUtil.StrobeGate(strobeHz, 0.1f);
        float sum = 0f;
        for (int i = 0; i < 4; i++)
        {
            VisUtil.SetColor(leds[i], cool, I * ledLevels[i] * 45f);
            sum += ledLevels[i];
        }
        glow.color = cool;
        glow.intensity = I * sum * 0.25f * lightIntensity;
    }
}
