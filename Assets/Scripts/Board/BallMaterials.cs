using System.Collections.Generic;
using UnityEngine;

namespace Plinko
{
    /// <summary>
    /// Material de cada nivel de bola: nombre, color para efectos y textura generada por código
    /// (madera con vetas, metales con reflejo de entorno, gemas facetadas, obsidiana, estelar).
    /// </summary>
    public static class BallMaterials
    {
        enum Kind { Wood, Metal, Gem, Glossy, Cosmic }

        struct Mat
        {
            public string Name; public Kind Kind; public Color Color; public float Roughness;
            public Mat(string n, Kind k, Color c, float rough = 0f) { Name = n; Kind = k; Color = c; Roughness = rough; }
        }

        static readonly Mat[] Mats =
        {
            new Mat("Madera",    Kind.Wood,   new Color(0.62f, 0.40f, 0.22f)),
            new Mat("Bronce",    Kind.Metal,  new Color(0.85f, 0.52f, 0.28f), 0.15f),
            new Mat("Plata",     Kind.Metal,  new Color(0.86f, 0.89f, 0.94f), 0.05f),
            new Mat("Hierro",    Kind.Metal,  new Color(0.52f, 0.54f, 0.58f), 0.55f),
            new Mat("Diamante",  Kind.Gem,    new Color(0.85f, 0.96f, 1.00f)),
            new Mat("Platino",   Kind.Metal,  new Color(0.93f, 0.92f, 0.86f), 0.0f),
            new Mat("Zafiro",    Kind.Gem,    new Color(0.20f, 0.38f, 1.00f)),
            new Mat("Esmeralda", Kind.Gem,    new Color(0.12f, 0.82f, 0.42f)),
            new Mat("Rubí",      Kind.Gem,    new Color(0.95f, 0.12f, 0.25f)),
            new Mat("Obsidiana", Kind.Glossy, new Color(0.12f, 0.08f, 0.18f)),
            new Mat("Amatista",  Kind.Gem,    new Color(0.65f, 0.30f, 1.00f)),
            new Mat("Estelar",   Kind.Cosmic, new Color(0.25f, 0.30f, 0.80f)),
        };

        static readonly Dictionary<int, Sprite> cache = new Dictionary<int, Sprite>();

        public static int Count => Mats.Length;
        static Mat Get(int level) => Mats[Mathf.Clamp(level, 1, Mats.Length) - 1];

        public static string GetName(int level)
        {
            if (level <= Mats.Length) return Get(level).Name;
            return Mats[Mats.Length - 1].Name + " +" + (level - Mats.Length); // por si se pasa de la lista
        }

        public static Color FxColor(int level) => Get(level).Color;

        public static Sprite GetSprite(int level)
        {
            int key = Mathf.Clamp(level, 1, Mats.Length);
            if (!cache.TryGetValue(key, out var s)) cache[key] = s = Make(Get(key), key);
            return s;
        }

        // ------------------------------------------------------------------ Generación

        static readonly Vector3 Light = new Vector3(-0.45f, 0.55f, 0.7f).normalized;

        static Sprite Make(Mat m, int seed)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float r = size * 0.5f;
            var rng = new System.Random(seed * 97 + 13);
            float woodOffX = (float)rng.NextDouble() * 0.6f - 0.3f, woodOffY = 1.4f + (float)rng.NextDouble();

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - r) / r, dy = (y + 0.5f - r) / r;
                float d2 = dx * dx + dy * dy;
                float edge = Mathf.Clamp01((1f - Mathf.Sqrt(d2)) * r);
                if (edge <= 0) { px[y * size + x] = new Color32(0, 0, 0, 0); continue; }
                var n = new Vector3(dx, dy, Mathf.Sqrt(Mathf.Max(0, 1 - d2)));
                float diffuse = Mathf.Max(0, Vector3.Dot(n, Light));
                float rim = Mathf.Pow(Mathf.Clamp01(d2), 3f);
                float spec = Mathf.Pow(Mathf.Max(0, Vector3.Dot(Vector3.Reflect(-Light, n), Vector3.forward)), 30f);
                Color c;

