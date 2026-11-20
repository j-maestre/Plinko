using UnityEngine;

namespace Plinko
{
    /// <summary>Lingote de oro dibujado por código: cara superior clara, frontal con degradado, biseles y brillo.</summary>
    public static class IngotIcon
    {
        const int W = 128, H = 80;
        static Sprite sprite;

        public static Sprite Sprite => sprite != null ? sprite : (sprite = Make());
        /// <summary>Proporción ancho/alto del sprite.</summary>
        public const float Aspect = (float)W / H;

        static readonly Vector2[] Front = { new Vector2(6, 10), new Vector2(122, 10), new Vector2(110, 50), new Vector2(18, 50) };
        static readonly Vector2[] Top = { new Vector2(18, 50), new Vector2(110, 50), new Vector2(98, 70), new Vector2(30, 70) };
        static readonly Vector2[] Outline =
        {
            new Vector2(2, 6), new Vector2(126, 6), new Vector2(113, 52),
            new Vector2(101, 74), new Vector2(27, 74), new Vector2(15, 52),
        };

        static Sprite Make()
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[W * H];
            const int ss = 3;
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                Color acc = Color.clear;
                float a = 0;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                {
                    var c = Sample(x + (sx + 0.5f) / ss, y + (sy + 0.5f) / ss);
                    acc += new Color(c.r * c.a, c.g * c.a, c.b * c.a, 0);
                    a += c.a;
                }
                float n = ss * ss;
                px[y * W + x] = a > 0
                    ? (Color32)new Color(acc.r / a, acc.g / a, acc.b / a, a / n)
                    : new Color32(0, 0, 0, 0);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return UnityEngine.Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100);
        }

        static Color Sample(float x, float y)
        {
            var p = new Vector2(x, y);
            if (InPolygon(p, Top))
            {
                var c = Color.Lerp(new Color(1f, 0.86f, 0.42f), new Color(1f, 0.95f, 0.7f), (y - 50f) / 20f);
                float s = x - (y - 50f) * 0.6f;
                if (s > 40f && s < 52f) c = Color.Lerp(c, Color.white, 0.55f);   // reflejo
                if (y < 52f) c = Color.Lerp(c, new Color(1f, 1f, 0.85f), 0.6f);  // arista brillante
                return c;
            }
            if (InPolygon(p, Front))
            {
                var c = Color.Lerp(new Color(0.78f, 0.47f, 0.08f), new Color(0.98f, 0.72f, 0.22f), (y - 10f) / 40f);
                float lx = 18f - (50f - y) * 12f / 40f, rx = 110f + (50f - y) * 12f / 40f;
                if (x - lx < 7f || rx - x < 7f) c = Color.Lerp(c, new Color(0.6f, 0.35f, 0.05f), 0.45f);   // biseles
                float s = x + (y - 10f) * 0.35f;
                if (s > 30f && s < 44f) c = Color.Lerp(c, new Color(1f, 0.97f, 0.8f), 0.4f);               // reflejo
                return c;
            }
            if (InPolygon(p, Outline)) return new Color(0.42f, 0.24f, 0.03f);
            return Color.clear;
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
    }
}
