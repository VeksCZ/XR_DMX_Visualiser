using UnityEngine;

// Procedurální gobo textury (cookies pro Spot light). Nejsou to přesné kopie gob
// z Gigbaru, jen 9 různých vzorů, aby se dalo poznat, že se gobo mění.
public static class Gobos
{
    static Texture2D[] cache;
    const int N = 128;

    public static Texture2D Get(int index)
    {
        if (index <= 0) return null; // 0 = open
        if (cache == null) cache = new Texture2D[10];
        index = Mathf.Clamp(index, 1, 9);
        if (cache[index] == null) cache[index] = Make(index);
        return cache[index];
    }

    static Texture2D Make(int id)
    {
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Atan2(v, u);
                bool on = Pattern(id, u, v, r, a) && r < 0.95f;
                byte b = on ? (byte)255 : (byte)0;
                px[y * N + x] = new Color32(b, b, b, b);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static bool Pattern(int id, float u, float v, float r, float a)
    {
        switch (id)
        {
            case 1: // kruh teček
                for (int i = 0; i < 7; i++)
                {
                    float ang = i / 7f * Mathf.PI * 2f;
                    float dx = u - Mathf.Cos(ang) * 0.6f, dy = v - Mathf.Sin(ang) * 0.6f;
                    if (dx * dx + dy * dy < 0.03f) return true;
                }
                return r < 0.15f;
            case 2: return Mathf.Sin(a * 8f) > 0.4f;                        // paprsky
            case 3: return Mathf.Sin(r * 22f) > 0.2f;                       // soustředné kruhy
            case 4: return Mathf.Sin(u * 14f) > 0.3f && Mathf.Sin(v * 14f) > 0.3f; // mřížka teček
            case 5: return Mathf.Sin(a * 3f + r * 14f) > 0.3f;              // spirála
            case 6: return u + 0.25f * Mathf.Sin(v * 6f) > 0f;              // půlměsíc / vlna
            case 7: return Mathf.Abs(u) < 0.18f || Mathf.Abs(v) < 0.18f;    // kříž
            case 8: return Mathf.PerlinNoise(u * 4f + 10f, v * 4f + 10f) > 0.55f; // střepy
            default: return Mathf.Cos(a * 5f) * 0.35f + 0.55f > r;          // květ
        }
    }
}
