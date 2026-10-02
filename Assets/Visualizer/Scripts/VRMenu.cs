using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Menu v brýlích: otevře/zavře tlačítko ≡ na levém ovladači (nebo × na panelu).
// Panel se objeví před hlavou a zůstane stát; kliká se paprskem z pravého ovladače + spouští.
// Na levém zápěstí je štítek se stavem (LIVE / DEMO).
public class VRMenu : MonoBehaviour
{
    const float W = 600f, H = 740f, Scale = 0.001f; // 0,6 × 0,74 m

    class Btn { public RectTransform rt; public Image img; public Text label; public Action act; }

    VRRig rig;
    VisualizerMenu app;
    VREnvironment env;
    Btn envVirtual, envScan, passBtn;
    RectTransform panel;
    Text status, hazeText, lightText, demoLabel, feedback, wrist;
    readonly List<Btn> buttons = new List<Btn>();
    readonly List<Btn> camButtons = new List<Btn>();
    InputAction menuBtn, trigger;
    LineRenderer ray;
    Transform dot;
    Font font;
    bool open;
    Btn hover;
    float feedbackUntil, nextRefresh;

    static readonly Color cPanel = new Color(0.07f, 0.08f, 0.1f, 0.94f);
    static readonly Color cBtn = new Color(0.18f, 0.2f, 0.25f, 1f);
    static readonly Color cHover = new Color(0.25f, 0.45f, 0.85f, 1f);
    static readonly Color cOn = new Color(0.2f, 0.55f, 0.3f, 1f);

    void Start()
    {
        rig = GetComponent<VRRig>();
        env = GetComponent<VREnvironment>();
        app = FindFirstObjectByType<VisualizerMenu>();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        menuBtn = new InputAction("Menu", InputActionType.Button);
        menuBtn.AddBinding("<XRController>{LeftHand}/{MenuButton}");
        menuBtn.AddBinding("<XRController>{LeftHand}/menu");
        trigger = new InputAction("Trigger", InputActionType.Button, "<XRController>{RightHand}/{TriggerButton}");
        menuBtn.Enable(); trigger.Enable();

        BuildPanel();
        BuildRay();
        BuildWrist();
        SetOpen(false);
    }

    // ---------- stavba UI ----------
    Canvas NewCanvas(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one * Scale;
        return c;
    }

