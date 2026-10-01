using UnityEngine;

// BeamZ DerbyStrobe: derby 4× 3W RGBW (112 paprsků) + strobo panel 14× bílá SMD LED.
// Derby část používá stejný model jako derby na GigBaru, jen s víc paprsky a širším vějířem.
public class DerbyStrobe : MonoBehaviour
{
    [HideInInspector] public Derby derby;
    [Range(0, 1)] public float strobeDimmer;
    public float strobeHz;
    public float strobeIntensity = 30f;

    Renderer panel;
    Light glow;
    static readonly Color cool = new Color(0.9f, 0.95f, 1f);

    public void Build()
    {
        var m = VisUtil.BodyMat;
        VisUtil.Prim(PrimitiveType.Cube, transform, new Vector3(0, 0, -0.05f), new Vector3(0.17f, 0.1f, 0.1f), m);
        var d = new GameObject("Derby").transform;
        d.SetParent(transform, false);
        d.localPosition = new Vector3(0.03f, 0, 0.002f);
        derby = d.gameObject.AddComponent<Derby>();
        derby.beamsPerColor = 8;
        derby.coverage = 140f;
        derby.lensCols = 7; derby.lensRows = 4;   // čelo derby: 7 × 4 čoček
        derby.Build();
        // strobo panel vedle čočky derby
        panel = VisUtil.Emitter(PrimitiveType.Cube, transform, new Vector3(-0.055f, 0, 0.001f), new Vector3(0.04f, 0.07f, 0.004f));
        glow = VisUtil.Point(transform, new Vector3(-0.055f, 0, 0.2f), 8f);
    }

    void Update()
    {
        if (panel == null) return;
        float I = strobeDimmer * VisUtil.StrobeGate(strobeHz, 0.1f);
        VisUtil.SetColor(panel, cool, I * 15f);
        glow.color = cool;
        glow.intensity = I * strobeIntensity;
    }
}
