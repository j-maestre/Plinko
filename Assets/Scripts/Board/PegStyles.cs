using UnityEngine;

namespace Plinko
{
    /// <summary>
    /// Aspecto de los pinchos según el nivel de "Pinchos reforzados": cada pocos niveles cambian de material
    /// (piedra → cobre → hierro → plata → cristal → amatista → rubí → zafiro → estelar) para que la mejora se vea.
    /// Las texturas son en gris y se tiñen con el color del material (así el destello del golpe sigue funcionando).
    /// </summary>
    public static class PegStyles
    {
        enum Look { Matte, Metal, Brushed, Gem, Star }

        static readonly int[] Thresholds = { 0, 1, 3, 6, 10, 15, 21, 28, 36 };
        static readonly string[] Names = { "Piedra", "Cobre", "Hierro", "Plata", "Cristal", "Amatista", "Rubí", "Zafiro", "Estelar" };
        static readonly Look[] Looks = { Look.Matte, Look.Metal, Look.Brushed, Look.Metal, Look.Gem, Look.Gem, Look.Gem, Look.Gem, Look.Star };
        static readonly Color[] Colors =
        {
            new Color(0.55f, 0.58f, 0.70f),   // piedra (apagado)
            new Color(0.93f, 0.55f, 0.35f),   // cobre
            new Color(0.66f, 0.71f, 0.80f),   // hierro
            new Color(0.92f, 0.94f, 1.00f),   // plata
            new Color(0.62f, 0.95f, 1.00f),   // cristal
            new Color(0.78f, 0.52f, 1.00f),   // amatista
            new Color(1.00f, 0.38f, 0.45f),   // rubí
            new Color(0.38f, 0.58f, 1.00f),   // zafiro
            new Color(1.00f, 0.88f, 0.98f),   // estelar
        };

        static readonly Sprite[] sprites = new Sprite[Thresholds.Length];

        public static int Count => Thresholds.Length;

        /// <summary>Estilo que corresponde a un nivel de la mejora.</summary>
        public static int StageFor(int level)
        {
            int s = 0;
            for (int i = 0; i < Thresholds.Length; i++) if (level >= Thresholds[i]) s = i;
            return s;
        }

        public static string Name(int stage) => Names[Mathf.Clamp(stage, 0, Names.Length - 1)];
        public static Color Color(int stage) => Colors[Mathf.Clamp(stage, 0, Colors.Length - 1)];
        /// <summary>Los de más nivel son un poco más grandes.</summary>
        public static float SizeMul(int stage) => 1f + 0.035f * stage;
        /// <summary>Brillo alrededor (solo las gemas y el estelar).</summary>
        public static float HaloAlpha(int stage) => stage < 4 ? 0f : 0.10f + 0.03f * (stage - 4);

        public static Sprite Sprite(int stage)
        {
            stage = Mathf.Clamp(stage, 0, sprites.Length - 1);
            if (sprites[stage] == null) sprites[stage] = Make(Looks[stage]);
            return sprites[stage];
        }

        static Sprite Make(Look look)
        {
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);
                float alpha = Mathf.Clamp01((0.96f - d) * N * 0.5f);
                float l = Shade(look, u, v, d);
                byte g = (byte)(Mathf.Clamp01(l) * 255);
                px[y * N + x] = new Color32(g, g, g, (byte)(alpha * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return UnityEngine.Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        }

        static float Shade(Look look, float u, float v, float d)
        {
            // luz suave desde arriba a la izquierda
            float light = 0.72f + 0.22f * Mathf.Clamp(-u * 0.6f + v * 0.8f, -1f, 1f) * (1f - d * 0.5f);
            float spec = Mathf.Pow(Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(-0.32f, 0.36f)) / 0.38f), 2f);
            switch (look)
            {
                case Look.Matte:
                    // piedra: grano irregular, sin brillo
                    float grain = Mathf.PerlinNoise(u * 4f + 10f, v * 4f + 3f) * 0.25f;
                    return light * 0.85f + grain - 0.08f;

                case Look.Metal:
                    // metal pulido: aro oscuro en el borde y reflejo marcado
                    float rim = d > 0.74f ? 0.62f : 1f;
                    return light * rim + spec * 0.55f;

                case Look.Brushed:
                    // metal cepillado: rayitas horizontales y aro
                    float lines = 0.92f + 0.08f * Mathf.Sin(v * 60f + Mathf.PerlinNoise(v * 9f, 1f) * 4f);
                    float rim2 = d > 0.76f ? 0.6f : 1f;
                    return light * lines * rim2 + spec * 0.35f;

                case Look.Gem:
                    return Gem(u, v, d, spec);

                default:
                {
                    // estelar: gema con destello de cuatro puntas en el centro
                    float star = Mathf.Max(0f, 1f - Mathf.Abs(u) * 9f) * Mathf.Max(0f, 1f - Mathf.Abs(v) * 1.6f)
                               + Mathf.Max(0f, 1f - Mathf.Abs(v) * 9f) * Mathf.Max(0f, 1f - Mathf.Abs(u) * 1.6f);
                    return Gem(u, v, d, spec) * 0.85f + star * 0.6f;
                }
            }
        }

        /// <summary>Talla de gema: mesa central y facetas alrededor con brillos alternos.</summary>
        static float Gem(float u, float v, float d, float spec)
        {
            if (d < 0.42f) return 0.95f + spec * 0.3f;   // mesa
            float ang = Mathf.Atan2(v, u);
            int facet = Mathf.FloorToInt((ang + Mathf.PI) / (Mathf.PI * 2f) * 8f);
            float shade = (facet % 2 == 0) ? 0.88f : 0.62f;
            shade += 0.12f * Mathf.Cos(ang - 2.2f);          // las facetas de arriba a la izquierda reciben más luz
            if (d > 0.82f) shade *= 0.78f;                    // filo
            return shade + spec * 0.4f;
        }
    }
}
