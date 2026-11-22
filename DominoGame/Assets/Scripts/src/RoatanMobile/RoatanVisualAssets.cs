using System.Collections.Generic;
using UnityEngine;

public static class RoatanVisualAssets
{
    private static bool initialized;

    public static Sprite RoundedCardSprite { get; private set; }
    public static Sprite RoundedOutlineSprite { get; private set; }
    public static Sprite CircleSprite { get; private set; }
    public static Sprite TableSurfaceSprite { get; private set; }
    public static Sprite WoodFrameSprite { get; private set; }
    public static Sprite DominoBackSprite { get; private set; }
    public static Sprite GlowSlotSprite { get; private set; }
    public static Sprite ShockwaveRingSprite { get; private set; }
    public static Sprite RoatanIslandMapSprite { get; private set; }
    public static Sprite SunsetReefGlowSprite { get; private set; }
    public static Sprite CoconutPalmSprite { get; private set; }

    private static readonly Dictionary<string, Sprite> DominoFaceCache = new Dictionary<string, Sprite>();

    public static readonly Color DeepOceanTeal = new Color(0.03f, 0.28f, 0.34f, 1f);
    public static readonly Color CaribbeanTurquoise = new Color(0.06f, 0.48f, 0.51f, 1f);
    public static readonly Color BrightLagoon = new Color(0.10f, 0.65f, 0.68f, 1f);
    public static readonly Color WarmTeak = new Color(0.36f, 0.20f, 0.10f, 1f);
    public static readonly Color RichMahogany = new Color(0.24f, 0.13f, 0.06f, 1f);
    public static readonly Color IslandGold = new Color(0.95f, 0.74f, 0.24f, 1f);
    public static readonly Color AmberHighlight = new Color(0.95f, 0.62f, 0.12f, 1f);
    public static readonly Color SunsetCoral = new Color(0.91f, 0.38f, 0.28f, 1f);
    public static readonly Color PalmGreen = new Color(0.14f, 0.66f, 0.39f, 1f);
    public static readonly Color IvoryTile = new Color(0.98f, 0.96f, 0.91f, 1f);
    public static readonly Color DarkSlate = new Color(0.08f, 0.13f, 0.19f, 0.95f);

    public static void EnsureInitialized()
    {
        if (initialized && RoundedCardSprite != null) return;
        initialized = true;

        RoundedCardSprite = CreateRoundedRectSprite(96, 96, 22, Color.white, new Color(0.90f, 0.90f, 0.90f, 1f), 22);
        RoundedOutlineSprite = CreateRoundedOutlineSprite(96, 96, 22, 6, Color.white);
        CircleSprite = CreateCircleSprite(96, Color.white);
        TableSurfaceSprite = CreateTropicalTableSprite(512, 768);
        WoodFrameSprite = CreateWoodStripSprite(128, 128);
        DominoBackSprite = CreateDominoBackSprite(128, 256);
        GlowSlotSprite = CreateGlowSlotSprite(128, 256);
        ShockwaveRingSprite = CreateShockwaveRingSprite(192);
        RoatanIslandMapSprite = LoadOrCreateRoatanIslandMapSprite();
        SunsetReefGlowSprite = CreateSunsetReefGlowSprite(512, 256);
        CoconutPalmSprite = CreateCoconutPalmSprite(256, 256);
    }

    public static Sprite GetDominoSprite(int topPip, int bottomPip)
    {
        EnsureInitialized();
        string key = $"{topPip}_{bottomPip}";
        if (DominoFaceCache.TryGetValue(key, out Sprite cached) && cached != null)
        {
            return cached;
        }

        Sprite created = CreateDominoFaceSprite(128, 256, topPip, bottomPip);
        DominoFaceCache[key] = created;
        return created;
    }

    private static Sprite CreateRoundedRectSprite(int w, int h, int radius, Color topColor, Color bottomColor, int borderSlice)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            float t = (float)y / (h - 1);
            Color baseCol = Color.Lerp(bottomColor, topColor, t);

            for (int x = 0; x < w; x++)
            {
                float alpha = RoundedRectAlpha(x, y, w, h, radius);
                pixels[y * w + x] = new Color(baseCol.r, baseCol.g, baseCol.b, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(borderSlice, borderSlice, borderSlice, borderSlice));
    }

