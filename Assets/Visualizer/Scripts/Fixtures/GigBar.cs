using UnityEngine;

// Chauvet GigBAR Move + ILS - zjednodušený model.
// Rozměry 1100 × 144 × 449 mm (d × hl × v).
// Pořadí efektů (zleva doprava při pohledu zepředu): moving head, derby, par,
// laser se strobem, par, derby, moving head. Moving heady a pary visí pod tyčí, derby a laser
// sedí nahoře, strobo (4 LED) je přímo na čele tyče.
// Pozice X uprav podle svého baru.
public class GigBar : MonoBehaviour
{
    [Header("Pozice na tyči (m od středu)")]
    public float headX = 0.47f;
    public float derbyX = 0.32f;
    public float parX = 0.18f;
    [Tooltip("Sklon derby dolů (°)")]
    public float frontTilt = 12f;
    [Tooltip("Laser natočený do stropu (záporné = nahoru), kvůli hostům a fotografům")]
    public float laserTilt = -60f;
    [Tooltip("Sklon visících parů (90 = kolmo dolů)")]
    public float parTilt = 40f;

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

        // Moving heady visí pod tyčí hlavou dolů (otočené o 180° kolem osy Z)
        headL = Make<MovingHead>("MovingHead L", new Vector3(-headX, 0, 0), new Vector3(0, 0, 180));
        headR = Make<MovingHead>("MovingHead R", new Vector3(headX, 0, 0), new Vector3(0, 0, 180));
        headL.Build(); headR.Build();

        // Derby sedí nahoře na tyči
        derbyL = Make<Derby>("Derby L", new Vector3(-derbyX, barH + 0.05f, 0.02f), new Vector3(frontTilt, 0, 0));
        derbyR = Make<Derby>("Derby R", new Vector3(derbyX, barH + 0.05f, 0.02f), new Vector3(frontTilt, 0, 0));
        derbyL.Build(); derbyR.Build();
        derbyR.rotationSpeed = -derbyR.rotationSpeed;

        // Pary visí pod tyčí v třmenu (blíž k tyči než hlavy), míří dolů na parket
        foreach (float sx in new[] { -parX, parX })
        {
            VisUtil.Prim(PrimitiveType.Cube, transform, new Vector3(sx, -0.006f, 0), new Vector3(0.14f, 0.012f, 0.05f), VisUtil.BodyMat);
            VisUtil.Prim(PrimitiveType.Cube, transform, new Vector3(sx - 0.065f, -0.05f, 0), new Vector3(0.01f, 0.09f, 0.04f), VisUtil.BodyMat);
            VisUtil.Prim(PrimitiveType.Cube, transform, new Vector3(sx + 0.065f, -0.05f, 0), new Vector3(0.01f, 0.09f, 0.04f), VisUtil.BodyMat);
        }
        parL = Make<ParLight>("Par L", new Vector3(-parX, -0.08f, 0.02f), new Vector3(parTilt, 0, 0));
        parR = Make<ParLight>("Par R", new Vector3(parX, -0.08f, 0.02f), new Vector3(parTilt, 0, 0));
        parL.ledCount = parR.ledCount = 3;   // kulaté pary se 3 LED do trojúhelníku
        parL.Build(); parR.Build();

        // Laser nahoře na tyči jako derby, ale nízko u tyče
        laser = Make<Laser>("Laser", new Vector3(0, barH + 0.035f, 0.02f), new Vector3(laserTilt, 0, 0));
        laser.Build();

        // Strobo: 4 malé LED vedle sebe přímo na čelní straně tyče
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
