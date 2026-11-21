using UnityEngine;

/// Generates and caches procedural, ultra-crisp anime VFX textures and sprites in memory.
/// Completely standalone: zero missing asset dependencies, instant load time, silky smooth rendering.
public static class AuraVFXLibrary
{
    static Sprite glowDisc;
    static Sprite sparkStar;
    static Sprite cometStreak;
    static Sprite runicCircle;
    static Texture2D impactActionLines;
    static Texture2D impactShatter;

    /// Soft exponential radial glow disc for aura motes, light halos, and flares.
    public static Sprite GetGlowDisc()
    {
        if (glowDisc != null) return glowDisc;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Aura_GlowDisc",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float radius = center;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) / radius;

                if (dist >= 1f)
                {
                    pixels[y * size + x] = new Color32(255, 255, 255, 0);
                }
                else
                {
                    // Exponential falloff for super soft radiant glow
                    float alpha = Mathf.Exp(-dist * dist * 4f) * (1f - dist);
                    byte a = (byte)(Mathf.Clamp01(alpha) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        glowDisc = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return glowDisc;
    }

    /// Sharp, radiant 4-pointed anime spark star for energy flares and impacts.
    public static Sprite GetSparkStar()
    {
        if (sparkStar != null) return sparkStar;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Aura_SparkStar",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = Mathf.Abs((x - center) / center);
                float ny = Mathf.Abs((y - center) / center);

                float star = Mathf.Max(0f, 1f - (nx * 0.15f + ny * 2f)) +
                             Mathf.Max(0f, 1f - (nx * 2f + ny * 0.15f));

                float core = Mathf.Max(0f, 1f - Mathf.Sqrt(nx * nx + ny * ny) * 2.5f);
                float val = Mathf.Clamp01(star * 0.65f + core);

                byte a = (byte)(val * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        sparkStar = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return sparkStar;
    }

    /// Tapered comet energy streak for sword drop trails and leap afterimage wakes.
    public static Sprite GetCometStreak()
    {
        if (cometStreak != null) return cometStreak;

        const int width = 32;
        const int height = 96;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Aura_CometStreak",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[width * height];
        float centerX = (width - 1) * 0.5f;

        for (int y = 0; y < height; y++)
        {
            float normY = y / (float)(height - 1); // 0 at bottom, 1 at top (head)
            float halfW = Mathf.Lerp(1.5f, centerX, Mathf.Sqrt(normY));

            for (int x = 0; x < width; x++)
            {
                float dx = Mathf.Abs(x - centerX);
                if (dx > halfW)
                {
                    pixels[y * width + x] = new Color32(255, 255, 255, 0);
                }
                else
                {
                    float across = 1f - (dx / halfW);
                    float along = Mathf.Pow(normY, 1.8f);
                    float alpha = across * along;
                    byte a = (byte)(Mathf.Clamp01(alpha) * 255f);
                    pixels[y * width + x] = new Color32(255, 255, 255, a);
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        cometStreak = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.9f), height);
        return cometStreak;
    }

    /// Concentric celestial summoning array: outer rune border, 8-pointed star, glyph rings.
    public static Sprite GetRunicCircle()
    {
        if (runicCircle != null) return runicCircle;

        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Aura_RunicCircle",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float maxR = center * 0.95f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);

                float intensity = 0f;

                // 1. Outer rim
                float ring1 = Mathf.Max(0f, 1f - Mathf.Abs(r - maxR) / 2.2f);
                // 2. Secondary outer ring with tick marks
                float ring2 = Mathf.Max(0f, 1f - Mathf.Abs(r - maxR * 0.88f) / 1.6f);
                float ticks = (r > maxR * 0.88f && r < maxR) ? Mathf.Abs(Mathf.Sin(angle * 16f)) : 0f;

                // 3. Middle ring
                float ring3 = Mathf.Max(0f, 1f - Mathf.Abs(r - maxR * 0.65f) / 1.8f);

                // 4. Sacred 8-pointed star rays
                float starR = maxR * 0.65f;
                float star = 0f;
                if (r <= starR)
                {
                    float oct = Mathf.Cos(angle * 8f);
                    float octVal = Mathf.Abs(oct);
                    if (octVal > 0.85f)
                    {
                        star = (octVal - 0.85f) / 0.15f * (1f - (r / starR));
                    }
                }

                // 5. Inner glyph core
                float ring4 = Mathf.Max(0f, 1f - Mathf.Abs(r - maxR * 0.32f) / 1.6f);
                float core = Mathf.Max(0f, 1f - (r / (maxR * 0.2f)));

                intensity = ring1 + ring2 + ticks * 0.7f + ring3 + star * 0.8f + ring4 + core * 0.5f;
                byte a = (byte)(Mathf.Clamp01(intensity) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        runicCircle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size * 0.5f);
        return runicCircle;
    }

    /// Fullscreen manga/anime radial speedlines for high-octane Impact Frames.
    public static Texture2D GetImpactActionLines()
    {
        if (impactActionLines != null) return impactActionLines;

        const int size = 512;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Aura_ImpactActionLines",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) / center;
                float angle = Mathf.Atan2(dy, dx);

                // High-frequency manga action rays
                float ray1 = Mathf.Sin(angle * 32f);
                float ray2 = Mathf.Cos(angle * 17f + 1.2f);
                float ray3 = Mathf.Sin(angle * 53f - 0.7f);
                float composite = (ray1 + ray2 + ray3) / 3f;

                // Open near center focal point, heavy at screen edges
                float mask = Mathf.SmoothStep(0.2f, 0.95f, dist);
                float alpha = (composite > 0.1f ? 1f : 0f) * mask;

                byte a = (byte)(Mathf.Clamp01(alpha) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        impactActionLines = tex;
        return impactActionLines;
    }

    /// High-contrast comic angular slash lines & shards for inverted impact frames.
    public static Texture2D GetImpactShatter()
    {
        if (impactShatter != null) return impactShatter;

        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Aura_ImpactShatter",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point
        };

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;

                // Sharp diagonal slashing facets
                float slash1 = Mathf.Abs(dx + dy * 0.6f);
                float slash2 = Mathf.Abs(dx * 0.7f - dy);
                float slash3 = Mathf.Abs(dx * 1.5f + dy * 1.5f);

                bool isShard = (slash1 < 0.08f) || (slash2 < 0.06f && (dx * dx + dy * dy) > 0.15f) || (slash3 < 0.12f && (dx * dx + dy * dy) > 0.35f);

                pixels[y * size + x] = isShard ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        impactShatter = tex;
        return impactShatter;
    }

    static Sprite iceSpike;
    static Sprite snowflakeRune;
    static Sprite frostShard;

    /// Crystalline, faceted ice lance with glowing cyan body and sharp white core.
    public static Sprite GetIceSpike()
    {
        if (iceSpike != null) return iceSpike;

        const int width = 32;
        const int height = 96;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Aura_IceSpike",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[width * height];
        float centerX = (width - 1) * 0.5f;

        for (int y = 0; y < height; y++)
        {
            float ny = y / (float)(height - 1); // 0 = base, 1 = tip
            // Tapered diamond silhouette
            float halfW = (1f - ny * 0.82f) * centerX * (1f - Mathf.Pow(ny, 3.5f));

            for (int x = 0; x < width; x++)
            {
                float dx = Mathf.Abs(x - centerX);
                if (dx > halfW || halfW <= 0.001f)
                {
                    pixels[y * width + x] = new Color32(0, 0, 0, 0);
                }
                else
                {
                    float edge = dx / halfW;
                    float facet = Mathf.Cos(edge * Mathf.PI * 0.5f);
                    float spine = Mathf.Pow(1f - edge, 3.5f); // intense white-hot central spine

                    Color c = Color.Lerp(new Color(0.2f, 0.75f, 1f, 0.9f), Color.white, spine);
                    c = Color.Lerp(new Color(0.05f, 0.35f, 0.85f, 0.6f), c, facet);

                    byte a = (byte)(Mathf.Clamp01(facet * (0.4f + spine * 0.6f)) * 255f);
                    pixels[y * width + x] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), a);
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        iceSpike = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), height * 0.5f);
        return iceSpike;
    }

    /// Hexagonal glacial snowflake mandala array for sub-zero ground summoning circles.
    public static Sprite GetSnowflakeRune()
    {
        if (snowflakeRune != null) return snowflakeRune;

        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Aura_SnowflakeRune",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float maxR = center * 0.92f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);

                if (r > maxR)
                {
                    pixels[y * size + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                // 6-fold snowflake symmetry
                float hexAngle = Mathf.Abs(Mathf.Sin(angle * 3f)); // 6 spikes
                float arm = (hexAngle < 0.12f) ? (1f - hexAngle / 0.12f) * (1f - (r / maxR)) : 0f;

                // Feathered branch needles
                float feather = 0f;
                if (r > maxR * 0.35f && r < maxR * 0.85f)
                {
                    float subAngle = Mathf.Abs(Mathf.Sin(angle * 6f + r * 0.2f));
                    if (subAngle > 0.88f) feather = (subAngle - 0.88f) / 0.12f * 0.8f;
                }

                // Concentric frost rings
                float ring1 = Mathf.Max(0f, 1f - Mathf.Abs(r - maxR * 0.95f) / 2f);
                float ring2 = Mathf.Max(0f, 1f - Mathf.Abs(r - maxR * 0.55f) / 1.8f);
                float ring3 = Mathf.Max(0f, 1f - Mathf.Abs(r - maxR * 0.25f) / 1.5f);
                float core = Mathf.Max(0f, 1f - r / (maxR * 0.15f));

                float val = Mathf.Clamp01(arm * 0.9f + feather * 0.7f + ring1 * 0.75f + ring2 * 0.8f + ring3 * 0.6f + core);
                Color c = Color.Lerp(new Color(0.35f, 0.85f, 1f), Color.white, core * 0.8f);

                byte a = (byte)(val * 255f);
                pixels[y * size + x] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        snowflakeRune = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size * 0.5f);
        return snowflakeRune;
    }

    /// Sharp diamond ice fragment for shattered crystal explosions.
    public static Sprite GetFrostShard()
    {
        if (frostShard != null) return frostShard;

        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Aura_FrostShard",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = Mathf.Abs((x - center) / center);
                float ny = Mathf.Abs((y - center) / center);

                float diamond = nx + ny * 1.6f;
                if (diamond <= 1f)
                {
                    float facet = 1f - diamond;
                    Color c = Color.Lerp(new Color(0.2f, 0.8f, 1f), Color.white, facet);
                    byte a = (byte)(Mathf.Clamp01(facet * 1.4f) * 255f);
                    pixels[y * size + x] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), a);
                }
                else
                {
                    pixels[y * size + x] = new Color32(0, 0, 0, 0);
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        frostShard = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return frostShard;
    }

    static Sprite holySword;
    static Sprite dimensionalRift;
    static Sprite slashCrescent;
    static Sprite sonicBoomRing;
    static Sprite rockSpike;

    /// Radiant golden divine greatsword (Excalibur) with glowing fuller, wings, and golden pommel.
    public static Sprite GetHolySword()
    {
        if (holySword != null) return holySword;

        const int width = 48;
        const int height = 144;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Aura_HolySword",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[width * height];
        float centerX = (width - 1) * 0.5f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = Mathf.Abs(x - centerX);
                Color c = Color.clear;

                if (y >= 40) // BLADE & TIP
                {
                    float bladeY = (y - 40) / (float)(height - 40); // 0 at guard, 1 at tip
                    float maxHalfW = 8.5f * (1f - Mathf.Pow(bladeY, 4.5f)); // tapers smoothly to sharp point

                    if (dx <= maxHalfW)
                    {
                        float edgeRatio = dx / Mathf.Max(0.01f, maxHalfW);
                        float spine = Mathf.Pow(1f - edgeRatio, 3f); // white-hot core
                        float glow = 1f - edgeRatio;

                        // Golden-white divine energy blade
                        Color edgeCol = new Color(1f, 0.82f, 0.35f, 0.95f);
                        Color coreCol = Color.white;
                        c = Color.Lerp(edgeCol, coreCol, spine);
                        c.a = Mathf.Clamp01(glow * 1.5f);
                    }
                    else if (dx <= maxHalfW + 3.5f) // outer aura flare
                    {
                        float auraT = 1f - ((dx - maxHalfW) / 3.5f);
                        c = new Color(1f, 0.75f, 0.2f, auraT * 0.4f);
                    }
                }
                else if (y >= 32) // CROSSGUARD
                {
                    float guardHalfW = 20f * Mathf.Sin((y - 32) / 8f * Mathf.PI);
                    if (dx <= guardHalfW)
                    {
                        c = new Color(1f, 0.85f, 0.25f, 1f); // ornate gold
                        if (dx < 3f) c = Color.white; // central gem highlight
                    }
                }
                else if (y >= 12) // GRIP / HILT
                {
                    if (dx <= 3.5f)
                    {
                        float gripBand = Mathf.Abs(Mathf.Sin(y * 1.2f));
                        c = Color.Lerp(new Color(0.25f, 0.18f, 0.12f, 1f), new Color(0.85f, 0.7f, 0.2f, 1f), gripBand * 0.6f);
                    }
                }
                else // POMMEL
                {
                    float pommelDy = Mathf.Abs(y - 6);
                    float pommelDist = Mathf.Sqrt(dx * dx + pommelDy * pommelDy);
                    if (pommelDist <= 5.5f)
                    {
                        float glow = 1f - (pommelDist / 5.5f);
                        c = Color.Lerp(new Color(1f, 0.8f, 0.2f, 1f), Color.white, glow * 0.8f);
                    }
                }

                pixels[y * width + x] = c;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        // Center pivot so rotating/scaling blade works cleanly
        holySword = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 72);
        return holySword;
    }

    /// Golden celestial dimensional rift where swords emerge from the heavens.
    public static Sprite GetDimensionalRift()
    {
        if (dimensionalRift != null) return dimensionalRift;

        const int width = 96;
        const int height = 32;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Aura_DimensionalRift",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[width * height];
        float cx = (width - 1) * 0.5f;
        float cy = (height - 1) * 0.5f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = (x - cx) / cx;
                float ny = (y - cy) / cy;
                float dist = Mathf.Sqrt(nx * nx + ny * ny * 3.5f);

                if (dist >= 1f)
                {
                    pixels[y * width + x] = new Color32(0, 0, 0, 0);
                }
                else
                {
                    float rim = Mathf.Max(0f, 1f - Mathf.Abs(dist - 0.75f) / 0.25f);
                    float core = Mathf.Max(0f, 1f - dist * 1.5f);
                    float val = Mathf.Clamp01(rim * 0.9f + core * 1.2f);

                    Color col = Color.Lerp(new Color(1f, 0.7f, 0.1f, 0.95f), Color.white, core);
                    pixels[y * width + x] = new Color32((byte)(col.r * 255f), (byte)(col.g * 255f), (byte)(col.b * 255f), (byte)(val * 255f));
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        dimensionalRift = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 48);
        return dimensionalRift;
    }

    /// Luminous anime crescent slash arc with tapered ends, glowing edge and soft inner falloff.
    public static Sprite GetSlashCrescent()
    {
        if (slashCrescent != null) return slashCrescent;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Aura_SlashCrescent",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float outerR = center * 0.92f;
        float innerR = center * 0.50f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg; // -180 to 180

                // Arc spans from -45 to 135 degrees (180 deg sweep)
                if (angle >= -45f && angle <= 135f && r >= innerR && r <= outerR)
                {
                    float angleNorm = (angle - (-45f)) / 180f; // 0 at tail, 1 at head
                    float angleTaper = Mathf.Sin(angleNorm * Mathf.PI);

                    // Radial cross-section: razor sharp at outer radius, soft falloff inwards
                    float rNorm = (r - innerR) / (outerR - innerR); // 0 at inner, 1 at outer
                    float razorEdge = Mathf.Pow(rNorm, 2.5f);

                    float intensity = razorEdge * angleTaper;
                    Color c = Color.Lerp(new Color(0.3f, 0.75f, 1f, 0f), Color.white, Mathf.Pow(rNorm, 4f));
                    c = Color.Lerp(c, Color.white, intensity);

                    byte a = (byte)(Mathf.Clamp01(intensity * 1.5f) * 255f);
                    pixels[y * size + x] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), a);
                }
                else
                {
                    pixels[y * size + x] = new Color32(0, 0, 0, 0);
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        slashCrescent = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size * 0.5f);
        return slashCrescent;
    }

