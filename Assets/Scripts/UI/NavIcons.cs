using System.Collections.Generic;
using UnityEngine;

namespace Plinko
{
    /// <summary>Iconos blancos (para teñir) de la barra de navegación, dibujados por código.</summary>
    public static class NavIcons
    {
        static readonly Dictionary<UpgradeCategory, Sprite> cache = new Dictionary<UpgradeCategory, Sprite>();
        const int Size = 96;

        public static Sprite Get(UpgradeCategory c)
        {
            if (cache.TryGetValue(c, out var s)) return s;
            System.Func<float, float, float> shape = c switch
            {
                UpgradeCategory.Bolas => Ball,
                UpgradeCategory.Pinchos => Pegs,
                UpgradeCategory.Rebotes => Bounce,
                UpgradeCategory.Sobrecarga => Bolt,
                _ => Bucket,
            };
            cache[c] = s = Make(shape);
            return s;
        }

        // Cada forma recibe coordenadas en [-1,1] (y hacia arriba) y devuelve cobertura 0..1.
        static float Circle(float x, float y, float cx, float cy, float r) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r ? 1f : 0f;

        static float Ball(float x, float y)
        {
            float a = Circle(x, y, 0, 0, 0.78f);
            float shine = Circle(x, y, -0.28f, 0.3f, 0.2f);
            return a * (1f - shine * 0.55f);   // con un reflejo hueco para que se lea como bola
        }

        static float Pegs(float x, float y)
        {
            float v = 0;
            float r = 0.17f;
            v = Mathf.Max(v, Circle(x, y, 0f, 0.55f, r));
            v = Mathf.Max(v, Circle(x, y, -0.32f, 0.05f, r));
            v = Mathf.Max(v, Circle(x, y, 0.32f, 0.05f, r));
            v = Mathf.Max(v, Circle(x, y, -0.64f, -0.45f, r));
            v = Mathf.Max(v, Circle(x, y, 0f, -0.45f, r));
            v = Mathf.Max(v, Circle(x, y, 0.64f, -0.45f, r));
            return v;
        }

        static float Bounce(float x, float y)
        {
            // arco (la trayectoria del rebote) con punta de flecha al final
            float cx = 0f, cy = -0.35f, R = 0.62f, th = 0.13f;
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            float ang = Mathf.Atan2(y - cy, x - cx) * Mathf.Rad2Deg;
            float arc = (Mathf.Abs(d - R) < th && ang > 20f && ang < 175f) ? 1f : 0f;
            // punta en el extremo derecho del arco, apuntando hacia abajo
            float tx = cx + R * Mathf.Cos(20f * Mathf.Deg2Rad), ty = cy + R * Mathf.Sin(20f * Mathf.Deg2Rad);
            float head = InTriangle(x, y, tx - 0.3f, ty + 0.05f, tx + 0.3f, ty + 0.05f, tx + 0.02f, ty - 0.38f);
            // bolita al inicio
            float dot = Circle(x, y, cx - R, cy + 0.02f, 0.16f);
            return Mathf.Max(arc, Mathf.Max(head, dot));
        }

        static float Bolt(float x, float y)
        {
            var p = new[]
            {
                new Vector2(0.15f, 0.92f), new Vector2(-0.55f, -0.08f), new Vector2(-0.05f, -0.08f),
                new Vector2(-0.25f, -0.92f), new Vector2(0.55f, 0.18f), new Vector2(0.05f, 0.18f),
            };
            return InPolygon(new Vector2(x, y), p) ? 1f : 0f;
        }

        static float Bucket(float x, float y)
        {
            // cubo: cuerpo trapezoidal + borde
            float rim = (y > 0.38f && y < 0.62f && Mathf.Abs(x) < 0.8f) ? 1f : 0f;
            float t = Mathf.InverseLerp(-0.7f, 0.38f, y);
            float body = (y >= -0.7f && y <= 0.38f && Mathf.Abs(x) < Mathf.Lerp(0.5f, 0.68f, t)) ? 1f : 0f;
            float band = (Mathf.Abs(y + 0.15f) < 0.05f) ? 0.45f : 1f;   // aro marcado
            return Mathf.Max(rim, body * band);
        }

        static float InTriangle(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = (x - bx) * (ay - by) - (ax - bx) * (y - by);
            float d2 = (x - cx) * (by - cy) - (bx - cx) * (y - cy);
            float d3 = (x - ax) * (cy - ay) - (cx - ax) * (y - ay);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos) ? 1f : 0f;
        }

        static bool InPolygon(Vector2 pt, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > pt.y) != (poly[j].y > pt.y) &&
                    pt.x < (poly[j].x - poly[i].x) * (pt.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }

        static Sprite Make(System.Func<float, float, float> shape)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[Size * Size];
            const int ss = 3; // supermuestreo para bordes suaves
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float sum = 0;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                {
                    float u = (x + (sx + 0.5f) / ss) / Size * 2f - 1f;
                    float v = (y + (sy + 0.5f) / ss) / Size * 2f - 1f;
                    sum += shape(u, v);
                }
                px[y * Size + x] = new Color32(255, 255, 255, (byte)(sum / (ss * ss) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }
    }
}
