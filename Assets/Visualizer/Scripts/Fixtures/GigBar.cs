using UnityEngine;

// Chauvet GigBAR Move + ILS - zjednodušený model.
// Rozměry 1100 × 144 × 449 mm (d × hl × v).
// Pořadí efektů (zleva doprava při pohledu zepředu): moving head, derby, par,
// laser se strobem, par, derby, moving head. Pozice X uprav podle svého baru.
public class GigBar : MonoBehaviour
{
    [Header("Pozice na tyči (m od středu)")]
    public float headX = 0.47f;
    public float derbyX = 0.32f;
    public float parX = 0.18f;
    [Tooltip("Sklon parů a derby dolů (°)")]
    public float frontTilt = 12f;
    [Tooltip("Laser natočený do stropu (záporné = nahoru), kvůli hostům a fotografům")]
    public float laserTilt = -60f;

    [HideInInspector] public MovingHead headL, headR;
    [HideInInspector] public ParLight parL, parR;
    [HideInInspector] public Derby derbyL, derbyR;
    [HideInInspector] public Laser laser;
    [HideInInspector] public Strobe strobe;

    const float barH = 0.12f, barD = 0.144f, barL = 1.1f;

    public void Build()
    {
        VisUtil.Prim(PrimitiveType.Cube, transform, new Vector3(0, barH * 0.5f, 0), new Vector3(barL, barH, barD), VisUtil.BodyMat);
        float fz = barD * 0.5f + 0.005f;
        float fy = barH * 0.5f;

        headL = Make<MovingHead>("MovingHead L", new Vector3(-headX, barH, 0), Vector3.zero);
        headR = Make<MovingHead>("MovingHead R", new Vector3(headX, barH, 0), Vector3.zero);
        headL.Build(); headR.Build();

        derbyL = Make<Derby>("Derby L", new Vector3(-derbyX, fy, fz + 0.04f), new Vector3(frontTilt, 0, 0));
        derbyR = Make<Derby>("Derby R", new Vector3(derbyX, fy, fz + 0.04f), new Vector3(frontTilt, 0, 0));
        derbyL.Build(); derbyR.Build();
        derbyR.rotationSpeed = -derbyR.rotationSpeed;

        parL = Make<ParLight>("Par L", new Vector3(-parX, fy, fz + 0.04f), new Vector3(frontTilt, 0, 0));
        parR = Make<ParLight>("Par R", new Vector3(parX, fy, fz + 0.04f), new Vector3(frontTilt, 0, 0));
        parL.Build(); parR.Build();

        laser = Make<Laser>("Laser", new Vector3(0, fy, fz + 0.04f), new Vector3(laserTilt, 0, 0));
        laser.Build();

        strobe = Make<Strobe>("Strobe", new Vector3(0, fy, fz), Vector3.zero);
        strobe.Build();
    }

    T Make<T>(string n, Vector3 pos, Vector3 euler) where T : Component
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        return go.AddComponent<T>();
    }
}