    /// Sonic boom compressed shockwave donut ring.
    public static Sprite GetSonicBoomRing()
    {
        if (sonicBoomRing != null) return sonicBoomRing;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Aura_SonicBoomRing",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float radius = center * 0.85f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float r = Mathf.Sqrt(dx * dx + dy * dy);

                float diff = Mathf.Abs(r - radius);
                if (diff < 12f)
                {
                    float t = 1f - (diff / 12f);
                    float alpha = Mathf.Sin(t * Mathf.PI * 0.5f);
                    Color c = Color.Lerp(new Color(1f, 0.9f, 0.6f, 0f), Color.white, Mathf.Pow(t, 2f));
                    pixels[y * size + x] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), (byte)(alpha * 255f));
                }
                else
                {
                    pixels[y * size + x] = new Color32(0, 0, 0, 0);
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        sonicBoomRing = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size * 0.5f);
        return sonicBoomRing;
    }

    /// Jagged erupting stone pillar / rock spire for ground slams and crater eruptions.
    public static Sprite GetRockSpike()
    {
        if (rockSpike != null) return rockSpike;

        const int width = 48;
        const int height = 96;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Aura_RockSpike",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[width * height];
        float centerX = (width - 1) * 0.5f;

        for (int y = 0; y < height; y++)
        {
            float ny = y / (float)(height - 1); // 0 at base, 1 at tip
            // Asymmetrical jagged silhouette
            float taper = (1f - ny * 0.92f);
            float noise = Mathf.Sin(ny * 18f) * 2.5f * (1f - ny);
            float halfW = Mathf.Max(1f, centerX * taper + noise);

            for (int x = 0; x < width; x++)
            {
                float dx = Mathf.Abs(x - centerX);
                if (dx > halfW)
                {
                    pixels[y * width + x] = new Color32(0, 0, 0, 0);
                }
                else
                {
                    float edge = dx / halfW;
                    // Charcoal stone body with molten underglow at the base
                    Color stone = Color.Lerp(new Color(0.28f, 0.25f, 0.22f), new Color(0.12f, 0.1f, 0.1f), edge);
                    Color molten = new Color(1f, 0.45f, 0.1f);

                    float baseHeat = Mathf.Clamp01(1f - (ny / 0.35f)) * (1f - edge * 0.7f);
                    Color finalCol = Color.Lerp(stone, molten, baseHeat);

                    pixels[y * width + x] = new Color32((byte)(finalCol.r * 255f), (byte)(finalCol.g * 255f), (byte)(finalCol.b * 255f), 255);
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        // Pivot at base bottom center
        rockSpike = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.05f), 48);
        return rockSpike;
    }

    static Sprite enchantedArrow;
    static Sprite slashCross;

    /// Luminous enchanted arrow with glowing energy arrowhead, runic shaft, and feathered fletching.
    /// Oriented horizontally along the +X axis (pointing right) for seamless 2D projectile rotation.
    public static Sprite GetEnchantedArrow()
    {
        if (enchantedArrow != null) return enchantedArrow;

        const int width = 96;
        const int height = 32;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Aura_EnchantedArrow",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[width * height];
        float centerY = (height - 1) * 0.5f;

        for (int y = 0; y < height; y++)
        {
            float dy = Mathf.Abs(y - centerY);

            for (int x = 0; x < width; x++)
            {
                Color c = Color.clear;

                if (x >= 64) // BROADHEAD / ARROWHEAD (Right side)
                {
                    float headX = (x - 64) / 31f; // 0 at base of head, 1 at tip
                    float maxDy = Mathf.Lerp(8.5f, 0.5f, headX); // tapers to sharp point at tip

                    if (dy <= maxDy)
                    {
                        float edge = dy / Mathf.Max(0.1f, maxDy);
                        float spine = Mathf.Pow(1f - edge, 2.5f);
                        // Ethereal emerald-cyan glowing energy arrowhead
                        Color auraEdge = new Color(0.15f, 0.95f, 0.75f, 0.95f);
                        c = Color.Lerp(auraEdge, Color.white, spine);
                        c.a = 1f;
                    }
                    else if (dy <= maxDy + 2.5f) // outer glow flare
                    {
                        float flare = 1f - ((dy - maxDy) / 2.5f);
                        c = new Color(0.1f, 0.95f, 0.7f, flare * 0.45f);
                    }
                }
                else if (x >= 20) // ARROW SHAFT (Middle)
                {
                    if (dy <= 2.2f)
                    {
                        // Dark sleek shaft with glowing runic energy pulses
                        float runePulse = Mathf.Abs(Mathf.Sin(x * 0.6f));
                        Color wood = new Color(0.2f, 0.18f, 0.15f, 1f);
                        Color rune = new Color(0.2f, 1f, 0.8f, 1f);
                        c = Color.Lerp(wood, rune, runePulse * 0.75f);
                        if (dy < 1f) c = Color.Lerp(c, Color.white, 0.4f); // core highlight
                    }
                    else if (dy <= 3.8f) // shaft energy aura
                    {
                        c = new Color(0.1f, 0.9f, 0.7f, (1f - (dy - 2.2f) / 1.6f) * 0.25f);
                    }
                }
                else // FLETCHING (Left side: feathered energy wings)
                {
                    float wingX = (20 - x) / 20f; // 0 near shaft, 1 at back
                    float maxDy = Mathf.Lerp(2.2f, 7.5f, Mathf.Sqrt(wingX));

                    if (dy <= maxDy)
                    {
                        float featherBarb = Mathf.Abs(Mathf.Sin((x + y) * 0.8f));
                        c = Color.Lerp(new Color(0.1f, 0.85f, 0.65f, 0.9f), Color.white, featherBarb * 0.6f);
                    }
                    else if (dy <= maxDy + 1.5f)
                    {
                        c = new Color(0.1f, 0.85f, 0.65f, 0.2f);
                    }
                }

                pixels[y * width + x] = c;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        // Center pivot (0.6f, 0.5f) puts the balance near the front
        enchantedArrow = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.6f, 0.5f), 48);
        return enchantedArrow;
    }

    /// Dual intersecting cross-slash crescent for critical combo cleaves.
    public static Sprite GetSlashCross()
    {
        if (slashCross != null) return slashCross;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Aura_SlashCross",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;

                // Two perpendicular sweeping diagonal blades
                float d1 = Mathf.Abs(dx - dy * 0.7f);
                float d2 = Mathf.Abs(dx + dy * 0.7f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float intensity = 0f;
                if (dist <= 0.95f)
                {
                    float blade1 = (d1 < 0.12f) ? Mathf.Pow(1f - (d1 / 0.12f), 2f) * (1f - dist) : 0f;
                    float blade2 = (d2 < 0.12f) ? Mathf.Pow(1f - (d2 / 0.12f), 2f) * (1f - dist) : 0f;
                    float core = Mathf.Max(0f, 1f - dist * 3.5f); // blazing center spark
                    intensity = Mathf.Clamp01(blade1 + blade2 + core * 1.5f);
                }

                Color c = Color.Lerp(new Color(1f, 0.8f, 0.25f, 0f), Color.white, intensity);
                byte a = (byte)(Mathf.Clamp01(intensity * 1.8f) * 255f);
                pixels[y * size + x] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        slashCross = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size * 0.5f);
        return slashCross;
    }
}