    private static Sprite CreateRoundedOutlineSprite(int w, int h, int radius, int thickness, Color color)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float outer = RoundedRectAlpha(x, y, w, h, radius);
                float inner = RoundedRectAlpha(x - thickness, y - thickness, w - thickness * 2, h - thickness * 2, Mathf.Max(2, radius - thickness));
                float a = Mathf.Clamp01(outer - inner);
                pixels[y * w + x] = new Color(color.r, color.g, color.b, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    private static Sprite CreateCircleSprite(int size, Color color)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        float c = (size - 1) * 0.5f;
        float r = c - 1.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                float alpha = Mathf.Clamp01(r - dist + 1f);
                pixels[y * size + x] = new Color(color.r, color.g, color.b, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateShockwaveRingSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        float c = (size - 1) * 0.5f;
        float maxR = c - 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / maxR;
                if (dist > 1f)
                {
                    pixels[y * size + x] = Color.clear;
                    continue;
                }

                // Crisp outer shockwave ring + softer inner dust ripple
                float outerRing = Mathf.Exp(-Mathf.Pow((dist - 0.84f) / 0.11f, 2f));
                float innerRipple = Mathf.Exp(-Mathf.Pow((dist - 0.52f) / 0.18f, 2f)) * 0.42f;
                float alpha = Mathf.Clamp01(outerRing * 0.92f + innerRipple);
                pixels[y * size + x] = new Color(1f, 0.96f, 0.84f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateTropicalTableSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[w * h];

        Color centerCol = new Color(0.08f, 0.53f, 0.54f, 1f); // Luminous Roatan lagoon turquoise-emerald
        Color edgeCol = new Color(0.03f, 0.25f, 0.30f, 1f);   // Deep Caribbean reef edge
        Color woodOuter = WarmTeak;
        Color woodInner = RichMahogany;
        Color goldTrim = new Color(0.86f, 0.67f, 0.24f, 1f);

        int woodBorder = 22;
        int trimWidth = 3;

        for (int y = 0; y < h; y++)
        {
            float ny = ((float)y / (h - 1)) * 2f - 1f;
            for (int x = 0; x < w; x++)
            {
                float nx = ((float)x / (w - 1)) * 2f - 1f;
                int distEdge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));

                if (distEdge < woodBorder)
                {
                    float grain = Mathf.PerlinNoise(x * 0.06f, y * 0.22f);
                    Color wc = Color.Lerp(woodInner, woodOuter, grain);
                    pixels[y * w + x] = wc;
                }
                else if (distEdge < woodBorder + trimWidth)
                {
                    pixels[y * w + x] = goldTrim;
                }
                else
                {
                    float radial = Mathf.Clamp01(Mathf.Sqrt(nx * nx * 0.85f + ny * ny * 0.65f));
                    Color col = Color.Lerp(centerCol, edgeCol, radial * radial);
                    // Subtle tropical woven felt + soft ocean caustics
                    float wave = Mathf.PerlinNoise(x * 0.025f, y * 0.025f) * 0.045f;
                    float grain = ((x + y) % 2 == 0 ? 0.008f : -0.008f);
                    col.r = Mathf.Clamp01(col.r + wave + grain);
                    col.g = Mathf.Clamp01(col.g + wave * 1.2f + grain);
                    col.b = Mathf.Clamp01(col.b + wave * 1.1f + grain);
                    pixels[y * w + x] = col;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateWoodStripSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float g = Mathf.PerlinNoise(x * 0.04f, y * 0.25f);
                Color col = Color.Lerp(RichMahogany, WarmTeak, g);
                if (y >= h - 4) col = IslandGold;
                pixels[y * w + x] = col;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateDominoBackSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[w * h];
        int radius = 18;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float a = RoundedRectAlpha(x, y, w, h, radius);
                bool inBorder = x < 8 || x >= w - 8 || y < 8 || y >= h - 8;
                Color c = inBorder ? new Color(0.84f, 0.67f, 0.28f, a) : new Color(0.08f, 0.23f, 0.32f, a);
                pixels[y * w + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateGlowSlotSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[w * h];
        int radius = 18;
        int thickness = 8;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float outer = RoundedRectAlpha(x, y, w, h, radius);
                float inner = RoundedRectAlpha(x - thickness, y - thickness, w - thickness * 2, h - thickness * 2, radius - thickness);
                float edge = Mathf.Clamp01(outer - inner);
                float fill = inner * 0.28f;
                float a = Mathf.Clamp01(edge + fill);
                pixels[y * w + x] = new Color(1f, 0.85f, 0.28f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateDominoFaceSprite(int w, int h, int topPip, int bottomPip)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[w * h];

        int radius = 18;
        Color faceTop = new Color(0.99f, 0.98f, 0.94f, 1f);
        Color faceBottom = new Color(0.92f, 0.89f, 0.82f, 1f);
        Color borderCol = new Color(0.73f, 0.68f, 0.58f, 1f);
        Color dividerCol = new Color(0.22f, 0.20f, 0.18f, 0.85f);

        // Fill ivory tile body with subtle bevel
        for (int y = 0; y < h; y++)
        {
            float t = (float)y / (h - 1);
            Color fill = Color.Lerp(faceBottom, faceTop, t);

            for (int x = 0; x < w; x++)
            {
                float outer = RoundedRectAlpha(x, y, w, h, radius);
                if (outer <= 0.001f)
                {
                    pixels[y * w + x] = Color.clear;
                    continue;
                }

                float inner = RoundedRectAlpha(x - 4, y - 4, w - 8, h - 8, radius - 4);
                Color c = Color.Lerp(borderCol, fill, inner);

                // Center horizontal divider bar
                if (Mathf.Abs(y - h / 2) <= 2 && x >= 14 && x <= w - 14)
                {
                    c = Color.Lerp(c, dividerCol, 0.9f);
                }

                pixels[y * w + x] = new Color(c.r, c.g, c.b, outer);
            }
        }

        // Draw crisp pips (note: Texture2D y=0 is bottom, y=h-1 is top)
        DrawPipsOnHalf(pixels, w, h, topPip, isTopHalf: true);
        DrawPipsOnHalf(pixels, w, h, bottomPip, isTopHalf: false);

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void DrawPipsOnHalf(Color[] pixels, int w, int h, int count, bool isTopHalf)
    {
        if (count <= 0) return;

        Color pipColor = GetPipColor(count);
        float halfH = h * 0.5f;
        float centerY = isTopHalf ? halfH * 1.5f : halfH * 0.5f;
        float centerX = w * 0.5f;
        float dx = w * 0.26f;
        float dy = halfH * 0.27f;
        float pipRadius = w * 0.105f;

        List<Vector2> pts = new List<Vector2>();
        switch (count)
        {
            case 1:
                pts.Add(new Vector2(centerX, centerY));
                break;
            case 2:
                pts.Add(new Vector2(centerX - dx, centerY + dy));
                pts.Add(new Vector2(centerX + dx, centerY - dy));
                break;
            case 3:
                pts.Add(new Vector2(centerX - dx, centerY + dy));
                pts.Add(new Vector2(centerX, centerY));
                pts.Add(new Vector2(centerX + dx, centerY - dy));
                break;
            case 4:
                pts.Add(new Vector2(centerX - dx, centerY + dy));
                pts.Add(new Vector2(centerX + dx, centerY + dy));
                pts.Add(new Vector2(centerX - dx, centerY - dy));
                pts.Add(new Vector2(centerX + dx, centerY - dy));
                break;
            case 5:
                pts.Add(new Vector2(centerX - dx, centerY + dy));
                pts.Add(new Vector2(centerX + dx, centerY + dy));
                pts.Add(new Vector2(centerX, centerY));
                pts.Add(new Vector2(centerX - dx, centerY - dy));
                pts.Add(new Vector2(centerX + dx, centerY - dy));
                break;
            case 6:
                pts.Add(new Vector2(centerX - dx, centerY + dy));
                pts.Add(new Vector2(centerX + dx, centerY + dy));
                pts.Add(new Vector2(centerX - dx, centerY));
                pts.Add(new Vector2(centerX + dx, centerY));
                pts.Add(new Vector2(centerX - dx, centerY - dy));
                pts.Add(new Vector2(centerX + dx, centerY - dy));
                break;
        }

        foreach (Vector2 p in pts)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(p.x - pipRadius - 2));
            int maxX = Mathf.Min(w - 1, Mathf.CeilToInt(p.x + pipRadius + 2));
            int minY = Mathf.Max(0, Mathf.FloorToInt(p.y - pipRadius - 2));
            int maxY = Mathf.Min(h - 1, Mathf.CeilToInt(p.y + pipRadius + 2));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), p);
                    float a = Mathf.Clamp01(pipRadius - dist + 0.8f);
                    if (a > 0f)
                    {
                        Color existing = pixels[y * w + x];
                        pixels[y * w + x] = Color.Lerp(existing, new Color(pipColor.r, pipColor.g, pipColor.b, existing.a), a);
                    }
                }
            }
        }
    }