    RectTransform Rect(Transform parent, float x, float y, float w, float h)
    {
        var go = new GameObject("e", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    Text Label(Transform parent, string s, float x, float y, float w, float h, int size, TextAnchor a = TextAnchor.MiddleLeft)
    {
        var t = Rect(parent, x, y, w, h).gameObject.AddComponent<Text>();
        t.font = font; t.fontSize = size; t.alignment = a; t.color = Color.white; t.text = s;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    Btn Button(Transform parent, string s, float x, float y, float w, float h, Action act, int size = 24)
    {
        var rt = Rect(parent, x, y, w, h);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = cBtn;
        var b = new Btn { rt = rt, img = img, act = act };
        b.label = Label(rt, s, 0, 0, w, h, size, TextAnchor.MiddleCenter);
        buttons.Add(b);
        return b;
    }

    void BuildPanel()
    {
        var c = NewCanvas("VR Menu", null, new Vector2(W, H));
        panel = (RectTransform)c.transform;
        var bg = Rect(panel, 0, 0, W, H).gameObject.AddComponent<Image>();
        bg.color = cPanel;

        Label(panel, "DMX Visualiser", 24, 14, 440, 50, 32);
        Button(panel, "×", W - 70, 14, 50, 50, () => SetOpen(false), 34);

        status = Label(panel, "", 24, 74, W - 48, 64, 22, TextAnchor.UpperLeft);

        var demo = Button(panel, "", 24, 146, W - 48, 64, () => app?.ToggleDemo(), 26);
        demoLabel = demo.label;

        // Haze a světlo v sále vedle sebe
        Label(panel, "Haze", 24, 230, 80, 60, 24);
        Button(panel, "−", 100, 230, 50, 60, () => app?.AddHaze(-0.1f), 34);
        hazeText = Label(panel, "", 152, 230, 76, 60, 22, TextAnchor.MiddleCenter);
        Button(panel, "+", 230, 230, 50, 60, () => app?.AddHaze(0.1f), 34);
        Label(panel, Loc.T("vrLight"), 300, 230, 96, 60, 24);
        Button(panel, "−", 396, 230, 50, 60, () => app?.AddRoomLight(-0.1f), 34);
        lightText = Label(panel, "", 448, 230, 76, 60, 22, TextAnchor.MiddleCenter);
        Button(panel, "+", 526, 230, 50, 60, () => app?.AddRoomLight(0.1f), 34);

        Label(panel, Loc.T("camera"), 24, 304, 300, 36, 22);
        int n = app != null ? app.CameraCount : 0;
        float bw = (W - 48 - (n - 1) * 10) / Mathf.Max(1, n);
        for (int i = 0; i < n; i++)
        {
            int k = i;
            camButtons.Add(Button(panel, app.CameraName(i), 24 + i * (bw + 10), 342, bw, 60, () => app.GoToCamera(k), 22));
        }

        // Prostředí: virtuální sál / naskenovaná místnost, passthrough, umístění stolku
        Label(panel, Loc.T("vrEnv"), 24, 418, 300, 36, 22);
        float hw = (W - 58) / 2f;
        envVirtual = Button(panel, Loc.T("vrEnvVirtual"), 24, 456, hw, 60, () => env?.SetScannedRoom(false), 22);
        envScan = Button(panel, Loc.T("vrEnvScan"), 34 + hw, 456, hw, 60, () => env?.SetScannedRoom(true), 22);
        passBtn = Button(panel, "", 24, 526, hw, 60, () => env?.SetPassthrough(!env.Passthrough), 22);
        Button(panel, Loc.T("vrPlace"), 34 + hw, 526, hw, 60, () => { SetOpen(false); env?.StartPlacement(); }, 22);

        Button(panel, Loc.T("vrCalib"), 24, 610, W - 48, 64, Calibrate, 22);
        feedback = Label(panel, "", 24, 680, W - 48, 50, 20, TextAnchor.MiddleCenter);
    }

    void BuildRay()
    {
        var go = new GameObject("VR Ray");
        ray = go.AddComponent<LineRenderer>();
        ray.positionCount = 2;
        ray.useWorldSpace = true;
        ray.startWidth = 0.004f; ray.endWidth = 0.002f;
        ray.material = Canvas.GetDefaultCanvasMaterial();
        ray.startColor = new Color(0.5f, 0.75f, 1f, 0.9f);
        ray.endColor = new Color(0.5f, 0.75f, 1f, 0.2f);
        var dr = VisUtil.Emitter(PrimitiveType.Sphere, null, Vector3.zero, Vector3.one * 0.012f);
        VisUtil.SetColor(dr, new Color(0.6f, 0.85f, 1f), 3f);
        dot = dr.transform;
    }

    // Malý štítek se stavem na levém zápěstí (vždy viditelný)
    void BuildWrist()
    {
        if (rig == null || rig.LeftHand == null) return;
        var c = NewCanvas("Wrist", rig.LeftHand, new Vector2(160, 44));
        c.transform.localPosition = new Vector3(0f, 0.035f, -0.06f);
        c.transform.localRotation = Quaternion.Euler(50f, 0f, 0f);
        c.transform.localScale = Vector3.one * 0.0005f;
        var bg = Rect(c.transform, 0, 0, 160, 44).gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.7f);
        wrist = Label(c.transform, "", 0, 0, 160, 44, 26, TextAnchor.MiddleCenter);
    }

    void SetOpen(bool o)
    {
        open = o;
        panel.gameObject.SetActive(o);
        ray.enabled = o;
        dot.gameObject.SetActive(false);
        if (o && rig != null && rig.Head != null)
        {
            // před hlavu do výšky očí, natočit k hráči (canvas se čte po své ose +Z)
            Vector3 f = rig.Head.forward; f.y = 0;
            if (f.sqrMagnitude < 0.001f) f = Vector3.forward;
            f.Normalize();
            panel.position = rig.Head.position + f * 0.7f - Vector3.up * 0.05f;
            panel.rotation = Quaternion.LookRotation(f, Vector3.up);
            Refresh();
        }
    }

    void Calibrate()
    {
        if (app == null) return;
        int n = app.CalibrateAllToCenter();
        feedback.text = n > 0 ? Loc.F("vrCalibDone", n) : Loc.T("vrCalibNoData");
        feedback.color = n > 0 ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.75f, 0.3f);
        feedbackUntil = Time.unscaledTime + 5f;
    }

    void Update()
    {
        if (panel == null) return;
        if (menuBtn.WasPressedThisFrame()) SetOpen(!open);

        if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + 0.25f; Refresh(); }

        if (!open || rig == null || rig.RightAim == null) return;

        // paprsek z pravého ovladače → průsečík s rovinou panelu → tlačítko pod ním
        var aim = rig.RightAim;
        var r = new Ray(aim.position, aim.forward);
        var plane = new Plane(panel.forward, panel.position);
        Btn hit = null;
        float len = 2f;
        if (plane.Raycast(r, out float d) && d > 0f && d < 5f)
        {
            Vector3 p = r.GetPoint(d);
            Vector3 lp = panel.InverseTransformPoint(p);
            if (panel.rect.Contains(new Vector2(lp.x, lp.y)))
            {
                len = d;
                dot.gameObject.SetActive(true);
                dot.position = p - panel.forward * 0.002f;
                foreach (var b in buttons)
                {
                    if (!b.rt.gameObject.activeInHierarchy) continue;
                    Vector3 bp = b.rt.InverseTransformPoint(p);
                    if (b.rt.rect.Contains(new Vector2(bp.x, bp.y))) { hit = b; break; }
                }
            }
            else dot.gameObject.SetActive(false);
        }
        else dot.gameObject.SetActive(false);

        ray.SetPosition(0, aim.position);
        ray.SetPosition(1, aim.position + aim.forward * len);

        if (hover != hit)
        {
            hover = hit;
            RefreshColors();
        }
        if (hit != null && trigger.WasPressedThisFrame()) { hit.act?.Invoke(); Refresh(); }
    }

