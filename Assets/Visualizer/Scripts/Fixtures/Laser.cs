using UnityEngine;

// RGB grating laser: mřížka tenkých paprsků přes 93°, vzor se otáčí.
public class Laser : MonoBehaviour
{
    [Header("Stav (později plní DMX)")]
    [Range(0, 1)] public float intensity;
    public Color color = Color.green;
    public float patternSpeed = 25f; // °/s
    public float strobeHz;

    [Header("Parametry")]
    public float coverage = 93f;
    public int columns = 9, rows = 5;
    public float beamLength = 15f;
    [Tooltip("Laser je jen desítky mW, paprsky jsou v hazu slabé")]
    public float brightness = 0.5f;

    Transform rotor;
    Renderer[] beams;
    Renderer aperture;

    public void Build()
    {
        VisUtil.Prim(PrimitiveType.Cube, transform, new Vector3(0, 0, -0.04f), new Vector3(0.09f, 0.08f, 0.08f), VisUtil.BodyMat);
        aperture = VisUtil.Emitter(PrimitiveType.Sphere, transform, Vector3.zero, new Vector3(0.012f, 0.012f, 0.004f));

        rotor = new GameObject("Rotor").transform;
        rotor.SetParent(transform, false);
        beams = new Renderer[columns * rows];
        float vCov = coverage * 0.45f;
        int k = 0;
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                float yaw = Mathf.Lerp(-coverage * 0.5f, coverage * 0.5f, x / (float)(columns - 1));
                float pitch = Mathf.Lerp(-vCov * 0.5f, vCov * 0.5f, y / (float)(rows - 1));
                var pivot = new GameObject("L" + k).transform;
                pivot.SetParent(rotor, false);
                pivot.localRotation = Quaternion.Euler(pitch, yaw, 0);
                beams[k++] = VisUtil.Beam(pivot, 0.12f, beamLength, 0.001f);
            }
    }

    void Update()
    {
        if (rotor == null) return;
        rotor.Rotate(0, 0, patternSpeed * Time.deltaTime, Space.Self);
        float I = intensity * VisUtil.StrobeGate(strobeHz);
        for (int i = 0; i < beams.Length; i++)
            VisUtil.SetColor(beams[i], color, I * brightness);
        VisUtil.SetColor(aperture, color, I * 4f);
    }
}