    private static Color GetPipColor(int count)
    {
        // Classic color-coded high-contrast pips (super easy to scan on phones like Jogatina!)
        switch (count)
        {
            case 1: return new Color(0.14f, 0.36f, 0.72f, 1f); // Deep ocean blue
            case 2: return new Color(0.10f, 0.52f, 0.28f, 1f); // Palm green
            case 3: return new Color(0.76f, 0.17f, 0.17f, 1f); // Hibiscus red
            case 4: return new Color(0.52f, 0.22f, 0.08f, 1f); // Warm mahogany
            case 5: return new Color(0.14f, 0.14f, 0.16f, 1f); // Classic charcoal
            case 6: return new Color(0.11f, 0.11f, 0.13f, 1f); // Bold jet black
            default: return Color.black;
        }
    }

    private static float RoundedRectAlpha(float x, float y, float w, float h, float r)
    {
        if (w <= 0 || h <= 0) return 0f;
        float halfW = w * 0.5f;
        float halfH = h * 0.5f;
        float px = Mathf.Abs(x - (halfW - 0.5f)) - (halfW - r);
        float py = Mathf.Abs(y - (halfH - 0.5f)) - (halfH - r);
        float dist = Mathf.Sqrt(Mathf.Max(px, 0f) * Mathf.Max(px, 0f) + Mathf.Max(py, 0f) * Mathf.Max(py, 0f)) + Mathf.Min(Mathf.Max(px, py), 0f) - r;
        return Mathf.Clamp01(0.5f - dist);
    }