                switch (m.Kind)
                {
                    case Kind.Wood:
                    {
                        // anillos de crecimiento vistos de lado + vetas finas
                        float ring = Mathf.Sqrt((dx - woodOffX) * (dx - woodOffX) * 4f + (dy + woodOffY) * (dy + woodOffY));
                        float bands = 0.5f + 0.5f * Mathf.Sin(ring * 26f + Mathf.Sin(dx * 9f) * 1.5f);
                        float grain = 0.5f + 0.5f * Mathf.Sin(dx * 60f + dy * 7f);
                        var baseC = m.Color * (0.75f + 0.35f * bands) * (0.95f + 0.08f * grain);
                        c = baseC * (0.45f + 0.65f * diffuse) - new Color(0.25f, 0.25f, 0.25f, 0) * rim + Color.white * spec * 0.25f;
                        break;
                    }
                    case Kind.Metal:
                    {
                        // reflejo de entorno: cielo claro arriba, horizonte marcado, suelo oscuro
                        var refl = Vector3.Reflect(new Vector3(0, 0, -1), n);
                        float env = refl.y > 0 ? Mathf.Lerp(0.75f, 1.05f, refl.y) : Mathf.Lerp(0.35f, 0.15f, -refl.y);
                        float horizon = Mathf.Exp(-Mathf.Pow(refl.y / 0.06f, 2f)) * 0.35f;
                        float noise = m.Roughness > 0 ? ((float)rng.NextDouble() - 0.5f) * m.Roughness * 0.25f : 0f;
                        float shade = Mathf.Lerp(env, 0.3f + 0.7f * diffuse, m.Roughness) + noise;
                        float sp = spec * (1.2f - m.Roughness);
                        c = m.Color * shade + Color.white * (sp * 0.9f + horizon * (1f - m.Roughness)) - new Color(0.2f, 0.2f, 0.2f, 0) * rim;
                        break;
                    }
                    case Kind.Gem:
                    {
                        // facetas: anillo central (mesa) y 8 sectores alrededor con brillos distintos
                        float ang = Mathf.Atan2(dy, dx) + Mathf.PI;
                        float dist = Mathf.Sqrt(d2);
                        int sector = Mathf.FloorToInt(ang / (Mathf.PI * 2f) * 8f) % 8;
                        int ringIdx = dist < 0.38f ? 0 : dist < 0.72f ? 1 : 2;
                        int facet = ringIdx == 0 ? 0 : 1 + sector + ringIdx * 8;
                        float fb = 0.55f + 0.5f * Hash(facet * 31 + seed);
                        if (ringIdx == 0) fb = 1.05f;
                        float sectorEdge = Mathf.Abs(Mathf.Repeat(ang / (Mathf.PI * 2f) * 8f, 1f) - 0.5f) * 2f; // 1 en el borde
                        float lines = (ringIdx > 0 ? Mathf.SmoothStep(0.88f, 1f, sectorEdge) : 0f)
                                      + Mathf.Exp(-Mathf.Pow((dist - 0.38f) / 0.02f, 2)) + Mathf.Exp(-Mathf.Pow((dist - 0.72f) / 0.02f, 2));
                        c = m.Color * fb * (0.6f + 0.5f * diffuse) + Color.white * (lines * 0.35f + spec * 1.1f)
                            - new Color(0.15f, 0.15f, 0.15f, 0) * rim;
                        break;
                    }
                    case Kind.Glossy:
                    {
                        float sheen = Mathf.Pow(1f - n.z, 2f) * 0.6f; // borde con brillo violeta
                        c = m.Color * (0.6f + 0.6f * diffuse) + new Color(0.55f, 0.35f, 0.9f) * sheen + Color.white * spec * 1.3f;
                        break;
                    }
                    default: // Cosmic
                    {
                        float star = Hash(x * 7 + y * 131 + seed) > 0.985f ? 1f : 0f;
                        float nebula = 0.5f + 0.5f * Mathf.Sin(dx * 5f + Mathf.Sin(dy * 4f) * 2f);
                        var neb = Color.Lerp(m.Color, new Color(0.85f, 0.35f, 0.9f), nebula * 0.6f);
                        c = neb * (0.45f + 0.6f * diffuse) + Color.white * (star * 0.9f + spec * 0.8f);
                        break;
                    }
                }

                c.r = Mathf.Clamp01(c.r); c.g = Mathf.Clamp01(c.g); c.b = Mathf.Clamp01(c.b);
                px[y * size + x] = new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), (byte)(edge * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return UnityEngine.Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static float Hash(int n)
        {
            n = (n << 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
        }
    }
}
