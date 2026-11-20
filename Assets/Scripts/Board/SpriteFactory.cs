using UnityEngine;

namespace Plinko
{
    /// <summary>Sprites generados en runtime para prototipar sin assets. Sustituibles por arte final.</summary>
    public static class SpriteFactory
    {
        static Sprite circle, roundedRect, uiPanel;

        /// <summary>Círculo de 1 unidad de diámetro.</summary>
        public static Sprite Circle => circle != null ? circle : (circle = MakeCircle(128));

        /// <summary>Rectángulo redondeado de 1x1 unidades.</summary>
        public static Sprite RoundedRect => roundedRect != null ? roundedRect : (roundedRect = MakeRounded(128, 0.22f, Vector4.zero));

        /// <summary>Rectángulo redondeado con bordes 9-slice para UI.</summary>
        public static Sprite UIPanel => uiPanel != null ? uiPanel : (uiPanel = MakeRounded(64, 0.3f, new Vector4(22, 22, 22, 22)));

        static Sprite ring, glow;

        /// <summary>Anillo de 1 unidad de diámetro (ondas de impacto).</summary>
        public static Sprite Ring => ring != null ? ring : (ring = MakeRadial(128, (d, r) => Mathf.Clamp01(1f - Mathf.Abs(d - r * 0.85f) / 5f)));

        /// <summary>Brillo radial suave de 1 unidad de diámetro.</summary>
        public static Sprite Glow => glow != null ? glow : (glow = MakeRadial(128, (d, r) => { float t = Mathf.Clamp01(1f - d / r); return t * t; }));

        static Sprite bucket;

        /// <summary>
        /// Cubo visto de frente (1x1 unidades, escala de grises para teñir): cuerpo que se
        /// estrecha hacia abajo con volumen cilíndrico, dos aros, borde superior y boca oscura.
        /// </summary>
        public static Sprite Bucket => bucket != null ? bucket : (bucket = MakeBucket(128));

