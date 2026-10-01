using UnityEngine;

// Moving head z Gigbaru: pan 540°, tilt 180°, 9 barev + open, 9 gob + open, 17°.
// Hlava v klidu (pan 0.5, tilt 0.5) míří vodorovně dopředu (+Z).
public class MovingHead : MonoBehaviour
{
    [Header("Stav (později plní DMX)")]
    [Range(0, 1)] public float pan = 0.5f;
    [Range(0, 1)] public float tilt = 0.5f;
    [Range(0, 1)] public float dimmer;
    public Color color = Color.white;
    public float strobeHz;
    [Range(0, 9)] public int gobo;

    [Header("Parametry")]
    public float panRange = 540f;
    public float tiltRange = 180f;
    public float maxPanSpeed = 300f;   // °/s - simulace motorů, aby hlava neskákala
    public float maxTiltSpeed = 200f;
    public float beamAngle = 17f;
    public float beamLength = 12f;
    public float lightIntensity = 60f;

    Transform yoke, head;
    Renderer beam, lens;
    Light spot;
    float curPan, curTilt;
    int curGobo = -1;

    public void Build()
    {
        var m = VisUtil.BodyMat;
        // Patka
        VisUtil.Prim(PrimitiveType.Cube, transform, new Vector3(0, 0.03f, 0), new Vector3(0.16f, 0.06f, 0.16f), m);
        // Jho (pan)
        yoke = new GameObject("Yoke").transform;
        yoke.SetParent(transform, false);
        yoke.localPosition = new Vector3(0, 0.06f, 0);
        VisUtil.Prim(PrimitiveType.Cube, yoke, new Vector3(0, 0.01f, 0), new Vector3(0.19f, 0.02f, 0.08f), m);
        VisUtil.Prim(PrimitiveType.Cube, yoke, new Vector3(-0.088f, 0.1f, 0), new Vector3(0.015f, 0.2f, 0.08f), m);
        VisUtil.Prim(PrimitiveType.Cube, yoke, new Vector3(0.088f, 0.1f, 0), new Vector3(0.015f, 0.2f, 0.08f), m);
        // Hlava (tilt)
        head = new GameObject("Head").transform;
        head.SetParent(yoke, false);
        head.localPosition = new Vector3(0, 0.14f, 0);
        VisUtil.Prim(PrimitiveType.Cylinder, head, new Vector3(0, 0, -0.01f), new Vector3(0.15f, 0.1f, 0.15f), m, new Vector3(90, 0, 0));
        lens = VisUtil.Emitter(PrimitiveType.Cylinder, head, new Vector3(0, 0, 0.091f), new Vector3(0.08f, 0.002f, 0.08f), new Vector3(90, 0, 0));

        beam = VisUtil.Beam(head, beamAngle, beamLength, 0.035f);
        beam.transform.localPosition = new Vector3(0, 0, 0.093f);

        spot = VisUtil.Spot(head, beamAngle * 1.25f, beamAngle * 0.8f, 25f);
        spot.transform.localPosition = new Vector3(0, 0, 0.1f);
    }

    void Update()
    {
        if (head == null) return;
        float tp = (pan - 0.5f) * panRange;
        float tt = (tilt - 0.5f) * tiltRange;
        curPan = Mathf.MoveTowards(curPan, tp, maxPanSpeed * Time.deltaTime);
        curTilt = Mathf.MoveTowards(curTilt, tt, maxTiltSpeed * Time.deltaTime);
        yoke.localRotation = Quaternion.Euler(0, curPan, 0);
        head.localRotation = Quaternion.Euler(curTilt, 0, 0);

        float I = dimmer * VisUtil.StrobeGate(strobeHz);
        VisUtil.SetColor(beam, color, I * 0.9f);
        VisUtil.SetColor(lens, color, I * 6f);
        spot.color = color;
        spot.intensity = I * lightIntensity;

        if (gobo != curGobo)
        {
            curGobo = gobo;
            spot.cookie = Gobos.Get(gobo);
        }
    }
}
