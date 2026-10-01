using UnityEngine;

// Wash par: Gigbar pary (22° beam / 33° field, RGBAW+UV) i čínské battery pary.
// Míří po lokální ose +Z.
public class ParLight : MonoBehaviour
{
    public enum Housing { Round, Box }

    [Header("Stav (později plní DMX)")]
    [Range(0, 1)] public float dimmer;
    public Color color = Color.white;
    public float strobeHz;

    [Header("Parametry")]
    public Housing housing = Housing.Round;
    public float beamAngle = 22f;
    public float fieldAngle = 33f;
    public float beamLength = 8f;
    public float lightIntensity = 25f;
    public float size = 0.09f;

    Renderer core, field, lens;
    Light spot;

    public void Build()
    {
        var m = VisUtil.BodyMat;
        if (housing == Housing.Round)
            VisUtil.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, 0, -size * 0.4f), new Vector3(size, size * 0.4f, size), m, new Vector3(90, 0, 0));
        else
            VisUtil.Prim(PrimitiveType.Cube, transform, new Vector3(0, 0, -size * 0.5f), new Vector3(size * 1.6f, size * 1.6f, size), m);

        lens = VisUtil.Emitter(PrimitiveType.Cylinder, transform, new Vector3(0, 0, 0.002f), new Vector3(size * 0.8f, 0.002f, size * 0.8f), new Vector3(90, 0, 0));

        core = VisUtil.Beam(transform, beamAngle, beamLength, size * 0.35f, "BeamCore");
        field = VisUtil.Beam(transform, fieldAngle, beamLength * 0.7f, size * 0.4f, "BeamField");

        spot = VisUtil.Spot(transform, fieldAngle, beamAngle, 15f);
        spot.transform.localPosition = new Vector3(0, 0, 0.01f);
    }

    void Update()
    {
        if (spot == null) return;
        float I = dimmer * VisUtil.StrobeGate(strobeHz);
        VisUtil.SetColor(core, color, I * 0.45f);
        VisUtil.SetColor(field, color, I * 0.15f);
        VisUtil.SetColor(lens, color, I * 5f);
        spot.color = color;
        spot.intensity = I * lightIntensity;
    }
}