    private static Sprite LoadOrCreateRoatanIslandMapSprite()
    {
        Sprite loaded = Resources.Load<Sprite>("Images/roatan_islandmap");
        if (loaded != null)
        {
            return loaded;
        }

        Texture2D tex = Resources.Load<Texture2D>("Images/roatan_islandmap");
        if (tex != null)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        // Procedural fallback silhouette resembling Roatan + Morat & Barbareta cays
        const int w = 512;
        const int h = 192;
        Texture2D fallback = new Texture2D(w, h, TextureFormat.RGBA32, false);
        fallback.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            float ny = (float)y / (h - 1);
            for (int x = 0; x < w; x++)
            {
                float nx = (float)x / (w - 1);
                float alpha = 0f;

                // Main Roatan island arc (SW West End -> NE Oak Ridge / Camp Bay)
                if (nx >= 0.03f && nx <= 0.83f)
                {
                    float u = (nx - 0.03f) / 0.80f;
                    float spineY = 0.14f + 0.72f * Mathf.Sin(u * Mathf.PI * 0.62f) + 0.04f * Mathf.Sin(u * 14f);
                    float halfThick = (0.04f + 0.095f * Mathf.Sin(u * Mathf.PI)) * (0.85f + 0.15f * Mathf.Cos(u * 18f));
                    float d = Mathf.Abs(ny - spineY);
                    alpha = Mathf.Max(alpha, Mathf.Clamp01((halfThick - d) * 32f));
                }

                // Morat & Barbareta cays at the eastern tip
                float dMorat = Vector2.Distance(new Vector2((nx - 0.875f) * 2.5f, ny - 0.80f), Vector2.zero);
                alpha = Mathf.Max(alpha, Mathf.Clamp01((0.038f - dMorat) * 40f));

                float dBarbareta = Vector2.Distance(new Vector2((nx - 0.945f) * 1.4f, (ny - 0.86f) * 1.8f), Vector2.zero);
                alpha = Mathf.Max(alpha, Mathf.Clamp01((0.068f - dBarbareta) * 35f));

                pixels[y * w + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        fallback.SetPixels(pixels);
        fallback.Apply();
        return Sprite.Create(fallback, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateSunsetReefGlowSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[w * h];

        Color warmGold = new Color(0.97f, 0.77f, 0.28f, 1f);
        Color reefTeal = new Color(0.10f, 0.74f, 0.72f, 1f);

        for (int y = 0; y < h; y++)
        {
            float ny = (float)y / (h - 1);
            for (int x = 0; x < w; x++)
            {
                float nx = (float)x / (w - 1);

                // Warm golden sunset halo slightly above center
                float dxG = (nx - 0.50f) * 1.45f;
                float dyG = (ny - 0.58f) * 1.65f;
                float distG = Mathf.Sqrt(dxG * dxG + dyG * dyG);
                float goldGlow = Mathf.Pow(Mathf.Clamp01(1f - distG * 1.35f), 2.1f) * 0.42f;

                // Broad Caribbean reef-turquoise elliptical glow across the island span
                float dxT = (nx - 0.50f) * 1.08f;
                float dyT = (ny - 0.48f) * 1.60f;
                float distT = Mathf.Sqrt(dxT * dxT + dyT * dyT);
                float tealGlow = Mathf.Pow(Mathf.Clamp01(1f - distT * 1.12f), 1.75f) * 0.48f;

                float totalA = Mathf.Clamp01(goldGlow + tealGlow);
                Color c = totalA > 0.001f
                    ? Color.Lerp(reefTeal, warmGold, goldGlow / (goldGlow + tealGlow + 0.0001f))
                    : Color.clear;

                pixels[y * w + x] = new Color(c.r, c.g, c.b, totalA);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateCoconutPalmSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float[] alphaBuf = new float[w * h];

        // 1. Curved tapering palm trunk (quadratic Bezier from bottom-left-center curving right to crown)
        Vector2 p0 = new Vector2(w * 0.42f, h * 0.02f);
        Vector2 p1 = new Vector2(w * 0.40f, h * 0.46f);
        Vector2 p2 = new Vector2(w * 0.56f, h * 0.72f);

        const int trunkSteps = 90;
        for (int s = 0; s <= trunkSteps; s++)
        {
            float t = s / (float)trunkSteps;
            float inv = 1f - t;
            Vector2 pt = inv * inv * p0 + 2f * inv * t * p1 + t * t * p2;
            float radius = Mathf.Lerp(10.5f, 4.2f, t);
            StampSoftDisc(alphaBuf, w, h, pt, radius);
        }

        // 2. Three small coconuts clustered directly under the crown
        StampSoftDisc(alphaBuf, w, h, p2 + new Vector2(-6f, -6f), 5.8f);
        StampSoftDisc(alphaBuf, w, h, p2 + new Vector2(5f, -7f), 5.6f);
        StampSoftDisc(alphaBuf, w, h, p2 + new Vector2(0f, -11f), 5.4f);

        // 3. Seven arched, drooping tropical palm fronds with serrated hanging leaflets
        float[] frondAnglesDeg = { 165f, 138f, 108f, 75f, 44f, 14f, -16f };
        float[] frondLengths = { 86f, 94f, 84f, 76f, 88f, 92f, 78f };
        float[] frondDroop = { 46f, 34f, 22f, 20f, 32f, 44f, 48f };

        for (int f = 0; f < frondAnglesDeg.Length; f++)
        {
            float rad = frondAnglesDeg[f] * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 end = p2 + dir * frondLengths[f] + new Vector2(0f, -frondDroop[f]);
            Vector2 ctrl = p2 + dir * (frondLengths[f] * 0.55f) + new Vector2(0f, frondDroop[f] * 0.35f);

            const int steps = 42;
            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                float inv = 1f - t;
                Vector2 spine = inv * inv * p2 + 2f * inv * t * ctrl + t * t * end;
                float spineWidth = Mathf.Lerp(3.6f, 1.4f, t);
                StampSoftDisc(alphaBuf, w, h, spine, spineWidth);

                // Hanging serrated palm leaflets along frond body
                if (s > 3 && s < steps - 1 && (s % 2 == 0))
                {
                    float leafLen = Mathf.Sin(t * Mathf.PI) * 20f;
                    Vector2 leafDir = new Vector2(dir.x * 0.25f, -1f).normalized;
                    for (int k = 1; k <= 5; k++)
                    {
                        float lt = k / 5f;
                        Vector2 lpt = spine + leafDir * (leafLen * lt);
                        StampSoftDisc(alphaBuf, w, h, lpt, Mathf.Lerp(2.5f, 0.9f, lt));
                    }
                }
            }
        }

        Color[] pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(1f, 1f, 1f, Mathf.Clamp01(alphaBuf[i]));
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void StampSoftDisc(float[] alphaBuf, int w, int h, Vector2 center, float radius)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radius - 1.5f));
        int maxX = Mathf.Min(w - 1, Mathf.CeilToInt(center.x + radius + 1.5f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radius - 1.5f));
        int maxY = Mathf.Min(h - 1, Mathf.CeilToInt(center.y + radius + 1.5f));

        for (int y = minY; y <= maxY; y++)
        {
            int row = y * w;
            for (int x = minX; x <= maxX; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                float a = Mathf.Clamp01(radius - d + 0.9f);
                if (a > alphaBuf[row + x])
                {
                    alphaBuf[row + x] = a;
                }
            }
        }
    }
}
