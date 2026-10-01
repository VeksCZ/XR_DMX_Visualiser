using UnityEngine;

// Testovací animace bez DMX, aby bylo hned vidět, že scéna funguje.
// Až bude hotový Art-Net přijímač, tento skript se vypne.
[RequireComponent(typeof(SceneBuilder))]
public class DemoDriver : MonoBehaviour
{
    public float bpm = 124f;
    SceneBuilder sb;

    void Start() { sb = GetComponent<SceneBuilder>(); }

    void Update()
    {
        if (sb == null || sb.gigbar == null || sb.gigbar.headL == null) return;
        float beat = Time.time * bpm / 60f;
        int bar = Mathf.FloorToInt(beat / 4f);
        int phrase = bar / 4 % 4; // každé 4 takty jiný look
        float pulse = Mathf.Pow(1f - Mathf.Repeat(beat, 1f), 3f);
        Color hue = Color.HSVToRGB(Mathf.Repeat(Time.time * 0.03f, 1f), 1f, 1f);
        Color hue2 = Color.HSVToRGB(Mathf.Repeat(Time.time * 0.03f + 0.5f, 1f), 1f, 1f);
        var g = sb.gigbar;

        // Moving heady - zrcadlově, osmičky přes parket
        float s = Mathf.Sin(beat * Mathf.PI / 4f);
        float c = Mathf.Cos(beat * Mathf.PI / 8f);
        g.headL.pan = 0.5f + 0.12f * s;
        g.headR.pan = 0.5f - 0.12f * s;
        // tilt 0.5 = paprsek kolmo dolů (hlavy visí), ~0.83 = cca 30° pod vodorovnou dopředu
        g.headL.tilt = g.headR.tilt = 0.80f + 0.06f * c;
        g.headL.dimmer = g.headR.dimmer = phrase == 3 ? pulse : 1f;
        g.headL.color = g.headR.color = phrase % 2 == 0 ? Color.white : hue2;
        g.headL.gobo = g.headR.gobo = bar % 10;

        // Pary
        g.parL.dimmer = g.parR.dimmer = 0.4f + 0.6f * pulse;
        g.parL.color = g.parR.color = hue;

        // Stav, který mohla nechat živá data ze SS (strobo, rychlost motorů)
        g.headL.strobeHz = g.headR.strobeHz = g.parL.strobeHz = g.parR.strobeHz = 0f;
        g.derbyL.strobeHz = g.derbyR.strobeHz = g.laser.strobeHz = 0f;
        g.headL.maxPanSpeed = g.headR.maxPanSpeed = 300f;
        g.headL.maxTiltSpeed = g.headR.maxTiltSpeed = 200f;

        // Pocket Pro na stolku – kříží se přes parket, opačná fáze než GigBar
        if (sb.pockets != null && sb.pockets.Length >= 2 && sb.pockets[0] != null)
        {
            float ps = Mathf.Sin(beat * Mathf.PI / 4f + Mathf.PI);
            sb.pockets[0].pan = 0.5f + 0.08f * ps + 0.04f;
            sb.pockets[1].pan = 0.5f - 0.08f * ps - 0.04f;
            // stojící hlavy: tilt 0.5 = nahoru, ~0.93 = lehce pod vodorovnou dopředu
            sb.pockets[0].tilt = sb.pockets[1].tilt = 0.90f + 0.04f * c;
            sb.pockets[0].dimmer = sb.pockets[1].dimmer = phrase == 0 ? 0f : 1f;
            sb.pockets[0].color = sb.pockets[1].color = phrase == 2 ? hue : Color.white;
            sb.pockets[0].gobo = sb.pockets[1].gobo = (bar + 3) % 8;
            sb.pockets[0].strobeHz = sb.pockets[1].strobeHz = 0f;
            sb.pockets[0].maxPanSpeed = sb.pockets[1].maxPanSpeed = 300f;
            sb.pockets[0].maxTiltSpeed = sb.pockets[1].maxTiltSpeed = 200f;
        }

        // Derby
        bool derbyOn = phrase == 1 || phrase == 2;
        g.derbyL.red = g.derbyR.red = derbyOn && bar % 2 == 0 ? 1 : 0;
        g.derbyL.blue = g.derbyR.blue = derbyOn ? 1 : 0;
        g.derbyL.green = g.derbyR.green = derbyOn && bar % 2 == 1 ? 1 : 0;
        g.derbyL.white = g.derbyR.white = 0;

        // Laser
        g.laser.intensity = phrase == 2 && bar % 4 == 3 ? 1 : 0; // laser jen občas
        g.laser.color = bar % 2 == 0 ? Color.green : new Color(0.2f, 0.2f, 1f);

        // Strobo jednou za frázi
        g.strobe.dimmer = Mathf.Repeat(beat, 16f) > 15f ? 1 : 0;
        g.strobe.strobeHz = 15f;
        for (int i = 0; i < 4; i++) g.strobe.ledLevels[i] = 1f;

        // Tuby - chase
        foreach (var t in sb.tubes)
        {
            if (t == null || t.pixels == null) continue;
            t.master = 1f; // po živých datech mohl zůstat master dimmer na 0
            for (int i = 0; i < t.pixels.Length; i++)
            {
                float k = Mathf.Repeat(beat * 2f - i / (float)t.pixels.Length * 2f, 1f);
                t.pixels[i] = Color.Lerp(hue, hue2, k) * (k < 0.5f ? 1f : 0.15f);
            }
        }

        // Kolegova rampa: pary pulzují, derby s fází GigBaru, helixy se kývají protiběžně
        foreach (var p in sb.blackPars)
        {
            if (p == null) continue;
            p.dimmer = 0.3f + 0.7f * pulse; p.color = hue2; p.strobeHz = 0f;
        }
        foreach (var ds in sb.derbyStrobes)
        {
            if (ds == null || ds.derby == null) continue;
            ds.derby.red = derbyOn && bar % 2 == 0 ? 1 : 0;
            ds.derby.green = derbyOn && bar % 2 == 1 ? 1 : 0;
            ds.derby.blue = derbyOn ? 1 : 0;
            ds.derby.white = 0; ds.derby.strobeHz = 0;
            ds.strobeDimmer = Mathf.Repeat(beat, 16f) > 15f ? 1 : 0;
            ds.strobeHz = 12f;
        }
        foreach (var hx in sb.helixes)
        {
            if (hx == null) continue;
            hx.tilt1 = 0.5f + 0.3f * Mathf.Sin(beat * Mathf.PI / 2f);
            hx.tilt2 = 0.5f - 0.3f * Mathf.Sin(beat * Mathf.PI / 2f);
            hx.color1 = hue; hx.color2 = hue2;
            hx.dimmer = phrase == 0 ? 0f : 1f; hx.strobeHz = 0f;
        }

        // Uplighty - pomalu teplá bílá + barva
        foreach (var p in sb.uplights)
        {
            if (p == null) continue;
            p.dimmer = 0.8f;
            p.strobeHz = 0f;
            p.color = Color.Lerp(VisUtil.RGBWA(0, 0, 0, 0.3f, 0.5f), hue, 0.6f);
        }
    }
}
