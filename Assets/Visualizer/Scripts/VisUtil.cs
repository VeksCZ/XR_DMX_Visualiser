using System.Collections.Generic;
using UnityEngine;

// Sdílené pomocné funkce: materiály, primitiva, generování kuželů paprsků.
public static class VisUtil
{
    public static Material BeamMat, EmissiveMat, BodyMat, FloorMat, WallMat;
    static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
    static MaterialPropertyBlock mpb;
    static Shader litShader;

    public static void Init(Shader beam, Shader emissive, Shader lit = null)
    {
        if (BeamMat != null) return;
        litShader = lit;
        BeamMat = new Material(beam != null ? beam : Shader.Find("Visualizer/Beam"));
        EmissiveMat = new Material(emissive != null ? emissive : Shader.Find("Visualizer/Emissive"));
        BodyMat = LitMat(new Color(0.06f, 0.06f, 0.07f));
        FloorMat = LitMat(new Color(0.35f, 0.33f, 0.3f));
        WallMat = LitMat(new Color(0.5f, 0.48f, 0.45f));
    }

    public static Material LitMat(Color c)
    {
        var sh = litShader != null ? litShader : Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh);
        m.SetColor("_BaseColor", c);
        m.SetColor("_Color", c);
        m.SetFloat("_Smoothness", 0.2f);
        m.SetFloat("_Glossiness", 0.2f);
        return m;
    }

    // Pozor: Unity válec má výšku 2 jednotky (osa Y) a průměr 1 -> scale (d, h/2, d)
    public static Transform Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(type);
        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }

    public static Renderer Emitter(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler = default)
    {
        var r = Prim(type, parent, pos, scale, EmissiveMat, euler).GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        SetColor(r, Color.black, 0);
        return r;
    }

    // Kužel paprsku míří po lokální ose +Z.
    public static Renderer Beam(Transform parent, float angleDeg, float length, float startRadius, string name = "Beam")
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = ConeMesh(angleDeg, length, startRadius);
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = BeamMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        SetColor(r, Color.black, 0);
        return r;
    }

    public static Light Spot(Transform parent, float angle, float inner, float range)
    {
        var go = new GameObject("Spot");
        go.transform.SetParent(parent, false);
        var l = go.AddComponent<Light>();
        l.type = LightType.Spot;
        l.spotAngle = angle;
        l.innerSpotAngle = inner;
        l.range = range;
        l.shadows = LightShadows.None;
        l.intensity = 0;
        return l;
    }

    // Spot s cookie z teček – promítá vícepaprskové efekty (derby, laser) na stěny a podlahu i bez hazu.
    public static Light DotSpot(Transform parent, float angle, float range, Texture2D cookie)
    {
        var l = Spot(parent, angle, angle * 0.9f, range);
        l.name = "DotSpot";
        l.cookie = cookie;
        return l;
    }

    static readonly Dictionary<string, Texture2D> cookieCache = new Dictionary<string, Texture2D>();

    // Cookie s tečkami ve směrech dirs (lokální, osa +Z), barva tečky podle cols.
    // Mapování odpovídá perspektivní projekci spotu se spotAngle.
    public static Texture2D DotCookie(string key, Vector3[] dirs, Color[] cols, float spotAngle, float dotDeg, int size = 512)
    {
        if (cookieCache.TryGetValue(key, out var cached) && cached != null) return cached;
        var px = new Color32[size * size];
        float tHalf = Mathf.Tan(spotAngle * 0.5f * Mathf.Deg2Rad);
        for (int k = 0; k < dirs.Length; k++)
        {
            Vector3 d = dirs[k].normalized;
            if (d.z <= 0.05f) continue;
            float u = 0.5f + 0.5f * (d.x / d.z) / tHalf;
            float v = 0.5f + 0.5f * (d.y / d.z) / tHalf;
            float cos = d.z;
            float r = Mathf.Max(1.2f, Mathf.Tan(dotDeg * 0.5f * Mathf.Deg2Rad) / (cos * cos) / tHalf * 0.5f * size);
            int cx = Mathf.RoundToInt(u * size), cy = Mathf.RoundToInt(v * size), ri = Mathf.CeilToInt(r + 1);
            for (int y = cy - ri; y <= cy + ri; y++)
                for (int x = cx - ri; x <= cx + ri; x++)
                {
                    if (x < 1 || y < 1 || x >= size - 1 || y >= size - 1) continue;   // okraj musí zůstat černý
                    float a = Mathf.Clamp01(r + 0.5f - Mathf.Sqrt((x - u * size) * (x - u * size) + (y - v * size) * (y - v * size)));
                    if (a <= 0f) continue;
                    var c = cols[k] * a;
                    int i = y * size + x;
                    var o = px[i];
                    px[i] = new Color32((byte)Mathf.Max(o.r, c.r * 255f), (byte)Mathf.Max(o.g, c.g * 255f), (byte)Mathf.Max(o.b, c.b * 255f), 255);
                }
        }
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "DotCookie " + key };
        tex.SetPixels32(px);
        tex.Apply(false, true);
        cookieCache[key] = tex;
        return tex;
    }

    public static Light Point(Transform parent, Vector3 pos, float range)
    {
        var go = new GameObject("Glow");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = range;
        l.shadows = LightShadows.None;
        l.intensity = 0;
        return l;
    }

    public static void SetColor(Renderer r, Color c, float intensity)
    {
        if (mpb == null) mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);
        mpb.SetColor("_Color", c);
        mpb.SetFloat("_Intensity", intensity);
        r.SetPropertyBlock(mpb);
    }

    // Jednoduché strobo: 0 Hz = svítí trvale.
    public static float StrobeGate(float hz, float duty = 0.15f)
    {
        if (hz <= 0.01f) return 1f;
        return Mathf.Repeat(Time.time * hz, 1f) < duty ? 1f : 0f;
    }

    // Převod RGBWA (0-1) na RGB pro zobrazení.
    public static Color RGBWA(float r, float g, float b, float w, float a)
    {
        return new Color(
            r + w * 1.0f + a * 1.0f,
            g + w * 0.95f + a * 0.55f,
            b + w * 0.85f + a * 0.05f);
    }

    public static Mesh ConeMesh(float angleDeg, float length, float startRadius, int seg = 24, int rings = 8)
    {
        string key = angleDeg + "_" + length + "_" + startRadius;
        Mesh cached;
        if (meshCache.TryGetValue(key, out cached) && cached != null) return cached;

        float endR = startRadius + length * Mathf.Tan(angleDeg * 0.5f * Mathf.Deg2Rad);
        float slope = (endR - startRadius) / length;
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        for (int r = 0; r <= rings; r++)
        {
            float t = r / (float)rings;
            float rad = Mathf.Lerp(startRadius, endR, t);
            for (int s = 0; s <= seg; s++)
            {
                float a = s / (float)seg * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                verts.Add(dir * rad + Vector3.forward * (t * length));
                norms.Add((dir - Vector3.forward * slope).normalized);
                uvs.Add(new Vector2(s / (float)seg, t));
            }
        }
        for (int r = 0; r < rings; r++)
            for (int s = 0; s < seg; s++)
            {
                int i = r * (seg + 1) + s;
                tris.Add(i); tris.Add(i + seg + 1); tris.Add(i + 1);
                tris.Add(i + 1); tris.Add(i + seg + 1); tris.Add(i + seg + 2);
            }

        var m = new Mesh { name = "Cone_" + key };
        m.SetVertices(verts);
        m.SetNormals(norms);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        meshCache[key] = m;
        return m;
    }
}