    void Refresh()
    {
        if (app == null) return;
        bool live = app.HasArtNet && !app.DemoForced;
        string st = app.DemoForced ? Loc.T("demoManual")
                  : app.HasArtNet ? Loc.F("liveFrom", app.Sender) + "  ·  " + Mathf.RoundToInt(app.PacketsPerSec) + " pkt/s"
                  : Loc.T("demoRunning");
        if (wrist != null)
        {
            wrist.text = live ? "• LIVE" : "• DEMO";
            wrist.color = live ? new Color(0.35f, 0.95f, 0.5f) : new Color(1f, 0.72f, 0.25f);
        }
        if (!open) return;
        status.text = st + "\n" + Mathf.RoundToInt(app.Fps) + " FPS";
        status.color = live ? new Color(0.6f, 1f, 0.7f) : new Color(1f, 0.82f, 0.5f);
        // bez úvodního symbolu (▶/■ ve vestavěném fontu nemusí být)
        demoLabel.text = System.Text.RegularExpressions.Regex.Replace(app.DemoForced ? Loc.T("stopDemo") : Loc.T("startDemo"), @"^[^\p{L}]+", "");
        hazeText.text = app.HazePct + " %";
        lightText.text = app.RoomLightPct + " %";
        if (env != null)
        {
            passBtn.label.text = "Passthrough: " + (env.Passthrough ? Loc.T("vrOn") : Loc.T("vrOff"));
            envScan.label.text = Loc.T("vrEnvScan") + (env.ScannedRoom ? "  (" + env.PlaneCount + ")" : "");
        }
        if (Time.unscaledTime > feedbackUntil) feedback.text = "";
        RefreshColors();
    }

    void RefreshColors()
    {
        foreach (var b in buttons) b.img.color = b == hover ? cHover : cBtn;
        if (app != null)
            for (int i = 0; i < camButtons.Count; i++)
                if (camButtons[i] != hover && app.CurrentCamera == i) camButtons[i].img.color = cOn;
        if (env != null)
        {
            if (envVirtual != hover && !env.ScannedRoom) envVirtual.img.color = cOn;
            if (envScan != hover && env.ScannedRoom) envScan.img.color = cOn;
            if (passBtn != hover && env.Passthrough) passBtn.img.color = cOn;
        }
    }

    void OnDestroy()
    {
        menuBtn?.Dispose(); trigger?.Dispose();
        if (panel != null) Destroy(panel.gameObject);
        if (ray != null) Destroy(ray.gameObject);
        if (dot != null) Destroy(dot.gameObject);
    }
}
