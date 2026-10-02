using UnityEngine;

// Demo bez DMX: animace všech světel ve scéně podle druhu (hlavy, pary, derby, laser, strobo, tuby, helixy).
// Levá a pravá strana se zrcadlí podle polohy světla.
[RequireComponent(typeof(SceneBuilder))]
public class DemoDriver : MonoBehaviour
{
    public float bpm = 124f;
    SceneBuilder sb;

    void Start() { sb = GetComponent<SceneBuilder>(); }

    void Update()
    {
        if (sb == null) return;
        float beat = Time.time * bpm / 60f;
        int bar = Mathf.FloorToInt(beat / 4f);
        int phrase = bar / 4 % 4; // každé 4 takty jiný look
        float pulse = Mathf.Pow(1f - Mathf.Repeat(beat, 1f), 3f);
        Color hue = Color.HSVToRGB(Mathf.Repeat(Time.time * 0.03f, 1f), 1f, 1f);
        Color hue2 = Color.HSVToRGB(Mathf.Repeat(Time.time * 0.03f + 0.5f, 1f), 1f, 1f);
        float s = Mathf.Sin(beat * Mathf.PI / 4f);
        float c = Mathf.Cos(beat * Mathf.PI / 8f);
        bool derbyOn = phrase == 1 || phrase == 2;
        bool strobeHit = Mathf.Repeat(beat, 16f) > 15f;

        foreach (var fi in sb.instances)
        {
            if (fi.parts == null || fi.entry == null || fi.entry.hidden) continue;
            foreach (var part in fi.parts)
            {
                if (part == null) continue;
                float side = part.transform.position.x < -0.01f ? -1f : 1f;   // levá / pravá strana
                switch (part)
                {
                    case MovingHead h:
                    {
                        bool hanging = part.transform.up.y < 0f;
                        if (hanging)
                        {
                            // zavěšené hlavy (GigBar): zrcadlově osmičky přes parket
                            h.pan = 0.5f - side * 0.12f * s;
                            h.tilt = 0.80f + 0.06f * c;   // ~30° pod vodorovnou dopředu
                            h.dimmer = phrase == 3 ? pulse : 1f;
                            h.color = phrase % 2 == 0 ? Color.white : hue2;
                            h.gobo = bar % 10;
                        }
                        else
                        {
                            // stojící hlavy (Pocket Pro na stole): kříží se přes parket, opačná fáze
                            float ps = Mathf.Sin(beat * Mathf.PI / 4f + Mathf.PI);
                            h.pan = 0.5f - side * (0.08f * ps + 0.04f);
                            h.tilt = 0.90f + 0.04f * c;
                            h.dimmer = phrase == 0 ? 0f : 1f;
                            h.color = phrase == 2 ? hue : Color.white;
                            h.gobo = (bar + 3) % 8;
                        }
                        h.strobeHz = 0f;
                        h.maxPanSpeed = 300f;
                        h.maxTiltSpeed = 200f;
                        break;
                    }
                    case ParLight p:
                        p.strobeHz = 0f;
                        if (fi.slot == "uplight")
                        {
                            p.dimmer = 0.8f;   // uplighty: pomalu teplá bílá + barva
                            p.color = Color.Lerp(VisUtil.RGBWA(0, 0, 0, 0.3f, 0.5f), hue, 0.6f);
                        }
                        else if (fi.slot == "trussHang")
                        {
                            p.dimmer = 0.3f + 0.7f * pulse; p.color = hue2;
                        }
                        else
                        {
                            p.dimmer = 0.4f + 0.6f * pulse; p.color = hue;
                        }
                        break;
                    case Derby d:
                        DerbyLook(d, derbyOn, bar, side);
                        break;
                    case DerbyStrobe ds:
                        if (ds.derby != null) DerbyLook(ds.derby, derbyOn, bar, side);
                        ds.strobeDimmer = strobeHit ? 1 : 0;
                        ds.strobeHz = 12f;
                        break;
                    case Laser l:
                        l.intensity = phrase == 2 && bar % 4 == 3 ? 1 : 0; // laser jen občas
                        l.color = bar % 2 == 0 ? Color.green : new Color(0.2f, 0.2f, 1f);
                        l.strobeHz = 0f;
                        l.patternSpeed = 25f;
                        break;
                    case Strobe st:
                        st.dimmer = strobeHit ? 1 : 0;   // strobo jednou za frázi
                        st.strobeHz = 15f;
                        for (int i = 0; i < st.ledLevels.Length; i++) st.ledLevels[i] = 1f;
                        break;
                    case PixelTube t:
                        if (t.pixels == null) break;
                        t.master = 1f;
                        for (int i = 0; i < t.pixels.Length; i++)
                        {
                            float k = Mathf.Repeat(beat * 2f - i / (float)t.pixels.Length * 2f, 1f);
                            t.pixels[i] = Color.Lerp(hue, hue2, k) * (k < 0.5f ? 1f : 0.15f);
                        }
                        break;
                    case Helix hx:
                        hx.tilt1 = 0.5f + 0.3f * Mathf.Sin(beat * Mathf.PI / 2f);
                        hx.tilt2 = 0.5f - 0.3f * Mathf.Sin(beat * Mathf.PI / 2f);
                        hx.color1 = hue; hx.color2 = hue2;
                        hx.dimmer = phrase == 0 ? 0f : 1f; hx.strobeHz = 0f;
                        break;
                }
            }
        }
    }

    static void DerbyLook(Derby d, bool on, int bar, float side)
    {
        d.red = on && bar % 2 == 0 ? 1 : 0;
        d.green = on && bar % 2 == 1 ? 1 : 0;
        d.blue = on ? 1 : 0;
        d.white = 0;
        d.strobeHz = 0;
        d.rotationSpeed = side < 0 ? 40f : -40f;
    }
}