        static Sprite MakeBucket(int size)
        {
            var tex = NewTex(size);
            var px = new Color32[size * size];
            const float rimBottom = 0.80f, top = 0.97f, bottomY = 0.03f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;     // -1..1
                float v = (y + 0.5f) / size;               // 0 abajo .. 1 arriba
                float aa = 2f / size;
                float a, shade;

                if (v >= rimBottom && v <= top)
                {
                    // Borde superior: más ancho, con esquinas redondeadas
                    float hw = 0.98f;
                    float corner = 0.08f;
                    float dx = Mathf.Max(0, Mathf.Abs(u) - (hw - corner));
                    float dy = Mathf.Max(0, Mathf.Max(rimBottom + corner - v, v - (top - corner)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - corner;
                    a = Mathf.Clamp01(-d / aa + 0.5f);
                    float cyl = Mathf.Cos(u / hw * Mathf.PI * 0.5f);
                    shade = 0.85f + 0.15f * cyl;
                    // boca del cubo: elipse oscura en la parte de arriba del borde
                    float eu = u / 0.86f, ev = (v - 0.915f) / 0.045f;
                    if (eu * eu + ev * ev < 1f) shade = 0.28f + 0.12f * Mathf.Clamp01(-ev);
                    // sombra bajo el borde
                    if (v < rimBottom + 0.02f) shade *= 0.8f;
                }
                else if (v >= bottomY && v < rimBottom)
                {
                    // Cuerpo trapezoidal
                    float t = (v - bottomY) / (rimBottom - bottomY);
                    float hw = Mathf.Lerp(0.70f, 0.90f, t);
                    a = Mathf.Clamp01((hw - Mathf.Abs(u)) / aa + 0.5f);
                    // esquinas inferiores redondeadas
                    if (v < bottomY + 0.08f)
                    {
                        float c = 0.08f;
                        float dx = Mathf.Max(0, Mathf.Abs(u) - (hw - c));
                        float dy = bottomY + c - v;
                        a = Mathf.Min(a, Mathf.Clamp01((c - Mathf.Sqrt(dx * dx + dy * dy)) / aa + 0.5f));
                    }
                    float cyl = Mathf.Cos(Mathf.Clamp(u / hw, -1f, 1f) * Mathf.PI * 0.5f);
                    shade = 0.45f + 0.45f * cyl;
                    // reflejo vertical a la izquierda
                    float hl = Mathf.Exp(-Mathf.Pow((u / hw + 0.45f) / 0.12f, 2));
                    shade += 0.25f * hl;
                    // aros del cubo
                    float band1 = Mathf.Abs(v - 0.30f), band2 = Mathf.Abs(v - 0.58f);
                    if (band1 < 0.025f || band2 < 0.025f) shade *= 0.72f;
                    // más oscuro hacia abajo
                    shade *= Mathf.Lerp(0.8f, 1f, t);
                }
                else { px[y * size + x] = new Color32(0, 0, 0, 0); continue; }

                byte g = (byte)(Mathf.Clamp01(shade) * 255);
                px[y * size + x] = new Color32(g, g, g, (byte)(Mathf.Clamp01(a) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite arrow;

        /// <summary>Flecha apuntando hacia abajo (para el tutorial).</summary>
        public static Sprite Arrow => arrow != null ? arrow : (arrow = MakeArrow(64, 96));

        static Sprite MakeArrow(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            float headH = h * 0.45f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float a;
                if (fy < headH)
                {
                    // punta: triángulo cuya anchura crece hacia arriba
                    float half = (fy / headH) * (w * 0.5f);
                    a = Mathf.Clamp01(half - Mathf.Abs(fx - w * 0.5f) + 0.5f);
                }
                else
                {
                    // tallo con extremo redondeado
                    float half = w * 0.18f;
                    a = Mathf.Clamp01(half - Mathf.Abs(fx - w * 0.5f) + 0.5f);
                    float top = h - half;
                    if (fy > top) a = Mathf.Min(a, Mathf.Clamp01(half - Vector2.Distance(new Vector2(fx, fy), new Vector2(w * 0.5f, top)) + 0.5f));
                }
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100);
        }

        static Sprite sphere;

        /// <summary>
        /// Esfera sombreada en escala de grises (se tiñe con el color del SpriteRenderer):
        /// luz desde arriba a la izquierda, borde oscurecido y un reflejo suave.
        /// </summary>
        public static Sprite Sphere => sphere != null ? sphere : (sphere = MakeSphere(128));

        static Sprite MakeSphere(int size)
        {
            var tex = NewTex(size);
            var px = new Color32[size * size];
            float r = size * 0.5f;
            var light = new Vector3(-0.45f, 0.55f, 0.7f).normalized;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - r) / r, dy = (y + 0.5f - r) / r;
                float d2 = dx * dx + dy * dy;
                float dist = Mathf.Sqrt(d2) * r;
                float a = Mathf.Clamp01(r - dist);
                if (a <= 0) { px[y * size + x] = new Color32(0, 0, 0, 0); continue; }

                var n = new Vector3(dx, dy, Mathf.Sqrt(Mathf.Max(0, 1 - d2)));
                float diffuse = Mathf.Max(0, Vector3.Dot(n, light));
                float rim = Mathf.Pow(Mathf.Clamp01(d2), 3f);                // oscurece el borde
                float spec = Mathf.Pow(Mathf.Max(0, Vector3.Dot(Vector3.Reflect(-light, n), Vector3.forward)), 24f);
                float v = Mathf.Clamp01(0.42f + 0.58f * diffuse - 0.25f * rim + 0.35f * spec);
                // reflejo de luz de abajo (rebote) para que no quede plano
                v += 0.12f * Mathf.Clamp01(-dy) * (1 - rim);
                byte c = (byte)(Mathf.Clamp01(v) * 255);
                px[y * size + x] = new Color32(c, c, c, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        /// <summary>Degradado vertical de 1x1 unidades (arriba → abajo).</summary>
        public static Sprite VerticalGradient(Color top, Color bottom)
        {
            const int h = 256;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            for (int y = 0; y < h; y++) tex.SetPixel(0, y, Color.Lerp(bottom, top, y / (h - 1f)));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), h);
        }

        static Sprite MakeRadial(int size, System.Func<float, float, float> alpha)
        {
            var tex = NewTex(size);
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float a = Mathf.Clamp01(alpha(Mathf.Sqrt(dx * dx + dy * dy), r));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Texture2D NewTex(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        static Sprite MakeCircle(int size)
        {
            var tex = NewTex(size);
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeRounded(int size, float radiusFrac, Vector4 border)
        {
            var tex = NewTex(size);
            var px = new Color32[size * size];
            float half = size * 0.5f;
            float rad = size * radiusFrac;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // SDF de rectángulo redondeado
                float qx = Mathf.Abs(x + 0.5f - half) - (half - rad);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - rad);
                float outside = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude;
                float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0) - rad;
                float a = Mathf.Clamp01(0.5f - d);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0,
                SpriteMeshType.FullRect, border);
        }
    }
}
